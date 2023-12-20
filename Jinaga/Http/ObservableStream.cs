using System.IO;

namespace Jinaga.Http
{
    public class ObservableStream<TResponse>
    {
        private Stream stream;

        public ObservableStream(Stream stream)
        {
            this.stream = stream;
        }
    }
}
