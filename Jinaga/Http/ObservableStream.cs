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

        public async void Start(Func<byte[], Task<int>> onData, Action<Exception> onError)
        {
            var buffer = new byte[1024];
            var unhandledData = new byte[0];
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    byte[] data = new byte[unhandledData.Length + bytesRead];
                    Array.Copy(unhandledData, data, unhandledData.Length);
                    Array.Copy(buffer, 0, data, unhandledData.Length, bytesRead);
                    var bytesHandled = await onData.Invoke(data);
                    unhandledData = new byte[data.Length - bytesHandled];
                    Array.Copy(data, bytesHandled, unhandledData, 0, unhandledData.Length);
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
