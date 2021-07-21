using Jinaga.Visualizers;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines2
{
    public class Pipeline
    {
        private readonly ImmutableList<Label> starts;
        private readonly ImmutableList<Path> paths;
        private readonly ImmutableList<Condition> conditions;

        public Pipeline(ImmutableList<Label> starts, ImmutableList<Path> paths, ImmutableList<Condition> conditions)
        {
            this.starts = starts;
            this.paths = paths;
            this.conditions = conditions;
        }

        public ImmutableList<Label> Starts => starts;
        public ImmutableList<Path> Paths => paths;
        public ImmutableList<Condition> Conditions => conditions;

        public string ToDescriptiveString(int depth = 0)
        {
            string indent = Strings.Indent(depth);
            string pathLines = paths
                .Select(path =>
                    path.ToDescriptiveString(depth + 1) +
                    conditions
                        .Where(condition => condition.Start == path.Target)
                        .Select(condition => condition.ToDescriptiveString(depth + 1))
                        .Join("")
                )
                .Join("");
            return $"{indent}{starts.Join(", ")} {{\n{pathLines}{indent}}}\n";
        }

        public override string ToString()
        {
            return ToDescriptiveString();
        }
    }
}
