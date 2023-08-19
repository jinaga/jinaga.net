using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
    public interface IObserver
    {
        Task<bool> Cached { get; }
        Task Loaded { get; }

        Task Refresh(CancellationToken? cancellationToken = null);
        void Stop();
    }
}