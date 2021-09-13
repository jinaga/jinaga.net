using System;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Definitions;
using Jinaga.Pipelines;
using Jinaga.Visualizers;

namespace Jinaga.Projections
{
    public class CompoundProjection : Projection
    {
        private ImmutableList<(string name, SymbolValue value)> fields =
            ImmutableList<(string name, SymbolValue value)>.Empty;

        public CompoundProjection()
        {
        }

        private CompoundProjection(ImmutableList<(string name, SymbolValue value)> fields)
        {
            this.fields = fields;
        }

        public CompoundProjection With(string name, SymbolValue value)
        {
            return new CompoundProjection(fields.Add((name, value)));
        }

        public override Projection Apply(Label parameter, Label argument)
        {
            return new CompoundProjection(fields
                .Select(field => (field.name, Apply(field.value, parameter, argument)))
                .ToImmutableList()
            );
        }

        private SymbolValue Apply(SymbolValue value, Label parameter, Label argument)
        {
            if (value is SymbolValueSetDefinition {
                SetDefinition: SetDefinitionInitial initialSetDefinition
            })
            {
                return new SymbolValueSetDefinition(new SetDefinitionInitial(argument.Name, argument.Type));
            }
            else
            {
                return value;
            }
        }

        public SymbolValue GetValue(string name)
        {
            return fields
                .Where(field => field.name == name)
                .Select(field => field.value)
                .Single();
        }

        public override string  ToDescriptiveString(int depth = 0)
        {
            string indent = Strings.Indent(depth);
            var fieldString = string.Join("", fields.Select(field => $"    {indent}{field.name} = {ValueDescriptiveString(field.value, depth + 1)}\r\n"));
            return $"{{\r\n{fieldString}{indent}}}";
        }

        private string ValueDescriptiveString(SymbolValue value, int depth)
        {
            if (value is SymbolValueSetDefinition {
                SetDefinition: SetDefinition setDefinition
            })
            {
                return setDefinition.Tag;
            }
            else if (value is SymbolValueCollection {
                Pipeline: Pipeline pipeline,
                Projection: Projection projection
            })
            {
                string indent = Strings.Indent(depth);
                string strPipeline = pipeline.ToDescriptiveString(depth + 1);
                string strProjection = projection.ToDescriptiveString(depth + 1);
                return $"[\r\n{strPipeline}    {indent}{strProjection}\r\n{indent}]";
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}