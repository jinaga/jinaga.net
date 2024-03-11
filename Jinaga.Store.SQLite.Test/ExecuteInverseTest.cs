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

        var inverses = specification.ComputeInverses();
        inverses.Count.Should().Be(2);
        inverses[0].InverseSpecification.ToDescriptiveString().Should().Be(
            "(projectTopics: Test.ProjectTopics [\n" +
            "    !E {\n" +
            "        next: Test.ProjectTopics [\n" +
            "            next->Prior: Test.ProjectTopics = projectTopics\n" +
            "        ]\n" +
            "    }\n" +
            "]) {\n" +
            "    root: Test.Root [\n" +
            "        root = projectTopics->Project: Test.Project->Root: Test.Root\n" +
            "    ]\n" +
            "    topic: Test.Topic [\n" +
            "        topic = projectTopics->Topics: Test.Topic\n" +
            "    ]\n" +
            "} => topic\n"
        );
        inverses[1].InverseSpecification.ToDescriptiveString().Should().Be(
            "(next: Test.ProjectTopics) {\n" +
            "    projectTopics: Test.ProjectTopics [\n" +
            "        projectTopics = next->Prior: Test.ProjectTopics\n" +
            "    ]\n" +
            "    root: Test.Root [\n" +
            "        root = projectTopics->Project: Test.Project->Root: Test.Root\n" +
            "    ]\n" +
            "    topic: Test.Topic [\n" +
            "        topic = projectTopics->Topics: Test.Topic\n" +
            "    ]\n" +
            "} => topic\n"
        );

        var inverse0Sql = inverses[0].InverseSpecification.ToSql();
        inverse0Sql.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +  // root
                "f3.hash as hash3, f3.fact_id as id3, f3.data as data3, " +  // projectTopics
                "f4.hash as hash4, f4.fact_id as id4, f4.data as data4 " +   // topic
            "FROM fact f1 " +  // root
            "JOIN edge e1 " +  // project->root
                "ON e1.successor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +  // project
                "ON f2.fact_id = e1.predecessor_fact_id " +
            "JOIN edge e2 " +  // projectTopics->project
                "ON e2.successor_fact_id = f2.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +  // projectTopics
                "ON f3.fact_id = e2.predecessor_fact_id " +
            "JOIN edge e3 " +  // projectTopics->topic
                "ON e3.successor_fact_id = f1.fact_id " +
                "AND e3.role_id = ?5 " +
            "JOIN fact f4 " +  // topic
                "ON f4.fact_id = e3.predecessor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "ORDER BY f3.fact_id ASC, f4.fact_id ASC"
        );

        var inverse1Sql = inverses[1].InverseSpecification.ToSql();
        inverse1Sql.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +  // next
                "f2.hash as hash2, f2.fact_id as id2, f2.data as data2, " +  // projectTopics
                "f4.hash as hash4, f4.fact_id as id4, f4.data as data4, " +  // root
                "f5.hash as hash5, f5.fact_id as id5, f5.data as data5 " +   // topic
            "FROM fact f1 " +  // next
            "JOIN edge e1 " +  // prior->next
                "ON e1.successor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +  // prior
                "ON f2.fact_id = e1.predecessor_fact_id " +
            "JOIN edge e2 " +  // projectTopics->prior
                "ON e2.successor_fact_id = f2.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +  // projectTopics
                "ON f3.fact_id = e2.predecessor_fact_id " +
            "JOIN edge e3 " +  // project->root
                "ON e3.successor_fact_id = f3.fact_id " +
                "AND e3.role_id = ?5 " +
            "JOIN fact f4 " +  // root
                "ON f4.fact_id = e3.predecessor_fact_id " +
            "JOIN edge e4 " +  // projectTopics->topic
                "ON e4.successor_fact_id = f2.fact_id " +
                "AND e4.role_id = ?6 " +
            "JOIN fact f5 " +  // topic
                "ON f5.fact_id = e4.predecessor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "ORDER BY f2.fact_id ASC, f4.fact_id ASC, f5.fact_id ASC"
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