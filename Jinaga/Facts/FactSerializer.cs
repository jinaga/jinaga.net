using System;
using System.Collections.Immutable;
using Jinaga.Parsers;

namespace Jinaga.Facts
{
    public class FactSerializer
    {
        public static Fact Serialize(object runtimeFact)
        {
            string type = runtimeFact.GetType().FactTypeName();
            ImmutableList<Field> fields = ImmutableList<Field>.Empty;
            return new Fact(type, fields);
        }
    }
}
