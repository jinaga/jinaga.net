using System;
using System.Linq;

namespace Jinaga.Repository
{
    public class FactRepository
    {
        public IQueryable<TFact> OfType<TFact>()
        {
            throw new NotImplementedException();
        }

        public bool None<TProjection>(IQueryable<TProjection> existential)
        {
            throw new NotImplementedException();
        }

        public bool Some<TProjection>(IQueryable<TProjection> existential)
        {
            throw new NotImplementedException();
        }
    }
}
