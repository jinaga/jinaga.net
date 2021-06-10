using System.Collections.Immutable;
using System.Linq.Expressions;
using Jinaga.Pipelines;

namespace Jinaga.Parsers
{
    public class SpecificationVisitor : ExperimentalVisitor
    {
        private string initialFactName;
        private string initialFactType;
        private ImmutableList<Path> paths;

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            var body = node.Body;
            var parameter = node.Parameters[0];
            var parameterName = parameter.Name;
            var parameterType = parameter.Type;
            string parameterTypeName = parameterType.FactTypeName();

            initialFactName = parameterName;
            initialFactType = parameterTypeName;

            var specificationBodyVisitor = new SpecificationBodyVisitor();
            specificationBodyVisitor.Visit(body);
            paths = specificationBodyVisitor.Paths;

            return node;
        }

        public Pipeline BuildPipeline()
        {
            return new Pipeline(initialFactName, initialFactType, paths);
        }
    }
}
