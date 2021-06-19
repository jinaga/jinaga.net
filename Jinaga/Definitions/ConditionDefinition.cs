using System;

namespace Jinaga.Definitions
{
    public class ConditionDefinition
    {
        private SetDefinition set;
        private bool exists;

        public ConditionDefinition(SetDefinition set, bool exists)
        {
            this.set = set;
            this.exists = exists;
        }

        public ConditionDefinition Invert()
        {
            return new ConditionDefinition(set, exists: false);
        }
    }
}
