using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Facts
{
    public class FactGraphBuilder
    {
        private FactGraph factGraph = FactGraph.Empty;
        private ImmutableList<Fact> reserve = ImmutableList<Fact>.Empty;

        public void Add(Fact fact)
        {
            if (factGraph.CanAdd(fact))
            {
                factGraph = factGraph.Add(fact);
            }
            else
            {
                reserve = reserve.Add(fact);
            }
        }
        
        public FactGraph Build()
        {
            while (reserve.Any())
            {
                var retry = reserve;
                reserve = ImmutableList<Fact>.Empty;
                foreach (var fact in retry)
                {
                    Add(fact);
                }
                if (retry.Count == reserve.Count)
                {
                    throw new System.Exception("The fact graph cannot be built because of a circular dependency.");
                }
            }
            return factGraph;
        }
    }
}
