using Jinaga.Projections;
using System;
using System.Collections.Generic;
using System.Text;

namespace Jinaga.Repository
{
    internal class Value
    {
        public SimpleProjection Projection { get; }

        public Value(SimpleProjection projection)
        {
            Projection = projection;
        }
    }
}
