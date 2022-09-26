using Jinaga.Facts;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Services
{
    public interface INetwork
    {
        Task Save(ImmutableList<Fact> facts, CancellationToken cancellationToken);
    }
}
