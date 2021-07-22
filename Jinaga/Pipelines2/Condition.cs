using System;
using Jinaga.Visualizers;

namespace Jinaga.Pipelines2
{
    public class Conditional
    {
        private readonly Label start;
        private readonly bool exists;
        private readonly Pipeline childPipeline;

        public Conditional(Label start, bool exists, Pipeline childPipeline)
        {
            this.start = start;
            this.exists = exists;
            this.childPipeline = childPipeline;
        }

        public Label Start => start;
        public bool Exists => exists;
        public Pipeline ChildPipeline => childPipeline;

        public string ToDescriptiveString(int depth = 0)
        {
            string op = exists ? "E" : "N";
            string indent = Strings.Indent(depth);
            string child = childPipeline.ToDescriptiveString(depth + 1);
            return $"{indent}{op}(\r\n{child}{indent})\r\n";
        }

        public string ToOldDescriptiveString()
        {
            string op = exists ? "E" : "N";
            string child = childPipeline.ToOldDescriptiveString();
            return $"{op}({child})";
        }

        public override string ToString()
        {
            return ToDescriptiveString();
        }
    }
}
