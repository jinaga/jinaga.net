using System.Collections.Immutable;
using System.Linq.Expressions;
using Jinaga.Pipelines;

namespace Jinaga.Parsers
{
    public class PredicateOperandVisitor : ExperimentalVisitor
    {
        public string ParameterName { get; private set; }
        public string ParameterTypeName { get; private set; }
        public string ClosureName { get; private set; }
        public ImmutableList<Step> Steps { get; private set; }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            ParameterName = node.Parameters[0].Name;
            ParameterTypeName = node.Parameters[0].Type.FactTypeName();
            
            var predicateOperandBodyVisitor = new PredicateOperandBodyVisitor(ParameterName);
            predicateOperandBodyVisitor.Visit(node.Body);
            ClosureName = predicateOperandBodyVisitor.ClosureName;
            Steps = predicateOperandBodyVisitor.Steps;

            return node;
        }
    }
}
