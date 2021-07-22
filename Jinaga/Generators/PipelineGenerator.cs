using System;
using Jinaga.Definitions;
using Jinaga.Pipelines2;

namespace Jinaga.Generators
{
    public static class PipelineGenerator
    {
        public static Pipeline CreatePipeline(SetDefinition setDefinition)
        {
            if (setDefinition is SetDefinitionInitial initialSet)
            {
                return Pipeline.Empty
                    .AddStart(new Label(initialSet.Tag, initialSet.FactType));
            }
            else if (setDefinition is SetDefinitionChainRole chainRoleSet)
            {
                var chainRole = chainRoleSet.ChainRole;
                var targetSetDefinition = chainRole.TargetSetDefinition;
                var pipeline = CreatePipeline(targetSetDefinition);
                var start = new Label(targetSetDefinition.Tag, targetSetDefinition.FactType);
                var target = new Label(chainRole.InferredTag, chainRole.TargetType);
                var path = new Path(start, target);
                return pipeline.AddPath(AddPredecessorSteps(path, chainRole));
            }
            else if (setDefinition is SetDefinitionJoin joinSet)
            {
                var head = joinSet.Head;
                var tail = joinSet.Tail;
                var targetSetDefinition = head.TargetSetDefinition;
                var pipeline = CreatePipeline(targetSetDefinition);
                var start = new Label(targetSetDefinition.Tag, targetSetDefinition.FactType);
                var target = new Label(tail.Tag, tail.TargetType);
                var path = new Path(start, target);
                return pipeline.AddPath(PrependSuccessorSteps(AddPredecessorSteps(path, head), tail));
            }
            else if (setDefinition is SetDefinitionConditional conditionalSet)
            {
                var targetSetDefinition = conditionalSet.Source;
                var pipeline = CreatePipeline(targetSetDefinition);
                var start = new Label(targetSetDefinition.Tag, targetSetDefinition.FactType);
                var childPipeline = CreatePipeline(conditionalSet.Condition.Set);
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
