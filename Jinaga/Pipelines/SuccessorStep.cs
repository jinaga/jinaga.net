namespace Jinaga.Pipelines
{
    public class SuccessorStep : Step
    {
        private string predecessorType;
        private string role;
        private string successorType;

        public SuccessorStep(string predecessorType, string role, string successorType)
        {
            this.predecessorType = predecessorType;
            this.role = role;
            this.successorType = successorType;
        }

        public override Step Reflect()
        {
            return new PredecessorStep(successorType, role, predecessorType);
        }

        public override string ToDescriptiveString()
        {
            return $"S.{role} {successorType}";
        }

        public override string ToOldDescriptiveString()
        {
            return $"S.{role} F.type=\"{successorType}\"";
        }
    }
}