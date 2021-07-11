using System.Collections.Immutable;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public abstract class Chain
    {
        public abstract bool IsTarget { get; }
        public abstract string Tag { get; }
        public abstract string TargetType { get; }

        public abstract Pipeline CreatePipeline();
        public abstract ImmutableList<Step> CreatePredecessorSteps();
        public abstract ImmutableList<Step> CreateSuccessorSteps();
    }

    public class ChainStart : Chain
    {
        private readonly SetDefinition setDefinition;

        public ChainStart(SetDefinition setDefinition)
        {
            this.setDefinition = setDefinition;
        }

        public override bool IsTarget => setDefinition is SetDefinitionTarget;

        public override string Tag => setDefinition.Tag;

        public override string TargetType => setDefinition.FactType;

        public override string ToString()
        {
            return "start";
        }

        public override Pipeline CreatePipeline()
        {
            return setDefinition.CreatePipeline();
        }

        public override ImmutableList<Step> CreatePredecessorSteps()
        {
            return ImmutableList<Step>.Empty;
        }

        public override ImmutableList<Step> CreateSuccessorSteps()
        {
            return ImmutableList<Step>.Empty;
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

        public override bool IsTarget => prior.IsTarget;

        public override string Tag => prior.Tag;

        public override string TargetType => targetType;

        public override string ToString()
        {
            return $"{prior}.{role}";
        }

        public override Pipeline CreatePipeline()
        {
            return prior.CreatePipeline();
        }

        public override ImmutableList<Step> CreatePredecessorSteps()
        {
            return prior.CreatePredecessorSteps()
                .Add(new PredecessorStep(prior.TargetType, role, targetType));
        }

        public override ImmutableList<Step> CreateSuccessorSteps()
        {
            return ImmutableList<Step>.Empty
                .Add(new SuccessorStep(targetType, role, prior.TargetType))
                .AddRange(prior.CreateSuccessorSteps());
        }
    }
}