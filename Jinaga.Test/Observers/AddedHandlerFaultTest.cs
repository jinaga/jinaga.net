using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;

namespace Jinaga.Test.Observers;

/// <summary>
/// What an exception from the caller's added handler is allowed to reach. The
/// observer is the boundary between Jinaga and caller code, so a handler that
/// throws is confined to its own row: the rest of the batch still arrives, other
/// observers still receive their rows, and the save that triggered the
/// notification still succeeds.
/// </summary>
public class AddedHandlerFaultTest
{
    /// <summary>A delay no test waits out, so only an explicit Push uploads.</summary>
    private const int DelayLongerThanAnyTest = 60000;

    private readonly ITestOutputHelper output;

    public AddedHandlerFaultTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    private static readonly Specification<School, Course> CoursesInSchool = Given<School>.Match((school, facts) =>
        from course in facts.OfType<Course>()
        where course.school == school
        select course
    );

    [Fact]
    public async Task Watch_AddedHandlerThrowsForOneRow_DeliversTheRemainingRows()
    {
        var network = new FakeNetwork(output);
        var j = GivenJinagaClient(network, new RecordingLoggerFactory());

        var school = await j.Fact(new School(Guid.NewGuid()));
        await j.Fact(new Course(school, "Math 101"));
        await j.Fact(new Course(school, "Math 102"));
        await j.Fact(new Course(school, "Math 103"));

        int addedCalls = 0;
        var delivered = new List<Course>();
        var watch = j.Local.Watch(CoursesInSchool, school, (Course added) =>
        {
            if (Interlocked.Increment(ref addedCalls) == 1)
            {
                throw new InvalidOperationException("the added handler failed for this row");
            }

            lock (delivered)
            {
                delivered.Add(added);
            }
            return Task.FromResult<Func<Task>>(() => Task.CompletedTask);
        });

        try
        {
            await watch.Loaded;

            // The handler saw every row, and the two it did not throw for arrived.
            Volatile.Read(ref addedCalls).Should().Be(3);
            lock (delivered)
            {
                delivered.Should().HaveCount(2);
            }
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_AnotherWatchStartedFirstThrows_StillDeliversEveryRow()
    {
        await TwoWatchesOnOneSpecification(throwingWatchStartsFirst: true);
    }

    [Fact]
    public async Task Watch_AnotherWatchStartedAfterwardThrows_StillDeliversEveryRow()
    {
        await TwoWatchesOnOneSpecification(throwingWatchStartsFirst: false);
    }

    /// <summary>
    /// Two watches on one specification are independent axes, so a defect in one
    /// cannot change what the other receives. The observable source notifies its
    /// listeners in the order they registered, so the order of the two watches
    /// decides which one a propagating exception would starve. Neither order may
    /// cost the healthy watch a row.
    /// </summary>
    private async Task TwoWatchesOnOneSpecification(bool throwingWatchStartsFirst)
    {
        var network = new FakeNetwork(output);
        var j = GivenJinagaClient(network, new RecordingLoggerFactory());

        var school = await j.Fact(new School(Guid.NewGuid()));

        var healthy = new List<Course>();
        Func<Course, Task<Func<Task>>> healthyHandler = (Course added) =>
        {
            lock (healthy)
            {
                healthy.Add(added);
            }
            return Task.FromResult<Func<Task>>(() => Task.CompletedTask);
        };
        Func<Course, Task<Func<Task>>> throwingHandler = (Course added) =>
            throw new InvalidOperationException("the added handler failed for this row");

        IObserver first = null;
        IObserver second = null;
        try
        {
            first = j.Local.Watch(CoursesInSchool, school,
                throwingWatchStartsFirst ? throwingHandler : healthyHandler);
            await first.Loaded;
            second = j.Local.Watch(CoursesInSchool, school,
                throwingWatchStartsFirst ? healthyHandler : throwingHandler);
            await second.Loaded;

            // Saved after both watches are listening, so one notification reaches
            // both of them in registration order.
            await j.Fact(new Course(school, "Math 101"));
            await j.Fact(new Course(school, "Math 102"));
            await j.Fact(new Course(school, "Math 103"));

            lock (healthy)
            {
                healthy.Select(c => c.identifier).Should().BeEquivalentTo(
                    new[] { "Math 101", "Math 102", "Math 103" });
            }
        }
        finally
        {
            first?.Stop();
            second?.Stop();
        }
    }

    [Fact]
    public async Task Fact_WatchHandlerThrows_SavesTheFactAndPushesIt()
    {
        var network = new FakeNetwork(output);
        var j = GivenJinagaClient(network, new RecordingLoggerFactory());

        var school = await j.Fact(new School(Guid.NewGuid()));

        Func<Course, Task<Func<Task>>> throwingHandler = (Course added) =>
            throw new InvalidOperationException("the added handler failed for this row");
        var watch = j.Local.Watch(CoursesInSchool, school, throwingHandler);

        try
        {
            await watch.Loaded;

            // The fact is committed before the handler runs, so the code that
            // saved it did nothing to deserve an exception.
            var course = await j.Fact(new Course(school, "Math 101"));

            await j.Push();

            network.UploadedFacts.Select(f => f.Reference.Type).Should().Contain("Course");
            course.identifier.Should().Be("Math 101");
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_AddedHandlerThrows_LogsTheExceptionAtError()
    {
        var network = new FakeNetwork(output);
        var loggerFactory = new RecordingLoggerFactory();
        var j = GivenJinagaClient(network, loggerFactory);

        var school = await j.Fact(new School(Guid.NewGuid()));
        await j.Fact(new Course(school, "Math 101"));

        var thrown = new InvalidOperationException("the added handler failed for this row");
        Func<Course, Task<Func<Task>>> throwingHandler = (Course added) => throw thrown;
        var watch = j.Local.Watch(CoursesInSchool, school, throwingHandler);

        try
        {
            await watch.Loaded;

            loggerFactory.Entries
                .Where(entry => entry.Level == LogLevel.Error)
                .Should().ContainSingle()
                .Which.Exception.Should().BeSameAs(thrown);
        }
        finally
        {
            watch.Stop();
        }
    }

    private static JinagaClient GivenJinagaClient(FakeNetwork network, ILoggerFactory loggerFactory)
    {
        // A persistent store queues a saved fact for the network rather than
        // uploading it inline, which is what makes Push the thing under test.
        var options = new JinagaClientOptions
        {
            QueueProcessingDelay = DelayLongerThanAnyTest
        };
        return new JinagaClient(new PersistentMemoryStore(), network, [], loggerFactory, options);
    }
}
