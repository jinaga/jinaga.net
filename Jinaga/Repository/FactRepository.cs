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
    }
}
