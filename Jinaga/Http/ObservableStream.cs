using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jinaga.Http
{
    public class ObservableStream<TResponse>
    {
        private readonly HttpResponseMessage httpResponse;
        private readonly Stream stream;
        private readonly ILogger logger;
        private readonly CancellationToken cancellationToken;

        public ObservableStream(HttpResponseMessage httpResponse, Stream stream, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
        {
            this.httpResponse = httpResponse;
            this.stream = stream;
            this.logger = loggerFactory.CreateLogger<ObservableStream<TResponse>>();
            this.cancellationToken = cancellationToken;
        }

        public async void Start(Func<string, Task> onData, Action<Exception> onError)
        {
            using var reader = new StreamReader(stream);
            using var cancellationRegistration = cancellationToken.Register(() => stream.Close());

            string line;
            logger.LogTrace("Stream opened.");
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
                    logger.LogTrace("Stream received data.");
                    await onData(line);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Stream failed processing data.");
                    onError(ex);
                    break;
                }
            }
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogTrace("Stream closed by client.");
            }
            else
            {
                logger.LogTrace("Stream closed by server.");
            }
            try
            {
                httpResponse.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Stream failed to dispose.");
            }
        }
    }
}
