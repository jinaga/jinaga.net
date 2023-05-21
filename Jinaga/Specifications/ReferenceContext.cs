using Jinaga.Pipelines;
using Jinaga.Projections;
using System.Collections.Immutable;

namespace Jinaga.Specifications
{
    internal class ReferenceContext
    {
        public ReferenceContext(Label label, ImmutableList<Role> roles)
        {
            Label = label;
            Roles = roles;
        }

        public Label Label { get; }
        public ImmutableList<Role> Roles { get; }
    }
}
