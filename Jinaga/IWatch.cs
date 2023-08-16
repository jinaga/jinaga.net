using System.Threading.Tasks;

namespace Jinaga
{
    public interface IWatch
    {
        Task<bool> Cached { get; }
        Task Loaded { get; }

        void Stop();
    }
}