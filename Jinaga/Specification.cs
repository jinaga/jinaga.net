using System.Collections.Immutable;
using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Parsers;
using Jinaga.Pipelines;
using Jinaga.Repository;
using Jinaga.Definitions;

namespace Jinaga
{
    public static class Given<TFact>
    {
        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, IQueryable<TProjection>>> spec)
        {
            var parameter = spec.Parameters[0];
            var initialFactName = parameter.Name;
            var initialFactType = parameter.Type.FactTypeName();

            var set = SpecificationParser.ParseSpecification(initialFactName, initialFactType, spec.Body);

            return new Specification<TFact, TProjection>(set);
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

            var (head, tag, steps) = SegmentParser.ParseSegment("", null, initialFactName, initialFactType, spec.Body);
            var last = steps.Last();
            var targetType = last.TargetType;
            if (last is PredecessorStep predecessorStep)
            {
                string targetName = predecessorStep.Role;
                var stepsDefinition = new StepsDefinition(targetName, initialFactName, steps);
                var set = new SimpleSetDefinition(targetType)
                    .WithSteps(targetName, stepsDefinition);
                return new Specification<TFact, TProjection>(set);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
    public class Specification<TFact, TProjection>
    {
        private SetDefinition set;

        public Specification(SetDefinition set)
        {
            this.set = set;
        }

        public Pipeline Compile()
        {
            return set.CreatePipeline();
        }
    }
}
