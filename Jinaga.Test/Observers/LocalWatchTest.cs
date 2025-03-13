using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;

namespace Jinaga.Test.Observers;
public class LocalWatchTest
{
    private ITestOutputHelper output;

    public LocalWatchTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task LocalWatch_NoCourses()
    {
        var school = new School(Guid.NewGuid());
        var network = GivenPopulatedNetwork(school);
        var j = GivenJinagaClient(network);

        var viewModel = new SchoolViewModel();
        var watch = viewModel.LoadLocal(j, school.identifier);

        try
        {
            await watch.Loaded;
            viewModel.Courses.Should().BeEmpty();
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task RemoteWatch_FetchesCourses()
    {
        var school = new School(Guid.NewGuid());
        var network = GivenPopulatedNetwork(school);
        var j = GivenJinagaClient(network);

        var viewModel = new SchoolViewModel();
        var watch = viewModel.LoadRemove(j, school.identifier);

        try
        {
            await watch.Loaded;
            viewModel.Courses.Select(c => c.identifier).Should().BeEquivalentTo(
                new[] { "Computer Science 101", "Computer Science 102" });
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task LocalWatch_CourseInLocalStore()
    {
        var school = new School(Guid.NewGuid());
        var network = GivenPopulatedNetwork(school);
        var j = GivenJinagaClient(network);

        var course = new Course(school, "Math 101");
        await j.Fact(course);

        var viewModel = new SchoolViewModel();
        var watch = viewModel.LoadLocal(j, school.identifier);

        try
        {
            await watch.Loaded;
            viewModel.Courses.Should().ContainSingle().Which.identifier.Should().Be("Math 101");
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task RemoteWatch_MergesCourses()
    {
        var school = new School(Guid.NewGuid());
        var network = GivenPopulatedNetwork(school);
        var j = GivenJinagaClient(network);

        var course = new Course(school, "Math 101");
        await j.Fact(course);

        var viewModel = new SchoolViewModel();
        var watch = viewModel.LoadRemove(j, school.identifier);

        try
        {
            await watch.Loaded;
            viewModel.Courses.Select(c => c.identifier).Should().BeEquivalentTo(
                new[] { "Computer Science 101", "Computer Science 102", "Math 101" });
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task LocalWatch_CourseAddedLater()
    {
        var school = new School(Guid.NewGuid());
        var network = GivenPopulatedNetwork(school);
        var j = GivenJinagaClient(network);

        var viewModel = new SchoolViewModel();
        var watch = viewModel.LoadLocal(j, school.identifier);

        try
        {
            await watch.Loaded;
            viewModel.Courses.Should().BeEmpty();

            var course = new Course(school, "Math 101");
            await j.Fact(course);

            viewModel.Courses.Should().ContainSingle().Which.identifier.Should().Be("Math 101");
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task RemoteWatch_CourseAddedLater()
    {
        var school = new School(Guid.NewGuid());
        var network = GivenPopulatedNetwork(school);
        var j = GivenJinagaClient(network);

        var viewModel = new SchoolViewModel();
        var watch = viewModel.LoadRemove(j, school.identifier);

        try
        {
            await watch.Loaded;
            viewModel.Courses.Select(c => c.identifier).Should().BeEquivalentTo(
                new[] { "Computer Science 101", "Computer Science 102" });

            var course = new Course(school, "Math 101");
            await j.Fact(course);

            viewModel.Courses.Select(c => c.identifier).Should().BeEquivalentTo(
                new[] { "Computer Science 101", "Computer Science 102", "Math 101" });
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task LocalWatch_RefreshHasNoEffect()
    {
        var school = new School(Guid.NewGuid());
        var network = GivenPopulatedNetwork(school);
        var j = GivenJinagaClient(network);

        var viewModel = new SchoolViewModel();
        var watch = viewModel.LoadLocal(j, school.identifier);

        try
        {
            await watch.Loaded;
            viewModel.Courses.Should().BeEmpty();

            WhenAddCourses(network, school);

            await watch.Refresh();

            viewModel.Courses.Should().BeEmpty();
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task RemoteWatch_RefreshFetchesAdditionalCourses()
    {
        var school = new School(Guid.NewGuid());
        var network = GivenPopulatedNetwork(school);
        var j = GivenJinagaClient(network);

        var viewModel = new SchoolViewModel();
        var watch = viewModel.LoadRemove(j, school.identifier);

        try
        {
            await watch.Loaded;
            viewModel.Courses.Select(c => c.identifier).Should().BeEquivalentTo(
                new[] { "Computer Science 101", "Computer Science 102" });

            WhenAddCourses(network, school);

            await watch.Refresh();

            viewModel.Courses.Select(c => c.identifier).Should().BeEquivalentTo(
                new[] { "Computer Science 101", "Computer Science 102", "Psychology 101", "Psychology 102" });
        }
        finally
        {
            watch.Stop();
        }
    }

    private FakeNetwork GivenPopulatedNetwork(School school)
    {
        FakeNetwork network = new FakeNetwork(output);
        network.AddFeed("courses", new object[]
        {
            school,
            new Course(school, "Computer Science 101"),
            new Course(school, "Computer Science 102")
        });
        return network;
    }

    private static JinagaClient GivenJinagaClient(FakeNetwork network)
    {
        var options = new JinagaClientOptions();
        return new JinagaClient(new MemoryStore(), network, [], NullLoggerFactory.Instance, options);
    }

    private void WhenAddCourses(FakeNetwork network, School school)
    {
        network.AddFeed("courses", new object[]
        {
            new Course(school, "Psychology 101"),
            new Course(school, "Psychology 102")
        });
    }

    private class SchoolViewModel
    {
        private static Specification<School, Course> CoursesInSchool = Given<School>.Match((school, facts) =>
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

        public List<Course> Courses { get; private set; } = new List<Course>();

        public IObserver LoadLocal(JinagaClient j, Guid identifier)
        {
            var school = new School(identifier);

            return j.Local.Watch(CoursesInSchool, school, CourseAdded);
        }

        public IObserver LoadRemove(JinagaClient j, Guid identifier)
        {
            var school = new School(identifier);

            return j.Watch(CoursesInSchool, school, CourseAdded);
        }

        private Action CourseAdded(Course course)
        {
            Courses.Add(course);

            return () =>
            {
                Courses.Remove(course);
            };
        }
    }
}
