using FluentAssertions;

namespace Jinaga.Store.SQLite.Test;

public class ExecuteInverseTest
{
    [Fact]
    public void CanGenerateSQLFromInverses()
    {
        var specification =
            Given<Root>.Match((root, facts) =>
                from projectTopics in facts.OfType<ProjectTopics>()
                where projectTopics.Project.Root == root && projectTopics.IsCurrent
                from topic in facts.OfType<Topic>()
                where projectTopics.Topics.Contains(topic)
                select topic
            );
        specification.ToDescriptiveString().Should().Be(
            "(root: Test.Root) {\n" +
            "    projectTopics: Test.ProjectTopics [\n" +
            "        projectTopics->Project: Test.Project->Root: Test.Root = root\n" +
            "        !E {\n" +
            "            next: Test.ProjectTopics [\n" +
            "                next->Prior: Test.ProjectTopics = projectTopics\n" +
            "            ]\n" +
            "        }\n" +
            "    ]\n" +
            "    topic: Test.Topic [\n" +
            "        topic = projectTopics->Topics: Test.Topic\n" +
            "    ]\n" +
            "} => topic\n"
        );
    }
}

[FactType("Test.Root")]
internal record Root(string Name) {}

[FactType("Test.Project")]
internal record Project(Root Root) {}

[FactType("Test.Topic")]
internal record Topic(string Name) {}

[FactType("Test.ProjectTopics")]
internal record ProjectTopics(Project Project, Topic[] Topics, ProjectTopics[] Prior)
{
    public Condition IsCurrent => new Condition(facts =>
        !facts.Any<ProjectTopics>(next => next.Prior.Contains(this)));
}