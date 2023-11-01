using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
    /// <summary>
    /// Continues to respond to new facts after Watch.
    /// Control the operation of the observer.
    /// </summary>
    public interface IObserver
    {
        /// <summary>
        /// True if the results were loaded from local storage.
        /// </summary>
        Task<bool> Cached { get; }
        /// <summary>
        /// Resolved when results are loaded from the remote replicator.
        /// </summary>
        Task Loaded { get; }

        /// <summary>
        /// Check the replicator for new facts.
        /// </summary>
        /// <param name="cancellationToken">To cancel the operation</param>
        /// <returns>Resolved when the results have been updated</returns>
        Task Refresh(CancellationToken? cancellationToken = null);
        /// <summary>
        /// Stop updating results.
        /// Call this when unloading a view model.
        /// </summary>
        void Stop();
    }
}