using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Visualizers;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines
{
    public class Pipeline
    {
        public static Pipeline Empty = new Pipeline(ImmutableList<Label>.Empty, ImmutableList<Path>.Empty, ImmutableList<Conditional>.Empty, true);

        private readonly ImmutableList<Label> starts;
        private readonly ImmutableList<Path> paths;
        private readonly ImmutableList<Conditional> conditionals;
        private readonly bool canRunOnGraph;

        public Pipeline(ImmutableList<Label> starts, ImmutableList<Path> paths, ImmutableList<Conditional> conditionals, bool canRunOnGraph)
        {
            this.starts = starts;
            this.paths = paths;
            this.conditionals = conditionals;
            this.canRunOnGraph = canRunOnGraph;
        }

        public ImmutableList<Label> Starts => starts;
        public ImmutableList<Path> Paths => paths;
        public ImmutableList<Conditional> Conditionals => conditionals;

        public Pipeline AddStart(Label label)
        {
            return new Pipeline(starts.Add(label), paths, conditionals, canRunOnGraph);
        }

        public Pipeline AddPath(Path path)
        {
            return new Pipeline(starts, paths.Add(path), conditionals,
                canRunOnGraph && !path.SuccessorSteps.Any());
        }

        public Pipeline PrependPath(Path path)
        {
            return new Pipeline(starts, paths.Insert(0, path), conditionals,
                canRunOnGraph && !path.SuccessorSteps.Any());
        }

        public Pipeline AddConditional(Conditional conditional)
        {
            return new Pipeline(starts, paths, conditionals.Add(conditional), false);
        }

        public Pipeline Compose(Pipeline pipeline)
        {
            var combinedStarts = starts
                .Union(pipeline.Starts)
                .ToImmutableList();
            var combinedPaths = paths
                .Union(pipeline.paths)
                .ToImmutableList();
            var combinedConditionals = conditionals
                .Union(pipeline.conditionals)
                .ToImmutableList();
            return new Pipeline(combinedStarts, combinedPaths, combinedConditionals,
                this.canRunOnGraph && pipeline.canRunOnGraph);
        }

        public Pipeline Apply(Label parameter, Label argument)
        {
            var starts = this.starts.Remove(parameter);
            var paths = this.paths
                .Select(path => path.Apply(parameter, argument))
                .ToImmutableList();
            var conditionals = this.conditionals
                .Select(conditional => conditional.Apply(parameter, argument))
                .ToImmutableList();
            return new Pipeline(starts, paths, conditionals, this.canRunOnGraph);
        }

        public bool CanRunOnGraph => canRunOnGraph;

        public ImmutableList<Product> Execute(FactReference startReference, FactGraph graph)
        {
            return Execute(ImmutableList<FactReference>.Empty.Add(startReference), graph);
        }

        public ImmutableList<Product> Execute(ImmutableList<FactReference> startReferences, FactGraph graph)
        {
            var start = starts.Single();
            var initialTag = start.Name;
            var initialType = start.Type;
            var startingProducts = startReferences
                .Where(startReference => startReference.Type == initialType)
                .Select(startReference => Product.Empty.With(initialTag, new SimpleElement(startReference)))
                .ToImmutableList();
            return paths.Aggregate(
                startingProducts,
                (products, path) => path.Execute(products, graph)
            );
        }

        public string ToDescriptiveString(int depth = 0)
        {
            string indent = Strings.Indent(depth);
            string pathLines = paths
                .Select(path =>
                    path.ToDescriptiveString(depth + 1) +
                    conditionals
                        .Where(condition => condition.Start == path.Target)
                        .Select(condition => condition.ToDescriptiveString(depth + 1))
                        .Join("")
                )
                .Join("");
            string strStarts = starts.Any() ? starts.Join(", ") + " " : "";
            return $"{indent}{strStarts}{{\r\n{pathLines}{indent}}}\r\n";
        }

        public string ToOldDescriptiveString()
        {
            var start = starts.Single();
            return PathOldDescriptiveString(start);
        }

        private string PathOldDescriptiveString(Label start)
        {
            var path = paths
                .Where(path => path.Start == start)
                .SingleOrDefault();
            if (path == null)
            {
                return "";
            }

            var conditional = conditionals
                .Where(conditional => conditional.Start == path.Target)
                .Select(conditional => conditional.ToOldDescriptiveString())
                .Join(" ");
            var tail = PathOldDescriptiveString(path.Target);
            return new[]
            {
                path.ToOldDescriptiveString(),
                conditional,
                tail
            }
            .Where(str => !string.IsNullOrWhiteSpace(str))
            .Join(" ");
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

            var that = (Pipeline)obj;
            return
                that.starts.SetEquals(starts) &&
                that.paths.SetEquals(paths) &&
                that.conditionals.SetEquals(conditionals);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                starts.SetHash(),
                paths.SetHash(),
                conditionals.SetHash());
        }
    }
}
