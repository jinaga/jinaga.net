using Jinaga.Facts;
using Jinaga.Visualizers;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines
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

        public Path(Label start, Label target) : this(start, target,
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

        public Path PrependPredecessorStep(Step predecessorStep)
        {
            return new Path(start, target, predecessorSteps.Insert(0, predecessorStep), successorSteps);
        }

        public Path PrependSuccessorStep(Step successorStep)
        {
            return new Path(start, target, predecessorSteps, successorSteps.Insert(0, successorStep));
        }

        public ImmutableList<Product> Execute(ImmutableList<Product> products, FactGraph graph)
        {
            var results = products
                .SelectMany(product =>
                    ExecuteSteps(product.GetFactReference(Start.Name), graph)
                        .Select(factReference => product.With(Target.Name, factReference)))
                .ToImmutableList();
            return results;
        }

        private ImmutableList<FactReference> ExecuteSteps(FactReference startingFactReference, FactGraph graph)
        {
            var startingSet = new FactReference[] { startingFactReference }.ToImmutableList();
            var afterPredecessors = PredecessorSteps
                .Aggregate(startingSet, (set, predecessorStep) => ExecutePredecessorStep(
                    set, predecessorStep.Role, predecessorStep.TargetType, graph
                ));
            return afterPredecessors;
        }

        private static ImmutableList<FactReference> ExecutePredecessorStep(ImmutableList<FactReference> set, string role, string targetType, FactGraph graph)
        {
            var results = set.SelectMany(
                factReference =>
                    graph
                        .GetFact(factReference)
                        .GetPredecessors(role)
                        .Where(r => r.Type == targetType)
                )
                .ToImmutableList();
            return results;
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
                that.start == start &&
                that.target == target &&
                that.predecessorSteps.SequenceEqual(predecessorSteps) &&
                that.successorSteps.SequenceEqual(successorSteps);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(start, target,
                predecessorSteps.SequenceHash(),
                successorSteps.SequenceHash());
        }
    }
}
