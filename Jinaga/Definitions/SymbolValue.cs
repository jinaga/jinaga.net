namespace Jinaga.Definitions
{
    public abstract class SymbolValue
    {
        public abstract SymbolValue WithSteps(string tag, StepsDefinition stepsDefinition);
        public abstract SymbolValue WithCondition(ConditionDefinition conditionDefinition);
        public abstract SymbolValue Compose(SymbolValue continuation, ProjectionDefinition projection);
    }
}
