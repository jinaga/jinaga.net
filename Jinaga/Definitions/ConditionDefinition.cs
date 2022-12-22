using System;
using Jinaga.Parsers;

namespace Jinaga.Definitions
{
    public class ConditionDefinition
    {
        private ConditionDefinition(SpecificationResult specificationResult, bool exists)
        {
            SpecificationResult = specificationResult;
            Exists = exists;
        }

        public SpecificationResult SpecificationResult { get; }
        public bool Exists { get; }

        public ConditionDefinition Invert()
        {
            return new ConditionDefinition(SpecificationResult, !Exists);
        }

        public static ConditionDefinition From(SpecificationResult result)
        {
            return new ConditionDefinition(result, true);
        }

        public override string ToString()
        {
            return $"{(Exists ? "E" : "!E")} {SpecificationResult}";
        }
    }
}
