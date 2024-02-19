using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Http
{
    public class ObservableStream<TResponse>
    {
        private readonly HttpResponseMessage httpResponse;
        private readonly Stream stream;
        private readonly CancellationToken cancellationToken;

        public ObservableStream(HttpResponseMessage httpResponse, Stream stream, CancellationToken cancellationToken)
        {
            this.httpResponse = httpResponse;
            this.stream = stream;
            this.cancellationToken = cancellationToken;
        }

        public async void Start(Func<string, Task> onData, Action<Exception> onError)
        {
            using var reader = new StreamReader(stream);
            using var cancellationRegistration = cancellationToken.Register(() => stream.Close());

            string line;
            while (true)
            {
                try
                {
                    line = await reader.ReadLineAsync();
                }
                catch (IOException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (line == null || cancellationToken.IsCancellationRequested)
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
            try
            {
                httpResponse.Dispose();
            }
            catch (Exception)
            {
                // Ignore
            }
        }
    }
}
