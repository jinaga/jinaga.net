using System;
using Jinaga.Pipelines;

namespace Jinaga.Projections
{
    public class Specification
    {
        public Specification(Pipeline pipeline, Projection projection)
        {
            Pipeline = pipeline;
            Projection = projection;
        }

        public Pipeline Pipeline { get; }
        public Projection Projection { get; }
    }
}
