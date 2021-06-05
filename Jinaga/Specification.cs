using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Repository;

namespace Jinaga
{
    public static class Given<TFact>
    {
        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, IQueryable<TProjection>>> spec)
        {
            throw new NotImplementedException();
        }

        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, TProjection>> spec)
        {
            throw new NotImplementedException();
        }
        
        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, TProjection>> spec)
        {
            throw new NotImplementedException();
        }
    }
    public class Specification<TFact, TProjection>
    {

    }
}
