using System.Linq.Expressions;

namespace Jinaga.Parsers
{
    public class SpecificationParser : ExperimentalVisitor
    {
        private string initialFactName;
        private string initialFactType;

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

            return node;
        }
    }
}
