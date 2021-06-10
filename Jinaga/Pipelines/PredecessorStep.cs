namespace Jinaga.Pipelines
{
    public class PredecessorStep : Step
    {
        private string successorType;
        private string role;
        private string predecessorType;

        public PredecessorStep(string successorType, string role, string predecessorType)
        {
            this.successorType = successorType;
            this.role = role;
            this.predecessorType = predecessorType;
        }

        public override Step Reflect()
        {
            return new SuccessorStep(predecessorType, role, successorType);
        }

        public override string ToDescriptiveString()
        {
            return $"P.{role} {predecessorType}";
        }
    }
}
