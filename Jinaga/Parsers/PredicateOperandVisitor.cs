using System.Linq.Expressions;

namespace Jinaga.Parsers
{
    public class PredicateOperandVisitor : ExperimentalVisitor
    {
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            var predicateOperandBodyVisitor = new PredicateOperandBodyVisitor();
            predicateOperandBodyVisitor.Visit(node.Body);
            return node;
        }
    }
}
