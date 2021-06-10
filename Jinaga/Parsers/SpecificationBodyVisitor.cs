using System;
using System.Linq;
using System.Linq.Expressions;

namespace Jinaga.Parsers
{
    public class SpecificationBodyVisitor : ExperimentalVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var method = node.Method;
            if (method.DeclaringType == typeof(Queryable))
            {
                if (method.Name == "Where")
                {
                    VisitWhere(node.Arguments[0], node.Arguments[1]);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new NotImplementedException();
            }

            return node;
        }

        private void VisitWhere(Expression collection, Expression predicate)
        {
            var collectionVisitor = new CollectionVisitor();
            collectionVisitor.Visit(collection);
            var predicateVisitor = new PredicateVisitor();
            predicateVisitor.Visit(predicate);
        }
    }
}
