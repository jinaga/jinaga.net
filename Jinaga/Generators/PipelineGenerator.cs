using System;
using Jinaga.Definitions;
using Jinaga.Pipelines;

namespace Jinaga.Generators
{
    public static class PipelineGenerator
    {
        public static Pipeline CreatePipeline(SetDefinition setDefinition, SetDefinition? seekSetDefinition = null, Label? replaceLabel = null)
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
                var pipeline = CreatePipeline(targetSetDefinition, seekSetDefinition, replaceLabel);
                var start = targetSetDefinition.Label;
                var target = new Label(predecessorChainSet.Tag, chain.TargetType);
                var path = new Path(start, target);
                return pipeline.AddPath(AddPredecessorSteps(path, chain));
            }
            else if (setDefinition is SetDefinitionJoin joinSet)
            {
                var head = joinSet.Head;
                var tail = joinSet.Tail;
                var sourceSetDefinition = head.TargetSetDefinition;
                var targetSetDefinition = tail.TargetSetDefinition;
                var pipeline = CreatePipeline(sourceSetDefinition, seekSetDefinition, replaceLabel);
                var source = sourceSetDefinition.Label;
                var target = joinSet.Label;
                var path = new Path(source, target);
                return pipeline.AddPath(PrependSuccessorSteps(AddPredecessorSteps(path, head), tail));
            }
            else if (setDefinition is SetDefinitionConditional conditionalSet)
            {
                var targetSetDefinition = conditionalSet.Source;
                var pipeline = CreatePipeline(targetSetDefinition, seekSetDefinition, replaceLabel);
                var start = targetSetDefinition.Label;
                var childPipeline = CreatePipeline(
                    conditionalSet.Condition.Set,
                    targetSetDefinition,
                    start);
                var conditional = new Conditional(start, conditionalSet.Condition.Exists, childPipeline);
                return pipeline.AddConditional(conditional);
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
                        chainRole.Role, chain.TargetType));
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
                        chainRole.Role, chainRole.Prior.TargetType));
            }
            else
            {
                return path;
            }
        }
    }
}
