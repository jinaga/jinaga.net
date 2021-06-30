using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Jinaga
{
    public class Jinaga
    {
        public Task<T> Fact<T>(T prototype)
        {
            throw new NotImplementedException();
        }

        public Task<ImmutableList<TProjection>> Query<TFact, TProjection>(TFact start, Specification<TFact, TProjection> specification)
        {
            throw new NotImplementedException();
        }
    }
}
