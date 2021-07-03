using System.Collections.Immutable;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Services;

namespace Jinaga.UnitTest
{
    public class MemoryStore : IStore
    {
        private ImmutableList<Fact> facts = ImmutableList<Fact>.Empty;

        public Task<ImmutableList<Fact>> Save(ImmutableList<Fact> newFacts)
        {
            facts = facts.AddRange(newFacts);
            return Task.FromResult(newFacts);
        }
    }
}