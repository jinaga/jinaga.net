using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Parsers;
using Jinaga.Repository;
using Jinaga.Definitions;
using Jinaga.Generators;
using Jinaga.Projections;
using Jinaga.Pipelines;

namespace Jinaga
{
    public static class Given<TFact>
    {
        public static Specification<TFact, TProjection> Match<TProjection>(Func<TFact, FactRepository, IQueryable<TProjection>> spec)
        {
            object proxy = SpecificationParser.InstanceOfFact(typeof(TFact));
            var result = (JinagaQueryable<TProjection>)spec((TFact)proxy, new FactRepository());

            var value = SpecificationParser.ParseSpecification(SymbolTable.Empty, result.Expression);
            var specification = SpecificationGenerator.CreateSpecification(value);
            return new Specification<TFact, TProjection>(specification.Pipeline, specification.Projection);
        }

        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, TProjection>> spec)
        {
            var parameter = spec.Parameters[0];
            var initialFactName = parameter.Name;
            var initialFactType = parameter.Type.FactTypeName();
            var startingSet = new SetDefinitionInitial(initialFactName, initialFactType);
            var symbolTable = SymbolTable.Empty.With(initialFactName, new SymbolValueSetDefinition(startingSet));

            var symbolValue = ValueParser.ParseValue(symbolTable, spec.Body).symbolValue;
            switch (symbolValue)
            {
                case SymbolValueSetDefinition setValue:
                    var pipeline = PipelineGenerator.CreatePipeline(setValue.SetDefinition);
                    var simpleProjection = new SimpleProjection(setValue.SetDefinition.Tag);
                    return new Specification<TFact, TProjection>(pipeline, simpleProjection);
                default:
                    throw new NotImplementedException();
            }
        }
    }

    public class Specification<TFact, TProjection> : Specification
    {
        public Specification(Pipeline pipeline, Projection projection) : base(pipeline, projection)
        {
        }
    }
}
