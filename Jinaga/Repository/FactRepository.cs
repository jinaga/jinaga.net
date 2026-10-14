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
        /// The users whose public keys have signed the fact, as this replica knows them.
        ///
        /// Signatures are kept beside a fact, not inside it, so reading provenance does not
        /// change the fact's identity: two users asserting the same thing still produce one
        /// fact. The result is a snapshot of the signatures held when the result is produced,
        /// and a signature that arrives later does not update a result already delivered.
        ///
        /// Provenance cannot affect matching. This method exists only inside a specification's
        /// projection; using it in a condition throws
        /// <see cref="SpecificationException"/>.
        /// </summary>
        /// <typeparam name="TFact">The type of the fact</typeparam>
        /// <param name="fact">The labeled fact whose signers to project</param>
        /// <returns>One user per signing public key</returns>
        public abstract IEnumerable<User> SignersOf<TFact>(TFact fact)
            where TFact : class;
    }
}
