using System;
using System.Linq;
using Jinaga.Definitions;
using Jinaga.Parsers;
using Jinaga.Pipelines;

namespace Jinaga.Generators
{
    public static class PipelineGenerator
    {
        public static Pipeline CreatePipeline(SpecificationContext context, SetDefinition setDefinition, SetDefinition? seekSetDefinition = null, Label? replaceLabel = null)
        {
            if (setDefinition == seekSetDefinition)
            {
                return Pipeline.Empty
                    .AddStart(replaceLabel!);
            }
            else if (setDefinition is SetDefinitionInitial initialSet)
            {
                return Pipeline.Empty
                    .AddStart(initialSet.Label);
            }
            else if (setDefinition is SetDefinitionPredecessorChain predecessorChainSet)
            {
                var chain = predecessorChainSet.ToChain();
                var targetSetDefinition = chain.TargetSetDefinition;
                var pipeline = CreatePipeline(context, targetSetDefinition, seekSetDefinition, replaceLabel);
                var start = targetSetDefinition.Label;
                var target = new Label(predecessorChainSet.Tag, chain.TargetFactType);
                var path = new Path(start, target);
                return pipeline.AddPath(AddPredecessorSteps(path, chain));
            }
            else if (setDefinition is SetDefinitionJoin joinSet)
            {
                var head = joinSet.Head;
                var tail = joinSet.Tail;
                var sourceSetDefinition = head.TargetSetDefinition;
                var pipeline = CreatePipeline(context, sourceSetDefinition, seekSetDefinition, replaceLabel);
                var source = sourceSetDefinition.Label;
                var target = joinSet.Label;
                var path = new Path(source, target);
                return pipeline.AddPath(PrependSuccessorSteps(AddPredecessorSteps(path, head), tail));
            }
            else if (setDefinition is SetDefinitionConditional conditionalSet)
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new NotImplementedException($"Cannot generate pipeline for {setDefinition}");
            }
        }

        public static Pipeline CreatePipeline(SpecificationContext context, SpecificationResult result)
        {
            SetDefinitionTarget? priorTarget = null;
            foreach (var target in result.Targets)
            {
                if (!result.SetDefinitions.Any(set => IsJoinTargeting(set, target)))
                {
                    if (result.TryGetLabelOf(target, out var label))
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
                    else
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
                }
                priorTarget = target;
            }
            var pipeline = context.Labels
                .Aggregate(Pipeline.Empty, (p, label) => p.AddStart(label));
            foreach (var setDefinition in result.SetDefinitions)
            {
                pipeline = AppendToPipeline(pipeline, context, setDefinition);
            }
            return pipeline;
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

        private static Pipeline AppendToPipeline(Pipeline pipeline, SpecificationContext context, SetDefinition setDefinition)
        {
            if (setDefinition is SetDefinitionPredecessorChain predecessorChainSet)
            {
                var chain = predecessorChainSet.ToChain();
                var targetSetDefinition = chain.TargetSetDefinition;
                var start = targetSetDefinition.Label;
                var target = new Label(predecessorChainSet.Tag, chain.TargetFactType);
                var path = new Path(start, target);
                return pipeline.AddPath(AddPredecessorSteps(path, chain));
            }
            else if (setDefinition is SetDefinitionJoin joinSet)
            {
                var head = joinSet.Head;
                var tail = joinSet.Tail;
                var sourceSetDefinition = head.TargetSetDefinition;
                var source = sourceSetDefinition.Label;
                var target = joinSet.Label;
                var path = new Path(source, target);
                return pipeline.AddPath(PrependSuccessorSteps(AddPredecessorSteps(path, head), tail));
            }
            else if (setDefinition is SetDefinitionConditional conditionalSet)
            {
                var targetSetDefinition = conditionalSet.Source;
                var start = targetSetDefinition.Label;
                var childPipeline = CreatePipeline(
                    SpecificationContext.Empty
                        .With(start, SpecificationParser.InstanceOfFact(targetSetDefinition.Type), targetSetDefinition.Type),
                    conditionalSet.Condition.SpecificationResult);
                var conditional = new Conditional(start, conditionalSet.Condition.Exists, childPipeline);
                return pipeline.AddConditional(conditional);
            }
            else
                throw new NotImplementedException();
        }

        public static Path AddPredecessorSteps(Path path, Chain chain)
        {
            if (chain is ChainRole chainRole)
            {
                return AddPredecessorSteps(path, chainRole.Prior)
                    .AddPredecessorStep(new Step(
                        chainRole.Role, chain.TargetFactType));
            }
            else
            {
                return path;
            }
        }

        public static Path PrependSuccessorSteps(Path path, Chain chain)
        {
            if (chain is ChainRole chainRole)
            {
                return PrependSuccessorSteps(path, chainRole.Prior)
                    .PrependSuccessorStep(new Step(
                        chainRole.Role, chainRole.Prior.TargetFactType));
            }
            else
            {
                return path;
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
