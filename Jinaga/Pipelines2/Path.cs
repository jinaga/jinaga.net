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

        public Path(Label start, Label target) : this (start, target,
            ImmutableList<Step>.Empty,
            ImmutableList<Step>.Empty
        )
        {
        }

        public Label Start => start;
        public Label Target => target;
        public ImmutableList<Step> PredecessorSteps => predecessorSteps;
        public ImmutableList<Step> SuccessorSteps => successorSteps;

        public Path AddPredecessorStep(Step predecessorStep)
        {
            return new Path(start, target, predecessorSteps.Add(predecessorStep), successorSteps);
        }

        public Path PrependSuccessorStep(Step successorStep)
        {
            return new Path(start, target, predecessorSteps, successorSteps.Insert(0, successorStep));
        }

        public string ToDescriptiveString(int depth = 0)
        {
            string steps = predecessorSteps
                .Select(step => $" P.{step.Role} {step.TargetType}")
                .Concat(successorSteps
                    .Select(step => $" S.{step.Role} {step.TargetType}")
                )
                .Join("");
            return $"{Strings.Indent(depth)}{target} = {start.Name}{steps}\r\n";
        }

        public string ToOldDescriptiveString()
        {
            string steps = predecessorSteps
                .Select(step => $"P.{step.Role} F.type=\"{step.TargetType}\"")
                .Concat(successorSteps
                    .Select(step => $"S.{step.Role} F.type=\"{step.TargetType}\"")
                )
                .Join(" ");
            return steps;
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
            
            var that = (Path)obj;
            return
                that.start == this.start &&
                that.target == this.target &&
                that.predecessorSteps.SequenceEqual(this.predecessorSteps) &&
                that.successorSteps.SequenceEqual(this.successorSteps);
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(start, target,
                predecessorSteps.SequenceHash(),
                successorSteps.SequenceHash());
        }
    }
}
