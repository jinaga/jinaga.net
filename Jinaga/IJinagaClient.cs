using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
    /// <summary>
    /// Common interface for connected or local Jinaga clients.
    /// </summary>
    public interface IJinagaClient
    {
        /// <summary>
        /// Process a new fact.
        /// The fact is stored locally, and if configured, sent upstream.
        /// Active observers are notified to update the user interface.
        /// </summary>
        /// <typeparam name="TFact">The type of the new fact</typeparam>
        /// <param name="prototype">The new fact</param>
        /// <returns>A copy of the fact as saved</returns>
        /// <exception cref="ArgumentNullException">If the fact is null</exception>
        Task<TFact> Fact<TFact>(TFact prototype) where TFact : class;

        /// <summary>
        /// Retrieve results of a specification from upstream or the local cache.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="cancellationToken">To cancel the operation</param>
        /// <returns>The results</returns>
        /// <exception cref="ArgumentNullException">If the given is null</exception>
        Task<ImmutableList<TProjection>> Query<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            CancellationToken cancellationToken = default) where TFact : class;

        /// <summary>
        /// Retrieve results of a specification from upstream or the local cache.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="cancellationToken">To cancel the operation</param>
        /// <returns>The results</returns>
        /// <exception cref="ArgumentNullException">If either given is null</exception>
        Task<ImmutableList<TProjection>> Query<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            CancellationToken cancellationToken = default) where TFact1 : class where TFact2 : class;

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        IObserver Watch<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Action<TProjection> added)
            where TFact : class;

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        IObserver Watch<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Func<TProjection, Action> added)
            where TFact : class;

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        IObserver Watch<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Func<TProjection, Task> added)
            where TFact : class;

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        IObserver Watch<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Func<TProjection, Task<Func<Task>>> added)
            where TFact : class;

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        IObserver Watch<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            Action<TProjection> added)
            where TFact1 : class
            where TFact2 : class;
            
        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        IObserver Watch<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            Func<TProjection, Action> added)
            where TFact1 : class
            where TFact2 : class;

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        IObserver Watch<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            Func<TProjection, Task> added)
            where TFact1 : class
            where TFact2 : class;

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        IObserver Watch<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            Func<TProjection, Task<Func<Task>>> added)
            where TFact1 : class
            where TFact2 : class;
    }
}
