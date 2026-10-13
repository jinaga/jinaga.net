using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Jinaga.Projections
{
    /// <summary>
    /// Projects the users whose public keys have signed a labeled fact, as this replica knows
    /// them. Signatures are kept beside a fact rather than inside it, so they do not take part
    /// in its identity: an application can read who asserted something without modeling the
    /// signer as a predecessor.
    ///
    /// The result is a snapshot of the signatures the store holds when the result is produced.
    /// Provenance cannot take part in matching, so no <see cref="Match"/>, <see cref="PathCondition"/>
    /// or <see cref="ExistentialCondition"/> can name this projection.
    /// </summary>
    public class SignerProjection : Projection
    {
        public string Tag { get; }
        public Type FactRuntimeType { get; }

        /// <summary>
        /// Signers come from the store, not from the graph a query starts from. That graph is
        /// the client's serialization of the given, which carries a signature only when this
        /// client signed the fact itself, and never carries the local author, a replica-local
        /// record the graph deliberately leaves out. So a specification that projects signers
        /// cannot be satisfied from a graph alone.
        /// </summary>
        public override bool CanRunOnGraph => false;

        public SignerProjection(string tag, Type factRuntimeType) :
            base(typeof(IEnumerable<User>))
        {
            Tag = tag;
            FactRuntimeType = factRuntimeType;
        }

        public override string ToDescriptiveString(int depth = 0)
        {
            return $"signers of {Tag}";
        }

        public override Projection Apply(ImmutableDictionary<string, string> replacements)
        {
            if (replacements.TryGetValue(Tag, out var replacement))
            {
                return new SignerProjection(replacement, FactRuntimeType);
            }
            else
            {
                return this;
            }
        }
    }
}
