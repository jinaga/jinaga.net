using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Repository;
using Jinaga.Projections;
using Jinaga.Pipelines;
using System.Collections.Immutable;
using Jinaga.Parsers;

namespace Jinaga
{
    static class SpecificationProcessor
    {
        public static (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Queryable<TProjection>(LambdaExpression specExpression)
        {
            var given = specExpression.Parameters
                .Take(specExpression.Parameters.Count - 1)
                .Select(p => new Label(p.Name, p.Type.FactTypeName()))
                .ToImmutableList();
            var matches = ImmutableList<Match>.Empty;
            var projection = new SimpleProjection(given.First().Name);
            return (given, matches, projection);
        }

        public static (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Scalar<TProjection>(LambdaExpression specExpression)
        {
            throw new NotImplementedException();
        }
    }

    public static class Given<TFact>
    {
        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, IQueryable<TProjection>>> specExpression)
        {
            (var given, var matches, var projection) = SpecificationProcessor.Queryable<TProjection>((LambdaExpression)specExpression);
            return new Specification<TFact, TProjection>(given, matches, projection);
        }

        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, TProjection>> specExpression)
        {
            (var given, var matches, var projection) = SpecificationProcessor.Scalar<TProjection>((LambdaExpression)specExpression);
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
