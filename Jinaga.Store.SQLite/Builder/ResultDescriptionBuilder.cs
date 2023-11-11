using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Store.SQLite.Description;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Store.SQLite.Builder
{
    internal class ResultDescriptionBuilder
    {
        private ImmutableDictionary<string, int> factTypes;
        private ImmutableDictionary<int, ImmutableDictionary<string, int>> roleMap;

        public ResultDescriptionBuilder(ImmutableDictionary<string, int> factTypes, ImmutableDictionary<int, ImmutableDictionary<string, int>> roleMap)
        {
            this.factTypes = factTypes;
            this.roleMap = roleMap;
        }

        public ResultDescription Build(FactReferenceTuple givenTuple, Specification specification)
        {
            // Verify that the number of start references matches the number of given facts.
            if (givenTuple.Names.Count() != specification.Given.Count)
            {
                throw new ArgumentException($"The number of start facts ({givenTuple.Names.Count()}) does not match the number of inputs ({specification.Given.Count}).");
            }
            // Verify that the start reference types match the given fact types.
            foreach (var label in specification.Given)
            {
                var reference = givenTuple.Get(label.Name);
                if (reference.Type != label.Type)
                {
                    throw new ArgumentException($"The start fact type ({reference.Type}) does not match the input type ({label.Type}).");
                }
            }

            var context = ResultDescriptionBuilderContext.Empty;
            return CreateResultDescription(context, specification.Given, givenTuple, specification.Matches, specification.Projection);
        }

        private ResultDescription CreateResultDescription(ResultDescriptionBuilderContext context, ImmutableList<Label> given, FactReferenceTuple givenTuple, ImmutableList<Match> matches, Projection projection)
        {
            context = AddEdges(context, given, givenTuple, matches);

            if (!context.QueryDescription.IsSatisfiable())
            {
                // Abort the branch if the query is not satisfiable
                return new ResultDescription(
                    context.QueryDescription,
                    ImmutableDictionary<string, ResultDescription>.Empty
                );
            }

            var childResultDescriptions = ImmutableDictionary<string, ResultDescription>.Empty;
            if (projection is CompoundProjection compoundProjection)
            {
                foreach (var name in compoundProjection.Names)
                {
                    var childProjection = compoundProjection.GetProjection(name);
                    if (childProjection is CollectionProjection collectionProjection)
                    {
                        var resultDescription = CreateResultDescription(
                            context,
                            given,
                            givenTuple,
                            collectionProjection.Matches,
                            collectionProjection.Projection);
                        childResultDescriptions = childResultDescriptions.Add(name, resultDescription);
                    }
                }
            }

            return new ResultDescription(
                context.QueryDescription,
                childResultDescriptions
            );
        }

        private ResultDescriptionBuilderContext AddEdges(ResultDescriptionBuilderContext context, ImmutableList<Label> given, FactReferenceTuple givenTuple, ImmutableList<Match> matches)
        {
            foreach (var match in matches)
            {
                foreach (var pathCondition in match.PathConditions)
                {
                    context = AddPathCondition(context, pathCondition, given, givenTuple, match.Unknown, "");
                }
                foreach (var existentialCondition in match.ExistentialConditions)
                {
                    // Apply the where clause and continue with the tuple where it is true.
                    // The path describes which not-exists condition we are currently building on.
                    // Because the path is not empty, labeled facts will be included in the output.
                    var contextWithCondition = context.WithExistentialCondition(existentialCondition.Exists);
                    var contextConditional = AddEdges(contextWithCondition, given, givenTuple, existentialCondition.Matches);

                    // If the negative existential condition is not satisfiable, then
                    // that means that the condition will always be true.
                    // We can therefore skip the branch for the negative existential condition.
                    if (contextConditional.QueryDescription.IsSatisfiable())
                    {
                        context = context.WithQueryDescription(contextConditional.QueryDescription);
                    }
                }
                if (!context.QueryDescription.IsSatisfiable())
                {
                    break;
                }
            }
            return context;
        }

        private ResultDescriptionBuilderContext AddPathCondition(ResultDescriptionBuilderContext context, PathCondition pathCondition, ImmutableList<Label> given, FactReferenceTuple givenTuple, Label unknown, string v)
        {
            // If no input parameter has been allocated, allocate one now.
            if (!context.KnownFacts.ContainsKey(pathCondition.LabelRight))
            {
                var givenIndex = given.FindIndex(given => given.Name == pathCondition.LabelRight);
                if (givenIndex < 0)
                {
                    throw new ArgumentException($"No input parameter found for label {pathCondition.LabelRight}");
                }

                var factReference = givenTuple.Get(pathCondition.LabelRight);
                // If the type is not known, then no facts matching the condition can
                // exist. The query is unsatisfiable.
                if (!factTypes.ContainsKey(factReference.Type))
                {
                    return context.WithQueryDescription(QueryDescription.Empty);
                }
                int factTypeId = EnsureGetFactTypeId(factReference.Type);
                context = context.WithInputParameter(
                    given[givenIndex],
                    factTypeId,
                    factReference.Hash
                );
            }

            var roleCount = pathCondition.RolesLeft.Count + pathCondition.RolesRight.Count;

            // Walk up the right-hand side.
            // This generates predecessor joins from a given or prior label.
            var fact = context.KnownFacts[pathCondition.LabelRight];
            var type = fact.Type;
            var factIndex = fact.FactIndex;
            for (int i = 0; i < pathCondition.RolesRight.Count; i++)
            {
                var role = pathCondition.RolesRight[i];
                // If the type or role is not known, then no facts matching the condition can
                // exist. The query is unsatisfiable.
                if (!factTypes.ContainsKey(type))
                {
                    return context.WithQueryDescription(QueryDescription.Empty);
                }
                var typeId = factTypes[type];

                if (!roleMap.ContainsKey(typeId) || !roleMap[typeId].ContainsKey(role.Name))
                {
                    return context.WithQueryDescription(QueryDescription.Empty);
                }
                var roleId = roleMap[typeId][role.Name];

                var successorFactIndex = factIndex;
                (context, factIndex) = context.WithFact(role.TargetType);
                context = context.WithEdge(factIndex, successorFactIndex, roleId);
                
                type = role.TargetType;
            }

            var rightType = type;

            // Walk up the left-hand side.
            // We will need to reverse this walk to generate successor joins.
            type = unknown.Type;
            var newEdges = ImmutableList<(int roleId, string successorType)>.Empty;
            foreach (var role in pathCondition.RolesLeft)
            {
                // If the type or role is not known, then no facts matching the condition can
                // exist. The query is unsatisfiable.
                if (!factTypes.ContainsKey(type))
                {
                    return context.WithQueryDescription(QueryDescription.Empty);
                }
                var typeId = factTypes[type];

                if (!roleMap.ContainsKey(typeId) || !roleMap[typeId].ContainsKey(role.Name))
                {
                    return context.WithQueryDescription(QueryDescription.Empty);
                }
                var roleId = roleMap[typeId][role.Name];

                newEdges = newEdges.Add((roleId, type));
                type = role.TargetType;
            }

            if (type != rightType)
            {
                throw new ArgumentException($"Type mismatch: {type} is compared to {rightType}");
            }

            // Reverse the walk to generate successor joins.
            for (int i = newEdges.Count - 1; i >= 0; i--)
            {
                var (roleId, successorType) = newEdges[i];
                var predecessorFactIndex = factIndex;
                (context, factIndex) = context.WithFact(successorType);
                context = context.WithEdge(predecessorFactIndex, factIndex, roleId);
            }

            context = context.WithLabel(unknown, factIndex);
            return context;
        }

        private int EnsureGetFactTypeId(string type)
        {
            if (!factTypes.ContainsKey(type))
            {
                throw new ArgumentException($"Unknown fact type {type}");
            }
            return factTypes[type];
        }
    }
}