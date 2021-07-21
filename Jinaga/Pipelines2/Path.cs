using Jinaga.Visualizers;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines2
{
    public class Path
    {
        private readonly Label start;
        private readonly Label target;
        private readonly ImmutableList<Step> predecessorSteps;
        private readonly ImmutableList<Step> successorSteps;

        public Path(Label start, Label target, ImmutableList<Step> predecessorSteps, ImmutableList<Step> successorSteps)
        {
            this.start = start;
            this.target = target;
            this.predecessorSteps = predecessorSteps;
            this.successorSteps = successorSteps;
        }

        public Label Start => start;
        public Label Target => target;
        public ImmutableList<Step> PredecessorSteps => predecessorSteps;
        public ImmutableList<Step> SuccessorSteps => successorSteps;

        public string ToDescriptiveString(int depth = 0)
        {
            string steps = predecessorSteps
                .Select(step => $" P.{step.Role} {step.TargetType}")
                .Concat(successorSteps
                    .Select(step => $" S.{step.Role} {step.TargetType}")
                )
                .Join("");
            return $"{Strings.Indent(depth)}{target} = {start.Name}{steps}\n";
        }

        public override string ToString()
        {
            return ToDescriptiveString();
        }
    }
}
