using System;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Definitions;
using Jinaga.Parsers;
using Jinaga.Pipelines;
using Jinaga.Projections;

namespace Jinaga.Generators
{
    public static class SpecificationGenerator
    {
        public static SpecificationOld CreateSpecification(SpecificationContext context, SpecificationResult result)
        {
            var pipeline = PipelineGenerator.CreatePipeline(context, result);
            var projection = CreateProjection(result.SymbolValue);
            return new SpecificationOld(pipeline, projection);
        }

        private static ProjectionOld CreateProjection(SymbolValue value)
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

        public static ImmutableList<Match> CreateMatches(SpecificationContext context, SpecificationResult result)
        {
            throw new NotImplementedException();
        }
    }
}
