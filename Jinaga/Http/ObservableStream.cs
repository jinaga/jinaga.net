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

        public async Task Start(Func<string, Task> onData, Action<Exception> onError)
        {
            using var reader = new StreamReader(stream);

            string line;
            logger.LogInformation("Stream opened.");
            while (true)
            {
                try
                {
                    logger.LogInformation("Stream reading data.");
                    line = await reader.ReadLineAsync();
                    logger.LogInformation("Stream returned a line: {line}.", line);
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
                    logger.LogInformation("Stream received data.");
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
                logger.LogInformation("Stream closed by client.");
            }
            else
            {
                logger.LogInformation("Stream closed by server.");
            }
            try
            {
                httpResponse.Dispose();
                logger.LogInformation("Stream disposed.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Stream failed to dispose.");
            }
        }
    }
}
