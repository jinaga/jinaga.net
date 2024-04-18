using Jianga.Store.SQLite.Test.Models;
using Jinaga.Facts;
using Jinaga.Store.SQLite.Test.Models;
using System.Collections.Immutable;

namespace Jinaga.Store.SQLite.Test;

public class QueryGeneratorTests
{
    [Fact]
    public void JoinToSuccessors()
    {
        var specification = Given<Company>.Match((company, facts) =>
            facts.OfType<Department>()
                .Where(department => department.company == company)
        );
        SqlQueryTree sqlQueryTree = specification.ToSql();

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +
                "f2.hash as hash2, f2.fact_id as id2, f2.data as data2 " +
            "FROM fact f1 " +
            "JOIN edge e1 " +
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +
                "ON f2.fact_id = e1.successor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "ORDER BY f2.fact_id ASC"
        );
        sqlQueryTree.ChildQueries.Should().BeEmpty();
    }

    [Fact]
    public void JoinToPredecessors()
    {
        var specification = Given<Department>.Match((department, facts) =>
            facts.OfType<Company>()
                .Where(company => company == department.company)
        );
        SqlQueryTree sqlQueryTree = specification.ToSql();

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +
                "f2.hash as hash2, f2.fact_id as id2, f2.data as data2 " +
            "FROM fact f1 " +
            "JOIN edge e1 " +
                "ON e1.successor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +
                "ON f2.fact_id = e1.predecessor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "ORDER BY f2.fact_id ASC"
        );
        sqlQueryTree.ChildQueries.Should().BeEmpty();
    }

    [Fact]
    public void ApplyNegativeExistentialConditions()
    {
        var specification = Given<Company>.Match<Models.Project>((company, facts) =>
            from project in facts.OfType<Models.Project>()
            where project.department.company == company
            where !(
                from deleted in facts.OfType<ProjectDeleted>()
                where deleted.project == project
                select deleted
            ).Any<ProjectDeleted>()
            select project
        );
        SqlQueryTree sqlQueryTree = specification.ToSql();

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +
                "f3.hash as hash3, f3.fact_id as id3, f3.data as data3 " +
            "FROM fact f1 " +
            "JOIN edge e1 " +
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +
                "ON f2.fact_id = e1.successor_fact_id " +
            "JOIN edge e2 " +
                "ON e2.predecessor_fact_id = f2.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +
                "ON f3.fact_id = e2.successor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "AND NOT EXISTS (" +
                "SELECT 1 " +
                "FROM edge e3 " +
                "JOIN fact f4 " +
                    "ON f4.fact_id = e3.successor_fact_id " +
                "WHERE e3.predecessor_fact_id = f3.fact_id " +
                    "AND e3.role_id = ?5" +
            ") " +
            "ORDER BY f3.fact_id ASC"
        );
    }

    [Fact]
    public void ApplyPositiveExistentialCondition()
    {
        var specification = Given<Company>.Match<Models.Project>((company, facts) =>
            from project in facts.OfType<Models.Project>()
            where project.department.company == company
            where (
                from deleted in facts.OfType<ProjectDeleted>()
                where deleted.project == project
                select deleted
            ).Any<ProjectDeleted>()
            select project
        );
        SqlQueryTree sqlQueryTree = specification.ToSql();

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +
                "f3.hash as hash3, f3.fact_id as id3, f3.data as data3 " +
            "FROM fact f1 " +
            "JOIN edge e1 " +
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +
                "ON f2.fact_id = e1.successor_fact_id " +
            "JOIN edge e2 " +
                "ON e2.predecessor_fact_id = f2.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +
                "ON f3.fact_id = e2.successor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "AND EXISTS (" +
                "SELECT 1 " +
                "FROM edge e3 " +
                "JOIN fact f4 " +
                    "ON f4.fact_id = e3.successor_fact_id " +
                "WHERE e3.predecessor_fact_id = f3.fact_id " +
                    "AND e3.role_id = ?5" +
            ") " +
            "ORDER BY f3.fact_id ASC"
        );
    }

    [Fact]
    public void ShouldApplyNestedExistentialConditions()
    {
        var specification = Given<Company>.Match<Models.Project>((company, facts) =>
            from project in facts.OfType<Models.Project>()
            where project.department.company == company
            where !(
                from deleted in facts.OfType<ProjectDeleted>()
                where deleted.project == project
                where !(
                    from restored in facts.OfType<ProjectRestored>()
                    where restored.deleted == deleted
                    select restored
                ).Any<ProjectRestored>()
                select deleted
            ).Any<ProjectDeleted>()
            select project
        );
        SqlQueryTree sqlQueryTree = specification.ToSql();

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +
                "f3.hash as hash3, f3.fact_id as id3, f3.data as data3 " +
            "FROM fact f1 " +
            "JOIN edge e1 " +
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +
                "ON f2.fact_id = e1.successor_fact_id " +
            "JOIN edge e2 " +
                "ON e2.predecessor_fact_id = f2.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +
                "ON f3.fact_id = e2.successor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "AND NOT EXISTS (" +
                "SELECT 1 " +
                "FROM edge e3 " +
                "JOIN fact f4 " +
                    "ON f4.fact_id = e3.successor_fact_id " +
                "WHERE e3.predecessor_fact_id = f3.fact_id " +
                    "AND e3.role_id = ?5 " +
                "AND NOT EXISTS (" +
                    "SELECT 1 " +
                    "FROM edge e4 " +
                    "JOIN fact f5 " +
                        "ON f5.fact_id = e4.successor_fact_id " +
                    "WHERE e4.predecessor_fact_id = f4.fact_id " +
                        "AND e4.role_id = ?6" +
                ")" +
            ") " +
            "ORDER BY f3.fact_id ASC"
        );
    }

    [Fact]
    public void ShouldIncludeChildProjection()
    {
        var specification = Given<Company>.Match((company, facts) =>
            from department in facts.OfType<Department>()
            where department.company == company
            select new
            {
                department,
                projects = facts.OfType<Models.Project>(project =>
                    project.department == department)
            }
        );
        SqlQueryTree sqlQueryTree = specification.ToSql();

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +
                "f2.hash as hash2, f2.fact_id as id2, f2.data as data2 " +
            "FROM fact f1 " +
            "JOIN edge e1 " +
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +
                "ON f2.fact_id = e1.successor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "ORDER BY f2.fact_id ASC"
        );
        var childQuery = sqlQueryTree.ChildQueries.Should().ContainSingle().Subject;
        childQuery.Key.Should().Be("projects");
        childQuery.Value.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +
                "f2.hash as hash2, f2.fact_id as id2, f2.data as data2, " +
                "f3.hash as hash3, f3.fact_id as id3, f3.data as data3 " +
            "FROM fact f1 " +
            "JOIN edge e1 " +
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +
                "ON f2.fact_id = e1.successor_fact_id " +
            "JOIN edge e2 " +
                "ON e2.predecessor_fact_id = f2.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +
                "ON f3.fact_id = e2.successor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "ORDER BY f2.fact_id ASC, f3.fact_id ASC"
        );
    }

    [Fact]
    public void ShouldHandleAdditionalPathConditions()
    {
        var specification = Given<Models.Project>.Match<ProjectName>((project, facts) =>
            from name in facts.OfType<ProjectName>()
            where name.project == project
            from next in facts.OfType<ProjectName>()
            where next.project == project &&
                next.prior.Contains(name)
            select next
        );
        SqlQueryTree sqlQueryTree = specification.ToSql();

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " + // project
                "f2.hash as hash2, f2.fact_id as id2, f2.data as data2, " + // name
                "f3.hash as hash3, f3.fact_id as id3, f3.data as data3 " +  // next
            "FROM fact f1 " +   // project
            "JOIN edge e1 " +   // name->project
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +   // name
                "ON f2.fact_id = e1.successor_fact_id " +
            "JOIN edge e2 " +   // next->project
                "ON e2.predecessor_fact_id = f1.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +   // next
                "ON f3.fact_id = e2.successor_fact_id " +
            "JOIN edge e3 " +   // next->prior
                "ON e3.predecessor_fact_id = f2.fact_id " +
                "AND e3.successor_fact_id = f3.fact_id " +
                "AND e3.role_id = ?5 " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "ORDER BY f2.fact_id ASC, f3.fact_id ASC"
        );
    }

    [Fact]
    public void ShouldHandleAdditionalPathConditionsInCondition()
    {
        var specification = Given<Models.Project>.Match<ProjectName>((project, facts) =>
            from name in facts.OfType<ProjectName>()
            where name.project == project &&
                !facts.Any<ProjectName>(next =>
                    next.project == project &&
                    next.prior.Contains(name))
            select name
        );
        SqlQueryTree sqlQueryTree = specification.ToSql();

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " + // project
                "f2.hash as hash2, f2.fact_id as id2, f2.data as data2 " +  // name
            "FROM fact f1 " +   // project
            "JOIN edge e1 " +   // name->project
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +   // name
                "ON f2.fact_id = e1.successor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "AND NOT EXISTS (" +
                "SELECT 1 " +
                "FROM edge e2 " +   // next->project
                "JOIN fact f3 " +   // next
                    "ON f3.fact_id = e2.successor_fact_id " +
                "JOIN edge e3 " +   // next->prior
                    "ON e3.predecessor_fact_id = f2.fact_id " +
                    "AND e3.successor_fact_id = f3.fact_id " +
                    "AND e3.role_id = ?5 " +
                "WHERE e2.predecessor_fact_id = f1.fact_id " +
                    "AND e2.role_id = ?4" +
            ") " +
            "ORDER BY f2.fact_id ASC"
        );
    }

    [Fact]
    public void ShouldHandleJoinToPredecessorCollection()
    {
        var specification = Given<Models.Project>.Match<ProjectName>((project, facts) =>
            from name in facts.OfType<ProjectName>()
            where name.project == project
            from prior in facts.OfType<ProjectName>()
            where prior.project == project &&
                name.prior.Contains(prior)
            select prior
        );
        SqlQueryTree sqlQueryTree = specification.ToSql();

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " + // project
                "f2.hash as hash2, f2.fact_id as id2, f2.data as data2, " + // name
                "f3.hash as hash3, f3.fact_id as id3, f3.data as data3 " +  // prior
            "FROM fact f1 " +   // project
            "JOIN edge e1 " +   // name->project
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +   // name
                "ON f2.fact_id = e1.successor_fact_id " +
            "JOIN edge e2 " +   // prior->project
                "ON e2.predecessor_fact_id = f1.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +   // prior
                "ON f3.fact_id = e2.successor_fact_id " +
            "JOIN edge e3 " +   // prior->name
                "ON e3.predecessor_fact_id = f3.fact_id " +
                "AND e3.successor_fact_id = f2.fact_id " +
                "AND e3.role_id = ?5 " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "ORDER BY f2.fact_id ASC, f3.fact_id ASC"
        );
    }

    [Fact]
    public void ShouldHandleTwoGivens()
    {
        var specification = Given<Company, User>.Match<Assignment>((company, user, facts) =>
            from project in facts.OfType<Models.Project>()
            where project.department.company == company
            from assignment in facts.OfType<Assignment>()
            where assignment.project == project
            where assignment.user == user
            select assignment
        );
        SqlQueryTree sqlQueryTree = specification.ToSql();

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +  // company
                "f5.hash as hash5, f5.fact_id as id5, f5.data as data5, " +  // user
                "f3.hash as hash3, f3.fact_id as id3, f3.data as data3, " +  // project
                "f4.hash as hash4, f4.fact_id as id4, f4.data as data4 " +   // assignment
            "FROM fact f1 " +  // company
            "JOIN edge e1 " +  // department->company
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +  // department
                "ON f2.fact_id = e1.successor_fact_id " +
            "JOIN edge e2 " +  // project->department
                "ON e2.predecessor_fact_id = f2.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +  // project
                "ON f3.fact_id = e2.successor_fact_id " +
            "JOIN edge e3 " +  // assignment->project
                "ON e3.predecessor_fact_id = f3.fact_id " +
                "AND e3.role_id = ?5 " +
            "JOIN fact f4 " +  // assignment
                "ON f4.fact_id = e3.successor_fact_id " +
            "JOIN edge e4 " +  // assignment->user
                "ON e4.successor_fact_id = f4.fact_id " +
                "AND e4.role_id = ?8 " +
            "JOIN fact f5 " +  // user
                "ON f5.fact_id = e4.predecessor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 AND f5.fact_type_id = ?6 AND f5.hash = ?7 " +
            "ORDER BY f3.fact_id ASC, f4.fact_id ASC"
        );
    }

    [Fact]
    public void ShouldHandleTwoGivensInOppositeOrder()
    {
        var specification = Given<Company, User>.Match<Assignment>((company, user, facts) =>
            from project in facts.OfType<Models.Project>()
            where project.department.company == company
            from assignment in facts.OfType<Assignment>()
            where assignment.user == user
            where assignment.project == project
            select assignment
        );
        SqlQueryTree sqlQueryTree = specification.ToSql();

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +  // company
                "f5.hash as hash5, f5.fact_id as id5, f5.data as data5, " +  // user
                "f3.hash as hash3, f3.fact_id as id3, f3.data as data3, " +  // project
                "f4.hash as hash4, f4.fact_id as id4, f4.data as data4 " +   // assignment
            "FROM fact f1 " +  // company
            "JOIN edge e1 " +  // department->company
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +  // department
                "ON f2.fact_id = e1.successor_fact_id " +
            "JOIN edge e2 " +  // project->department
                "ON e2.predecessor_fact_id = f2.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +  // project
                "ON f3.fact_id = e2.successor_fact_id " +
            "JOIN edge e3 " +  // assignment->project
                "ON e3.predecessor_fact_id = f3.fact_id " +
                "AND e3.role_id = ?5 " +
            "JOIN fact f4 " +  // assignment
                "ON f4.fact_id = e3.successor_fact_id " +
            "JOIN edge e4 " +  // assignment->user
                "ON e4.successor_fact_id = f4.fact_id " +
                "AND e4.role_id = ?8 " +
            "JOIN fact f5 " +  // user
                "ON f5.fact_id = e4.predecessor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 AND f5.fact_type_id = ?6 AND f5.hash = ?7 " +
            "ORDER BY f3.fact_id ASC, f4.fact_id ASC"
        );
    }

    [Fact]
    public void ShouldHandleTwoGivensWithExistentialCondition()
    {
        var specification = Given<Company, User>.Match<Models.Project>((company, user, facts) =>
            from project in facts.OfType<Models.Project>()
            where project.department.company == company &&
                facts.Any<Assignment>(assignment =>
                    assignment.project == project &&
                    assignment.user == user)
            select project
        );
        SqlQueryTree sqlQueryTree = specification.ToSql();

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +  // company
                "f5.hash as hash5, f5.fact_id as id5, f5.data as data5, " +  // user
                "f3.hash as hash3, f3.fact_id as id3, f3.data as data3 " +   // project
            "FROM fact f1 " +  // company
            "JOIN edge e1 " +  // department->company
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +  // department
                "ON f2.fact_id = e1.successor_fact_id " +
            "JOIN edge e2 " +  // project->department
                "ON e2.predecessor_fact_id = f2.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +  // project
                "ON f3.fact_id = e2.successor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 AND f5.fact_type_id = ?6 AND f5.hash = ?7 " +
            "AND EXISTS (" +
                "SELECT 1 " +
                "FROM edge e3 " +  // assignment->project
                "JOIN fact f4 " +  // assignment
                    "ON f4.fact_id = e3.successor_fact_id " +
                "JOIN edge e4 " +  // assignment->user
                    "ON e4.successor_fact_id = f4.fact_id " +
                    "AND e4.role_id = ?8 " +
                "JOIN fact f5 " +  // user
                    "ON f5.fact_id = e4.predecessor_fact_id " +
                "WHERE e3.predecessor_fact_id = f3.fact_id " +
                "AND e3.role_id = ?5" +
            ") " +
            "ORDER BY f3.fact_id ASC"
        );
    }

    [Fact]
    public void ShouldHandleSpecificationWithTwoPaths()
    {
        var specification = Given<DeviceSession, OrderSourceKey>.Match((session, school, facts) =>
            facts.OfType<SavedOrder>()
                .Where(saved => saved.Session == session)
                .Where(saved => saved.Details.School == school)
                .Where(saved => !facts.OfType<SavedOrder>()
                    .Where(s => s.History.Contains(saved))
                    .Any()
                )
                .Where(saved => !facts.OfType<ReceivedOrder>()
                    .Where(s => s.Order == saved.Details.Order)
                    .Any()
                )
        );

        var expected =
        """
        (session: Qma.DeviceSession, school: Qma.Order.Source.Key) {
            saved: Qma.Order.Saved [
                saved->Session: Qma.DeviceSession = session
                saved->Details: Qma.Order.Details->School: Qma.Order.Source.Key = school
                !E {
                    s: Qma.Order.Saved [
                        s->History: Qma.Order.Saved = saved
                    ]
                }
                !E {
                    s: Qma.Order.Received [
                        s->Order: Qma.Order = saved->Details: Qma.Order.Details->Order: Qma.Order
                    ]
                }
            ]
        } => saved

        """.Replace("\r\n", "\n");

        var actual = specification.ToDescriptiveString().Replace("\r\n", "\n");

        actual.Should().Be(expected);

        SqlQueryTree sqlQueryTree = specification.ToSql();

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +  // session
                "f2.hash as hash2, f2.fact_id as id2, f2.data as data2, " +  // school
                "f3.hash as hash3, f3.fact_id as id3, f3.data as data3 " +   // saved
            "FROM fact f1 " +  // session
            "JOIN edge e1 " +  // saved->session
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = ?3 " +
            "JOIN fact f2 " +  // school
                "ON f2.fact_id = e1.successor_fact_id " +
            "JOIN edge e2 " +  // saved->details->school
                "ON e2.predecessor_fact_id = f3.fact_id " +
                "AND e2.role_id = ?4 " +
            "JOIN fact f3 " +  // saved
                "ON f3.fact_id = e2.successor_fact_id " +
            "WHERE f1.fact_type_id = ?1 AND f1.hash = ?2 " +
            "AND f2.fact_type_id = ?5 AND f2.hash = ?6 " +
            "AND NOT EXISTS (" +
                "SELECT 1 " +
                "FROM edge e3 " +  // saved->history
                "JOIN fact f4 " +  // saved
                    "ON f4.fact_id = e3.successor_fact_id " +
                "JOIN edge e4 " +  // saved->history->saved
                    "ON e4.predecessor_fact_id = f4.fact_id " +
                    "AND e4.successor_fact_id = f3.fact_id " +
                    "AND e4.role_id = ?7 " +
                "WHERE e3.predecessor_fact_id = f3.fact_id " +
                    "AND e3.role_id = ?8" +
            ") " +
            "AND NOT EXISTS (" +
                "SELECT 1 " +
                "FROM edge e5 " +  // received->order
                "JOIN fact f6 ");
    }
}