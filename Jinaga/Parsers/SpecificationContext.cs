using Jinaga.Pipelines;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Parsers
{
    public class SpecificationContext
    {
        public static SpecificationContext Empty = new SpecificationContext(ImmutableDictionary<object, SpecificationVariable>.Empty);

        private readonly ImmutableDictionary<object, SpecificationVariable> proxyInfo;

        private SpecificationContext(ImmutableDictionary<object, SpecificationVariable> proxyInfo)
        {
            this.proxyInfo = proxyInfo;
        }

        public SpecificationContext With(Label label, object proxy, Type type)
        {
            return new SpecificationContext(proxyInfo.Add(proxy, new SpecificationVariable(label, type)));
        }

        public SpecificationVariable GetVariable(object value)
        {
            return proxyInfo[value];
        }

        public SpecificationVariable GetFirstVariable()
        {
            return proxyInfo.Values.First();
        }
    }
}