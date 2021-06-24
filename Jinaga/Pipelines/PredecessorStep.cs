namespace Jinaga.Pipelines
{
    public class PredecessorStep : Step
    {
        private readonly string successorType;
        public string Role { get; }
        private readonly string predecessorType;

        public PredecessorStep(string successorType, string role, string predecessorType)
        {
            this.successorType = successorType;
            Role = role;
            this.predecessorType = predecessorType;
        }

        public override Step Reflect()
        {
            return new SuccessorStep(predecessorType, Role, successorType);
        }

        public override string ToDescriptiveString(int depth)
        {
            return $"P.{Role} {predecessorType}";
        }

        public override string ToOldDescriptiveString()
        {
            return $"P.{Role} F.type=\"{predecessorType}\"";
        }

        public override string InitialType => successorType;
        public override string TargetType => predecessorType;
    }
}
