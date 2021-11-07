using Jinaga.Pipelines;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Parsers
{
    public class SpecificationContext
    {
        public static SpecificationContext Empty = new SpecificationContext(ImmutableDictionary<object, Label>.Empty);

        private readonly ImmutableDictionary<object, Label> labelByProxy;

        private SpecificationContext(ImmutableDictionary<object, Label> labelByProxy)
        {
            this.labelByProxy = labelByProxy;
        }

        public SpecificationContext With(Label label, object proxy)
        {
            return new SpecificationContext(labelByProxy.Add(proxy, label));
        }

        public Label GetLabel(object value)
        {
            return labelByProxy[value];
        }

        public Label GetFirstLabel()
        {
            return labelByProxy.Values.First();
        }
    }
}