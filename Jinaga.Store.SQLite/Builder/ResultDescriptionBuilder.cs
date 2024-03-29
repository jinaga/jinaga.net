﻿using Jinaga.Facts;
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
            if (givenTuple.Names.Count() != specification.Givens.Count)
            {
                throw new ArgumentException($"The number of start facts ({givenTuple.Names.Count()}) does not match the number of inputs ({specification.Givens.Count}).");
            }
            // Verify that the start reference types match the given fact types.
            foreach (var given in specification.Givens)
            {
                var reference = givenTuple.Get(given.Label.Name);
                if (reference.Type != given.Label.Type)
                {
                    throw new ArgumentException($"The start fact type ({reference.Type}) does not match the input type ({given.Label.Type}).");
                }
            }

            var context = ResultDescriptionBuilderContext.Empty;
            return CreateResultDescription(context, specification.Givens, givenTuple, specification.Matches, specification.Projection);
        }

        private ResultDescription CreateResultDescription(ResultDescriptionBuilderContext context, ImmutableList<SpecificationGiven> givens, FactReferenceTuple givenTuple, ImmutableList<Match> matches, Projection projection)
        {
            context = AddEdges(context, givens, givenTuple, matches);

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
                            givens,
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

        private ResultDescriptionBuilderContext AddEdges(ResultDescriptionBuilderContext context, ImmutableList<SpecificationGiven> givens, FactReferenceTuple givenTuple, ImmutableList<Match> matches)
        {
            foreach (var match in matches)
            {
                var sortedPathConditions = SortPathConditions(context, match.PathConditions);
                foreach (var pathCondition in sortedPathConditions)
                {
                    context = AddPathCondition(context, pathCondition, givens, givenTuple, match.Unknown, "");
                    if (!context.QueryDescription.IsSatisfiable())
                    {
                        return context;
                    }
                }
                foreach (var existentialCondition in match.ExistentialConditions)
                {
                    // Apply the where clause and continue with the tuple where it is true.
                    // The path describes which not-exists condition we are currently building on.
                    // Because the path is not empty, labeled facts will be included in the output.
                    var contextWithCondition = context.WithExistentialCondition(existentialCondition.Exists);

                    // Remove all labels that share a name with an unknown in the existential
                    // condition so that they can be redefined within its scope.
                    var nestedContext = existentialCondition.Matches.Select(match => match.Unknown)
                        .Aggregate(contextWithCondition, (context, label) => context.WithoutLabel(label));

                    var contextConditional = AddEdges(nestedContext, givens, givenTuple, existentialCondition.Matches);

                    // If the negative existential condition is not satisfiable, then
                    // that means that the condition will always be true.
                    // We can therefore skip the branch for the negative existential condition.
                    if (contextConditional.QueryDescription.IsSatisfiable())
                    {
                        context = context.WithQueryDescription(contextConditional.QueryDescription);
                    }
                    else if (existentialCondition.Exists)
                    {
                        // If a positive existential condition is not satisfiable,
                        // then the whole expression is not satisfiable.
                        return ResultDescriptionBuilderContext.Empty;
                    }
                }
            }
            return context;
        }

        private ImmutableList<PathCondition> SortPathConditions(ResultDescriptionBuilderContext context, ImmutableList<PathCondition> pathConditions)
        {
            if (pathConditions.Count <= 1)
            {
                return pathConditions;
            }
            // Favor path conditions that reference known facts.
            var knownFacts = context.KnownFacts.Keys.ToImmutableHashSet();
            var sortedPathConditions = pathConditions
                .OrderByDescending(pathCondition => knownFacts.Contains(pathCondition.LabelRight))
                .ToImmutableList();

            return sortedPathConditions;
        }

        private ResultDescriptionBuilderContext AddPathCondition(ResultDescriptionBuilderContext context, PathCondition pathCondition, ImmutableList<SpecificationGiven> givens, FactReferenceTuple givenTuple, Label unknown, string v)
        {
            // If no input parameter has been allocated, allocate one now.
            if (!context.KnownFacts.ContainsKey(pathCondition.LabelRight))
            {
                var givenIndex = givens.FindIndex(given => given.Label.Name == pathCondition.LabelRight);
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
                    givens[givenIndex].Label,
                    factTypeId,
                    factReference.Hash
                );
                foreach (var existentialCondition in givens[givenIndex].ExistentialConditions)
                {
                    var contextWithExistentialCondition = context.WithExistentialCondition(existentialCondition.Exists);
                    contextWithExistentialCondition = AddEdges(contextWithExistentialCondition, givens, givenTuple, existentialCondition.Matches);

                    if (contextWithExistentialCondition.QueryDescription.IsSatisfiable())
                    {
                        context = context.WithQueryDescription(contextWithExistentialCondition.QueryDescription);
                    }
                    else if (existentialCondition.Exists)
                    {
                        // If an existential condition is not satisfiable,
                        // then the whole query is not satisfiable.
                        return context.WithQueryDescription(QueryDescription.Empty);
                    }
                }
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

                // If we have already written the output, we can use the fact index.
                if (context.KnownFacts.TryGetValue(unknown.Name, out var knownFact))
                {
                    context = context.WithEdge(knownFact.FactIndex, factIndex, roleId);
                    factIndex = knownFact.FactIndex;
                }
                // If we have not written the fact, we need to write it now.
                else
                {
                    var successorFactIndex = factIndex;
                    (context, factIndex) = context.WithFact(role.TargetType);
                    context = context.WithEdge(factIndex, successorFactIndex, roleId);
                }
                
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
                // If we have already written the output, we can use the fact index.
                if (i == 0 && context.KnownFacts.TryGetValue(unknown.Name, out var knownFact))
                {
                    context = context.WithEdge(factIndex, knownFact.FactIndex, roleId);
                    factIndex = knownFact.FactIndex;
                }
                // If we have not written the fact, we need to write it now.
                else
                {
                    var predecessorFactIndex = factIndex;
                    (context, factIndex) = context.WithFact(successorType);
                    context = context.WithEdge(predecessorFactIndex, factIndex, roleId);
                }
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