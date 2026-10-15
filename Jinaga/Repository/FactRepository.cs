using Jinaga.Observers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Jinaga.Repository
{
    public abstract class FactRepository
    {
        public abstract IQueryable<TFact> OfType<TFact>()
            where TFact : class;
        public abstract IQueryable<TFact> OfType<TFact>(Expression<Func<TFact, bool>> predicate)
            where TFact : class;
        public abstract bool Any<TFact>(Expression<Func<TFact, bool>> predicate)
            where TFact : class;
        public abstract IObservableCollection<TProjection> Observable<TFact, TProjection>(
            TFact start,
            Specification<TFact, TProjection> specification)
            where TFact : class;
        public abstract IObservableCollection<TProjection> Observable<TProjection>(IQueryable<TProjection> queryable);

        /// <summary>
        /// The users this replica knows asserted the fact, among those the rules would sign it
        /// with.
        ///
        /// These are the users whose public keys have signed the fact, as the store holds the
        /// signatures, together with each user who saved the fact on this replica and whom
        /// <see cref="JinagaClientOptions.AuthorizationRules"/> would sign it with. A client
        /// signs nothing: the replicator signs on upload, and a signature for a fact this
        /// replica already holds is not fetched again, so without the savers the facts a user
        /// just created on this device would show no signer at all.
        ///
        /// A replicator signs a fact with the uploader's key only when the rules admit it from
        /// that user. So with no rules configured, the client cannot tell whom a replicator
        /// would sign with, and the result is the stored signatures alone; the logged-in user
        /// is never assumed to be a signer. A fact the rules admit from anyone (an <c>Any</c>
        /// rule, which includes <see cref="User"/> under the usual <c>Any&lt;User&gt;()</c>)
        /// is stored by a replicator with no signatures, so it has no signers. A rule that
        /// walks successors is evaluated against the local store, so a saver whose authorizing
        /// facts this replica has not loaded is not counted.
        ///
        /// The result can include savers the replicator never recorded. A queued fact is
        /// uploaded once, under one user's token, so when two users save the same fact on one
        /// device, or a user saves a fact this replica already holds, the replicator signs it
        /// for at most one of them while this replica shows them all. Issue #288 tracks
        /// bringing the replicator into agreement.
        ///
        /// Each user appears once, keyed by public key, so the same user reached through a
        /// signature and as a saver is one signer. A verified signer is not distinguished from
        /// a saver this replica witnessed, because provenance never takes part in matching and
        /// never authorizes.
        ///
        /// Signatures are kept beside a fact, not inside it, so reading provenance does not
        /// change the fact's identity: two users asserting the same thing still produce one
        /// fact. The result is a snapshot of what the store holds when the result is produced,
        /// and a signature that arrives later does not update a result already delivered.
        ///
        /// Provenance cannot affect matching. This method exists only inside a specification's
        /// projection; using it in a condition throws
        /// <see cref="SpecificationException"/>.
        /// </summary>
        /// <typeparam name="TFact">The type of the fact</typeparam>
        /// <param name="fact">The labeled fact whose signers to project</param>
        /// <returns>One user per asserting public key</returns>
        public abstract IEnumerable<User> SignersOf<TFact>(TFact fact)
            where TFact : class;

        /// <summary>
        /// The UTC moment at which this replica first stored the fact.
        ///
        /// The value is replica-local. Jinaga has no global clock, so two replicas that hold
        /// the same fact report different dates for it, and neither is more correct than the
        /// other. It is recorded by whichever store learned the fact, and it never travels
        /// between replicas.
        ///
        /// It is not a creation time. A fact carries no record of when it was asserted, so the
        /// date a replica learned it says nothing about when, or by whom, it was written. A
        /// fact that is purged and learned again is learned again: it gets a new date.
        ///
        /// Provenance cannot affect matching. This method exists only inside a specification's
        /// projection; using it in a condition throws
        /// <see cref="SpecificationException"/>.
        /// </summary>
        /// <typeparam name="TFact">The type of the fact</typeparam>
        /// <param name="fact">The labeled fact whose date learned to project</param>
        /// <returns>The UTC date and time at which this replica first stored the fact</returns>
        public abstract DateTime DateLearned<TFact>(TFact fact)
            where TFact : class;
    }
}
