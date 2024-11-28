using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Repository;
using Jinaga.Serialization;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Jinaga
{

    public static class Given<TFact>
        where TFact: class
    {
        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, IQueryable<TProjection>>> specExpression)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Queryable<TProjection>(specExpression);
            var specificationGivens = givens
                .Select(g => new SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
                .ToImmutableList();
            return new Specification<TFact, TProjection>(specificationGivens, matches, projection);
        }

        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, IQueryable<TProjection>>> specExpression)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Queryable<TProjection>(specExpression);
            var specificationGivens = givens
                .Select(g => new SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
                .ToImmutableList();
            return new Specification<TFact, TProjection>(specificationGivens, matches, projection);
        }

        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, TProjection>> specExpression)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Scalar<TProjection>(specExpression);
            var specificationGivens = givens
                .Select(g => new SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
                .ToImmutableList();
            return new Specification<TFact, TProjection>(specificationGivens, matches, projection);
        }

        public static Specification<TFact, TProjection> Select<TProjection>(Expression<Func<TFact, FactRepository, TProjection>> specSelector)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Select<TProjection>(specSelector);
            var specificationGivens = givens
                .Select(g => new SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
                .ToImmutableList();
            return new Specification<TFact, TProjection>(specificationGivens, matches, projection);
        }

        public static Specification<TFact, TProjection> Select<TProjection>(Expression<Func<TFact, TProjection>> specSelector)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Select<TProjection>(specSelector);
            var specificationGivens = givens
                .Select(g => new SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
                .ToImmutableList();
            return new Specification<TFact, TProjection>(specificationGivens, matches, projection);
        }
    }

    public static class Given<TFact1, TFact2>
        where TFact1 : class
        where TFact2 : class
    {
        public static Specification<TFact1, TFact2, TProjection> Match<TProjection>(Expression<Func<TFact1, TFact2, FactRepository, IQueryable<TProjection>>> specExpression)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Queryable<TProjection>((LambdaExpression)specExpression);
            var specificationGivens = givens
                .Select(g => new SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
                .ToImmutableList();
            return new Specification<TFact1, TFact2, TProjection>(specificationGivens, matches, projection);
        }

        public static Specification<TFact1, TFact2, TProjection> Match<TProjection>(Expression<Func<TFact1, TFact2, IQueryable<TProjection>>> specExpression)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Queryable<TProjection>((LambdaExpression)specExpression);
            var specificationGivens = givens
                .Select(g => new SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
                .ToImmutableList();
            return new Specification<TFact1, TFact2, TProjection>(specificationGivens, matches, projection);
        }

        public static Specification<TFact1, TFact2, TProjection> Match<TProjection>(Expression<Func<TFact1, TFact2, TProjection>> specExpression)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Scalar<TProjection>((LambdaExpression)specExpression);
            var specificationGivens = givens
                .Select(g => new SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
                .ToImmutableList();
            return new Specification<TFact1, TFact2, TProjection>(specificationGivens, matches, projection);
        }

        public static Specification<TFact1, TFact2, TProjection> Select<TProjection>(Expression<Func<TFact1, TFact2, FactRepository, TProjection>> specSelector)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Select<TProjection>(specSelector);
            var specificationGivens = givens
                .Select(g => new SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
                .ToImmutableList();
            return new Specification<TFact1, TFact2, TProjection>(specificationGivens, matches, projection);
        }

        public static Specification<TFact1, TFact2, TProjection> Select<TProjection>(Expression<Func<TFact1, TFact2, TProjection>> specSelector)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Select<TProjection>(specSelector);
            var specificationGivens = givens
                .Select(g => new SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
                .ToImmutableList();
            return new Specification<TFact1, TFact2, TProjection>(specificationGivens, matches, projection);
        }
    }

    public class Specification<TFact, TProjection> : Specification
        where TFact : class
    {
        public Specification(ImmutableList<SpecificationGiven> given, ImmutableList<Match> matches, Projections.Projection projection)
            : base(given, matches, projection)
        {
        }

        public string ToDescriptiveString(TFact given)
        {
            var collector = new Collector(SerializerCache.Empty, new ConditionalWeakTable<object, FactGraph>());
            var givenReference = collector.Serialize(given);
            var givenTuple = FactReferenceTuple.Empty
                .Add(Givens.Single().Label.Name, givenReference);

            string startString = GenerateDeclarationString(givenTuple);
            string specificationString = ToDescriptiveString();
            return startString + "\n" + specificationString;
        }
    }

    public class Specification<TFact1, TFact2, TProjection> : Specification
        where TFact1: class
        where TFact2 : class
    {
        public Specification(ImmutableList<SpecificationGiven> given, ImmutableList<Match> matches, Projections.Projection projection)
            : base(given, matches, projection)
        {
        }
        public string ToDescriptiveString(TFact1 given1, TFact2 given2)
        {
            var collector = new Collector(SerializerCache.Empty, new ConditionalWeakTable<object, FactGraph>());
            var aReference = collector.Serialize(given1);
            var bReference = collector.Serialize(given2);
            var givenTuple = FactReferenceTuple.Empty
                .Add(Givens.ElementAt(0).Label.Name, aReference)
                .Add(Givens.ElementAt(1).Label.Name, bReference);

            string startString = GenerateDeclarationString(givenTuple);
            string specificationString = ToDescriptiveString();
            return startString + "\n" + specificationString;
        }
    }
}
