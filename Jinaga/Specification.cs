using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Parsers;
using Jinaga.Repository;
using Jinaga.Definitions;
using Jinaga.Generators;
using Jinaga.Projections;
using Jinaga.Pipelines;

namespace Jinaga
{
    public static class Given<TFact>
    {
        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, IQueryable<TProjection>>> specExpression)
        {
            var spec = specExpression.Compile();
            object proxy = SpecificationParser.InstanceOfFact(typeof(TFact));
            var label = new Label(specExpression.Parameters[0].Name, specExpression.Parameters[0].Type.FactTypeName());
            var context = SpecificationContext.Empty.With(label, proxy, specExpression.Parameters[0].Type);
            var queryable = (JinagaQueryable<TProjection>)spec((TFact)proxy, new FactRepository());

            var result = SpecificationParser.ParseSpecification(SymbolTable.Empty, context, queryable.Expression);
            var specification = SpecificationGenerator.CreateSpecification(context, result);
            return new Specification<TFact, TProjection>(specification.Pipeline, specification.Projection);
        }

        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, TProjection>> spec)
        {
            var parameter = spec.Parameters[0];
            var initialFactName = parameter.Name;
            var initialFactType = parameter.Type.FactTypeName();
            var label = new Label(initialFactName, initialFactType);
            var startingSet = new SetDefinitionInitial(label, parameter.Type);
            var symbolTable = SymbolTable.Empty.With(initialFactName, new SymbolValueSetDefinition(startingSet));

            var symbolValue = ValueParser.ParseValue(symbolTable, SpecificationContext.Empty, spec.Body).symbolValue;
            if (symbolValue is SymbolValueSetDefinition setValue)
            {
                var pipeline = PipelineGenerator.CreatePipeline(SpecificationContext.Empty, setValue.SetDefinition);
                var simpleProjection = new SimpleProjection(setValue.SetDefinition.Tag);
                return new Specification<TFact, TProjection>(pipeline, simpleProjection);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }

    public class Specification<TFact, TProjection> : Specification
    {
        public Specification(Pipeline pipeline, Projection projection) : base(pipeline, projection)
        {
        }
    }
}
