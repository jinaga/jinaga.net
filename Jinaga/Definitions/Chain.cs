namespace Jinaga.Definitions
{
    public abstract class Chain
    {
        public abstract bool IsTarget { get; }
        public abstract SetDefinition TargetSetDefinition { get; }
        public abstract string Tag { get; }
        public abstract string SourceType { get; }
        public abstract string TargetType { get; }
    }

    public class ChainStart : Chain
    {
        private readonly SetDefinition setDefinition;

        public ChainStart(SetDefinition setDefinition)
        {
            this.setDefinition = setDefinition;
        }

        public override bool IsTarget => setDefinition is SetDefinitionTarget;
        public override SetDefinition TargetSetDefinition => setDefinition;

        public override string Tag => setDefinition.Tag;

        public override string SourceType => setDefinition.FactType;

        public override string TargetType => setDefinition.FactType;

        public override string ToString()
        {
            return "start";
        }
    }

    public class ChainRole : Chain
    {
        private readonly Chain prior;
        private readonly string role;
        private readonly string targetType;

        public ChainRole(Chain prior, string role, string targetType)
        {
            this.prior = prior;
            this.role = role;
            this.targetType = targetType;
        }

        public Chain Prior => prior;
        public string Role => role;
        public override bool IsTarget => prior.IsTarget;
        public override SetDefinition TargetSetDefinition => prior.TargetSetDefinition;

        public override string Tag => prior.Tag;

        public override string SourceType => prior.SourceType;

        public override string TargetType => targetType;

        public string InferredTag => role;

        public override string ToString()
        {
            return $"{prior}.{role}";
        }
    }
}