using System;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public abstract class SetDefinition
    {
        protected readonly string factType;

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
        private readonly string name;

        public SetDefinitionInitial(string factType, string name) : base(factType)
        {
            this.name = name;
        }

        public override Pipeline CreatePipeline()
        {
            return Pipeline.FromInitialFact(name, factType);
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
            throw new NotImplementedException();
        }
    }

    public class SetDefinitionJoin : SetDefinition
    {
        private readonly Chain left;
        private readonly Chain right;

        public SetDefinitionJoin(
            string factType,
            Chain left,
            Chain right) : base(factType)
        {
            this.left = left;
            this.right = right;
        }

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
            throw new NotImplementedException();
            // var pipeline = head.CreatePipeline();
            // var startingTag = head.Tag;
            // var tag = tail.Tag;
            // var steps = head.CreatePredecessorSteps().AddRange(tail.CreateSuccessorSteps());
            // var path = new Path(tag, factType, startingTag, steps);
            // return pipeline.WithPath(path);
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

        public override Pipeline CreatePipeline()
        {
            throw new NotImplementedException();
        }
    }
}
