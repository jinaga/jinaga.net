using System;

namespace Jinaga.Pipelines
{
    public class Inverse
    {
        private readonly Pipeline inversePipeline;
        private readonly string affectedTag;

        public Inverse(Pipeline inversePipeline, string affectedTag)
        {
            this.inversePipeline = inversePipeline;
            this.affectedTag = affectedTag;
        }

        public Pipeline InversePipeline => inversePipeline;

        public string AffectedTag => affectedTag;
    }
}