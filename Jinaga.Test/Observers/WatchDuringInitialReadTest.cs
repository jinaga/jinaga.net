using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Storage;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Jinaga.Test.Observers;

/// <summary>
/// What a watch observes when facts are saved while its initial read is in
/// flight. The read's result set is computed at one instant and delivered at a
/// later one, so a fact saved in between is in neither the result set nor, if
/// the listeners are registered after the read, any listener's reach.
/// </summary>
public class WatchDuringInitialReadTest
{
    private static readonly Specification<School, Course> CoursesInSchool = Given<School>.Match((school, facts) =>
        from course in facts.OfType<Course>()
        where course.school == school &&
            !facts.Any<CourseDeleted>(deleted =>
                deleted.course == course &&
                    !facts.Any<CourseRestored>(restored =>
                        restored.deleted == deleted
                    )
            )
        select course
    );

    [Fact]
    public async Task Watch_FactSavedWhileTheInitialReadIsBlocked_CallsTheAddedHandlerOnce()
    {
        var store = new GatedReadStore(new MemoryStore());
        var j = CreateClient(store);
        var school = await j.Fact(new School(Guid.NewGuid()));

        var observed = new ObservedCourses();
        var watch = observed.Watch(j, school);

        try
        {
            // The read has computed its result set, which holds no courses, and
            // is holding it. A course saved now is in the store and in neither
            // that result set nor the watch, unless a listener is already there.
            await store.FirstReadHeld;

            await j.Fact(new Course(school, "Math 101"));

            store.ReleaseRead();
            await watch.Loaded;

            observed.AddedCalls.Should().Be(1);
            observed.Courses.Should().ContainSingle()
                .Which.identifier.Should().Be("Math 101");
        }
        finally
        {
            store.ReleaseRead();
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_RemovalSavedWhileTheInitialReadIsBlocked_LeavesTheRowOutOfTheObservedState()
    {
        var store = new GatedReadStore(new MemoryStore());
        var j = CreateClient(store);
        var school = await j.Fact(new School(Guid.NewGuid()));
        var course = await j.Fact(new Course(school, "Math 101"));

        var observed = new ObservedCourses();
        var watch = observed.Watch(j, school);

        try
        {
            // This time the held result set does hold the course, so the read is
            // about to deliver a row that the deletion below has already removed.
            await store.FirstReadHeld;

            await j.Fact(new CourseDeleted(course, DateTime.UtcNow));

            store.ReleaseRead();
            await watch.Loaded;

            observed.Courses.Should().BeEmpty();
            // Either the row was never delivered, or it was delivered and then
            // removed. Both leave the row out, and both call the removal function
            // exactly as often as the added handler.
            observed.RemovalCalls.Should().Be(observed.AddedCalls);
        }
        finally
        {
            store.ReleaseRead();
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_NoConcurrentSaves_CallsTheAddedHandlerOncePerRow()
    {
        var j = JinagaTest.Create();
        var school = await j.Fact(new School(Guid.NewGuid()));
        await j.Fact(new Course(school, "Math 101"));
        await j.Fact(new Course(school, "History 201"));

        var observed = new ObservedCourses();
        var watch = observed.Watch(j, school);

        try
        {
            await watch.Loaded;

            // The listeners are registered before the read, so each row is now
            // reachable by two paths. Nothing is saved here, so only the read
            // delivers, and it delivers each row once.
            observed.AddedCalls.Should().Be(2);
            observed.Courses.Select(course => course.identifier)
                .Should().BeEquivalentTo(new[] { "Math 101", "History 201" });
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_RemovalForAnUndeliveredRowAfterTheInitialRead_StillAllowsTheRowToBeAddedLater()
    {
        var j = JinagaTest.Create();
        var school = await j.Fact(new School(Guid.NewGuid()));

        var observed = new ObservedCourses();
        var watch = observed.Watch(j, school);

        try
        {
            await watch.Loaded;
            observed.AddedCalls.Should().Be(0);

            // A course that is already deleted by the time it is saved. The watch
            // never delivers it, so its row has no state, and the deletion still
            // reaches the observer's removal path for that row -- well after the
            // initial read has completed.
            var deleted = await j.Fact(new CourseDeleted(
                new Course(school, "Chemistry 301"), DateTime.UtcNow));

            observed.AddedCalls.Should().Be(0);
            observed.Courses.Should().BeEmpty();

            // Restoring it is this row's first legitimate add. A removal recorded
            // against a row with no state would cancel it instead.
            await j.Fact(new CourseRestored(deleted));

            observed.AddedCalls.Should().Be(1);
            observed.Courses.Should().ContainSingle()
                .Which.identifier.Should().Be("Chemistry 301");
        }
        finally
        {
            watch.Stop();
        }
    }

    /// <summary>
    /// JinagaTest.Create builds its own store, so a test that needs to wrap one
    /// constructs the client through its public IStore constructor instead.
    /// </summary>
    private static JinagaClient CreateClient(IStore store)
    {
        return new JinagaClient(
            store,
            new SimulatedNetwork(null),
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            new JinagaClientOptions());
    }

    /// <summary>
    /// A view of the observed rows. Its added handler returns immediately, so
    /// what these tests race against is the observer's initial read rather than
    /// the caller's handler.
    /// </summary>
    private class ObservedCourses
    {
        private readonly List<Course> courses = new List<Course>();
        private int addedCalls;
        private int removalCalls;

        public int AddedCalls => Volatile.Read(ref addedCalls);
        public int RemovalCalls => Volatile.Read(ref removalCalls);

        public IReadOnlyList<Course> Courses
        {
            get
            {
                lock (courses)
                {
                    return courses.ToArray();
                }
            }
        }

        public IObserver Watch(JinagaClient j, School school)
        {
            return j.Local.Watch(CoursesInSchool, school, (Course added) =>
            {
                Interlocked.Increment(ref addedCalls);
                lock (courses)
                {
                    courses.Add(added);
                }

                Func<Task> removal = () =>
                {
                    Interlocked.Increment(ref removalCalls);
                    lock (courses)
                    {
                        courses.Remove(added);
                    }
                    return Task.CompletedTask;
                };
                return Task.FromResult(removal);
            });
        }
    }

    /// <summary>
    /// A store that holds the result set of its first read until the test
    /// releases it, reproducing the interval between the instant a read's result
    /// set is correct and the instant the observer delivers it.
    ///
    /// Only the first read is held. ObservableSource.NotifyFactSaved reads the
    /// store itself on the save path that is supposed to release the gate, so a
    /// store that held every read would deadlock.
    /// </summary>
    private class GatedReadStore : IStore
    {
        private readonly IStore inner;
        private readonly TaskCompletionSource<bool> gate =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> firstReadHeld =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int reads;

        public GatedReadStore(IStore inner)
        {
            this.inner = inner;
        }

        /// <summary>Completes once the first read has its result set and is holding it.</summary>
        public Task FirstReadHeld => firstReadHeld.Task;

        public void ReleaseRead()
        {
            gate.TrySetResult(true);
        }

        public async Task<ImmutableList<Product>> Read(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            // Compute the result set before waiting, so that what the caller
            // finally receives is the set as it stood when the read began.
            var products = await inner.Read(givenTuple, specification, cancellationToken).ConfigureAwait(false);
            if (Interlocked.Increment(ref reads) == 1)
            {
                firstReadHeld.TrySetResult(true);
                await gate.Task.ConfigureAwait(false);
            }
            return products;
        }

        public bool IsPersistent => inner.IsPersistent;

        public Task<ImmutableList<Fact>> Save(FactGraph graph, bool queue, CancellationToken cancellationToken) =>
            inner.Save(graph, queue, cancellationToken);

        public Task<FactGraph> Load(ImmutableList<FactReference> references, CancellationToken cancellationToken) =>
            inner.Load(references, cancellationToken);

        public Task<string> LoadBookmark(string feed) => inner.LoadBookmark(feed);

        public Task<ImmutableList<FactReference>> ListKnown(ImmutableList<FactReference> factReferences) =>
            inner.ListKnown(factReferences);

        public Task SaveBookmark(string feed, string bookmark) => inner.SaveBookmark(feed, bookmark);

        public Task<DateTime?> GetMruDate(string specificationHash) => inner.GetMruDate(specificationHash);

        public Task SetMruDate(string specificationHash, DateTime mruDate) => inner.SetMruDate(specificationHash, mruDate);

        public Task<QueuedFacts> GetQueue() => inner.GetQueue();

        public Task SetQueueBookmark(string bookmark) => inner.SetQueueBookmark(bookmark);

        public Task<IEnumerable<Fact>> GetAllFacts() => inner.GetAllFacts();

        public Task Purge(ImmutableList<Specification> purgeConditions) => inner.Purge(purgeConditions);

        public Task PurgeDescendants(FactReference purgeRoot, ImmutableList<FactReference> triggers) =>
            inner.PurgeDescendants(purgeRoot, triggers);
    }
}
