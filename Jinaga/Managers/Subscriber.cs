using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Services;

namespace Jinaga.Managers
{
    class Subscriber
    {
        private string feed;
        private INetwork network;
        private IStore store;
        private Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers;

        private int refCount = 0;
        private string bookmark = string.Empty;
        private bool resolved = false;

        public Subscriber(string feed, INetwork network, IStore store, Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers)
        {
            this.feed = feed;
            this.network = network;
            this.store = store;
            this.notifyObservers = notifyObservers;
        }

        public bool AddRef()
        {
            refCount++;
            return refCount == 1;
        }

        public bool Release()
        {
            refCount--;
            return refCount == 0;
        }

        public async Task Start()
        {
            /*
    this.bookmark = await this.store.loadBookmark(this.feed);
    await new Promise<void>((resolve, reject) => {
      this.resolved = false;
      // Refresh the connection every 4 minutes.
      this.disconnect = this.connectToFeed(resolve, reject);
      this.timer = setInterval(() => {
        if (this.disconnect) {
          this.disconnect();
        }
        this.disconnect = this.connectToFeed(resolve, reject);
      }, 4 * 60 * 1000);
    });
            */
            bookmark = await store.LoadBookmark(feed);
            resolved = false;
            
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
}