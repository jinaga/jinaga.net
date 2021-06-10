using System;

namespace Jinaga.Pipelines
{
    public class Projection
    {
        private readonly string tag;

        public Projection(string tag)
        {
            this.tag = tag;
        }

        public string ToDescriptiveString()
        {
            return tag;
        }
    }
}