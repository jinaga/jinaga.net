using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Parsers;
using Jinaga.Repository;
using Jinaga.Definitions;
using Jinaga.Generators;
using Jinaga.Projections;
using Jinaga.Pipelines;
using System.Collections.Immutable;

namespace Jinaga
{
    static class SpecificationProcessor
    {
        public static (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Queryable<TProjection>(LambdaExpression specExpression)
        {
            var spec = specExpression.Compile();
            var proxies = ImmutableList<object>.Empty;
            var given = ImmutableList<Label>.Empty;
            var context = SpecificationContext.Empty;

            foreach (var parameter in specExpression.Parameters.Take(specExpression.Parameters.Count - 1))
            {
                var proxy = SpecificationParser.InstanceOfFact(parameter.Type);
                var label = new Label(parameter.Name, parameter.Type.FactTypeName());

                proxies = proxies.Add(proxy);
                given = given.Add(label);
                context = context.With(label, proxy, parameter.Type);
            }

            object factRepository = new FactRepository();
            var queryable = (JinagaQueryable<TProjection>)spec.DynamicInvoke(proxies.Add(factRepository).ToArray());

            var result = SpecificationParser.ParseSpecification(SymbolTable.Empty, context, queryable.Expression);
            var matches = SpecificationGenerator.CreateMatches(context, result);
            var projection = SpecificationGenerator.CreateProjection(result.SymbolValue);

            return (given, matches, projection);
        }

        public static (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Scalar<TProjection>(LambdaExpression specExpression)
        {
            var given = ImmutableList<Label>.Empty;
            var proxies = ImmutableList<object>.Empty;
            var context = SpecificationContext.Empty;
            var symbolTable = SymbolTable.Empty;

            foreach (var parameter in specExpression.Parameters)
            {
                var proxy = SpecificationParser.InstanceOfFact(parameter.Type);
                var label = new Label(parameter.Name, parameter.Type.FactTypeName());
                var startingSet = new SetDefinitionInitial(label, parameter.Type);

                proxies = proxies.Add(proxy);
                given = given.Add(label);
                context = context.With(label, proxy, parameter.Type);
                symbolTable = symbolTable.With(parameter.Name, new SymbolValueSetDefinition(startingSet));
            }

            var symbolValue = ValueParser.ParseValue(symbolTable, context, specExpression.Body).symbolValue;
            var result = SpecificationParser.ParseValue(symbolValue);
            var matches = SpecificationGenerator.CreateMatches(context, result);
            var projection = SpecificationGenerator.CreateProjection(result.SymbolValue);

            return (given, matches, projection);
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
