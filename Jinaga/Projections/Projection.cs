using Jinaga.Pipelines;
using System;
using System.Collections.Immutable;

namespace Jinaga.Projections
{
    public abstract class ProjectionOld
    {
        public abstract string ToDescriptiveString(int depth = 0);

        public abstract ProjectionOld Apply(Label parameter, Label argument);
        public virtual ImmutableList<(string name, SpecificationOld specification)> GetNamedSpecifications()
        {
            return ImmutableList<(string, SpecificationOld)>.Empty;
        }

        public abstract ProjectionOld Apply(ImmutableDictionary<string, string> replacements);
    }
}