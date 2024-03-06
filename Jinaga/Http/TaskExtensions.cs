using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Http
{
    public static class TaskExtensions
    {
        /// <summary>
        /// Associate a CancellationToken with a task that doesn't support cancellation.
        /// </summary>
        /// <typeparam name="T">The type of the result of the task.</typeparam>
        /// <param name="task">The task to associate with the CancellationToken.</param>
        /// <param name="cancellationToken">The CancellationToken to associate with the task.</param>
        /// <returns>The result of the task if it completes before the CancellationToken is cancelled; otherwise, throws an OperationCanceledException.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the CancellationToken is cancelled before the task completes.</exception>
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            // Create a TaskCompletionSource to represent the cancellation of the token
            var tcs = new TaskCompletionSource<bool>();

            // Register a delegate with the CancellationToken that will complete the TaskCompletionSource<bool> if the token is cancelled
            using (cancellationToken.Register(
                    s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
                // If the TaskCompletionSource<bool> task completes before the original task, throw an OperationCanceledException
                if (task != await Task.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);

            // If the original task completes before the CancellationToken is cancelled, return the result of the task
            return await task;
        }
    }
}