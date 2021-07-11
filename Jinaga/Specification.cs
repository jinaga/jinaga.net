using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Parsers;
using Jinaga.Pipelines;
using Jinaga.Repository;
using Jinaga.Definitions;
using Jinaga.Generators;

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
                return new Specification<TFact, TProjection>(PipelineGenerator.CreatePipeline(setDefinitionValue.SetDefinition));
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

            switch (ValueParser.ParseValue(symbolTable, spec.Body))
            {
                case SymbolValueSetDefinition setValue:
                    var pipeline = PipelineGenerator.CreatePipeline(setValue.SetDefinition);
                    return new Specification<TFact, TProjection>(pipeline);
                default:
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
