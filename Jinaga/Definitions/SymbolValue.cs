namespace Jinaga.Definitions
{
    public abstract class SymbolValue
    {
        public abstract SymbolValue WithSteps(StepsDefinition stepsDefinition);
        public abstract SymbolValue WithCondition(ConditionDefinition conditionDefinition);
    }
}
