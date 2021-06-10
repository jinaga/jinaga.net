using System;
using System.Linq.Expressions;
using Jinaga.Repository;

namespace Jinaga.Parsers
{
    public class CollectionVisitor : ExperimentalVisitor
    {
        private string factTypeName;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var method = node.Method;

            if (method.DeclaringType == typeof(FactRepository))
            {
                if (method.Name == nameof(FactRepository.OfType))
                {
                    VisitFactsOfType(method.GetGenericArguments()[0]);
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

        private void VisitFactsOfType(Type type)
        {
            this.factTypeName = type.FactTypeName();
        }
    }
}
