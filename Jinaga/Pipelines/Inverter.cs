using System.Linq;
using System.Collections.Generic;
using Jinaga.Projections;
using System.Collections.Immutable;
using System;

namespace Jinaga.Pipelines
{
    class Inverter
    {
        public static ImmutableList<Inverse> InvertSpecification(Specification specification)
        {
            var given = specification.Given.ToImmutableDictionary(g => g.Name);
            var tail = ImmutableList<Match>.Empty;
            var projection = new CompoundProjection();
            var inverses = InvertMatches(given, given, tail, projection, specification.Matches);
            return inverses;
        }

        private static ImmutableList<Inverse> InvertMatches(ImmutableDictionary<string, Label> given, ImmutableDictionary<string, Label> labelByName, ImmutableList<Match> tail, CompoundProjection projection, ImmutableList<Match> matches)
        {
            var inverses = ImmutableList<Inverse>.Empty;
            foreach (var match in matches)
            {
                var matchInverses = InvertMatch(given, labelByName, tail, projection, match);
                inverses = inverses.AddRange(matchInverses);
                tail = matchInverses.Last().InverseSpecification.Matches;
            }
            return inverses;
        }

        public static ImmutableList<Inverse> InvertMatch(ImmutableDictionary<string, Label> given, ImmutableDictionary<string, Label> labelByName, ImmutableList<Match> tail, CompoundProjection projection, Match match)
        {
            var inverses = ImmutableList<Inverse>.Empty;
            var unknown = match.Unknown;
            foreach (var condition in match.Conditions)
            {
                if (condition is PathCondition pathCondition)
                {
                    var inverse = InvertPathCondition(given, labelByName, tail, projection, unknown, pathCondition);
                    inverses = inverses.Add(inverse);
                    tail = inverse.InverseSpecification.Matches;
                    projection = (CompoundProjection)inverse.InverseSpecification.Projection;
                }
                else if (condition is ExistentialCondition existentialCondition)
                {
                    var childLabelByName = labelByName.Add(unknown.Name, unknown);
                    var conditionalInverses = InvertExistentialCondition(given, childLabelByName, tail, projection, existentialCondition);
                    inverses = inverses.AddRange(conditionalInverses);
                    var inverse = conditionalInverses.Last();
                    tail = inverse.InverseSpecification.Matches;
                    projection = (CompoundProjection)inverse.InverseSpecification.Projection;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            return inverses;
        }

        private static Inverse InvertPathCondition(ImmutableDictionary<string, Label> given, ImmutableDictionary<string, Label> labelByName, ImmutableList<Match> tail, CompoundProjection projection, Label unknown, PathCondition pathCondition)
        {
            var inverseCondition = new PathCondition(
                pathCondition.RolesRight,
                unknown.Name,
                pathCondition.RolesLeft
            );
            var input = labelByName[pathCondition.LabelRight];
            var inverseMatch = new Match(
                input,
                ImmutableList.Create<MatchCondition>(inverseCondition)
            );
            if (given.ContainsKey(input.Name) && !projection.Names.Contains(input.Name))
            {
                projection = projection
                    .With(input.Name, new SimpleProjection(input.Name));
            }
            var inverseSpecification = new Specification(
                ImmutableList.Create(unknown),
                ImmutableList.Create(inverseMatch).AddRange(tail),
                projection
            );
            var inverse = new Inverse(inverseSpecification);
            return inverse;
        }

        private static ImmutableList<Inverse> InvertExistentialCondition(ImmutableDictionary<string, Label> given, ImmutableDictionary<string, Label> labelByName, ImmutableList<Match> tail, CompoundProjection projection, ExistentialCondition existentialCondition)
        {
            var inverses = InvertMatches(given, labelByName, tail, projection, existentialCondition.Matches);
            return inverses;
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