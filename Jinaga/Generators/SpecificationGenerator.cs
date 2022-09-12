using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Definitions;
using Jinaga.Parsers;
using Jinaga.Pipelines;
using Jinaga.Projections;

namespace Jinaga.Generators
{
    public static class SpecificationGenerator
    {
        public static SpecificationOld CreateSpecification(SpecificationContext context, SpecificationResult result)
        {
            var pipeline = PipelineGenerator.CreatePipeline(context, result);
            var projection = CreateProjection(result.SymbolValue);
            return new SpecificationOld(pipeline, projection);
        }

        public static ProjectionOld CreateProjection(SymbolValue value)
        {
            if (value is SymbolValueSetDefinition setDefinitionValue)
            {
                return new SimpleProjection(setDefinitionValue.SetDefinition.Tag);
            }
            else if (value is SymbolValueComposite compositeValue)
            {
                var projectionDefinition = compositeValue.CreateProjectionDefinition();
                var projection = projectionDefinition
                    .AllTags()
                    .Aggregate(new CompoundProjection(), (p, tag) => p.With(
                        tag,
                        CreateProjection(projectionDefinition.GetValue(tag))));
                return projection;
            }
            else if (value is SymbolValueCollectionOld collectionValue)
            {
                return new CollectionProjection(collectionValue.Specification);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static ImmutableList<Match> CreateMatches(SpecificationContext context, SpecificationResult result)
        {
            SetDefinitionTarget? priorTarget = null;
            foreach (var target in result.Targets)
            {
                if (!result.SetDefinitions.Any(set => IsJoinTargeting(set, target)))
                {
                    if (result.TryGetLabelOf(target, out var label))
                    {
                        ThrowSpecificationErrorWithLabel(context, result, priorTarget, target, label);
                    }
                    else
                    {
                        ThrowSpecificationErrorWithoutLabel(context, result, priorTarget, target);
                    }
                }
                priorTarget = target;
            }
            var matches = result.SetDefinitions
                .GroupBy(set => set.Label)
                .Select(group => CreateMatch(context, group.Key, group))
                .ToImmutableList();
            return matches;
        }

        private static Match CreateMatch(SpecificationContext context, Label target, IEnumerable<SetDefinition> setDefinitions)
        {
            var conditions = setDefinitions
                .Select(setDefinition => CreateCondition(context, setDefinition))
                .ToImmutableList();
            return new Match(target, conditions);
        }

        private static MatchCondition CreateCondition(SpecificationContext context, SetDefinition setDefinition)
        {
            if (setDefinition is SetDefinitionPredecessorChain predecessorChainSet)
            {
                var chain = predecessorChainSet.ToChain();
                var targetSetDefinition = chain.TargetSetDefinition;
                var start = targetSetDefinition.Label;
                var target = predecessorChainSet.Label;
                ImmutableList<Role> rolesLeft = ImmutableList<Role>.Empty;
                ImmutableList<Role> rolesRight = AddPredecessorSteps(ImmutableList<Role>.Empty, chain);
                var condition = new PathCondition(rolesLeft, start.Name, rolesRight);
                return condition;
            }
            else if (setDefinition is SetDefinitionJoin joinSet)
            {
                var head = joinSet.Head;
                var tail = joinSet.Tail;
                var sourceSetDefinition = head.TargetSetDefinition;
                var source = sourceSetDefinition.Label;
                var target = joinSet.Label;
                ImmutableList<Role> rolesLeft = AddSuccessorSteps(ImmutableList<Role>.Empty, tail);
                ImmutableList<Role> rolesRight = AddPredecessorSteps(ImmutableList<Role>.Empty, head);
                var condition = new PathCondition(rolesLeft, source.Name, rolesRight);
                return condition;
            }
            else if (setDefinition is SetDefinitionConditional conditionalSet)
            {
                var targetSetDefinition = conditionalSet.Source;
                var start = targetSetDefinition.Label;
                var innerContext = SpecificationContext.Empty
                    .With(start, SpecificationParser.InstanceOfFact(targetSetDefinition.Type), targetSetDefinition.Type);
                var matches = CreateMatches(innerContext, conditionalSet.Condition.SpecificationResult);
                var condition = new ExistentialCondition(conditionalSet.Condition.Exists, matches);
                return condition;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static ImmutableList<Role> AddSuccessorSteps(ImmutableList<Role> roles, Chain chain)
        {
            if (chain is ChainRole chainRole)
            {
                return AddSuccessorSteps(roles, chainRole.Prior)
                    .Add(new Role(chainRole.Role, chainRole.TargetFactType));
            }
            else
            {
                return roles;
            }
        }

        private static ImmutableList<Role> AddPredecessorSteps(ImmutableList<Role> roles, Chain chain)
        {
            if (chain is ChainRole chainRole)
            {
                return AddPredecessorSteps(roles, chainRole.Prior)
                    .Add(new Role(chainRole.Role, chain.TargetFactType));
            }
            else
            {
                return roles;
            }
        }

        private static void ThrowSpecificationErrorWithLabel(SpecificationContext context, SpecificationResult result, SetDefinitionTarget? priorTarget, SetDefinitionTarget target, Label label)
        {
            string targetDescription;
            Type targetType;
            string targetName;
            if (priorTarget == null)
            {
                var parameter = context.GetFirstVariable();
                targetDescription = $"parameter \"{parameter.Label.Name}\"";
                targetType = parameter.Type;
                targetName = parameter.Label.Name;
            }
            else if (result.TryGetLabelOf(priorTarget, out var priorLabel))
            {
                targetDescription = $"prior variable \"{priorLabel.Name}\"";
                targetType = priorTarget.Type;
                targetName = priorLabel.Name;
            }
            else
            {
                targetDescription = $"prior variable";
                targetType = priorTarget.Type;
                targetName = "x";
            }
            var variable = label.Name;
            var message = $"The variable \"{variable}\" should be joined to the {targetDescription}.";
            var recommendation = RecommendationEngine.RecommendJoin(
                new CodeExpression(target.Type, variable),
                new CodeExpression(targetType, targetName)
            );

            throw new SpecificationException(recommendation == null ? message : $"{message} Consider \"where {recommendation}\".");
        }

        private static void ThrowSpecificationErrorWithoutLabel(SpecificationContext context, SpecificationResult result, SetDefinitionTarget? priorTarget, SetDefinitionTarget target)
        {
            string targetDescription;
            Type targetType;
            string targetName;
            if (priorTarget == null)
            {
                var parameter = context.GetFirstVariable();
                targetDescription = $"parameter \"{parameter.Label.Name}\"";
                targetType = parameter.Type;
                targetName = parameter.Label.Name;
            }
            else if (result.TryGetLabelOf(priorTarget, out var priorLabel))
            {
                targetDescription = $"prior variable \"{priorLabel.Name}\"";
                targetType = priorTarget.Type;
                targetName = priorLabel.Name;
            }
            else
            {
                targetDescription = $"prior variable";
                targetType = priorTarget.Type;
                targetName = "x";
            }
            var typeName = target.Type.Name;
            var variable = ToInitialLowerCase(typeName);
            var message = $"The set should be joined to the {targetDescription}.";
            var recommendation = RecommendationEngine.RecommendJoin(
                new CodeExpression(target.Type, variable),
                new CodeExpression(targetType, targetName)
            );

            throw new SpecificationException(recommendation == null ? message : $"{message} Consider \"facts.OfType<{typeName}>({variable} => {recommendation})\".");
        }

        private static bool IsJoinTargeting(SetDefinition set, SetDefinitionTarget target)
        {
            if (set is SetDefinitionJoin joinSet)
            {
                return joinSet.Tail.TargetSetDefinition == target;
            }
            else
            {
                return false;
            }
        }

        private static string ToInitialLowerCase(string name)
        {
            if (name.Length > 0)
            {
                return $"{name.Substring(0, 1).ToLower()}{name.Substring(1)}";
            }
            else
            {
                return name;
            }
        }
    }
}
