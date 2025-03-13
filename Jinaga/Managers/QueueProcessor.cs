using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Managers
{
    /// <summary>
    /// Processes the outgoing queue with debouncing to improve performance when saving multiple facts in quick succession.
    /// </summary>
    class QueueProcessor
    {
        private readonly NetworkManager networkManager;
        private readonly ILogger logger;
        private readonly int delayMilliseconds;
        
        private Timer? debounceTimer;
        private readonly object timerLock = new object();
        private bool isProcessing = false;
        private TaskCompletionSource<bool>? currentProcessingTask;

        /// <summary>
        /// Creates a new queue processor.
        /// </summary>
        /// <param name="networkManager">The network manager to use for sending facts.</param>
        /// <param name="loggerFactory">A factory configured for logging.</param>
        /// <param name="delayMilliseconds">The delay in milliseconds before processing the queue.</param>
        public QueueProcessor(NetworkManager networkManager, ILoggerFactory loggerFactory, int delayMilliseconds)
        {
            this.networkManager = networkManager;
            this.logger = loggerFactory.CreateLogger<QueueProcessor>();
            this.delayMilliseconds = delayMilliseconds;
        }

        /// <summary>
        /// Schedules the queue for processing after the configured delay.
        /// If called multiple times within the delay period, only one processing operation will occur.
        /// </summary>
        /// <returns>A task that completes when the queue has been processed.</returns>
        public Task ScheduleProcessing()
        {
            lock (timerLock)
            {
                // If immediate processing is configured, process right away
                if (delayMilliseconds <= 0)
                {
                    return ProcessQueueImmediately();
                }

                // If we're already processing, return the current task
                if (isProcessing && currentProcessingTask != null)
                {
                    return currentProcessingTask.Task;
                }

                // If we have a pending timer, stop it and create a new one
                debounceTimer?.Dispose();

                // Create a new task completion source if needed
                if (currentProcessingTask == null)
                {
                    currentProcessingTask = new TaskCompletionSource<bool>();
                }

                // Start a new timer
                debounceTimer = new Timer(
                    ProcessQueueCallback,
                    null,
                    delayMilliseconds,
                    Timeout.Infinite);

                return currentProcessingTask.Task;
            }
        }

        /// <summary>
        /// Processes the queue immediately, bypassing any delay.
        /// </summary>
        /// <param name="cancellationToken">To cancel the operation.</param>
        /// <returns>A task that completes when the queue has been processed.</returns>
        public async Task ProcessQueueNow(CancellationToken cancellationToken = default)
        {
            lock (timerLock)
            {
                // Cancel any pending timer
                debounceTimer?.Dispose();
                debounceTimer = null;

                // If we're already processing, return the current task
                if (isProcessing && currentProcessingTask != null)
                {
                    return;
                }

                // Mark as processing
                isProcessing = true;
                
                // Create a new task completion source if needed
                if (currentProcessingTask == null)
                {
                    currentProcessingTask = new TaskCompletionSource<bool>();
                }
            }

            try
            {
                await networkManager.Save(cancellationToken).ConfigureAwait(false);
                
                lock (timerLock)
                {
                    currentProcessingTask?.SetResult(true);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing queue");
                
                lock (timerLock)
                {
                    currentProcessingTask?.SetException(ex);
                }
                
                throw;
            }
            finally
            {
                lock (timerLock)
                {
                    isProcessing = false;
                    currentProcessingTask = null;
                }
            }
        }

        private Task ProcessQueueImmediately()
        {
            return ProcessQueueNow(CancellationToken.None);
        }

        private void ProcessQueueCallback(object? state)
        {
            // Start a new task to process the queue
            _ = ProcessQueueNow();
        }
    }
}
