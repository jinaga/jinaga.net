using System;

namespace Jinaga.Pipelines
{
    public class Inverse
    {
        private readonly Pipeline inversePipeline;
        private readonly string affectedTag;
        private readonly Operation operation;

        public Inverse(Pipeline inversePipeline, string affectedTag, Operation operation)
        {
            this.inversePipeline = inversePipeline;
            this.affectedTag = affectedTag;
            this.operation = operation;
        }

        public Pipeline InversePipeline => inversePipeline;

        public string AffectedTag => affectedTag;

        public Operation Operation => operation;
    }
}