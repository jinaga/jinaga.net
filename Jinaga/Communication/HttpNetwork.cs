using Jinaga.Facts;
using Jinaga.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Communication
{
    public class HttpNetwork : INetwork
    {
        public Task Save(ImmutableList<Fact> facts, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
