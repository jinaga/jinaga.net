using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Repository;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;

namespace Jinaga
{

    public static class Given<TFact>
    {
        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, IQueryable<TProjection>>> specExpression)
        {
            (var given, var matches, var projection) = SpecificationProcessor.Queryable<TProjection>(specExpression);
            return new Specification<TFact, TProjection>(given, matches, projection);
        }

        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, TProjection>> specExpression)
        {
            (var given, var matches, var projection) = SpecificationProcessor.Scalar<TProjection>(specExpression);
            return new Specification<TFact, TProjection>(given, matches, projection);
        }
    }

    public static class Given<TFact1, TFact2>
    {
        public static Specification<TFact1, TFact2, TProjection> Match<TProjection>(Expression<Func<TFact1, TFact2, FactRepository, IQueryable<TProjection>>> specExpression)
        {
            (var given, var matches, var projection) = SpecificationProcessor.Queryable<TProjection>((LambdaExpression)specExpression);
            return new Specification<TFact1, TFact2, TProjection>(given, matches, projection);
        }

        public static Specification<TFact1, TFact2, TProjection> Match<TProjection>(Expression<Func<TFact1, TFact2, TProjection>> specExpression)
        {
            (var given, var matches, var projection) = SpecificationProcessor.Scalar<TProjection>((LambdaExpression)specExpression);
            return new Specification<TFact1, TFact2, TProjection>(given, matches, projection);
        }
    }

    public class Specification<TFact, TProjection> : Specification
    {
        public Specification(ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection)
            : base(given, matches, projection)
        {
        }
    }

    public class Specification<TFact1, TFact2, TProjection> : Specification
    {
        public Specification(ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection)
            : base(given, matches, projection)
        {
        }
    }
}
