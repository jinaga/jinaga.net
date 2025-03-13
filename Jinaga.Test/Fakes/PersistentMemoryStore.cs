using Jinaga.Services;
using Jinaga.Storage;

namespace Jinaga.Test.Fakes
{
    public class PersistentMemoryStore : MemoryStore, IStore
    {
        public new bool IsPersistent => true;
    }
}
