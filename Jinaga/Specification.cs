using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Parsers;
using Jinaga.Repository;
using Jinaga.Definitions;
using Jinaga.Generators;
using Jinaga.Projections;
using Jinaga.Pipelines;
using Jinaga.Visualizers;

namespace Jinaga
{
    public static class Given<TFact>
    {
        public static Specification<TFact, TProjection> Match<TProjection>(Func<TFact, FactRepository, IQueryable<TProjection>> spec)
        {
            object proxy = SpecificationParser.InstanceOfFact(typeof(TFact));
            var result = (JinagaQueryable<TProjection>)spec((TFact)proxy, new FactRepository());

            var value = SpecificationParser.ParseSpecification(SymbolTable.Empty, result.Expression);
            if (value is SymbolValueSetDefinition setDefinitionValue)
            {
                return new Specification<TFact, TProjection>(
                    PipelineGenerator.CreatePipeline(setDefinitionValue.SetDefinition),
                    new SimpleProjection(setDefinitionValue.SetDefinition.Tag)
                );
            }
            else if (value is SymbolValueComposite compositeValue)
            {
                var projectionDefinition = compositeValue.CreateProjectionDefinition();
                var pipeline = projectionDefinition
                    .AllSetDefinitions()
                    .Select(s => PipelineGenerator.CreatePipeline(s))
                    .Aggregate((a, b) => a.Compose(b));
                var projection = projectionDefinition
                    .AllTags()
                    .Aggregate(new CompoundProjection(), (p, tag) => p.With(tag, tag));
                return new Specification<TFact, TProjection>(
                    pipeline,
                    new CompoundProjection()
                );
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        // public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, TProjection>> spec)
        // {
        //     throw new NotImplementedException();
        // }
        
        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, TProjection>> spec)
        {
            var parameter = spec.Parameters[0];
            var initialFactName = parameter.Name;
            var initialFactType = parameter.Type.FactTypeName();
            var symbolTable = SymbolTable.WithParameter(initialFactName, initialFactType);

            var symbolValue = ValueParser.ParseValue(symbolTable, spec.Body).symbolValue;
            switch (symbolValue)
            {
                case SymbolValueSetDefinition setValue:
                    var pipeline = PipelineGenerator.CreatePipeline(setValue.SetDefinition);
                    return new Specification<TFact, TProjection>(
                        pipeline,
                        new SimpleProjection(setValue.SetDefinition.Tag)
                    );
                default:
                    throw new NotImplementedException();
            }
        }
    }
    public class Specification<TFact, TProjection>
    {
        private readonly Pipeline pipeline;
        private readonly Projection projection;

        public Specification(Pipeline pipeline, Projection projection)
        {
            this.pipeline = pipeline;
            this.projection = projection;
        }

        public Pipeline Pipeline => pipeline;
        public Projection Projection => projection;
    }
}
