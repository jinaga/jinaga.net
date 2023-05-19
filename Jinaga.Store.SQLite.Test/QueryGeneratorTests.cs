using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Store.SQLite.Builder;
using Jinaga.Store.SQLite.Generation;
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
                "AND e1.role_id = $3 " +
            "JOIN fact f2 " +
                "ON f2.fact_id = e1.successor_fact_id " +
            "WHERE f1.fact_type_id = $1 AND f1.hash = $2 " +
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
                "AND e1.role_id = $3 " +
            "JOIN fact f2 " +
                "ON f2.fact_id = e1.predecessor_fact_id " +
            "WHERE f1.fact_type_id = $1 AND f1.hash = $2 " +
            "ORDER BY f2.fact_id ASC"
        );
        sqlQueryTree.ChildQueries.Should().BeEmpty();
    }

    private SqlQueryTree SqlFor(Specification specification)
    {
        var factTypes = GetAllFactTypes(specification);
        var roleMap = GetAllRoles(specification, factTypes);

        var startReferences = specification.Given
            .Select((label, index) => new FactReference(label.Type, $"{index+1001}"))
            .ToImmutableList();

        var descriptionBuilder = new ResultDescriptionBuilder(factTypes, roleMap);
        var description = descriptionBuilder.Build(startReferences, specification);

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
            foreach (var condition in match.Conditions)
            {
                if (condition is PathCondition pathCondition)
                {
                    factTypeNames = factTypeNames.AddRange(
                        pathCondition.RolesLeft.Select(role =>
                            role.TargetType));
                    factTypeNames = factTypeNames.AddRange(
                        pathCondition.RolesRight.Select(role =>
                            role.TargetType));
                }
                else if (condition is ExistentialCondition existentialCondition)
                {
                    factTypeNames = factTypeNames.AddRange(
                        GetAllFactTypesFromMatches(existentialCondition.Matches));
                }
            }
        }
        return factTypeNames.Distinct();
    }

    private IEnumerable<string> GetAllFactTypesFromProjection(CompoundProjection compoundProjection)
    {
        throw new NotImplementedException();
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
        return roles;
    }

    private IEnumerable<(string factType, string roleName)> GetAllRolesFromMatches(ImmutableDictionary<string, string> typesByLabel, ImmutableList<Match> matches)
    {
        var roles = ImmutableList<(string factType, string roleName)>.Empty;
        foreach (var match in matches)
        {
            foreach (var condition in match.Conditions)
            {
                if (condition is PathCondition pathCondition)
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
                else if (condition is ExistentialCondition existentialCondition)
                {
                    typesByLabel = typesByLabel.AddRange(existentialCondition.Matches
                        .Select(match => KeyValuePair.Create(match.Unknown.Name, match.Unknown.Type)));
                    roles = roles.AddRange(GetAllRolesFromMatches(typesByLabel, existentialCondition.Matches));
                }
            }
        }
        return roles;
    }
}