using System.Linq;
using System.Collections.Generic;
using Jinaga.Projections;
using System.Collections.Immutable;
using System;

namespace Jinaga.Pipelines
{
    class Inverter
    {
        public static IEnumerable<Inverse> InvertSpecification(Specification specification)
        {
            var labelByName = specification.Given.ToImmutableDictionary(g => g.Name);
            var inverses = InvertMatches(labelByName, specification.Matches);
            foreach (var inverse in inverses)
            {
                yield return inverse;
            }
        }

        private static IEnumerable<Inverse> InvertMatches(ImmutableDictionary<string, Label> labelByName, ImmutableList<Match> matches)
        {
            foreach (var match in matches)
            {
                var inverses = InvertMatch(labelByName, match);
                foreach (var inverse in inverses)
                {
                    yield return inverse;
                }
            }
        }

        public static IEnumerable<Inverse> InvertMatch(ImmutableDictionary<string, Label> labelByName, Match match)
        {
            var unknown = match.Unknown;
            foreach (var condition in match.Conditions)
            {
                if (condition is PathCondition pathCondition)
                {
                    var inverse = InvertPathCondition(labelByName, unknown, pathCondition);
                    yield return inverse;
                }
                else if (condition is ExistentialCondition existentialCondition)
                {
                    var childLabelByName = labelByName.Add(unknown.Name, unknown);
                    IEnumerable<Inverse> inverses = InvertExistentialCondition(childLabelByName, existentialCondition);
                    foreach (var inverse in inverses)
                    {
                        yield return inverse;
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        private static Inverse InvertPathCondition(ImmutableDictionary<string, Label> labelByName, Label unknown, PathCondition pathCondition)
        {
            var inverseCondition = new PathCondition(
                pathCondition.RolesRight,
                unknown.Name,
                pathCondition.RolesLeft
            );
            var given = labelByName[pathCondition.LabelRight];
            var inverseMatch = new Match(
                given,
                ImmutableList.Create<MatchCondition>(inverseCondition)
            );
            var inverseProjection = new CompoundProjection()
                .With(given.Name, new SimpleProjection(given.Name));
            var inverseSpecification = new Specification(
                ImmutableList.Create(unknown),
                ImmutableList.Create(inverseMatch),
                inverseProjection
            );
            var inverse = new Inverse(inverseSpecification);
            return inverse;
        }

        private static IEnumerable<Inverse> InvertExistentialCondition(ImmutableDictionary<string, Label> labelByName, ExistentialCondition existentialCondition)
        {
            var inverses = InvertMatches(labelByName, existentialCondition.Matches);
            foreach (var inverse in inverses)
            {
                yield return inverse;
            }
        }
    }

    class InverterOld
    {
        public static IEnumerable<InverseOld> InvertSpecification(SpecificationOld specification)
        {
            var inverses =
                from start in specification.Pipeline.Starts
                from path in specification.Pipeline.Paths
                where path.Start == start
                from inverse in InvertPaths(start, specification.Pipeline, path, specification.Projection, PipelineOld.Empty, ImmutableList<CollectionIdentifier>.Empty)
                select inverse;
            return inverses;
        }

        public static IEnumerable<InverseOld> InvertPipeline(PipelineOld pipeline)
        {
            var inverses =
                from start in pipeline.Starts
                from path in pipeline.Paths
                where path.Start == start
                from inverse in InvertPaths(start, pipeline, path, new EmptyProjection(), PipelineOld.Empty, ImmutableList<CollectionIdentifier>.Empty)
                select inverse;
            return inverses;
        }

        public static IEnumerable<InverseOld> InvertPaths(Label start, PipelineOld pipeline, Path path, Projection projection, PipelineOld backward, ImmutableList<CollectionIdentifier> collectionIdentifiers)
        {
            var nextBackward = backward.PrependPath(ReversePath(path));
            var conditionalInverses =
                from conditional in pipeline.Conditionals
                where conditional.Start == path.Target
                from inverse in InvertConditionals(conditional, nextBackward, start.Name, collectionIdentifiers)
                select inverse;
            var nestedInverses =
                from namedSpecification in projection.GetNamedSpecifications()
                let nestedSpecification = namedSpecification.specification
                from nestedPath in nestedSpecification.Pipeline.Paths
                where nestedPath.Start == path.Target
                let collectionIdentifier = NewCollectionIdentifier(namedSpecification.name, Subset.FromPipeline(nextBackward))
                from nestedInverse in InvertPaths(
                    start,
                    nestedSpecification.Pipeline,
                    nestedPath,
                    nestedSpecification.Projection,
                    nextBackward,
                    collectionIdentifiers.Add(collectionIdentifier))
                select nestedInverse;
            var inversePipeline = nextBackward.AddStart(path.Target);
            var newInverse = new InverseOld(
                inversePipeline,
                Subset.Empty.Add(start.Name),
                Operation.Add,
                Subset.FromPipeline(inversePipeline),
                projection,
                collectionIdentifiers);
            var nextInverses =
                from nextPath in pipeline.Paths
                where nextPath.Start == path.Target
                from nextInverse in InvertPaths(start, pipeline, nextPath, projection, nextBackward, collectionIdentifiers)
                select nextInverse;
            return nextInverses.Concat(conditionalInverses).Concat(nestedInverses).Prepend(newInverse);
        }

        private static CollectionIdentifier NewCollectionIdentifier(string collectionName, Subset intermediateSubset)
        {
            return new CollectionIdentifier(collectionName, intermediateSubset);
        }

        public static IEnumerable<InverseOld> InvertConditionals(Conditional conditional, PipelineOld backward, string affectedTag, ImmutableList<CollectionIdentifier> collectionIdentifiers)
        {
            return InvertPipeline(conditional.ChildPipeline)
                .Select(childInverse => new InverseOld(
                    childInverse.InversePipeline.Compose(backward),
                    Subset.Empty.Add(affectedTag),
                    conditional.Exists ? Operation.Add : Operation.Remove,
                    Subset.FromPipeline(backward),
                    childInverse.Projection,
                    collectionIdentifiers));
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