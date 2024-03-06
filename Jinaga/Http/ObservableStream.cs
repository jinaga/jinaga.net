using System;
using System.IO;
using System.Net.Http;
using System.Text;
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
            byte[] buffer = new byte[1024];
            StringBuilder sb = new StringBuilder();
            logger.LogInformation("Stream opened.");

            while (true)
            {
                try
                {
                    logger.LogInformation("Stream reading data.");
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    logger.LogInformation("Stream ReadAsync returned {bytesRead}.", bytesRead);
                    if (bytesRead == 0 || cancellationToken.IsCancellationRequested)
                    {
                        logger.LogInformation("Stream cancelled during ReadAsync.");
                        break;
                    }

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                    string? line;
                    while ((line = GetLine(sb)) != null)
                    {
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
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation("Stream cancelled due to OperationCanceledException.");
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Stream failed reading data.");
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

        private string? GetLine(StringBuilder sb)
        {
            int endOfLine = sb.ToString().IndexOf('\n');
            if (endOfLine == -1)
            {
                return null;
            }

            string line = sb.ToString().Substring(0, endOfLine).Trim();
            sb.Remove(0, endOfLine + 1);
            return line;
        }
    }
}
