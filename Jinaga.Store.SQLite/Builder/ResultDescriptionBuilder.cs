using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Store.SQLite.Description;
using System;
using System.Collections.Generic;
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

        public ResultDescription Build(ImmutableList<FactReference> startReferences, Specification specification)
        {
            // Verify that the number of start references matches the number of given facts.
            if (startReferences.Count != specification.Given.Count)
            {
                throw new ArgumentException($"The number of start facts ({startReferences.Count}) does not match the number of inputs ({specification.Given.Count}).");
            }
            // Verify that the start reference types match the given fact types.
            for (var i = 0; i < startReferences.Count; i++)
            {
                var startReference = startReferences[i];
                var given = specification.Given[i];
                if (startReference.Type != given.Type)
                {
                    throw new ArgumentException($"The start fact type ({startReference.Type}) does not match the input type ({given.Type}).");
                }
            }

            var context = ResultDescriptionBuilderContext.Empty;
            return CreateResultDescription(context, specification.Given, startReferences, specification.Matches, specification.Projection);
        }

        private ResultDescription CreateResultDescription(ResultDescriptionBuilderContext context, ImmutableList<Label> given, ImmutableList<FactReference> startReferences, ImmutableList<Match> matches, Projection projection)
        {
            var givenTuple = given
                .Select((label, index) =>
                    KeyValuePair.Create(label.Name, startReferences[index]))
                .ToImmutableDictionary();
            context = AddEdges(context, given, startReferences, matches);
            if (projection is CompoundProjection)
            {
                throw new NotImplementedException();
            }
            return new ResultDescription
            {
                QueryDescription = context.QueryDescription,
                ChildResultDescriptions = ImmutableDictionary<string, ResultDescription>.Empty
            };
        }

        private ResultDescriptionBuilderContext AddEdges(ResultDescriptionBuilderContext context, ImmutableList<Label> given, ImmutableList<FactReference> startReferences, ImmutableList<Match> matches)
        {
            foreach (var match in matches)
            {
                foreach (var condition in match.Conditions)
                {
                    if (condition is PathCondition pathCondition)
                    {
                        context = AddPathCondition(context, pathCondition, given, startReferences, match.Unknown, "");
                    }
                    else if (condition is ExistentialCondition existentialCondition)
                    {
                        // Apply the where clause and continue with the tuple where it is true.
                        // The path describes which not-exists condition we are currently building on.
                        // Because the path is not empty, labeled facts will be included in the output.
                        var contextWithCondition = context.WithExistentialCondition(existentialCondition.Exists);
                        var contextConditional = AddEdges(contextWithCondition, given, startReferences, existentialCondition.Matches);

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
                if (!context.QueryDescription.IsSatisfiable())
                {
                    break;
                }
            }
            return context;
        }

        private ResultDescriptionBuilderContext AddPathCondition(ResultDescriptionBuilderContext context, PathCondition pathCondition, ImmutableList<Label> given, ImmutableList<FactReference> startReferences, Label unknown, string v)
        {
            // If no input parameter has been allocated, allocate one now.
            if (!context.KnownFacts.ContainsKey(pathCondition.LabelRight))
            {
                var givenIndex = given.FindIndex(given => given.Name == pathCondition.LabelRight);
                if (givenIndex < 0)
                {
                    throw new ArgumentException($"No input parameter found for label {pathCondition.LabelRight}");
                }
                int factTypeId = EnsureGetFactTypeId(startReferences[givenIndex].Type);
                context = context.WithInputParameter(
                    given[givenIndex],
                    factTypeId,
                    startReferences[givenIndex].Hash
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
                var typeId = factTypes[type];
                var roleId = roleMap[typeId][role.Name];

                (context, factIndex) = context.WithEdgeToPredecessor(role.TargetType, roleId, factIndex);
                type = role.TargetType;
            }

            var rightType = type;

            // Walk up the left-hand side.
            // We will need to reverse this walk to generate successor joins.
            type = unknown.Type;
            var newEdges = ImmutableList<(int roleId, string declaringType)>.Empty;
            foreach (var role in pathCondition.RolesLeft)
            {
                // If the type or role is not known, then no facts matching the condition can
                // exist. The query is unsatisfiable.
                var typeId = factTypes[type];
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
                var (roleId, declaringType) = newEdges[i];
                (context, factIndex) = context.WithEdgeToSuccessor(declaringType, roleId, factIndex);
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