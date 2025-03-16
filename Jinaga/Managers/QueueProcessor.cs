using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Managers
{
    /// <summary>
    /// Processes the outgoing queue with debouncing to improve performance when saving multiple facts in quick succession.
    /// Uses a long-running background task to continuously monitor and process the queue.
    /// </summary>
    class QueueProcessor : IAsyncDisposable
    {
        private readonly NetworkManager networkManager;
        private readonly ILogger logger;
        private readonly int delayMilliseconds;
        
        // Background task management
        private Task? _backgroundTask;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly AsyncSignal _processingSignal = new AsyncSignal();
        private readonly AsyncSignal _delaySignal = new AsyncSignal();
        private readonly AsyncSignal _currentProcessingSignal = new AsyncSignal();

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
            
            // Start the background processing task
            _cancellationTokenSource = new CancellationTokenSource();
            _backgroundTask = Task.Factory.StartNew(
                () => ProcessQueueAsync(_cancellationTokenSource.Token),
                _cancellationTokenSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
                .Unwrap(); // Unwrap is needed because ProcessQueueAsync returns a Task
        }

        /// <summary>
        /// Schedules the queue for processing after the configured delay.
        /// If called multiple times within the delay period, only one processing operation will occur.
        /// </summary>
        public void ScheduleProcessing()
        {
            // Signal that processing is needed
            _processingSignal.Signal();
        }

        /// <summary>
        /// Processes the queue immediately, bypassing any delay.
        /// </summary>
        /// <param name="cancellationToken">To cancel the operation.</param>
        /// <returns>A task that completes when the queue has been processed.</returns>
        public async Task ProcessQueueNow(CancellationToken cancellationToken = default)
        {
            // Get ready for processing
            _currentProcessingSignal.Reset();
            // Interrupt the delay
            _delaySignal.Signal();
            // Signal that processing is needed
            _processingSignal.Signal();
            // Wait for the processing to complete
            await _currentProcessingSignal.WaitAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Stops the background process gracefully.
        /// </summary>
        /// <returns>A task that completes when the background process has stopped.</returns>
        private async Task StopBackgroundProcessAsync()
        {
            if (_backgroundTask == null || _cancellationTokenSource == null)
                return;

            try
            {
                // Signal cancellation
                _cancellationTokenSource.Cancel();
                
                // Interrupt the delay
                _delaySignal.Signal();
                // Signal that processing is needed
                _processingSignal.Signal();
                
                // Wait for the task to complete
                await _backgroundTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // This is expected when cancelling the task
                logger.LogInformation("Background queue processor was cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error stopping background queue processor");
            }
            finally
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
                _backgroundTask = null;
            }
        }
        
        /// <summary>
        /// The main processing loop that runs as a background task.
        /// Continuously monitors for queue processing requests and processes them.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Background queue processor started");
            
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Wait for a signal to process the queue
                        await _processingSignal.WaitAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                        
                        // Skip processing if cancelled
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        // Wait for a short delay to allow more items to be added to the queue
                        await _delaySignal.WaitAsync(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken).ConfigureAwait(false);

                        // Skip processing if cancelled
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        // Reset the signals for the next round
                        _processingSignal.Reset();
                        _delaySignal.Reset();
                            
                        // Process the queue
                        await networkManager.Save(cancellationToken).ConfigureAwait(false);

                        // Mark that processing is complete
                        _currentProcessingSignal.Signal();
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // Expected when cancellation is requested
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Log but don't rethrow to keep the background task running
                        logger.LogError(ex, "Error in background queue processor");
                        
                        // Brief delay before retrying after an error
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected when cancellation is requested
                logger.LogInformation("Background queue processor cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fatal error in background queue processor");
                throw;
            }
            finally
            {
                logger.LogInformation("Background queue processor stopped");
            }
        }
        
        /// <summary>
        /// Disposes of resources used by the queue processor.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await StopBackgroundProcessAsync().ConfigureAwait(false);
        }
    }
}
