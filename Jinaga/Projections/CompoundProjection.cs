using System;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Definitions;
using Jinaga.Pipelines;

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

        public string GetTag(string name)
        {
            return fields
                .Where(field => field.name == name)
                .Select(field => GetTagOf(field.value))
                .Single();
        }

        private string GetTagOf(SymbolValue value)
        {
            if (value is SymbolValueSetDefinition {
                SetDefinition: SetDefinition setDefinition
            })
            {
                return setDefinition.Tag;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public override string ToDescriptiveString()
        {
            var fieldString = string.Join("", fields.Select(field => $"        {field.name} = {ValueDescriptiveString(field.value)}\r\n"));
            return $"{{\r\n{fieldString}    }}";
        }

        private string ValueDescriptiveString(SymbolValue value)
        {
            if (value is SymbolValueSetDefinition {
                SetDefinition: SetDefinition setDefinition
            })
            {
                return setDefinition.Tag;
            }
            else if (value is SymbolValueCollection {
                Projection: Projection projection
            })
            {
                return $"[{projection.ToDescriptiveString()}]";
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}