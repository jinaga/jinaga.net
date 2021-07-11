using System;
using System.Linq;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public abstract class SetDefinition
    {
        private readonly string factType;

        public string FactType => factType;
        public virtual string Tag => throw new NotImplementedException();

        protected SetDefinition(string factType)
        {
            this.factType = factType;
        }

        public virtual SetDefinition AppendChain(string role, string predecessorType)
        {
            return new SetDefinitionChainRole(
                predecessorType,
                new ChainRole(new ChainStart(this), role, predecessorType)
            );
        }

        public virtual Chain ToChain()
        {
            return new ChainStart(this);
        }

        public abstract Pipeline CreatePipeline();
    }

    public class SetDefinitionInitial : SetDefinition
    {
        private readonly string tag;

        public SetDefinitionInitial(string factType, string tag) : base(factType)
        {
            this.tag = tag;
        }

        public override string Tag => tag;

        public override Pipeline CreatePipeline()
        {
            return Pipeline.FromInitialFact(tag, FactType);
        }
    }

    public class SetDefinitionTarget : SetDefinition
    {
        public SetDefinitionTarget(string factType) : base(factType)
        {
        }

        public override Pipeline CreatePipeline()
        {
            throw new NotImplementedException();
        }
    }

    public class SetDefinitionChainRole : SetDefinition
    {
        private readonly ChainRole chainRole;

        public SetDefinitionChainRole(string factType, ChainRole chainRole) : base(factType)
        {
            this.chainRole = chainRole;
        }

        public override SetDefinition AppendChain(string role, string predecessorType)
        {
            return new SetDefinitionChainRole(
                predecessorType,
                new ChainRole(this.chainRole, role, predecessorType)
            );
        }

        public override Chain ToChain()
        {
            return chainRole;
        }

        public override Pipeline CreatePipeline()
        {
            var pipeline = chainRole.CreatePipeline();
            var steps = chainRole.CreatePredecessorSteps();
            string tag = chainRole.InferredTag;
            var path = new Path(tag, chainRole.TargetType, chainRole.Tag, steps);
            return pipeline.WithPath(path);
        }
    }

    public class SetDefinitionJoin : SetDefinition
    {
        private readonly string tag;
        private readonly Chain left;
        private readonly Chain right;

        public SetDefinitionJoin(
            string factType,
            string tag,
            Chain left,
            Chain right) : base(factType)
        {
            this.tag = tag;
            this.left = left;
            this.right = right;
        }

        public override string Tag => tag;

        public override Pipeline CreatePipeline()
        {
            bool leftIsTarget = left.IsTarget;
            bool rightIsTarget = right.IsTarget;

            if (leftIsTarget && !rightIsTarget)
            {
                return BuildPipeline(right, left);
            }
            else if (rightIsTarget && !leftIsTarget)
            {
                return BuildPipeline(left, right);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private Pipeline BuildPipeline(Chain head, Chain tail)
        {
            var pipeline = head.CreatePipeline();
            var startingTag = head.Tag;
            var steps = head.CreatePredecessorSteps().AddRange(tail.CreateSuccessorSteps());
            var path = new Path(tag, FactType, startingTag, steps);
            return pipeline.WithPath(path);
        }
    }

    public class SetDefinitionConditional : SetDefinition
    {
        private readonly SetDefinition source;
        private readonly ConditionDefinition condition;

        public SetDefinitionConditional(
            string factType,
            SetDefinition source,
            ConditionDefinition condition) : base(factType)
        {
            this.source = source;
            this.condition = condition;
        }

        public override string Tag => source.Tag;

        public override Pipeline CreatePipeline()
        {
            throw new NotImplementedException();
        }
    }
}
