using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines
{
    public class ConditionalStep : Step
    {
        private readonly ImmutableList<Step> steps;
        private readonly bool exists;

        public ConditionalStep(ImmutableList<Step> steps, bool exists)
        {
            this.steps = steps;
            this.exists = exists;
        }

        public override string InitialType => throw new System.NotImplementedException();

        public override string TargetType => throw new System.NotImplementedException();

        public override Step Reflect()
        {
            throw new System.NotImplementedException();
        }

        public override string ToDescriptiveString(int depth)
        {
            string stepsDescriptiveString = string.Join(" ", steps
                .Select(step => step.ToDescriptiveString(depth)));
            string op = exists ? "E" : "N";
            string indent = String.Join("", Enumerable.Repeat("    ", depth));
            return $"{op}(\r\n    {indent}{stepsDescriptiveString}\r\n{indent})";
        }

        public override string ToOldDescriptiveString()
        {
            string stepsOldDescriptiveString = string.Join(" ", steps
                .Select(step => step.ToOldDescriptiveString()));
            string op = exists ? "E": "N";
            return $"{op}({stepsOldDescriptiveString})";
        }
    }
}