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
    class SpecificationProcessor
    {
        private ImmutableList<Source> sources = ImmutableList<Source>.Empty;
        private ImmutableList<Source> givenSources = ImmutableList<Source>.Empty;

        private SpecificationProcessor()
        {
        }

        public static (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Queryable<TProjection>(LambdaExpression specExpression)
        {
            var processor = new SpecificationProcessor();
            foreach (var parameter in specExpression.Parameters.Take(specExpression.Parameters.Count - 1))
            {
                var symbol = processor.NewSource(parameter.Type.FactTypeName());
                processor.AddGiven(symbol);
                processor.SetSourceName(symbol, parameter.Name);
            }
            return processor.Process<TProjection>();
        }

        public static (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Scalar<TProjection>(LambdaExpression specExpression)
        {
            throw new NotImplementedException();
        }

        private Source NewSource(string factType)
        {
            var source = new Source(factType);
            sources = sources.Add(source);
            return source;
        }

        private void AddGiven(Source source)
        {
            givenSources = givenSources.Add(source);
        }

        private void SetSourceName(Source source, string name)
        {
            source.Label = new Label(name, source.FactType);
        }

        private (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Process<TProjection>()
        {
            var given = givenSources
                .Select(g => g.Label!)
                .ToImmutableList();
            var matches = ImmutableList<Match>.Empty;
            var projection = new SimpleProjection(given.First().Name);
            return (given, matches, projection);
            throw new NotImplementedException();
        }
    }

    class Source
    {
        public Source(string factType)
        {
            FactType = factType;
        }

        public Label? Label { get; set; }
        public string FactType { get; }
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
