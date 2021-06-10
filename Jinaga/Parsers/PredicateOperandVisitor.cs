using System.Linq.Expressions;

namespace Jinaga.Parsers
{
    public class PredicateOperandVisitor : ExperimentalVisitor
    {
        public string ParameterName { get; private set; }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            ParameterName = node.Parameters[0].Name;
            
            var predicateOperandBodyVisitor = new PredicateOperandBodyVisitor();
            predicateOperandBodyVisitor.Visit(node.Body);
            return node;
        }
    }
}
