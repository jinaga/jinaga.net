using System;
using System.Collections.Immutable;
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
                    var inferredPath = new Path(chainRole.InferredTag, chainRole.TargetType, chainRole.Tag, steps);
                    return pipeline.WithPath(inferredPath);
                case SetDefinitionJoin joinSet:
                    return BuildPipeline(joinSet.Tag, joinSet.Head, joinSet.Tail);
                case SetDefinitionConditional conditional:
                    Pipeline sourcePipeline = CreateHeadPipeline(conditional.Source);
                    var conditionalSteps = CreateSteps(conditional);
                    string startingTag = GetStartingTag(conditional);
                    Path conditionalPath = new Path(conditional.Tag, conditional.FactType, startingTag, conditionalSteps);
                    return sourcePipeline.WithPath(conditionalPath);
                default:
                    throw new NotImplementedException($"Cannot generate pipeline for {setDefinition}");
            }
        }

        private static Pipeline CreateHeadPipeline(SetDefinition setDefinition)
        {
            switch (setDefinition)
            {
                case SetDefinitionJoin joinSet:
                    return joinSet.Head.CreatePipeline();
                case SetDefinitionConditional conditional:
                    return CreateHeadPipeline(conditional.Source);
                default:
                    throw new NotImplementedException($"Cannot generate head pipeline for {setDefinition}");
            }
        }

        private static ImmutableList<Step> CreateSteps(SetDefinition setDefinition)
        {
            switch (setDefinition)
            {
                case SetDefinitionConditional conditionalSet:
                    var priorSteps = CreateSteps(conditionalSet.Source);
                    var set = conditionalSet.Condition.Set;
                    var exists = conditionalSet.Condition.Exists;
                    var pipeline = CreatePipeline(set);
                    var steps = pipeline.Linearize(set.Tag);
                    var conditionalStep = new ConditionalStep(steps, exists);
                    return priorSteps.Add(conditionalStep);
                case SetDefinitionJoin joinSet:
                    return CreateSteps(joinSet.Head, joinSet.Tail);
                default:
                    throw new NotImplementedException($"Cannot create steps for {setDefinition}");
            }
        }

        private static string GetStartingTag(SetDefinition setDefinition)
        {
            switch (setDefinition)
            {
                case SetDefinitionConditional conditionalSet:
                    return GetStartingTag(conditionalSet.Source);
                case SetDefinitionJoin joinSet:
                    return joinSet.Head.Tag;
                default:
                    throw new NotImplementedException($"Cannot get starting tag for {setDefinition}");
            }
        }

        private static Pipeline BuildPipeline(string tag, Chain head, Chain tail)
        {
            var pipeline = head.CreatePipeline();
            var startingTag = head.Tag;
            var steps = CreateSteps(head, tail);
            var path = new Path(tag, tail.SourceType, startingTag, steps);
            return pipeline.WithPath(path);
        }

        private static ImmutableList<Step> CreateSteps(Chain head, Chain tail)
        {
            return head.CreatePredecessorSteps().AddRange(tail.CreateSuccessorSteps());
        }
    }
}
