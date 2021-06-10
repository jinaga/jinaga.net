using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Pipelines;

namespace Jinaga.Parsers
{
    public class SpecificationBodyVisitor : ExperimentalVisitor
    {
        public ImmutableList<Path> Paths { get; private set; } = ImmutableList<Path>.Empty;

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
            string tag = predicateVisitor.Tag;
            string targetType = predicateVisitor.TargetType;
            string startingTag = predicateVisitor.StartingTag;
            ImmutableList<Step> steps = predicateVisitor.Steps;

            Paths = Paths.Add(new Path(tag, targetType, startingTag, steps));
        }
    }
}
