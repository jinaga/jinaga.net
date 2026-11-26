namespace Jinaga.Store.SQLite.Test;

/// <summary>
/// The base class of every test that opens a SQLite database, and the one owner of the
/// database files this project writes.
///
/// Each database path it hands out names a file no other test names, so no test's cleanup
/// decides whether the next test can start. SQLite in WAL mode writes three files for one
/// database, and the suffixes of those files are written here and nowhere else.
///
/// xUnit builds a fresh instance of a test class for every test in it, so an instance of
/// this class is one test's databases. The clients a test hands to <see cref="Track"/> are
/// unloaded before those files are deleted, so nothing is still writing to a database when
/// it goes.
/// </summary>
public abstract class SQLiteTest : IAsyncLifetime
{
    /// <summary>
    /// What SQLite appends to a database's path for the write-ahead log and the shared
    /// memory file beside it. A reset that deletes only the first of the three leaves the
    /// other two to meet whatever database is created next.
    /// </summary>
    private static readonly string[] fileSuffixes = new[] { "", "-wal", "-shm" };

    private readonly List<string> databasePaths = new List<string>();
    private readonly List<IAsyncDisposable> clients = new List<IAsyncDisposable>();

    /// <summary>
    /// The one directory every test database lives in, so what a run left behind can be
    /// read off a single place.
    /// </summary>
    public static string DatabaseDirectory { get; } = Path.Combine(
        Path.GetTempPath(),
        "JinagaSQLiteTest");

    /// <summary>
    /// This test's database. It is a file no other test names, so what an earlier test
    /// left behind cannot reach it.
    /// </summary>
    protected string DatabasePath { get; }

    protected SQLiteTest()
    {
        DatabasePath = CreateDatabasePath();
    }

    /// <summary>
    /// A database path no test has used before. Call it again for a second database in
    /// the same test.
    /// </summary>
    protected string CreateDatabasePath()
    {
        Directory.CreateDirectory(DatabaseDirectory);
        string databasePath = Path.Combine(DatabaseDirectory, $"{Guid.NewGuid():N}.db");
        databasePaths.Add(databasePath);
        return databasePath;
    }

    /// <summary>
    /// Hands a client to the test to unload when it ends. Returns the client, so a factory
    /// method can wrap the one it builds.
    /// </summary>
    protected T Track<T>(T client) where T : IAsyncDisposable
    {
        clients.Add(client);
        return client;
    }

    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Unloads every tracked client, and then deletes every file of every database this
    /// test created.
    /// </summary>
    public virtual async Task DisposeAsync()
    {
        foreach (var client in clients)
        {
            await client.DisposeAsync();
        }
        clients.Clear();

        foreach (string databasePath in databasePaths)
        {
            foreach (string suffix in fileSuffixes)
            {
                File.Delete(databasePath + suffix);
            }
        }
        databasePaths.Clear();
    }
}
