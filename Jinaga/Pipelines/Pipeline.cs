using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines
{
    public class Pipeline
    {
        private readonly string initialFactName;
        private readonly string initialFactType;
        private readonly ImmutableList<Path> paths;

        public Pipeline(string initialFactName, string initialFactType, ImmutableList<Path> paths)
        {
            this.initialFactName = initialFactName;
            this.initialFactType = initialFactType;
            this.paths = paths;
        }

        public string ToDescriptiveString()
        {
            string pathDescriptiveString = string.Join("", paths
                .Select(path => path.ToDescriptiveString()));
            return $"{initialFactName}: {initialFactType} {{\r\n{pathDescriptiveString}}}";
        }

        public string ToOldDescriptiveString()
        {
            return "";
        }
    }
}
