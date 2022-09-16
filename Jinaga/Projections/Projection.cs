using Jinaga.Pipelines;
using System;
using System.Collections.Immutable;

namespace Jinaga.Projections
{
    public abstract class Projection
    {
        public abstract string ToDescriptiveString(int depth = 0);

        public abstract Projection Apply(Label parameter, Label argument);
        public virtual ImmutableList<(string name, SpecificationOld specification)> GetNamedSpecifications()
        {
            return ImmutableList<(string, SpecificationOld)>.Empty;
        }

        public abstract Projection Apply(ImmutableDictionary<string, string> replacements);
        
        public abstract bool CanRunOnGraph { get; }
    }
}