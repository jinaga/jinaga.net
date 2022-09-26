using Jinaga.Facts;
using Jinaga.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.UnitTest
{
    class SimulatedNetwork : INetwork
    {
        public Task Save(ImmutableList<Fact> facts, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
