using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Pipelines;

namespace Jinaga.Parsers
{
    public static class SpecificationParser
    {
        public static (ImmutableList<Path>, Projection) ParseSpecification(Expression body)
        {
            if (body is MethodCallExpression node)
            {
                var method = node.Method;
                if (method.DeclaringType == typeof(Queryable))
                {
                    if (method.Name == "Where")
                    {
                        return VisitWhere(node.Arguments[0], node.Arguments[1]);
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
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static (ImmutableList<Path>, Projection) VisitWhere(Expression collection, Expression predicate)
        {
            var collectionVisitor = new CollectionVisitor();
            collectionVisitor.Visit(collection);

            var predicateVisitor = new PredicateVisitor();
            predicateVisitor.Visit(predicate);
            string tag = predicateVisitor.Tag;
            string targetType = predicateVisitor.TargetType;
            string startingTag = predicateVisitor.StartingTag;
            ImmutableList<Step> steps = predicateVisitor.Steps;

            var path = new Path(tag, targetType, startingTag, steps);
            var paths = ImmutableList<Path>.Empty.Add(path);
            var projection = new Projection(tag);

            return (paths, projection);
        }
    }
}
