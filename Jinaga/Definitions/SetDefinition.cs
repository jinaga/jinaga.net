using System;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public abstract class SetDefinition
    {
        public Label Label => new Label(Tag, FactType);
        public abstract string FactType { get; }
        public virtual string Tag => throw new NotImplementedException();

        public virtual SetDefinition AppendChain(string role, string predecessorType)
        {
            return new SetDefinitionPredecessorChain(
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
        private readonly string factType;

        public SetDefinitionInitial(string tag, string factType)
        {
            this.tag = tag;
            this.factType = factType;
        }

        public override string Tag => tag;
        public override string FactType => factType;
    }

    public class SetDefinitionTarget : SetDefinition
    {
        private readonly string factType;

        public SetDefinitionTarget(string factType)
        {
            this.factType = factType;
        }

        public override string FactType => factType;
    }

    public class SetDefinitionPredecessorChain : SetDefinition
    {
        private readonly ChainRole chainRole;

        public override string FactType => throw new NotImplementedException();
        public override string Tag => chainRole.Role;

        public SetDefinitionPredecessorChain(ChainRole chainRole)
        {
            this.chainRole = chainRole;
        }

        public override SetDefinition AppendChain(string role, string predecessorType)
        {
            return new SetDefinitionPredecessorChain(
                new ChainRole(chainRole, role, predecessorType)
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
        private readonly Chain head;
        private readonly Chain tail;

        public Chain Head => head;
        public Chain Tail => tail;

        public SetDefinitionJoin(
            string tag,
            Chain head,
            Chain tail)
        {
            this.tag = tag;
            this.head = head;
            this.tail = tail;
        }

        public override string Tag => tag;

        public override string FactType => tail.SourceType;
    }

    public class SetDefinitionConditional : SetDefinition
    {
        private readonly SetDefinition source;
        private readonly ConditionDefinition condition;

        public SetDefinition Source => source;
        public ConditionDefinition Condition => condition;

        public SetDefinitionConditional(
            SetDefinition source,
            ConditionDefinition condition)
        {
            this.source = source;
            this.condition = condition;
        }

        public override string Tag => source.Tag;

        public override string FactType => source.FactType;
    }
}
