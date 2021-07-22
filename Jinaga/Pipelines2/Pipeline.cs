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

        public Pipeline AddConditional(Conditional conditional)
        {
            return new Pipeline(starts, paths, conditionals.Add(conditional));
        }

        internal ImmutableList<Inverse> ComputeInverses()
        {
            throw new NotImplementedException();
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
    }
}
