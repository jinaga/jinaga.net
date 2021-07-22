using System;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Generators;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public class ProjectionDefinition
    {
        private readonly ImmutableDictionary<string, SymbolValue> fields;

        public ProjectionDefinition(ImmutableDictionary<string, SymbolValue> fields)
        {
            this.fields = fields;
        }

        public Pipeline CreatePipeline()
        {
            var orderedFields = fields.OrderBy(field => field.Key).ToImmutableList();
            var name = orderedFields.First().Key;
            var setDefinition = ((SymbolValueSetDefinition)orderedFields.First().Value).SetDefinition;
            var tag = setDefinition.Tag;
            var pipeline = PipelineGenerator_Old.CreatePipeline(setDefinition).WithProjection(name, tag);
            foreach (var field in orderedFields.Skip(1))
            {
                var fieldName = field.Key;
                var fieldSetDefinition = ((SymbolValueSetDefinition)field.Value).SetDefinition;
                var fieldPipeline = PipelineGenerator_Old.CreatePipeline(fieldSetDefinition);
                var fieldTag = fieldSetDefinition.Tag;
                pipeline = pipeline.Compose(fieldPipeline).WithProjection(fieldName, fieldTag);
            }

            return pipeline;
        }
    }
}