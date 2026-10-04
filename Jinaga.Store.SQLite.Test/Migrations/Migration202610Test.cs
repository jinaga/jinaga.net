using Jinaga.Store.SQLite.Database;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jinaga.Store.SQLite.Test.Migrations;

/// <summary>
/// Regression test for https://github.com/jinaga/jinaga.net/issues/184.
///
/// Earlier versions purged the fact row alone, so a database written by one of
/// them still holds edge, ancestor and signature rows whose fact is gone. Those
/// attach to the next fact that takes the missing fact_id.
/// </summary>
public class Migration202610Test
{
    private static readonly string sqlitePath = Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData),
        "Migration202610Test.db");

    [Fact]
    public void DeletesRowsWhoseFactIsGone()
    {
        if (File.Exists(sqlitePath))
        {
            File.Delete(sqlitePath);
        }

        var connectionFactory = new ConnectionFactory(sqlitePath);

        // Facts 1 and 2 exist. Fact 99 does not, standing in for a fact an
        // earlier version purged.
        connectionFactory.WithTxn((conn, i) =>
        {
            conn.ExecuteNonQuery("INSERT INTO fact_type (fact_type_id, name) VALUES (1, 'Blog.Site')");
            conn.ExecuteNonQuery("INSERT INTO fact_type (fact_type_id, name) VALUES (2, 'Blog.Post')");
            conn.ExecuteNonQuery("INSERT INTO role (role_id, defining_fact_type_id, name) VALUES (1, 2, 'site')");
            conn.ExecuteNonQuery("INSERT INTO fact (fact_id, fact_type_id, hash, data) VALUES (1, 1, 'site-hash', '{\"predecessors\":{},\"fields\":{}}')");
            conn.ExecuteNonQuery("INSERT INTO fact (fact_id, fact_type_id, hash, data) VALUES (2, 2, 'post-hash', '{\"predecessors\":{},\"fields\":{}}')");
            conn.ExecuteNonQuery("INSERT INTO public_key (public_key_id, public_key) VALUES (1, 'public-key')");

            conn.ExecuteNonQuery("INSERT INTO edge (role_id, successor_fact_id, predecessor_fact_id) VALUES (1, 2, 1)");
            conn.ExecuteNonQuery("INSERT INTO edge (role_id, successor_fact_id, predecessor_fact_id) VALUES (1, 99, 1)");
            conn.ExecuteNonQuery("INSERT INTO edge (role_id, successor_fact_id, predecessor_fact_id) VALUES (1, 2, 99)");
            conn.ExecuteNonQuery("INSERT INTO ancestor (fact_id, ancestor_fact_id) VALUES (2, 1)");
            conn.ExecuteNonQuery("INSERT INTO ancestor (fact_id, ancestor_fact_id) VALUES (99, 1)");
            conn.ExecuteNonQuery("INSERT INTO ancestor (fact_id, ancestor_fact_id) VALUES (2, 99)");
            conn.ExecuteNonQuery("INSERT INTO signature (fact_id, public_key_id, signature) VALUES (1, 1, 'one')");
            conn.ExecuteNonQuery("INSERT INTO signature (fact_id, public_key_id, signature) VALUES (99, 1, 'ninety-nine')");
            return 0;
        });

        // Opening the store runs the migration.
        _ = new SQLiteStore(sqlitePath, NullLoggerFactory.Instance);

        connectionFactory.WithTxn((conn, i) =>
        {
            // The rows between facts that exist are kept; only the orphans go.
            var edges = conn.ExecuteQueryRaw("SELECT successor_fact_id, predecessor_fact_id FROM edge").ToList();
            edges.Should().HaveCount(1);
            edges[0]["successor_fact_id"].Should().Be("2");
            edges[0]["predecessor_fact_id"].Should().Be("1");

            var ancestors = conn.ExecuteQueryRaw("SELECT fact_id, ancestor_fact_id FROM ancestor").ToList();
            ancestors.Should().HaveCount(1);
            ancestors[0]["fact_id"].Should().Be("2");
            ancestors[0]["ancestor_fact_id"].Should().Be("1");

            var signatures = conn.ExecuteQueryRaw("SELECT fact_id FROM signature").ToList();
            signatures.Should().HaveCount(1);
            signatures[0]["fact_id"].Should().Be("1");
            return 0;
        });
    }
}
