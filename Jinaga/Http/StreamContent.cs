using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Jinaga.Http
{
    public class StreamContent : HttpContent
    {
        private readonly Action<Stream> writeAction;

        public StreamContent(Action<Stream> writeAction)
        {
            this.writeAction = writeAction;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            writeAction(stream);
            return Task.CompletedTask;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}