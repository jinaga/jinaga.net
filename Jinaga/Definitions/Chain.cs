using System;

namespace Jinaga.Definitions
{
    public abstract class Chain
    {
        public abstract bool IsTarget { get; }
        public abstract SetDefinition TargetSetDefinition { get; }
        public abstract string SourceFactType { get; }
        public abstract string TargetFactType { get; }
        public abstract Type SourceType { get; }
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

        public override string SourceFactType => setDefinition.FactType;

        public override string TargetFactType => setDefinition.FactType;
        public override Type SourceType => setDefinition.Type;

        public override string ToString()
        {
            return "start";
        }
    }

    public class ChainRole : Chain
    {
        private readonly Chain prior;
        private readonly string role;
        private readonly string targetFactType;

        public ChainRole(Chain prior, string role, string targetFactType)
        {
            this.prior = prior;
            this.role = role;
            this.targetFactType = targetFactType;
        }

        public Chain Prior => prior;
        public string Role => role;
        public override bool IsTarget => prior.IsTarget;
        public override SetDefinition TargetSetDefinition => prior.TargetSetDefinition;

        public override string SourceFactType => prior.SourceFactType;

        public override string TargetFactType => targetFactType;
        public override Type SourceType => prior.SourceType;

        public override string ToString()
        {
            return $"{prior}.{role}";
        }
    }
}