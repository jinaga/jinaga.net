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
            throw new NotImplementedException();
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
