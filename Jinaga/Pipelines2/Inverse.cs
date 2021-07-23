using System;

namespace Jinaga.Pipelines2
{
    public class Inverse
    {
        private readonly Pipeline inversePipeline;

        public Inverse(Pipeline inversePipeline)
        {
            this.inversePipeline = inversePipeline;
        }

        public Pipeline InversePipeline => inversePipeline;

        public string AffectedTag => throw new NotImplementedException();
    }
}