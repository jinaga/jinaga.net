using System;

namespace Jinaga.Pipelines
{
    public class Inverse
    {
        private readonly Pipeline inversePipeline;
        private readonly string affectedTag;
        private readonly Operation operation;
        private readonly Subset subset;

        public Inverse(Pipeline inversePipeline, string affectedTag, Operation operation, Subset subset)
        {
            this.inversePipeline = inversePipeline;
            this.affectedTag = affectedTag;
            this.operation = operation;
            this.subset = subset;
        }

        public Pipeline InversePipeline => inversePipeline;

        public string AffectedTag => affectedTag;

        public Operation Operation => operation;
        public Subset Subset => subset;
    }
}