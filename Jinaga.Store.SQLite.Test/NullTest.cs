using Jinaga.DefaultImplementations;
using Jinaga.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jinaga.Store.SQLite.Test;

public class NullTest
{
    [FactType("Test.Root")]
    record Root(string RootValue);

    [FactType("Test.Child")]
    record ChildFact(string Property1, string Property2);

    [FactType("Test.Parent")]
    record ParentFact(Root Root, ChildFact Child);

    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "NullTest.db");

    [Fact]
    public async Task ReproduceError()
    {
        // Delete the SQLite file if it exists
        if (File.Exists(SQLitePath))
        {
            File.Delete(SQLitePath);
        }
        var j = GivenJinagaClient();

        var root = new Root("Root");

        // Notice I'm passing in null for Property2, despite saying it is non-nullable.
        var child = new ChildFact("Property1", null!);
        var parent = new ParentFact(root, child);

        await j.Fact(parent);

        // FAILS: System.Collections.Generic.KeyNotFoundException : The given key 'Test.Parent: ATfF6kzhSp7A+mAklTkoJsTjT8YYmbEuySlA46wyt2ZrZXyo2o/NeAB7PTt5Bg2m8T2aIsPtdbkH+d6RXJNVBA==' was not present in the dictionary.
        var results = await j.QueryLocal(
                            Given<Root>.Match((root, facts) =>
                                facts.OfType<ParentFact>(parent => parent.Root == root)),
                            root);

        results.Should().ContainSingle().Which.Should().BeEquivalentTo(parent);

        await j.Unload();
    }

    private static JinagaClient GivenJinagaClient(IStore? store = null)
    {
        return new JinagaClient(store ?? new SQLiteStore(SQLitePath, NullLoggerFactory.Instance), new LocalNetwork(), NullLoggerFactory.Instance);
    }
}