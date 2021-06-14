using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines
{
    public class ConditionalStep : Step
    {
        private readonly ImmutableList<Path> paths;
        private readonly bool exists;

        public ConditionalStep(ImmutableList<Path> paths, bool exists)
        {
            this.paths = paths;
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
            string pathDescriptiveString = string.Join("", paths
                .Select(path => path.ToDescriptiveString(depth + 1)));
            string op = exists ? "E" : "N";
            string indent = String.Join("", Enumerable.Repeat("    ", depth));
            return $"{op}(\r\n{pathDescriptiveString}{indent})";
        }

        public override string ToOldDescriptiveString()
        {
            throw new System.NotImplementedException();
        }

        public ConditionalStep Invert()
        {
            return new ConditionalStep(paths, !exists);
        }
    }
}