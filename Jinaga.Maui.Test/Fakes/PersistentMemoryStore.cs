using Jinaga.Services;
using Jinaga.Storage;

namespace Jinaga.Maui.Test.Fakes;

/// <summary>
/// A memory store that reports itself persistent, so the client queues saved facts and
/// leaves sending them to an explicit push, as it does with the SQLite store.
/// </summary>
internal class PersistentMemoryStore : MemoryStore, IStore
{
    public new bool IsPersistent => true;
}
