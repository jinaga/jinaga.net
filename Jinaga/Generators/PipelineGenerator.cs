using System;
using Jinaga.Definitions;
using Jinaga.Pipelines;

namespace Jinaga.Generators
{
    public static class PipelineGenerator
    {
        public static Pipeline CreatePipeline(SetDefinition setDefinition)
        {
            switch (setDefinition)
            {
                case SetDefinitionInitial initialSet:
                    return Pipeline.FromInitialFact(initialSet.Tag, initialSet.FactType);
                case SetDefinitionChainRole chainRoleSet:
                    var chainRole = chainRoleSet.ChainRole;
                    var pipeline = chainRole.CreatePipeline();
                    var steps = chainRole.CreatePredecessorSteps();
                    var path = new Path(chainRole.InferredTag, chainRole.TargetType, chainRole.Tag, steps);
                    return pipeline.WithPath(path);
                case SetDefinitionJoin joinSet:
                    var left = joinSet.Left;
                    var right = joinSet.Right;
                    bool leftIsTarget = left.IsTarget;
                    bool rightIsTarget = right.IsTarget;

                    if (leftIsTarget && !rightIsTarget)
                    {
                        return BuildPipeline(joinSet.Tag, joinSet.FactType, right, left);
                    }
                    else if (rightIsTarget && !leftIsTarget)
                    {
                        return BuildPipeline(joinSet.Tag, joinSet.FactType, left, right);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                default:
                    throw new NotImplementedException();
            }
        }
        private static Pipeline BuildPipeline(string tag, string factType, Chain head, Chain tail)
        {
            var pipeline = head.CreatePipeline();
            var startingTag = head.Tag;
            var steps = head.CreatePredecessorSteps().AddRange(tail.CreateSuccessorSteps());
            var path = new Path(tag, factType, startingTag, steps);
            return pipeline.WithPath(path);
        }
    }
}
