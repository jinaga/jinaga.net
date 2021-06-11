using System;

namespace Jinaga.Pipelines
{
    public class Projection
    {
        public string Tag { get; }

        public Projection(string tag)
        {
            Tag = tag;
        }

        public string ToDescriptiveString()
        {
            return Tag;
        }
    }
}