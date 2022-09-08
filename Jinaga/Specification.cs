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
    public static class Given<TFact>
    {
        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, IQueryable<TProjection>>> specExpression)
        {
            var spec = specExpression.Compile();
            ImmutableList<object> proxies = ImmutableList<object>.Empty;
            ImmutableList<Label> given = ImmutableList<Label>.Empty;
            SpecificationContext context = SpecificationContext.Empty;

            foreach (var parameter in specExpression.Parameters.Take(specExpression.Parameters.Count - 1))
            {
                object proxy = SpecificationParser.InstanceOfFact(parameter.Type);
                var label = new Label(parameter.Name, parameter.Type.FactTypeName());
                proxies = proxies.Add(proxy);
                given = given.Add(label);
                context = context.With(label, proxy, parameter.Type);
            }

            object factRepository = new FactRepository();
            var queryable = (JinagaQueryable<TProjection>)spec.DynamicInvoke(proxies.Add(factRepository).ToArray());

            var result = SpecificationParser.ParseSpecification(SymbolTable.Empty, context, queryable.Expression);
            ImmutableList<Match> matches = SpecificationGenerator.CreateMatches(context, result);
            return new Specification<TFact, TProjection>(given, matches);
        }
    }
    public static class GivenOld<TFact>
    {
        public static SpecificationOld<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, IQueryable<TProjection>>> specExpression)
        {
            var spec = specExpression.Compile();
            object proxy = SpecificationParser.InstanceOfFact(typeof(TFact));
            var label = new Label(specExpression.Parameters[0].Name, specExpression.Parameters[0].Type.FactTypeName());
            var context = SpecificationContext.Empty.With(label, proxy, specExpression.Parameters[0].Type);
            var queryable = (JinagaQueryable<TProjection>)spec((TFact)proxy, new FactRepository());

            var result = SpecificationParser.ParseSpecification(SymbolTable.Empty, context, queryable.Expression);
            var specification = SpecificationGenerator.CreateSpecification(context, result);
            return new SpecificationOld<TFact, TProjection>(specification.Pipeline, specification.Projection);
        }

        public static SpecificationOld<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, TProjection>> spec)
        {
            var parameter = spec.Parameters[0];
            var initialFactName = parameter.Name;
            var initialFactType = parameter.Type.FactTypeName();
            object proxy = SpecificationParser.InstanceOfFact(typeof(TFact));
            var label = new Label(initialFactName, initialFactType);
            var context = SpecificationContext.Empty.With(label, proxy, parameter.Type);
            var startingSet = new SetDefinitionInitial(label, parameter.Type);
            var symbolTable = SymbolTable.Empty.With(initialFactName, new SymbolValueSetDefinition(startingSet));

            var symbolValue = ValueParser.ParseValue(symbolTable, context, spec.Body).symbolValue;
            var result = SpecificationParser.ParseValue(symbolValue);
            var specification = SpecificationGenerator.CreateSpecification(context, result);
            return new SpecificationOld<TFact, TProjection>(specification.Pipeline, specification.Projection);
        }
    }

    public class Specification<TFact, TProjection> : Specification
    {
        public Specification(ImmutableList<Label> given, ImmutableList<Match> matches)
            : base(given, matches)
        {
        }

        internal string ToDescriptiveString()
        {
            throw new NotImplementedException();
        }
    }

    public class SpecificationOld<TFact, TProjection> : SpecificationOld
    {
        public SpecificationOld(PipelineOld pipeline, ProjectionOld projection) : base(pipeline, projection)
        {
        }
    }
}
