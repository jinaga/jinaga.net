using Jinaga.DefaultImplementations;
using Jinaga.Store.SQLite.Database;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jinaga.Store.SQLite.Test;

/// <summary>
/// What <see cref="SQLiteTest"/> promises the tests that derive from it: a database file
/// no other test holds, every file of it gone when the test ends, and the clients it was
/// given unloaded before those files go.
///
/// Both sides of each assertion here are read back from the running system rather than
/// written down twice: the files are found by listing the directory, so a suffix missing
/// from the base class fails this test without that list being repeated in it, and the
/// client is proven unloaded by the library's own refusal to use it afterward.
/// </summary>
public class SQLiteTestDatabaseTest
{
    /// <summary>
    /// Stands in for a test class, so this test can run one's teardown and then look at
    /// what it left behind. The members a derived test uses are protected, so they are
    /// re-exposed here rather than opened up on the base class.
    /// </summary>
    private sealed class Probe : SQLiteTest
    {
        public new string DatabasePath => base.DatabasePath;

        public new T Track<T>(T client) where T : IAsyncDisposable => base.Track(client);

        public string CreateAnotherDatabasePath() => CreateDatabasePath();
    }

    private static string[] FilesOf(string databasePath) =>
        Directory.GetFiles(SQLiteTest.DatabaseDirectory, Path.GetFileName(databasePath) + "*");

    [Fact]
    public async Task Teardown_DeletesEveryFileSQLiteWroteForTheDatabase()
    {
        var probe = new Probe();

        // The factory creates the database and puts it in WAL mode. The connection is
        // then held open on purpose: SQLite removes the log and the shared memory file
        // when the last connection closes, and the point here is that they are present
        // when the test ends.
        _ = new ConnectionFactory(probe.DatabasePath);
        var holder = new Conn(probe.DatabasePath, 99);
        try
        {
            holder.ExecuteNonQuery("INSERT INTO fact_type (name) VALUES ('Blog.Site')");

            FilesOf(probe.DatabasePath).Should().HaveCountGreaterThan(1,
                "a database in WAL mode is more than one file");

            await probe.DisposeAsync();

            FilesOf(probe.DatabasePath).Should().BeEmpty();
        }
        finally
        {
            holder.Close();
        }
    }

    [Fact]
    public async Task Teardown_UnloadsATrackedClient()
    {
        var probe = new Probe();
        var client = probe.Track(new JinagaClient(
            new SQLiteStore(probe.DatabasePath, NullLoggerFactory.Instance),
            new LocalNetwork(),
            [],
            NullLoggerFactory.Instance,
            new JinagaClientOptions()));
        await client.Fact(new Site("teardown.example.com"));

        await probe.DisposeAsync();

        // A client that has been unloaded refuses the next save, so the refusal is what
        // shows the teardown unloaded it.
        Func<Task> afterTeardown = () => client.Fact(new Site("after.example.com"));
        await afterTeardown.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task EveryDatabasePathNamesAFileOfItsOwn()
    {
        var first = new Probe();
        var second = new Probe();
        try
        {
            second.DatabasePath.Should().NotBe(first.DatabasePath);
            first.CreateAnotherDatabasePath().Should().NotBe(first.DatabasePath);
        }
        finally
        {
            await first.DisposeAsync();
            await second.DisposeAsync();
        }
    }
}
