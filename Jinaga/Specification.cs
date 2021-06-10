using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Parsers;
using Jinaga.Pipelines;
using Jinaga.Repository;

namespace Jinaga
{
    public static class Given<TFact>
    {
        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, IQueryable<TProjection>>> spec)
        {
            var specificationParser = new SpecificationParser();
            specificationParser.Visit(spec);
            return new Specification<TFact, TProjection>();
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
        public Pipeline Compile()
        {
            return new Pipeline();
        }
    }
}
