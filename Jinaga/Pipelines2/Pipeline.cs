using Jinaga.Visualizers;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines2
{
    public class Pipeline
    {
        public static Pipeline Empty = new Pipeline(ImmutableList<Label>.Empty, ImmutableList<Path>.Empty, ImmutableList<Conditional>.Empty);
        
        private readonly ImmutableList<Label> starts;
        private readonly ImmutableList<Path> paths;
        private readonly ImmutableList<Conditional> conditionals;

        public Pipeline(ImmutableList<Label> starts, ImmutableList<Path> paths, ImmutableList<Conditional> conditionals)
        {
            this.starts = starts;
            this.paths = paths;
            this.conditionals = conditionals;
        }

        public ImmutableList<Label> Starts => starts;
        public ImmutableList<Path> Paths => paths;
        public ImmutableList<Conditional> Conditionals => conditionals;

        public Pipeline AddStart(Label label)
        {
            return new Pipeline(starts.Add(label), paths, conditionals);
        }

        public Pipeline AddPath(Path path)
        {
            return new Pipeline(starts, paths.Add(path), conditionals);
        }

        public Pipeline PrependPath(Path path)
        {
            return new Pipeline(starts, paths.Insert(0, path), conditionals);
        }

        public Pipeline AddConditional(Conditional conditional)
        {
            return new Pipeline(starts, paths, conditionals.Add(conditional));
        }

        public ImmutableList<Inverse> ComputeInverses()
        {
            return Inverter.InvertPipeline(this).ToImmutableList();
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
            return new Pipeline(combinedStarts, combinedPaths, combinedConditionals);
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
            return $"{indent}{starts.Join(", ")} {{\r\n{pathLines}{indent}}}\r\n";
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
                that.starts.SetEquals(this.starts) &&
                that.paths.SetEquals(this.paths) &&
                that.conditionals.SetEquals(this.conditionals);
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
