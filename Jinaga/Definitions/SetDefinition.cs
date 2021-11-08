using System;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public abstract class SetDefinition
    {
        protected SetDefinition(Type type)
        {
            Type = type;
        }

        public Label Label => new Label(Tag, FactType);
        public abstract string FactType { get; }
        public virtual string Tag => throw new NotImplementedException();

        public Type Type { get; }

        public virtual SetDefinition AppendChain(string role, string predecessorType, Type type)
        {
            return new SetDefinitionPredecessorChain(
                new ChainRole(new ChainStart(this), role, predecessorType),
                type
            );
        }

        public virtual Chain ToChain()
        {
            return new ChainStart(this);
        }
    }

    public class SetDefinitionInitial : SetDefinition
    {
        private readonly Label label;

        public SetDefinitionInitial(Label label, Type type) : base(type)
        {
            this.label = label;
        }

        public override string Tag => label.Name;
        public override string FactType => label.Type;
    }

    public class SetDefinitionTarget : SetDefinition
    {
        private readonly string factType;

        public SetDefinitionTarget(string factType, Type type): base(type)
        {
            this.factType = factType;
        }

        public override string FactType => factType;
    }

    public class SetDefinitionLabeledTarget : SetDefinitionTarget
    {
        private readonly Label label;

        public SetDefinitionLabeledTarget(Label label, Type type) : base(label.Type, type)
        {
            this.label = label;
        }

        public override string Tag => label.Name;
    }

    public class SetDefinitionPredecessorChain : SetDefinition
    {
        private readonly ChainRole chainRole;

        public override string FactType => chainRole.TargetFactType;
        public override string Tag => chainRole.Role;

        public SetDefinitionPredecessorChain(ChainRole chainRole, Type type) : base(type)
        {
            this.chainRole = chainRole;
        }

        public override SetDefinition AppendChain(string role, string predecessorType, Type type)
        {
            return new SetDefinitionPredecessorChain(
                new ChainRole(chainRole, role, predecessorType),
                type
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
            Chain tail,
            Type type) : base(type)
        {
            this.tag = tag;
            this.head = head;
            this.tail = tail;
        }

        public override string Tag => tag;

        public override string FactType => tail.SourceFactType;
    }

    public class SetDefinitionConditional : SetDefinition
    {
        private readonly SetDefinition source;
        private readonly ConditionDefinition condition;

        public SetDefinition Source => source;
        public ConditionDefinition Condition => condition;

        public SetDefinitionConditional(
            SetDefinition source,
            ConditionDefinition condition,
            Type type) : base(type)
        {
            this.source = source;
            this.condition = condition;
        }

        public override string Tag => source.Tag;

        public override string FactType => source.FactType;
    }
}
