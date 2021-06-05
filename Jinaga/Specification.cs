using System;
using System.Linq;
using Jinaga.Repository;

namespace Jinaga
{
    public static class Specification
    {
        public static SpecificationHead<TFact> From<TFact>() =>
            throw new NotImplementedException();
    }

    public class SpecificationHead<TFact>
    {
        public Specification<TFact, TProjection> To<TProjection>(Func<TFact, FactRepository, IQueryable<TProjection>> spec)
        {
            throw new NotImplementedException();
        }
    }
    public class Specification<TFact, TProjection>
    {

    }
}
