namespace Jinaga.Pipelines
{
    public class SuccessorStep : Step
    {
        private string predecessorType;
        public string Role { get; }
        private string successorType;

        public SuccessorStep(string predecessorType, string role, string successorType)
        {
            this.predecessorType = predecessorType;
            this.Role = role;
            this.successorType = successorType;
        }

        public override Step Reflect()
        {
            return new PredecessorStep(successorType, Role, predecessorType);
        }

        public override string ToDescriptiveString(int depth)
        {
            return $"S.{Role} {successorType}";
        }

        public override string ToOldDescriptiveString()
        {
            return $"S.{Role} F.type=\"{successorType}\"";
        }

        public override string InitialType => predecessorType;
        public override string TargetType => successorType;
    }
}