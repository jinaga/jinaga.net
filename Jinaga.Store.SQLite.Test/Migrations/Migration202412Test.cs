using Jinaga.Store.SQLite.Database;
using Jinaga.Store.SQLite.Database.Migrations;

namespace Jinaga.Store.SQLite.Test.Migrations;

public class Migration202412Test
{
    private static readonly string sqlitePath = Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData),
        "Migration202412Test.db");

    [Fact]
    public void DropsTheQueuedColumn()
    {
        // Delete the SQLite file if it exists
        if (File.Exists(sqlitePath))
        {
            File.Delete(sqlitePath);
        }

        var connectionFactory = new ConnectionFactory(sqlitePath);

        RunMigration202405(connectionFactory);

        // Verify that the fact table has a 'queued' column
        connectionFactory.WithTxn((conn, i) =>
        {
            var columns = conn.ExecuteQueryRaw("PRAGMA table_info(fact)");
            Assert.Contains(columns, column => column["name"] == "queued");
            return 0;
        });

        RunMigration202412(connectionFactory);

        // Verify that the fact table does not have a 'queued' column
        connectionFactory.WithTxn((conn, i) =>
        {
            var columns = conn.ExecuteQueryRaw("PRAGMA table_info(fact)");
            Assert.DoesNotContain(columns, column => column["name"] == "queued");
            return 0;
        });
    }

    private static void RunMigration202405(ConnectionFactory connectionFactory)
    {
        connectionFactory.WithTxn((conn, i) =>
        {
            Migration202405.CreateDb(conn);
            return 0;
        });
    }

    private static void RunMigration202412(ConnectionFactory connectionFactory)
    {
        connectionFactory.WithTxn((conn, i) =>
        {
            Migration202412.CreateDb(conn);
            return 0;
        });
    }
}