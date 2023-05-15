using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Projections;

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
        SqlQueryTree sqlQueryTree = SqlFor(specification);

        sqlQueryTree.SqlQuery.Sql.Should().Be(
            "SELECT " +
                "f1.hash as hash1, f1.fact_id as id1, f1.data as data1, " +
                "f2.hash as hash2, f2.fact_id as id2, f2.data as data2 " +
            "FROM public.fact f1 " +
            "JOIN public.edge e1 " +
                "ON e1.predecessor_fact_id = f1.fact_id " +
                "AND e1.role_id = $3 " +
            "JOIN public.fact f2 " +
                "ON f2.fact_id = e1.successor_fact_id " +
            "WHERE f1.fact_type_id = $1 AND f1.hash = $2 " +
            "ORDER BY f2.fact_id ASC"
        );
        sqlQueryTree.ChildQueries.Should().BeEmpty();
    }

    private SqlQueryTree SqlFor(Specification specification)
    {
        throw new NotImplementedException();
    }
}