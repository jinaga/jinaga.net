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

        public override string ToDescriptiveString(int depth)
        {
            return $"S.{role} {successorType}";
        }

        public override string ToOldDescriptiveString()
        {
            return $"S.{role} F.type=\"{successorType}\"";
        }

        public override string InitialType => predecessorType;
        public override string TargetType => successorType;
    }
}