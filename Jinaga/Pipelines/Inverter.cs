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
            return pipeline.Paths
                .Where(p => p.Start == path.Target)
                .SelectMany(next => InvertPaths(pipeline, next, nextBackward))
                .Concat(conditionalInverses)
                .Prepend(new Inverse(nextBackward.AddStart(path.Target), affectedTag));
        }

        public static IEnumerable<Inverse> InvertConditionals(Conditional conditional, Pipeline backward, string affectedTag)
        {
            return InvertPipeline(conditional.ChildPipeline)
                .Select(childInverse => childInverse.InversePipeline.Compose(backward))
                .Select(pipeline => new Inverse(pipeline, affectedTag));
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