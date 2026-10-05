using Jinaga.Test.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Jinaga.Test.Observers;

/// <summary>
/// The lifecycle of one observed row across the interval in which the caller's
/// added handler has been invoked and has not yet returned. The observer has no
/// removal function for the row until that handler returns one, so a removal
/// that arrives in the interval has to be remembered rather than dropped.
/// </summary>
public class ObservedRowLifecycleTest
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
    public async Task Watch_RemovalWhileAddedHandlerIsBlocked_CallsTheRemovalFunction()
    {
        var j = JinagaTest.Create();
        var school = await j.Fact(new School(Guid.NewGuid()));
        var course = await j.Fact(new Course(school, "Math 101"));

        var observed = new GatedObserverState();
        var watch = observed.Watch(j, school);

        try
        {
            // The handler is now inside the caller's code, which is also after the
            // observer registered its listeners, so the removal below is delivered.
            await observed.HandlerEntered;

            await j.Fact(new CourseDeleted(course, DateTime.UtcNow));

            observed.OpenGate();
            await watch.Loaded;

            observed.RemovalCalls.Should().Be(1);
            observed.Courses.Should().BeEmpty();
        }
        finally
        {
            observed.OpenGate();
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_RowRestoredAfterRacedRemoval_CallsTheAddedHandlerAgain()
    {
        var j = JinagaTest.Create();
        var school = await j.Fact(new School(Guid.NewGuid()));
        var course = await j.Fact(new Course(school, "Math 101"));

        var observed = new GatedObserverState();
        var watch = observed.Watch(j, school);

        try
        {
            await observed.HandlerEntered;

            var deleted = await j.Fact(new CourseDeleted(course, DateTime.UtcNow));

            observed.OpenGate();
            await watch.Loaded;

            await j.Fact(new CourseRestored(deleted));

            observed.AddedCalls.Should().Be(2);
            observed.Courses.Should().ContainSingle()
                .Which.identifier.Should().Be("Math 101");
        }
        finally
        {
            observed.OpenGate();
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_TwoRemovalsWhileAddedHandlerIsBlocked_CallsTheRemovalFunctionOnce()
    {
        var j = JinagaTest.Create();
        var school = await j.Fact(new School(Guid.NewGuid()));
        var course = await j.Fact(new Course(school, "Math 101"));

        var observed = new GatedObserverState();
        var watch = observed.Watch(j, school);

        try
        {
            await observed.HandlerEntered;

            await j.Fact(new CourseDeleted(course, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            await j.Fact(new CourseDeleted(course, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)));

            observed.OpenGate();
            await watch.Loaded;

            observed.RemovalCalls.Should().Be(1);
            observed.Courses.Should().BeEmpty();
        }
        finally
        {
            observed.OpenGate();
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_AddedHandlerThrows_LeavesTheRowUnnotified()
    {
        var j = JinagaTest.Create();
        var school = await j.Fact(new School(Guid.NewGuid()));
        var course = await j.Fact(new Course(school, "Math 101"));

        int addedCalls = 0;
        int removalCalls = 0;
        var watch = j.Local.Watch(CoursesInSchool, school, (Course added) =>
        {
            if (Interlocked.Increment(ref addedCalls) == 1)
            {
                throw new InvalidOperationException("the added handler failed for this row");
            }

            Func<Task> removal = () =>
            {
                Interlocked.Increment(ref removalCalls);
                return Task.CompletedTask;
            };
            return Task.FromResult(removal);
        });

        try
        {
            // The observer confines the handler's exception to its own row, so
            // the watch loads normally. What this asserts is the state the row
            // is left in.
            await watch.Loaded;

            // Nothing was delivered for this row, so there is nothing to remove.
            var deleted = await j.Fact(new CourseDeleted(course, DateTime.UtcNow));
            Volatile.Read(ref removalCalls).Should().Be(0);

            await j.Fact(new CourseRestored(deleted));

            Volatile.Read(ref addedCalls).Should().Be(2);
            Volatile.Read(ref removalCalls).Should().Be(0);
        }
        finally
        {
            watch.Stop();
        }
    }

    /// <summary>
    /// A view of the observed rows whose added handler blocks until the test opens
    /// its gate, so that a notification the test sends meanwhile lands in the
    /// interval between the handler being called and its removal function arriving.
    /// </summary>
    private class GatedObserverState
    {
        private readonly TaskCompletionSource<bool> gate =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> handlerEntered =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<Course> courses = new List<Course>();
        private int addedCalls;
        private int removalCalls;

        /// <summary>Completes once the added handler has been called and is blocked.</summary>
        public Task HandlerEntered => handlerEntered.Task;

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

        public void OpenGate()
        {
            gate.TrySetResult(true);
        }

        public IObserver Watch(JinagaClient j, School school)
        {
            return j.Local.Watch(CoursesInSchool, school, async (Course added) =>
            {
                Interlocked.Increment(ref addedCalls);
                handlerEntered.TrySetResult(true);
                await gate.Task;

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
                return removal;
            });
        }
    }
}
