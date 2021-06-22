using System;
using System.Collections.Immutable;

namespace Jinaga.Definitions
{
    public class FieldDefinition
    {
        public string Name { get; }
        public int Position { get; }

        public FieldDefinition(string name, int position)
        {
            Name = name;
            Position = position;
        }
    }
    public class ProjectionDefinition
    {
        public ImmutableList<FieldDefinition> Fields { get; }

        public ProjectionDefinition(ImmutableList<FieldDefinition> fields)
        {
            Fields = fields;
        }
    }
}
