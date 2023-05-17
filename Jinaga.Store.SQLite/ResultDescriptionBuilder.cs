using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Projections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Store.SQLite
{
    internal class InputDescription
    {
        public string Label { get; }
        public string Type { get; }
        public int FactIndex { get; }
        public int FactTypeParameter { get; }
        public int FactHashParameter { get; }

        public InputDescription(string label, string type, int factIndex, int factTypeParameter, int factHashParameter)
        {
            Label = label;
            Type = type;
            FactIndex = factIndex;
            FactTypeParameter = factTypeParameter;
            FactHashParameter = factHashParameter;
        }
    }

    internal class FactDescription
    {
        public FactDescription(string type, int factIndex)
        {
            Type = type;
            FactIndex = factIndex;
        }

        public string Type { get; }
        public int FactIndex { get; }
    }

    internal class OutputDescription
    {
        public string Label { get; }
        public string Type { get; }
        public int FactIndex { get; }

        public OutputDescription(string label, string type, int factIndex)
        {
            Label = label;
            Type = type;
            FactIndex = factIndex;
        }
    }

    internal class EdgeDescription
    {
        int EdgeIndex { get; }
        int PredecessorFactIndex { get; }
        int SuccessorFactIndex { get; }
        int RoleParameter { get; }

        public EdgeDescription(int edgeIndex, int predecessorFactIndex, int successorFactIndex, int roleParameter)
        {
            EdgeIndex = edgeIndex;
            PredecessorFactIndex = predecessorFactIndex;
            SuccessorFactIndex = successorFactIndex;
            RoleParameter = roleParameter;
        }
    }

    internal class ExistentialConditionDescription
    {

    }

    internal class QueryDescription
    {
        public static readonly QueryDescription Empty = new QueryDescription(
            ImmutableList<InputDescription>.Empty,
            ImmutableList<object>.Empty,
            ImmutableList<OutputDescription>.Empty,
            ImmutableList<FactDescription>.Empty,
            ImmutableList<EdgeDescription>.Empty,
            ImmutableList<ExistentialConditionDescription>.Empty);

        public ImmutableList<InputDescription> Inputs { get; }
        public ImmutableList<object> Parameters { get; }
        public ImmutableList<OutputDescription> Outputs { get; }
        public ImmutableList<FactDescription> Facts { get; }
        public ImmutableList<EdgeDescription> Edges { get; }
        public ImmutableList<ExistentialConditionDescription> ExistentialConditions { get; }

        private QueryDescription(ImmutableList<InputDescription> inputs, ImmutableList<object> parameters, ImmutableList<OutputDescription> outputs, ImmutableList<FactDescription> facts, ImmutableList<EdgeDescription> edges, ImmutableList<ExistentialConditionDescription> existentialConditions)
        {
            Inputs = inputs;
            Parameters = parameters;
            Outputs = outputs;
            Facts = facts;
            Edges = edges;
            ExistentialConditions = existentialConditions;
        }

        public SpecificationSqlQuery GenerateResultSqlQuery()
        {
            throw new NotImplementedException();
        }

        public bool IsSatisfiable()
        {
            return true;
        }

        public int OutputLength()
        {
            throw new NotImplementedException();
        }

        public QueryDescription WithInputs(ImmutableList<InputDescription> inputs)
        {
            return new QueryDescription(inputs, Parameters, Outputs, Facts, Edges, ExistentialConditions);
        }

        public QueryDescription WithParameters(ImmutableList<object> parameters)
        {
            return new QueryDescription(Inputs, parameters, Outputs, Facts, Edges, ExistentialConditions);
        }

        public QueryDescription WithFacts(ImmutableList<FactDescription> facts)
        {
            return new QueryDescription(Inputs, Parameters, Outputs, facts, Edges, ExistentialConditions);
        }

        public QueryDescription WithEdges(ImmutableList<EdgeDescription> edgeDescriptions)
        {
            return new QueryDescription(Inputs, Parameters, Outputs, Facts, edgeDescriptions, ExistentialConditions);
        }

        public QueryDescription WithOutputs(ImmutableList<OutputDescription> outputs)
        {
            return new QueryDescription(Inputs, Parameters, outputs, Facts, Edges, ExistentialConditions);
        }
    }

    internal class ResultDescription
    {
        public QueryDescription QueryDescription { get; set; }
        public ImmutableDictionary<string, ResultDescription> ChildResultDescriptions { get; set; }

        public SqlQueryTree CreateSqlQueryTree(int parentFactIdLength)
        {
            SpecificationSqlQuery sqlQuery = QueryDescription.GenerateResultSqlQuery();
            var childQueries = ChildResultDescriptions
                .Select(child => KeyValuePair.Create(
                    child.Key,
                    child.Value.CreateSqlQueryTree(QueryDescription.OutputLength())))
                .ToImmutableDictionary();
            return new SqlQueryTree(sqlQuery, parentFactIdLength, childQueries);
        }
    }

    internal class ResultDescriptionBuilder
    {
        private class Context
        {
            public static readonly Context Empty = new Context(
                QueryDescription.Empty,
                ImmutableDictionary<string, FactDescription>.Empty,
                ImmutableList<int>.Empty);

            public QueryDescription QueryDescription { get; }
            public ImmutableDictionary<string, FactDescription> FactByLabel { get; }
            public ImmutableList<int> Path { get; }

            public Context(QueryDescription queryDescription, ImmutableDictionary<string, FactDescription> factByLabel, ImmutableList<int> path)
            {
                QueryDescription = queryDescription;
                FactByLabel = factByLabel;
                Path = path;
            }

            public Context WithExistentialCondition(bool exists)
            {
                throw new NotImplementedException();
            }

            public Context WithQueryDescription(QueryDescription queryDescription)
            {
                throw new NotImplementedException();
            }

            public Context WithInputParameter(Label label, int factTypeId, string hash)
            {
                int factTypeParameter = QueryDescription.Parameters.Count + 1;
                int factHashParameter = QueryDescription.Parameters.Count + 2;
                int factIndex = QueryDescription.Facts.Count + 1;
                var factDescription = new FactDescription(label.Type, factIndex);
                var facts = QueryDescription.Facts.Add(factDescription);
                var input = new InputDescription(label.Name, label.Type, factIndex, factTypeParameter, factHashParameter);
                var parameters = QueryDescription.Parameters.Add(factTypeId).Add(hash);
                if (Path.Count == 0)
                {
                    var inputs = QueryDescription.Inputs.Add(input);
                    var queryDescription = QueryDescription.WithInputs(inputs).WithParameters(parameters).WithFacts(facts);
                    return new Context(queryDescription, FactByLabel.Add(label.Name, factDescription), Path);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            public (Context context, int factIndex) WithEdge(string predecessorType, int roleId, int factIndex)
            {
                var roleParameter = QueryDescription.Parameters.Count + 1;
                var parameters = QueryDescription.Parameters.Add(roleId);
                var queryDescription = QueryDescription.WithParameters(parameters);
                // If we have not written the fact, we need to write it now.
                var predecessorFactIndex = QueryDescription.Facts.Count + 1;
                var fact = new FactDescription(predecessorType, factIndex);
                queryDescription = queryDescription.WithFacts(queryDescription.Facts.Add(fact));
                int edgeIndex = queryDescription.Edges.Count + 1;
                var edge = new EdgeDescription(
                    edgeIndex,
                    predecessorFactIndex,
                    factIndex,
                    roleParameter);
                queryDescription = queryDescription.WithEdges(queryDescription.Edges.Add(edge));
                var context = new Context(queryDescription, FactByLabel, Path);
                return (context, predecessorFactIndex);
            }

            public Context WithLabel(Label label, int factIndex)
            {
                // If we have not captured the known fact, add it now.
                if (!FactByLabel.ContainsKey(label.Name))
                {
                    var factByLabel = FactByLabel.Add(label.Name, new FactDescription(label.Type, factIndex));
                    // If we have not written the output, write it now.
                    // Only write the output if we are not inside of an existential condition.
                    // Use the prefix, which will be set for projections.
                    if (Path.Count == 0)
                    {
                        var prefix = "";
                        var output = new OutputDescription(prefix + label.Name, label.Type, factIndex);
                        var outputs = QueryDescription.Outputs.Add(output);
                        var queryDescription = QueryDescription.WithOutputs(outputs);
                        return new Context(queryDescription, factByLabel, Path);
                    }
                }
                return this;
            }
        }

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

            var context = Context.Empty;
            return CreateResultDescription(context, specification.Given, startReferences, specification.Matches, specification.Projection);
        }

        private ResultDescription CreateResultDescription(Context context, ImmutableList<Label> given, ImmutableList<FactReference> startReferences, ImmutableList<Match> matches, Projection projection)
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

        private Context AddEdges(Context context, ImmutableList<Label> given, ImmutableList<FactReference> startReferences, ImmutableList<Match> matches)
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

        private Context AddPathCondition(Context context, PathCondition pathCondition, ImmutableList<Label> given, ImmutableList<FactReference> startReferences, Label unknown, string v)
        {
            // If no input parameter has been allocated, allocate one now.
            if (!context.FactByLabel.ContainsKey(pathCondition.LabelRight))
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
            if (!context.FactByLabel.ContainsKey(pathCondition.LabelRight))
            {
                throw new ArgumentException($"Label {pathCondition.LabelRight} not found.");
            }
            var fact = context.FactByLabel[pathCondition.LabelRight];
            var type = fact.Type;
            var factIndex = fact.FactIndex;
            for (int i = 0; i < pathCondition.RolesRight.Count; i++)
            {
                var role = pathCondition.RolesRight[i];
                // If the type or role is not known, then no facts matching the condition can
                // exist. The query is unsatisfiable.
                var typeId = factTypes[type];
                var roleId = roleMap[typeId][role.Name];

                (context, factIndex) = context.WithEdge(role.TargetType, roleId, factIndex);
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
                (context, factIndex) = context.WithEdge(declaringType, roleId, factIndex);
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