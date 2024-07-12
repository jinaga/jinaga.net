namespace Jinaga.Store.SQLite.Test;

internal static class SpecificationExtensions
{
    public static SqlQueryTree ToSql(this Specification specification)
    {
        var factTypes = GetAllFactTypes(specification);
        var roleMap = GetAllRoles(specification, factTypes);

        var givenTuple = specification.Givens
            .Select((given, index) => (
                name: given.Label.Name,
                reference: new FactReference(given.Label.Type, $"{index + 1001}")
            ))
            .Aggregate(FactReferenceTuple.Empty, (tuple, item) => tuple.Add(item.name, item.reference));

        var descriptionBuilder = new ResultDescriptionBuilder(factTypes, roleMap);
        string str = specification.ToString();
        var description = descriptionBuilder.Build(givenTuple, specification);

        var sqlQueryTree = SqlGenerator.CreateSqlQueryTree(description);
        return sqlQueryTree;
    }

    private static ImmutableDictionary<string, int> GetAllFactTypes(Specification specification)
    {
        return GetAllFactTypesFromSpecification(specification)
            .Select((name, index) => KeyValuePair.Create(name, index + 1))
            .ToImmutableDictionary();
    }

    private static IEnumerable<string> GetAllFactTypesFromSpecification(Specification specification)
    {
        var factTypeNames = specification.Givens
            .Select(g => g.Label.Type)
            .ToImmutableList();
        factTypeNames = factTypeNames.AddRange(GetAllFactTypesFromMatches(specification.Matches));
        if (specification.Projection is CompoundProjection compoundProjection)
        {
            factTypeNames = factTypeNames.AddRange(GetAllFactTypesFromProjection(compoundProjection));
        }
        return factTypeNames.Distinct();
    }

    private static IEnumerable<string> GetAllFactTypesFromMatches(ImmutableList<Match> matches)
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

    private static IEnumerable<string> GetAllFactTypesFromProjection(CompoundProjection compoundProjection)
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

    private static ImmutableDictionary<int, ImmutableDictionary<string, int>> GetAllRoles(Specification specification, ImmutableDictionary<string, int> factTypes)
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

    private static IEnumerable<(string factType, string roleName)> GetAllRolesFromSpecification(Specification specification)
    {
        var typesByLabel = specification.Givens
            .Select(g => KeyValuePair.Create(g.Label.Name, g.Label.Type))
            .ToImmutableDictionary();
        typesByLabel = typesByLabel.AddRange(specification.Matches
            .Select(match => KeyValuePair.Create(match.Unknown.Name, match.Unknown.Type)));
        var roles = GetAllRolesFromMatches(typesByLabel, specification.Matches).ToImmutableList();
        foreach (var existentialCondition in specification.Givens.SelectMany(g => g.ExistentialConditions))
        {
            typesByLabel = typesByLabel.AddRange(existentialCondition.Matches
                .Select(match => KeyValuePair.Create(match.Unknown.Name, match.Unknown.Type)));
            roles = roles.AddRange(GetAllRolesFromMatches(typesByLabel, existentialCondition.Matches));
        }
        roles = roles.AddRange(GetAllRolesFromProjection(typesByLabel, specification.Projection));
        return roles;
    }

    private static IEnumerable<(string factType, string roleName)> GetAllRolesFromMatches(ImmutableDictionary<string, string> typesByLabel, ImmutableList<Match> matches)
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
                var extendedTypesByLabel = typesByLabel.AddRange(existentialCondition.Matches
                    .Select(match => KeyValuePair.Create(match.Unknown.Name, match.Unknown.Type)));
                roles = roles.AddRange(GetAllRolesFromMatches(extendedTypesByLabel, existentialCondition.Matches));
            }
        }
        return roles;
    }

    private static IEnumerable<(string factType, string roleName)> GetAllRolesFromProjection(ImmutableDictionary<string, string> typesByLabel, Projection projection)
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