using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Managers
{
    class AsyncSignal
    {
        private volatile bool _isSignaled = false;
        private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _lock = new object();

        /// <summary>
        /// Signals the event, allowing all waiting tasks to proceed.
        /// </summary>
        public void Signal()
        {
            lock (_lock)
            {
                _isSignaled = true;
                _tcs.TrySetResult(true);
            }
        }

        /// <summary>
        /// Waits for the event to be signaled. The signal remains set until Reset() is called.
        /// </summary>
        /// <param name="timeout">The maximum amount of time to wait for the signal (optional).</param>
        /// <param name="cancellationToken">A cancellation token to stop waiting early (optional).</param>
        /// <returns>True if the signal was received; false if the timeout expired.</returns>
        public async Task<bool> WaitAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_isSignaled)
                    return true; // Return immediately if already signaled
            }

            Task waitTask = _tcs.Task;
            Task timeoutTask = timeout.HasValue ? Task.Delay(timeout.Value, cancellationToken) : Task.Delay(Timeout.Infinite, cancellationToken);

            try
            {
                Task completedTask = await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == waitTask)
                {
                    return true; // The signal was received
                }
                else
                {
                    return false; // Either timeout expired or cancellation was requested
                }
            }
            catch (TaskCanceledException)
            {
                return false; // CancellationToken was triggered
            }
        }

        /// <summary>
        /// Resets the signal, causing future calls to WaitAsync() to block.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                if (!_isSignaled) return; // No need to reset if already reset

                _isSignaled = false;
                _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); // Create a new task for future waiters
            }
        }
    }
}
