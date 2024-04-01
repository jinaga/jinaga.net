using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Projections;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Observers
{

    class Observer : ObserverLocal
    {
        internal Observer(Specification specification, FactReferenceTuple givenTuple, FactManager factManager, Func<object, Task<Func<Task>>> onAdded, ILoggerFactory loggerFactory) :
            base(specification, givenTuple, factManager, onAdded, loggerFactory)
        {
        }

        internal void Start(bool keepAlive)
        {
            logger.LogInformation("Observer starting for {Specification}", specification.ToDescriptiveString());

            // Capture the synchronization context so that notifications
            // can be executed on the same thread.
            synchronizationContext = SynchronizationContext.Current;

            var cancellationToken = cancelInitialize.Token;
            cachedTask = Task.Run(async () =>
                await ReadFromStore(cancellationToken).ConfigureAwait(false));
            loadedTask = Task.Run(async () =>
            {
                bool cached = await cachedTask.ConfigureAwait(false);
                await FetchFromNetwork(cached, keepAlive, cancellationToken).ConfigureAwait(false);
            });
        }

        public override async Task Refresh(CancellationToken? cancellationToken = null)
        {
            if (!loadedTask?.IsCompleted ?? false)
            {
                return;
            }
            
            if (cancellationToken != null)
            {
                await FetchFromNetwork(true, false, cancellationToken.Value).ConfigureAwait(false);
            }
            else
            {
                using var source = new CancellationTokenSource();
                await FetchFromNetwork(true, false, source.Token).ConfigureAwait(false);
            }
        }

        private async Task FetchFromNetwork(bool cached, bool keepAlive, CancellationToken cancellationToken)
        {
            if (!cached)
            {
                // Fetch from the network first,
                // then read from local storage.
                await Fetch(cancellationToken, keepAlive).ConfigureAwait(false);
                await Read(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Already read from local storage.
                // Fetch from the network to update the cache.
                await Fetch(cancellationToken, keepAlive).ConfigureAwait(false);
            }
            await factManager.SetMruDate(specificationHash, DateTime.UtcNow).ConfigureAwait(false);
        }

        private async Task Fetch(CancellationToken cancellationToken, bool keepAlive)
        {
            if (keepAlive)
            {
                var feeds = await factManager.Subscribe(givenTuple, specification, cancellationToken);
                lock (this)
                {
                    this.feeds = feeds;
                }
            }
            else
            {
                await factManager.Fetch(givenTuple, specification, cancellationToken);
            }
        }
    }
}
