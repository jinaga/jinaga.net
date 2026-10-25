namespace Jinaga.Store.SQLite.Test.Models;

[FactType("Interleaving.Root")]
internal record IsolationRoot(string identifier);

[FactType("Interleaving.A")]
internal record IsolationA(IsolationRoot root, int index);

[FactType("Interleaving.B")]
internal record IsolationB(IsolationRoot root, int index);
