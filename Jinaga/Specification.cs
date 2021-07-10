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
            var symbolTable = SymbolTable.WithParameter(initialFactName, initialFactType);

            var value = SpecificationParser.ParseSpecification(symbolTable, spec.Body);
            if (value is SymbolValueSetDefinition setDefinitionValue)
            {
                return new Specification<TFact, TProjection>(setDefinitionValue.SetDefinition.CreatePipeline());
            }
            else if (value is SymbolValueComposite compositeValue)
            {
                return new Specification<TFact, TProjection>(compositeValue.CreateProjectionDefinition().CreatePipeline());
            }
            else
            {
                throw new NotImplementedException();
            }
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
            var symbolTable = SymbolTable.WithParameter(initialFactName, initialFactType);

            var (head, tag, startingSet, steps) = SegmentParser.ParseSegment(symbolTable, spec.Body);
            var last = steps.Last();
            var targetType = last.TargetType;
            if (last is PredecessorStep predecessorStep)
            {
                string targetName = predecessorStep.Role;
                var stepsDefinition = new StepsDefinition(targetName, startingSet, steps);
                var set = new SetDefinition(targetType)
                    .WithSteps(stepsDefinition);
                return new Specification<TFact, TProjection>(set.CreatePipeline());
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
    public class Specification<TFact, TProjection>
    {
        public Pipeline Pipeline { get; }

        public Specification(Pipeline pipeline)
        {
            this.Pipeline = pipeline;
        }
    }
}
