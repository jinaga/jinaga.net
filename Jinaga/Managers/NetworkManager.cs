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

        public Task Send(ImmutableList<Fact> facts, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}