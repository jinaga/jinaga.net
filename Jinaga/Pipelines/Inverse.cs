namespace Jinaga.Pipelines
{
    public class Inverse
    {
        private readonly Pipeline inversePipeline;

        public Inverse(Pipeline inversePipeline)
        {
            this.inversePipeline = inversePipeline;
        }

        public Pipeline InversePipeline => inversePipeline;
    }
}