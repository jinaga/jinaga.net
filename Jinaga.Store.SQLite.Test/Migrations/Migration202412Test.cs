using Jinaga.DefaultImplementations;
using Jinaga.Facts;
using Jinaga.Store.SQLite.Database;
using Jinaga.Store.SQLite.Database.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

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

    [Fact]
    public void MigratesQueuedFactsToOutboundQueue()
    {
        // Delete the SQLite file if it exists
        if (File.Exists(sqlitePath))
        {
            File.Delete(sqlitePath);
        }

        var connectionFactory = new ConnectionFactory(sqlitePath);

        RunMigration202405(connectionFactory);

        // Insert a queued fact and a queue bookmark
        string hash = "wJ9ShDT5oSZPRZt/q4kTArPD3ToIBjGdv1hTVFnJqcrdFWaxZ+SkHQU/C+xOOzGCWBdMhxIlXT0ielCknAxS7w==";
        connectionFactory.WithTxn((conn, i) =>
        {
            conn.ExecuteNonQuery("INSERT INTO fact_type (fact_type_id, name) VALUES (1, 'Blog.Site')");
            conn.ExecuteNonQuery($"INSERT INTO fact (fact_type_id, hash, data, queued) VALUES (1, '{hash}', '{{\"predecessors\":{{}},\"fields\":{{\"domain\":\"qedcode.com\"}}}}', 1)");
            conn.ExecuteNonQuery("INSERT INTO queue_bookmark (replicator, bookmark) VALUES ('primary', '0')");
            return 0;
        });

        RunMigration202412(connectionFactory);

        // Verify that the queued fact has been migrated to the outbound queue
        connectionFactory.WithTxn((conn, i) =>
        {
            var queuedFacts = conn.ExecuteQueryRaw("SELECT fact_id, graph_data FROM outbound_queue");
            Assert.Single(queuedFacts);
            Assert.Equal("1", queuedFacts.Single()["fact_id"]);
            Assert.Equal("[{\"type\":\"Blog.Site\",\"fact\":{\"fields\":{\"domain\":\"qedcode.com\"},\"predecessors\":{}},\"signatures\":[]}]", queuedFacts.Single()["graph_data"]);
            return 0;
        });
    }

    [Fact]
    public async Task JinagaClientCanReadGraphFromOutboundQueue()
    {
        // Delete the SQLite file if it exists
        if (File.Exists(sqlitePath))
        {
            File.Delete(sqlitePath);
        }

        var connectionFactory = new ConnectionFactory(sqlitePath);

        RunMigration202405(connectionFactory);

        // Insert a queued fact and a queue bookmark
        string hash = "wJ9ShDT5oSZPRZt/q4kTArPD3ToIBjGdv1hTVFnJqcrdFWaxZ+SkHQU/C+xOOzGCWBdMhxIlXT0ielCknAxS7w==";
        connectionFactory.WithTxn((conn, i) =>
        {
            conn.ExecuteNonQuery("INSERT INTO fact_type (fact_type_id, name) VALUES (1, 'Blog.Site')");
            conn.ExecuteNonQuery($"INSERT INTO fact (fact_type_id, hash, data, queued) VALUES (1, '{hash}', '{{\"predecessors\":{{}},\"fields\":{{\"domain\":\"qedcode.com\"}}}}', 1)");
            conn.ExecuteNonQuery("INSERT INTO queue_bookmark (replicator, bookmark) VALUES ('primary', '0')");
            return 0;
        });

        RunMigration202412(connectionFactory);

        // Verify that the Jinaga client can read the graph from the outbound queue
        var network = new LocalNetwork();
        var client = GivenJinagaClient(network);
        await client.Push();

        var reference = network.UploadedGraph.FactReferences.Should().ContainSingle().Subject;
        reference.Type.Should().Be("Blog.Site");
        reference.Hash.Should().Be(hash);

        var fact = network.UploadedGraph.GetFact(reference);
        fact.Predecessors.Should().BeEmpty();
        var field = fact.Fields.Should().ContainSingle().Which;
        field.Name.Should().Be("domain");
        field.Value.Should().BeOfType<FieldValueString>().Which
            .StringValue.Should().Be("qedcode.com");
    }

    [Fact]
    public void DropsQueueBookmarkTable()
    {
        // Delete the SQLite file if it exists
        if (File.Exists(sqlitePath))
        {
            File.Delete(sqlitePath);
        }

        var connectionFactory = new ConnectionFactory(sqlitePath);

        RunMigration202405(connectionFactory);

        // Verify that the queue_bookmark table exists
        connectionFactory.WithTxn((conn, i) =>
        {
            var tables = conn.ExecuteQueryRaw("SELECT name FROM sqlite_master WHERE type='table' AND name='queue_bookmark'");
            Assert.Single(tables);
            return 0;
        });

        RunMigration202412(connectionFactory);

        // Verify that the queue_bookmark table does not exist
        connectionFactory.WithTxn((conn, i) =>
        {
            var tables = conn.ExecuteQueryRaw("SELECT name FROM sqlite_master WHERE type='table' AND name='queue_bookmark'");
            Assert.Empty(tables);
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
    private static JinagaClient GivenJinagaClient(Services.INetwork network)
    {
        var store = new SQLiteStore(
            sqlitePath,
            new NullLoggerFactory());
        var jinagaClient = new JinagaClient(
            store,
            network,
            [],
            new NullLoggerFactory());
        return jinagaClient;
    }
}