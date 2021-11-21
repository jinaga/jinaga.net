using System;
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
                var targetSetDefinition = conditionalSet.Source;
                var pipeline = CreatePipeline(context, targetSetDefinition, seekSetDefinition, replaceLabel);
                var start = targetSetDefinition.Label;
                var childPipeline = CreatePipeline(
                    SpecificationContext.Empty,
                    conditionalSet.Condition.Set,
                    targetSetDefinition,
                    start);
                var conditional = new Conditional(start, conditionalSet.Condition.Exists, childPipeline);
                return pipeline.AddConditional(conditional);
            }
            else if (setDefinition is SetDefinitionLabeledTarget labeledTargetSet)
            {
                var variable = labeledTargetSet.Label.Name;
                var parameter = context.GetFirstVariable();
                var message = $"The variable \"{variable}\" should be joined to the parameter \"{parameter.Label.Name}\".";
                var recommendation = RecommendationEngine.RecommendJoin(
                    new CodeExpression(labeledTargetSet.Type, variable),
                    new CodeExpression(parameter.Type, parameter.Label.Name)
                );

                throw new SpecificationException(recommendation == null ? message : $"{message} Consider \"where {recommendation}\".");
            }
            else if (setDefinition is SetDefinitionTarget targetSet)
            {
                var typeName = targetSet.Type.Name;
                var variable = ToInitialLowerCase(typeName);
                var parameter = context.GetFirstVariable();
                var message = $"The set should be joined to the parameter \"{parameter.Label.Name}\".";
                var recommendation = RecommendationEngine.RecommendJoin(
                    new CodeExpression(targetSet.Type, variable),
                    new CodeExpression(parameter.Type, parameter.Label.Name)
                );

                throw new SpecificationException(recommendation == null ? message : $"{message} Consider \"facts.OfType<{typeName}>({variable} => {recommendation})\".");
            }
            else
            {
                throw new NotImplementedException($"Cannot generate pipeline for {setDefinition}");
            }
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
