using System.Collections.Immutable;
using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Parsers;
using Jinaga.Pipelines;
using Jinaga.Repository;

namespace Jinaga
{
    public static class Given<TFact>
    {
        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, IQueryable<TProjection>>> spec)
        {
            var parameter = spec.Parameters[0];
            var initialFactName = parameter.Name;
            var initialFactType = parameter.Type.FactTypeName();

            var paths = SpecificationParser.ParseSpecification(spec.Body);
            var lastPath = paths.Last();
            var projection = new Projection(lastPath.Tag);

            var pipeline = new Pipeline(initialFactName, initialFactType, paths, projection);
            return new Specification<TFact, TProjection>(pipeline);
        }

        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, TProjection>> spec)
        {
            throw new NotImplementedException();
        }
        
        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, TProjection>> spec)
        {
            var parameter = spec.Parameters[0];
            var initialFactName = parameter.Name;
            var initialFactType = parameter.Type.FactTypeName();

            var (rootName, steps) = SegmentParser.ParseSegment(spec.Body);
            var last = steps.Last();
            var targetType = last.TargetType;
            if (last is PredecessorStep predecessorStep)
            {
                string targetName = predecessorStep.Role;
                var path = new Path(targetName, targetType, initialFactName, steps);
                var pipeline = new Pipeline(
                    initialFactName,
                    initialFactType,
                    ImmutableList<Path>.Empty.Add(path),
                    new Projection(targetName));
                return new Specification<TFact, TProjection>(pipeline);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
    public class Specification<TFact, TProjection>
    {
        private Pipeline pipeline;

        public Specification(Pipeline pipeline)
        {
            this.pipeline = pipeline;
        }

        public Pipeline Compile()
        {
            return this.pipeline;
        }
    }
}
