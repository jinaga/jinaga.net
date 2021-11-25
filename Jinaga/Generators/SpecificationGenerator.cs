using System;
using System.Linq;
using Jinaga.Definitions;
using Jinaga.Parsers;
using Jinaga.Pipelines;
using Jinaga.Projections;

namespace Jinaga.Generators
{
    public static class SpecificationGenerator
    {
        public static Specification CreateSpecification(SpecificationContext context, SpecificationResult result)
        {
            var pipeline = PipelineGenerator.CreatePipeline(context, result);
            var projection = CreateProjection(result.SymbolValue);
            return new Specification(pipeline, projection);
        }

        private static Projection CreateProjection(SymbolValue value)
        {
            if (value is SymbolValueSetDefinition setDefinitionValue)
            {
                return new SimpleProjection(setDefinitionValue.SetDefinition.Tag);
            }
            else if (value is SymbolValueComposite compositeValue)
            {
                var projectionDefinition = compositeValue.CreateProjectionDefinition();
                var projection = projectionDefinition
                    .AllTags()
                    .Aggregate(new CompoundProjection(), (p, tag) => p.With(
                        tag,
                        CreateProjection(projectionDefinition.GetValue(tag))));
                return projection;
            }
            else if (value is SymbolValueCollection collectionValue)
            {
                return new CollectionProjection(collectionValue.Specification);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
