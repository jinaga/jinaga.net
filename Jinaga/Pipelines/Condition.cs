using System;
using Jinaga.Visualizers;

namespace Jinaga.Pipelines
{
    public class Conditional
    {
        private readonly Label start;
        private readonly bool exists;
        private readonly PipelineOld childPipeline;

        public Conditional(Label start, bool exists, PipelineOld childPipeline)
        {
            this.start = start;
            this.exists = exists;
            this.childPipeline = childPipeline;
        }

        public Label Start => start;
        public bool Exists => exists;
        public PipelineOld ChildPipeline => childPipeline;

        public Conditional Apply(Label parameter, Label argument)
        {
            if (start == parameter)
            {
                return new Conditional(argument, exists, childPipeline.Apply(parameter, argument));
            }
            else
            {
                return new Conditional(start, exists, childPipeline.Apply(parameter, argument));
            }
        }

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

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var that = (Conditional)obj;
            return
                that.exists == exists &&
                that.start == start &&
                that.childPipeline.Equals(childPipeline);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(exists, start, childPipeline);
        }
    }
}
