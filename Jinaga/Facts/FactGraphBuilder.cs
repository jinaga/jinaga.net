﻿using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Facts
{
    public class FactGraphBuilder
    {
        private FactGraph factGraph = FactGraph.Empty;
        private ImmutableList<FactEnvelope> reserve = ImmutableList<FactEnvelope>.Empty;

        public void Add(FactEnvelope envelope)
        {
            if (factGraph.CanAdd(envelope.Fact))
            {
                factGraph = factGraph.Add(envelope);
            }
            else
            {
                reserve = reserve.Add(envelope);
            }
        }

        public FactGraph Build()
        {
            while (reserve.Any())
            {
                var retry = reserve;
                reserve = ImmutableList<FactEnvelope>.Empty;
                foreach (var fact in retry)
                {
                    Add(fact);
                }
                if (retry.Count == reserve.Count)
                {
                    // We did the best we can.
                    break;
                }
            }
            return factGraph;
        }
    }
}
