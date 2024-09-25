using Jinaga.Pipelines;
using Jinaga.Projections;
using System.Collections.Immutable;

namespace Jinaga.Specifications
{
    public class ReferenceContext
    {
        public ReferenceContext(Label label, ImmutableList<Role> roles)
        {
            Label = label;
            Roles = roles;
        }

        public Label Label { get; }
        public ImmutableList<Role> Roles { get; }

        public static ReferenceContext From(Label label)
        {
            return new ReferenceContext(label, ImmutableList<Role>.Empty);
        }

        public ReferenceContext Push(Role role)
        {
            return new ReferenceContext(Label, Roles.Add(role));
        }
    }
}
