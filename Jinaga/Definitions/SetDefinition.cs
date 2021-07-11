using System;

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
    }

    public class SetDefinitionInitial : SetDefinition
    {
        private readonly string tag;

        public SetDefinitionInitial(string factType, string tag) : base(factType)
        {
            this.tag = tag;
        }

        public override string Tag => tag;
    }

    public class SetDefinitionTarget : SetDefinition
    {
        public SetDefinitionTarget(string factType) : base(factType)
        {
        }
    }

    public class SetDefinitionChainRole : SetDefinition
    {
        private readonly ChainRole chainRole;

        public ChainRole ChainRole => chainRole;

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
    }

    public class SetDefinitionJoin : SetDefinition
    {
        private readonly string tag;
        private readonly Chain left;
        private readonly Chain right;

        public Chain Left => left;
        public Chain Right => right;

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
    }
}
