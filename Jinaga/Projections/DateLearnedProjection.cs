using System;
using System.Collections.Immutable;

namespace Jinaga.Projections
{
    /// <summary>
    /// Projects the moment this replica first stored a fact.
    ///
    /// The date is held by the store, not by the fact or the graph, so a specification
    /// that projects it cannot be satisfied from a graph alone.
    /// </summary>
    public class DateLearnedProjection : Projection
    {
        public string Tag { get; }
        public Type FactRuntimeType { get; }

        public override bool CanRunOnGraph => false;

        public DateLearnedProjection(string tag, Type factRuntimeType) :
            base(typeof(DateTime))
        {
            Tag = tag;
            FactRuntimeType = factRuntimeType;
        }

        public override string ToDescriptiveString(int depth = 0)
        {
            return $"date learned of {Tag}";
        }

        public override Projection Apply(ImmutableDictionary<string, string> replacements)
        {
            if (replacements.TryGetValue(Tag, out var replacement))
            {
                return new DateLearnedProjection(replacement, FactRuntimeType);
            }
            else
            {
                return this;
            }
        }
    }
}
