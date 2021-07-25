using System.Linq;
using System.Collections.Generic;

namespace Jinaga.Pipelines
{
    class Inverter
    {
        public static IEnumerable<Inverse> InvertPipeline(Pipeline pipeline)
        {
            return pipeline.Starts
                .SelectMany(start => pipeline.Paths
                    .Where(path => path.Start == start)
                )
                .SelectMany(path => InvertPaths(pipeline, path, Pipeline.Empty));
        }

        public static IEnumerable<Inverse> InvertPaths(Pipeline pipeline, Path path, Pipeline backward)
        {
            string affectedTag = pipeline.Starts.Single().Name;
            var nextBackward = backward.PrependPath(ReversePath(path));
            var conditionalInverses = pipeline.Conditionals
                .Where(conditional => conditional.Start == path.Target)
                .SelectMany(conditional => InvertConditionals(conditional, nextBackward, affectedTag));
            var inversePipeline = nextBackward.AddStart(path.Target);
            return pipeline.Paths
                .Where(p => p.Start == path.Target)
                .SelectMany(next => InvertPaths(pipeline, next, nextBackward))
                .Concat(conditionalInverses)
                .Prepend(new Inverse(
                    inversePipeline,
                    affectedTag,
                    Operation.Add,
                    Subset.FromPipeline(inversePipeline)));
        }

        public static IEnumerable<Inverse> InvertConditionals(Conditional conditional, Pipeline backward, string affectedTag)
        {
            return InvertPipeline(conditional.ChildPipeline)
                .Select(childInverse => new Inverse(
                    childInverse.InversePipeline.Compose(backward),
                    affectedTag,
                    conditional.Exists ? Operation.Add : Operation.Remove,
                    Subset.FromPipeline(backward)));
        }

        public static Path ReversePath(Path path)
        {
            var reverse = new Path(path.Target, path.Start);
            var type = path.Start.Type;
            foreach (var step in path.PredecessorSteps)
            {
                reverse = reverse.PrependSuccessorStep(new Step(step.Role, type));
                type = step.TargetType;
            }
            foreach (var step in path.SuccessorSteps)
            {
                reverse = reverse.PrependPredecessorStep(new Step(step.Role, type));
                type = step.TargetType;
            }
            return reverse;
        }
    }
}