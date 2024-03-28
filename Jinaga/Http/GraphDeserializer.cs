using System;

namespace Jinaga.Http
{
    internal class GraphDeserializer
    {
        public GraphDeserializer()
        {
        }

        public LoadResponse Graph { get; } = new LoadResponse();

        internal void DeserializeLine(string line)
        {
            throw new NotImplementedException();
        }
    }
}
