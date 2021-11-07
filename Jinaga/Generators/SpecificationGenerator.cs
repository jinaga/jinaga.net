using System;
using System.Linq;
using Jinaga.Definitions;
using Jinaga.Parsers;
using Jinaga.Projections;

namespace Jinaga.Generators
{
    public static class SpecificationGenerator
    {
        public static Specification CreateSpecification(SpecificationContext context, SymbolValue value)
        {
            if (value is SymbolValueSetDefinition setDefinitionValue)
            {
                var pipeline = PipelineGenerator.CreatePipeline(context, setDefinitionValue.SetDefinition);
                var projection = new SimpleProjection(setDefinitionValue.SetDefinition.Tag);
                return new Specification(pipeline, projection);
            }
            else if (value is SymbolValueComposite compositeValue)
            {
                var projectionDefinition = compositeValue.CreateProjectionDefinition();
                var pipeline = projectionDefinition
                    .AllSetDefinitions()
                    .Select(s => PipelineGenerator.CreatePipeline(context, s))
                    .Aggregate((a, b) => a.Compose(b));
                var projection = projectionDefinition
                    .AllTags()
                    .Aggregate(new CompoundProjection(), (p, tag) => p.With(
                        tag,
                        CreateProjection(projectionDefinition.GetValue(tag))));
                return new Specification(pipeline, projection);
            }
            else
            {
                throw new NotImplementedException();
            }
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
