using Jinaga.Pipelines;
using System;
using System.Collections.Immutable;

namespace Jinaga.Projections
{
    public abstract class Projection
    {
        public abstract string ToDescriptiveString(int depth = 0);

        public abstract Projection Apply(Label parameter, Label argument);
        public virtual ImmutableList<(string name, Specification specification)> GetNamedSpecifications()
        {
            return ImmutableList<(string, Specification)>.Empty;
        }
    }
}