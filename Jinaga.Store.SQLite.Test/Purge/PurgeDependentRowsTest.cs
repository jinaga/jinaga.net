using System.Collections.Immutable;
using Jinaga.DefaultImplementations;
using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Services;
using Jinaga.Storage;
using Jinaga.Store.SQLite.Database;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jinaga.Store.SQLite.Test.Purge;

/// <summary>
/// Regression tests for https://github.com/jinaga/jinaga.net/issues/184.
///
/// Both purge paths used to delete the fact row alone. The edge, ancestor and
/// signature rows derived from the fact stayed behind, and fact_id is a PRIMARY
/// KEY without AUTOINCREMENT, so SQLite hands the id of a deleted fact to the
/// next fact saved. That fact inherited the rows left behind.
/// </summary>
public class PurgeDependentRowsTest
{
    [Fact]
    public async Task WhenPurgeDescendantsRemovesAFact_ThenNoDependentRowOutlivesIt()
    {
        var path = GivenEmptyDatabase(nameof(WhenPurgeDescendantsRemovesAFact_ThenNoDependentRowOutlivesIt));
        var store = new SQLiteStore(path, NullLoggerFactory.Instance);
        var j = GivenClient(store, PurgeConditions.Empty);

        var project = await GivenProject(j);
        var deleted = await j.Fact(new ProjectDeleted(project));
        await GivenSignedName(j, project);

        await store.PurgeDescendants(
            ReferenceOf(project),
            ImmutableList.Create(ReferenceOf(deleted)));

        var orphans = OrphanedRows(path);
        orphans.Edges.Should().Be(0);
        orphans.Ancestors.Should().Be(0);
        orphans.Signatures.Should().Be(0);
    }

    [Fact]
    public async Task WhenPurgeRemovesAFact_ThenNoDependentRowOutlivesIt()
    {
        var path = GivenEmptyDatabase(nameof(WhenPurgeRemovesAFact_ThenNoDependentRowOutlivesIt));
        var store = new SQLiteStore(path, NullLoggerFactory.Instance);
        var purgeConditions = PurgeConditions.Empty
            .Purge<Project>().WhenExists<ProjectDeleted>(deleted => deleted.project);
        var j = GivenClient(store, purgeConditions);

        var project = await GivenProject(j);
        await j.Fact(new ProjectDeleted(project));
        // Saved after the trigger, so the real-time purge does not see it and the
        // on-demand purge below is what removes it.
        await GivenSignedName(j, project);

        await j.Purge();

        var orphans = OrphanedRows(path);
        orphans.Edges.Should().Be(0);
        orphans.Ancestors.Should().Be(0);
        orphans.Signatures.Should().Be(0);
    }

    [Fact]
    public async Task WhenANewFactReusesAPurgedFactsId_ThenItCarriesOnlyItsOwnRows()
    {
        var path = GivenEmptyDatabase(nameof(WhenANewFactReusesAPurgedFactsId_ThenItCarriesOnlyItsOwnRows));
        var store = new SQLiteStore(path, NullLoggerFactory.Instance);
        var j = GivenClient(store, PurgeConditions.Empty);

        var project = await GivenProject(j);
        var deleted = await j.Fact(new ProjectDeleted(project));
        var name = await GivenSignedName(j, project);

        var purgeRoot = ReferenceOf(project);
        var triggers = ImmutableList.Create(ReferenceOf(deleted));
        var purgedFactId = FactIdOf(path, ReferenceOf(name));

        await store.PurgeDescendants(purgeRoot, triggers);

        var site = await j.Fact(new Site("unrelated.example.com"));
        var siteReference = ReferenceOf(site);

        // The test says nothing about dependent rows unless the new fact really
        // takes the purged fact's id, so hold the premise to account.
        FactIdOf(path, siteReference).Should().Be(purgedFactId);

        await store.PurgeDescendants(purgeRoot, triggers);

        var facts = await store.GetAllFacts();
        facts.Select(fact => fact.Reference).Should().Contain(siteReference);

        // Load is an explicit IStore implementation, so it is reachable only
        // through the interface.
        var graph = await ((IStore)store).Load(ImmutableList.Create(siteReference), CancellationToken.None);
        graph.GetSignatures(siteReference).Should().BeEmpty();
    }

    private static async Task<Project> GivenProject(JinagaClient j)
    {
        var company = await j.Fact(new Company());
        var department = await j.Fact(new Department(company));
        return await j.Fact(new Project(department));
    }

    private static Task<ProjectName> GivenSignedName(JinagaClient j, Project project) =>
        j.SingleUse(_ => j.Fact(new ProjectName(project, "Signed", [])));

    private static JinagaClient GivenClient(SQLiteStore store, PurgeConditions purgeConditions) =>
        new JinagaClient(
            store,
            new LocalNetwork(),
            purgeConditions.Validate(),
            NullLoggerFactory.Instance,
            new JinagaClientOptions());

    private static string GivenEmptyDatabase(string name)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JinagaSQLiteTest",
            $"PurgeDependentRowsTest.{name}.db");
        if (File.Exists(path))
            File.Delete(path);
        return path;
    }

    /// <summary>
    /// Counts the rows that reference a fact_id no fact has. Reads through its own
    /// connection rather than a ConnectionFactory, because opening one runs the
    /// migration that deletes exactly these rows.
    /// </summary>
    private static (int Edges, int Ancestors, int Signatures) OrphanedRows(string path)
    {
        var conn = new Conn(path, 0);
        try
        {
            return (
                ReadInt(conn, @"
                    SELECT COUNT(*) FROM edge
                    WHERE successor_fact_id NOT IN (SELECT fact_id FROM fact)
                        OR predecessor_fact_id NOT IN (SELECT fact_id FROM fact)"),
                ReadInt(conn, @"
                    SELECT COUNT(*) FROM ancestor
                    WHERE fact_id NOT IN (SELECT fact_id FROM fact)
                        OR ancestor_fact_id NOT IN (SELECT fact_id FROM fact)"),
                ReadInt(conn, @"
                    SELECT COUNT(*) FROM signature
                    WHERE fact_id NOT IN (SELECT fact_id FROM fact)"));
        }
        finally
        {
            conn.Close();
        }
    }

    private static int FactIdOf(string path, FactReference reference)
    {
        var conn = new Conn(path, 0);
        try
        {
            return ReadInt(conn, @"
                SELECT f.fact_id
                FROM fact f
                JOIN fact_type t
                    ON t.fact_type_id = f.fact_type_id
                WHERE t.name = ?1
                    AND f.hash = ?2",
                reference.Type, reference.Hash);
        }
        finally
        {
            conn.Close();
        }
    }

    private static int ReadInt(Conn conn, string sql, params object[] parameters)
    {
        var scalar = conn.ExecuteScalar(sql, parameters);
        return string.IsNullOrEmpty(scalar) ? 0 : int.Parse(scalar);
    }

    private static FactReference ReferenceOf(object fact)
    {
        var store = new MemoryStore();
        var loggerFactory = NullLoggerFactory.Instance;
        var networkManager = new NetworkManager(
            new LocalNetwork(),
            store,
            loggerFactory,
            (FactGraph g, ImmutableList<Fact> l, CancellationToken c) => Task.CompletedTask,
            new JinagaClientOptions().MaxBatchSize);
        var factManager = new FactManager(store, networkManager, [], loggerFactory, 0);
        return factManager.Serialize(fact).Last;
    }
}
