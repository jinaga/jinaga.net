using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Store.SQLite.Builder;
using Jinaga.Store.SQLite.Generation;
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
        SqlQueryTree sqlQueryTree = SqlFor(specification);

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
        SqlQueryTree sqlQueryTree = SqlFor(specification);

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
        var specification = Given<Company>.Match((company, facts) =>
            from project in facts.OfType<Project>()
            where project.department.company == company
            where !(
                from deleted in facts.OfType<ProjectDeleted>()
                where deleted.project == project
                select deleted
            ).Any()
            select project
        );
        SqlQueryTree sqlQueryTree = SqlFor(specification);

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
        var specification = Given<Company>.Match((company, facts) =>
            from project in facts.OfType<Project>()
            where project.department.company == company
            where (
                from deleted in facts.OfType<ProjectDeleted>()
                where deleted.project == project
                select deleted
            ).Any()
            select project
        );
        SqlQueryTree sqlQueryTree = SqlFor(specification);

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
        var specification = Given<Company>.Match((company, facts) =>
            from project in facts.OfType<Project>()
            where project.department.company == company
            where !(
                from deleted in facts.OfType<ProjectDeleted>()
                where deleted.project == project
                where !(
                    from restored in facts.OfType<ProjectRestored>()
                    where restored.deleted == deleted
                    select restored
                ).Any()
                select deleted
            ).Any()
            select project
        );
        SqlQueryTree sqlQueryTree = SqlFor(specification);

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
                projects = facts.OfType<Project>(project =>
                    project.department == department)
            }
        );
        SqlQueryTree sqlQueryTree = SqlFor(specification);

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

    private SqlQueryTree SqlFor(Specification specification)
    {
        var factTypes = GetAllFactTypes(specification);
        var roleMap = GetAllRoles(specification, factTypes);

        var givenTuple = specification.Given
            .Select((label, index) => (
                name: label.Name,
                reference: new FactReference(label.Type, $"{index + 1001}")
            ))
            .Aggregate(FactReferenceTuple.Empty, (tuple, item) => tuple.Add(item.name, item.reference));

        var descriptionBuilder = new ResultDescriptionBuilder(factTypes, roleMap);
        string str = specification.ToString();
        var description = descriptionBuilder.Build(givenTuple, specification);

        var sqlQueryTree = SqlGenerator.CreateSqlQueryTree(description);
        return sqlQueryTree;
    }

    private ImmutableDictionary<string, int> GetAllFactTypes(Specification specification)
    {
        return GetAllFactTypesFromSpecification(specification)
            .Select((name, index) => KeyValuePair.Create(name, index + 1))
            .ToImmutableDictionary();
    }

    private IEnumerable<string> GetAllFactTypesFromSpecification(Specification specification)
    {
        var factTypeNames = specification.Given
            .Select(label => label.Type)
            .ToImmutableList();
        factTypeNames = factTypeNames.AddRange(GetAllFactTypesFromMatches(specification.Matches));
        if (specification.Projection is CompoundProjection compoundProjection)
        {
            factTypeNames = factTypeNames.AddRange(GetAllFactTypesFromProjection(compoundProjection));
        }
        return factTypeNames.Distinct();
    }

    private IEnumerable<string> GetAllFactTypesFromMatches(ImmutableList<Match> matches)
    {
        var factTypeNames = ImmutableList<string>.Empty;
        foreach (var match in matches)
        {
            factTypeNames = factTypeNames.Add(match.Unknown.Type);
            foreach (var pathCondition in match.PathConditions)
            {
                factTypeNames = factTypeNames.AddRange(
                    pathCondition.RolesLeft.Select(role =>
                        role.TargetType));
                factTypeNames = factTypeNames.AddRange(
                    pathCondition.RolesRight.Select(role =>
                        role.TargetType));
            }
            foreach (var existentialCondition in match.ExistentialConditions)
            {
                factTypeNames = factTypeNames.AddRange(
                    GetAllFactTypesFromMatches(existentialCondition.Matches));
            }
        }
        return factTypeNames.Distinct();
    }

    private IEnumerable<string> GetAllFactTypesFromProjection(CompoundProjection compoundProjection)
    {
        var factTypeNames = ImmutableList<string>.Empty;

        foreach (var name in compoundProjection.Names)
        {
            var projection = compoundProjection.GetProjection(name);
            if (projection is CompoundProjection nestedCompoundProjection)
            {
                factTypeNames = factTypeNames.AddRange(
                    GetAllFactTypesFromProjection(nestedCompoundProjection));
            }
            else if (projection is CollectionProjection collectionProjection)
            {
                factTypeNames = factTypeNames.AddRange(
                    GetAllFactTypesFromMatches(collectionProjection.Matches));
                if (collectionProjection.Projection is CompoundProjection nestedCompoundProjection2)
                {
                    factTypeNames = factTypeNames.AddRange(
                        GetAllFactTypesFromProjection(nestedCompoundProjection2));
                }
            }
        }
        return factTypeNames.Distinct();
    }

    private ImmutableDictionary<int, ImmutableDictionary<string, int>> GetAllRoles(Specification specification, ImmutableDictionary<string, int> factTypes)
    {
        var distinctRoles = GetAllRolesFromSpecification(specification)
            .GroupBy(pair => pair.factType, pair => pair.roleName)
            .SelectMany(group => group.Distinct().Select(roleName => new
            {
                FactType = group.Key,
                RoleName = roleName
            }));
        var rolesByFactTypeId = distinctRoles
            .Select((pair, index) => new
            {
                FactTypeId = factTypes[pair.FactType],
                RoleName = pair.RoleName,
                RoleId = index + 1
            })
            .GroupBy(role => role.FactTypeId, role => KeyValuePair.Create(
                role.RoleName, role.RoleId
            ))
            .Select(pair => KeyValuePair.Create(pair.Key, pair.ToImmutableDictionary()))
            .ToImmutableDictionary();
        return rolesByFactTypeId;
    }

    private IEnumerable<(string factType, string roleName)> GetAllRolesFromSpecification(Specification specification)
    {
        var typesByLabel = specification.Given
            .Select(label => KeyValuePair.Create(label.Name, label.Type))
            .ToImmutableDictionary();
        typesByLabel = typesByLabel.AddRange(specification.Matches
            .Select(match => KeyValuePair.Create(match.Unknown.Name, match.Unknown.Type)));
        var roles = GetAllRolesFromMatches(typesByLabel, specification.Matches).ToImmutableList();
        roles = roles.AddRange(GetAllRolesFromProjection(typesByLabel, specification.Projection));
        return roles;
    }

    private IEnumerable<(string factType, string roleName)> GetAllRolesFromMatches(ImmutableDictionary<string, string> typesByLabel, ImmutableList<Match> matches)
    {
        var roles = ImmutableList<(string factType, string roleName)>.Empty;
        foreach (var match in matches)
        {
            foreach (var pathCondition in match.PathConditions)
            {
                var type = match.Unknown.Type;
                foreach (var role in pathCondition.RolesLeft)
                {
                    roles = roles.Add((type, role.Name));
                    type = role.TargetType;
                }
                type = typesByLabel[pathCondition.LabelRight];
                foreach (var role in pathCondition.RolesRight)
                {
                    roles = roles.Add((type, role.Name));
                    type = role.TargetType;
                }
            }
            foreach (var existentialCondition in match.ExistentialConditions)
            {
                typesByLabel = typesByLabel.AddRange(existentialCondition.Matches
                    .Select(match => KeyValuePair.Create(match.Unknown.Name, match.Unknown.Type)));
                roles = roles.AddRange(GetAllRolesFromMatches(typesByLabel, existentialCondition.Matches));
            }
        }
        return roles;
    }

    private IEnumerable<(string factType, string roleName)> GetAllRolesFromProjection(ImmutableDictionary<string, string> typesByLabel, Projection projection)
    {
        var roles = ImmutableList<(string factType, string roleName)>.Empty;
        if (projection is CompoundProjection compoundProjection)
        {
            foreach (var name in compoundProjection.Names)
            {
                var nestedProjection = compoundProjection.GetProjection(name);
                if (nestedProjection is CompoundProjection nestedCompoundProjection)
                {
                    roles = roles.AddRange(GetAllRolesFromProjection(typesByLabel, nestedCompoundProjection));
                }
                else if (nestedProjection is CollectionProjection collectionProjection)
                {
                    roles = roles.AddRange(GetAllRolesFromMatches(typesByLabel, collectionProjection.Matches));
                    if (collectionProjection.Projection is CompoundProjection nestedCompoundProjection2)
                    {
                        roles = roles.AddRange(GetAllRolesFromProjection(typesByLabel, nestedCompoundProjection2));
                    }
                }
            }
        }
        return roles;
    }
}