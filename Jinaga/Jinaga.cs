using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Jinaga.Facts;

namespace Jinaga
{
    public class Jinaga
    {
        public async Task<T> Fact<T>(T prototype)
        {
            var fact = FactSerializer.Serialize(prototype);
            return prototype;
        }

        public async Task<ImmutableList<TProjection>> Query<TFact, TProjection>(TFact start, Specification<TFact, TProjection> specification)
        {
            return ImmutableList<TProjection>.Empty;
        }
    }
}
