using System;
using System.Collections.Immutable;
using System.Linq;
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
            var name = fields.First().Key;
            var setDefinition = ((SymbolValueSetDefinition)fields.First().Value).SetDefinition;
            var tag = setDefinition.Tag;
            var pipeline = setDefinition.CreatePipeline().WithProjection(name, tag);
            foreach (var field in fields.Skip(1))
            {
                var fieldName = field.Key;
                var fieldSetDefinition = ((SymbolValueSetDefinition)field.Value).SetDefinition;
                var fieldPipeline = fieldSetDefinition.CreatePipeline();
                var fieldTag = fieldSetDefinition.Tag;
                pipeline = pipeline.Compose(fieldPipeline).WithProjection(fieldName, fieldTag);
            }

            return pipeline;
        }
    }
}