using Jinaga.Facts;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Managers
{
    class NetworkManager
    {
        private INetwork network;

        public NetworkManager(INetwork network)
        {
            this.network = network;
        }

        public async Task Save(ImmutableList<Fact> facts, CancellationToken cancellationToken)
        {
            // TODO: Queue the facts for sending.
            // Send the facts using the network provider.
            await network.Save(facts, cancellationToken);
        }
    }
}