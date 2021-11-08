using Jinaga.Pipelines;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Parsers
{
    public class SpecificationContext
    {
        public static SpecificationContext Empty = new SpecificationContext(ImmutableDictionary<object, (Label label, Type type)>.Empty);

        private readonly ImmutableDictionary<object, (Label label, Type type)> proxyInfo;

        private SpecificationContext(ImmutableDictionary<object, (Label label, Type type)> proxyInfo)
        {
            this.proxyInfo = proxyInfo;
        }

        public SpecificationContext With(Label label, object proxy, Type type)
        {
            return new SpecificationContext(proxyInfo.Add(proxy, (label, type)));
        }

        public Label GetLabel(object value)
        {
            return proxyInfo[value].label;
        }

        public Type GetType(object value)
        {
            return proxyInfo[value].type;
        }

        public Label GetFirstLabel()
        {
            return proxyInfo.Values.First().label;
        }

        public Type GetFirstType()
        {
            return proxyInfo.Values.First().type;
        }
    }
}