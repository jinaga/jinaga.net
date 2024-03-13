using FluentAssertions;

namespace Jinaga.Store.SQLite.Test;

public class ExecuteInverseTest
{
    [Fact]
    public void CanGenerateSQLFromInverses()
    {
        var specification =
                Given<Root>.Match((root, facts) =>
                    from child in facts.OfType<Child>()
                    where child.Parent.Root == root && child.IsCurrent
                    from source in facts.OfType<Source>()
                    where child.Sources.Contains(source)
                    select source
                );
        specification.ToDescriptiveString().Should().Be(
            "(root: Root) {\n" +
            "    child: Sources [\n" +
            "        child->Parent: Parent->Root: Root = root\n" +
            "        E {\n" +
            "            x: Parent [\n" +
            "                x = child->Parent: Parent\n" +
            "                !E {\n" +
            "                    f: Parent [\n" +
            "                        f->History: Parent = x\n" +
            "                    ]\n" +
            "                }\n" +
            "            ]\n" +
            "        }\n" +
            "    ]\n" +
            "    source: Source [\n" +
            "        source = child->Sources: Source\n" +
            "    ]\n" +
            "} => source\n"
        );

        var inverses = specification.ComputeInverses();
        inverses.Count.Should().Be(3);
        inverses[0].InverseSpecification.ToDescriptiveString().Should().Be(
            "(child: Sources [\n" +
            "    E {\n" +
            "        x: Parent [\n" +
            "            x = child->Parent: Parent\n" +
            "            !E {\n" +
            "                f: Parent [\n" +
            "                    f->History: Parent = x\n" +
            "                ]\n" +
            "            }\n" +
            "        ]\n" +
            "    }\n" +
            "]) {\n" +
            "    root: Root [\n" +
            "        root = child->Parent: Parent->Root: Root\n" +
            "    ]\n" +
            "    source: Source [\n" +
            "        source = child->Sources: Source\n" +
            "    ]\n" +
            "} => source\n"
        );
        inverses[1].InverseSpecification.ToDescriptiveString().Should().Be(
            "(x: Parent [\n" +
            "    !E {\n" +
            "        f: Parent [\n" +
            "            f->History: Parent = x\n" +
            "        ]\n" +
            "    }\n" +
            "]) {\n" +
            "    child: Sources [\n" +
            "        child->Parent: Parent = x\n" +
            "    ]\n" +
            "    root: Root [\n" +
            "        root = child->Parent: Parent->Root: Root\n" +
            "    ]\n" +
            "    source: Source [\n" +
            "        source = child->Sources: Source\n" +
            "    ]\n" +
            "} => source\n"
        );
        inverses[2].InverseSpecification.ToDescriptiveString().Should().Be(
            "(f: Parent) {\n" +
            "    x: Parent [\n" +
            "        x = f->History: Parent\n" +
            "    ]\n" +
            "    child: Sources [\n" +
            "        child->Parent: Parent = x\n" +
            "        E {\n" +
            "            x: Parent [\n" +
            "                x = child->Parent: Parent\n" +
            "                !E {\n" +
            "                    f: Parent [\n" +
            "                        f->History: Parent = x\n" +
            "                    ]\n" +
            "                }\n" +
            "            ]\n" +
            "        }\n" +
            "    ]\n" +
            "    root: Root [\n" +
            "        root = child->Parent: Parent->Root: Root\n" +
            "    ]\n" +
            "    source: Source [\n" +
            "        source = child->Sources: Source\n" +
            "    ]\n" +
            "} => source\n"
        );

        var inverse0Sql = inverses[0].InverseSpecification.ToSql();
        inverse0Sql.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " + // child
                "f5.hash as hash5, f5.fact_id as id5, f5.data as data5, " + // root
                "f6.hash as hash6, f6.fact_id as id6, f6.data as data6 " +  // source
            "FROM fact f1 " +       // child
            "JOIN edge e3 " +       // child->Parent
                "ON e3.successor_fact_id = f1.fact_id " +
                "AND e3.role_id = ?5 " +
            "JOIN fact f4 " +       // Parent
                "ON f4.fact_id = e3.predecessor_fact_id " +
            "JOIN edge e4 " +       // Parent->Root
                "ON e4.successor_fact_id = f4.fact_id " +
                "AND e4.role_id = ?6 " +
            "JOIN fact f5 " +       // root
                "ON f5.fact_id = e4.predecessor_fact_id " +
            "JOIN edge e5 " +       // child->Sources
                "ON e5.successor_fact_id = f1.fact_id " +
                "AND e5.role_id = ?7 " +
            "JOIN fact f6 " +       // source
                "ON f6.fact_id = e5.predecessor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "AND EXISTS (" +
                "SELECT 1 FROM edge e1 " +  // child->Parent
                "JOIN fact f2 " +           // Parent
                    "ON f2.fact_id = e1.predecessor_fact_id " +
                "WHERE e1.successor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
                "AND NOT EXISTS (" +
                    "SELECT 1 FROM edge e2 " + // Parent->History
                    "JOIN fact f3 " +          // Parent
                        "ON f3.fact_id = e2.successor_fact_id " +
                    "WHERE e2.predecessor_fact_id = f2.fact_id " +
                    "AND e2.role_id = ?4" +
                ")" +
            ") " +
            "ORDER BY f5.fact_id ASC, f6.fact_id ASC"
        );

        var inverse1Sql = inverses[1].InverseSpecification.ToSql();
        inverse1Sql.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " + // x: Parent
                "f3.hash as hash3, f3.fact_id as id3, f3.data as data3, " + // child
                "f5.hash as hash5, f5.fact_id as id5, f5.data as data5, " + // root
                "f6.hash as hash6, f6.fact_id as id6, f6.data as data6 " +  // source
            "FROM fact f1 " +       // x: Parent
            "JOIN edge e2 " +       // child->Parent
                "ON e2.predecessor_fact_id = f1.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +       // child
                "ON f3.fact_id = e2.successor_fact_id " +
            "JOIN edge e3 " +       // child->Parent
                "ON e3.successor_fact_id = f3.fact_id " +
                "AND e3.role_id = ?5 " +
            "JOIN fact f4 " +       // Parent
                "ON f4.fact_id = e3.predecessor_fact_id " +
            "JOIN edge e4 " +       // Parent->Root
                "ON e4.successor_fact_id = f4.fact_id " +
                "AND e4.role_id = ?6 " +
            "JOIN fact f5 " +       // root
                "ON f5.fact_id = e4.predecessor_fact_id " +
            "JOIN edge e5 " +       // child->Sources
                "ON e5.successor_fact_id = f3.fact_id " +
                "AND e5.role_id = ?7 " +
            "JOIN fact f6 " +       // source
                "ON f6.fact_id = e5.predecessor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "AND NOT EXISTS (" +
                "SELECT 1 FROM edge e1 " + // Parent->History
                "JOIN fact f2 " +          // Parent
                    "ON f2.fact_id = e1.successor_fact_id " +
                "WHERE e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3" +
            ") " +
            "ORDER BY f3.fact_id ASC, f5.fact_id ASC, f6.fact_id ASC"
        );

        var inverse2Sql = inverses[2].InverseSpecification.ToSql();
        inverse2Sql.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " + // f: Parent
                "f2.hash as hash2, f2.fact_id as id2, f2.data as data2, " + // x: Parent
                "f3.hash as hash3, f3.fact_id as id3, f3.data as data3, " + // child
                "f5.hash as hash5, f5.fact_id as id5, f5.data as data5, " + // root
                "f6.hash as hash6, f6.fact_id as id6, f6.data as data6 " +  // source
            "FROM fact f1 " +       // f: Parent
            "JOIN edge e1 " +       // f->History
                "ON e1.successor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +       // x: Parent
                "ON f2.fact_id = e1.predecessor_fact_id " +
            "JOIN edge e2 " +       // child->Parent
                "ON e2.predecessor_fact_id = f2.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +       // child
                "ON f3.fact_id = e2.successor_fact_id " +
            "JOIN edge e5 " +       // child->Sources
                "ON e5.successor_fact_id = f3.fact_id " +
                "AND e5.role_id = ?7 " +
            "JOIN fact f4 " +       // Parent
                "ON f4.fact_id = e5.predecessor_fact_id " +
            "JOIN edge e6 " +       // Parent->Root
                "ON e6.successor_fact_id = f4.fact_id " +
                "AND e6.role_id = ?8 " +
            "JOIN fact f5 " +       // root
                "ON f5.fact_id = e6.predecessor_fact_id " +
            "JOIN edge e7 " +       // child->Sources
                "ON e7.successor_fact_id = f3.fact_id " +
                "AND e7.role_id = ?9 " +
            "JOIN fact f6 " +       // source
                "ON f6.fact_id = e7.predecessor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "AND EXISTS (" +
                "SELECT 1 FROM edge e3 " +  // child->Parent
                "WHERE e3.predecessor_fact_id = f2.fact_id " +
                "AND e3.successor_fact_id = f3.fact_id " +
                "AND e3.role_id = ?5 " +
                "AND NOT EXISTS (" +
                    "SELECT 1 FROM edge e4 " + // Parent->History
                    "WHERE e4.predecessor_fact_id = f2.fact_id " +
                    "AND e4.successor_fact_id = f1.fact_id " +
                    "AND e4.role_id = ?6" +
                ")" +
            ") " +
            "ORDER BY f2.fact_id ASC, f3.fact_id ASC, f5.fact_id ASC, f6.fact_id ASC"
        );
    }
}

[FactType("Root")]
public record Root(string Id);

[FactType("Parent")]
public record Parent(Root Root, DateTime CreatedOn, Parent[] History);

[FactType("Sources")]
public record Child(Parent Parent, Source[] Sources)
{
    public Condition IsCurrent => new Condition(facts =>
        facts.Any<Parent>(x => this.Parent == x &&
            !facts.Any<Parent>(f => f.History.Contains(x))
        )
    );

}

[FactType("Source")]
public record Source(SourceCode Code, string Description, int Order);

[FactType("Source.Code")]
public record SourceCode(string Code);