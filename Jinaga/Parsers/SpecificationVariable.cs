using System;
using Jinaga.Pipelines;

namespace Jinaga.Parsers
{
    public class SpecificationVariable
    {
        public SpecificationVariable(Label label, Type type)
        {
            Label = label;
            Type = type;
        }

        public Label Label { get; }
        public Type Type { get; }
    }
}
