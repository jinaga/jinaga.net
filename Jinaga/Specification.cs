using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Repository;
using Jinaga.Serialization;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;

namespace Jinaga
{

    public static class Given<TFact>
        where TFact: class
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
        where TFact1 : class
        where TFact2 : class
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
        where TFact : class
    {
        public Specification(ImmutableList<Label> given, ImmutableList<Match> matches, Projections.Projection projection)
            : base(given, matches, projection)
        {
        }

        public string ToDescriptiveString(TFact given)
        {
            var collector = new Collector(SerializerCache.Empty);
            var givenReference = collector.Serialize(given);
            var givenReferences = ImmutableList.Create(givenReference);

            string startString = GenerateDeclarationString(givenReferences);
            string specificationString = ToDescriptiveString();
            return startString + "\n" + specificationString;
        }
    }

    public class Specification<TFact1, TFact2, TProjection> : Specification
        where TFact1: class
        where TFact2 : class
    {
        public Specification(ImmutableList<Label> given, ImmutableList<Match> matches, Projections.Projection projection)
            : base(given, matches, projection)
        {
        }
        public string ToDescriptiveString(TFact1 given1, TFact2 given2)
        {
            var collector = new Collector(SerializerCache.Empty);
            var aReference = collector.Serialize(given1);
            var bReference = collector.Serialize(given2);
            var givenReferences = ImmutableList.Create(aReference, bReference);

            string startString = GenerateDeclarationString(givenReferences);
            string specificationString = ToDescriptiveString();
            return startString + "\n" + specificationString;
        }
    }
}
