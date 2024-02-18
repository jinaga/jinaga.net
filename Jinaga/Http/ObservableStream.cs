using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Http
{
    public class ObservableStream<TResponse>
    {
        private readonly Stream stream;
        private readonly CancellationToken cancellationToken;

        public ObservableStream(Stream stream, CancellationToken cancellationToken)
        {
            this.stream = stream;
            this.cancellationToken = cancellationToken;
        }

        public async void Start(Func<string, Task> onData, Action<Exception> onError)
        {
            using var reader = new StreamReader(stream);

            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    await onData(line);
                }
                catch (Exception ex)
                {
                    onError(ex);
                    break;
                }
            }
        }
    }
}
