using Microsoft.Extensions.Logging.Abstractions;

using Jinaga.DefaultImplementations;
using Jinaga.Facts;
using Jinaga.Services;
using Jinaga.Store.SQLite.Test.Models;

namespace Jinaga.Store.SQLite.Test;

public class SaveLocallyTest
{
    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "SaveLocallyTest.db");

    [Fact]
    public async Task SaveNormallyUploadsFact()
    {
        // Delete the database file if present.
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        // Write a fact to the store.
        var sqliteStore = GivenSQLiteStore();
        var network = GivenLocalNetwork();
        var jinagaClient = GivenJinagaClient(sqliteStore, network);
        await jinagaClient.Fact(new Airline("IA"));

        // Verify that the fact was sent.
        await jinagaClient.Unload();
        network.SavedFactReferences.Should().ContainSingle().Which
            .Type.Should().Be("Skylane.Airline");
    }

    [Fact]
    public async Task SaveLocallyDoesNotUploadFact()
    {
        // Delete the database file if present.
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        // Write a fact to the store.
        var sqliteStore = GivenSQLiteStore();
        var network = GivenLocalNetwork();
        var jinagaClient = GivenJinagaClient(sqliteStore, network);
        await jinagaClient.Local.Fact(new Airline("IA"));

        // Verify that the fact was not sent.
        await jinagaClient.Unload();
        network.SavedFactReferences.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveLocallyFollowedByPushDoesNotUploadFact()
    {
        // Delete the database file if present.
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        // Write a fact to the store.
        var sqliteStore = GivenSQLiteStore();
        var network = GivenLocalNetwork();
        var jinagaClient = GivenJinagaClient(sqliteStore, network);
        await jinagaClient.Local.Fact(new Airline("IA"));
        await jinagaClient.Push();

        // Verify that the fact was not sent.
        await jinagaClient.Unload();
        network.SavedFactReferences.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveLocallyFollowedBySaveSuccessorDoesUploadFact()
    {
        // Delete the database file if present.
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        // Write a fact to the store.
        var sqliteStore = GivenSQLiteStore();
        var network = GivenLocalNetwork();
        var jinagaClient = GivenJinagaClient(sqliteStore, network);
        var airline = await jinagaClient.Local.Fact(new Airline("IA"));
        await jinagaClient.Fact(new AirlineDay(airline, DateTime.Parse("2021-07-04").Date));

        // Verify that both facts were sent.
        await jinagaClient.Unload();
        network.SavedFactReferences.Count.Should().Be(2);
        network.SavedFactReferences.Should().Contain(r => r.Type == "Skylane.Airline");
        network.SavedFactReferences.Should().Contain(r => r.Type == "Skylane.Airline.Day");
    }

    private static JinagaClient GivenJinagaClient(IStore? store = null, INetwork? network = null)
    {
        return new JinagaClient(store ?? new SQLiteStore(SQLitePath, NullLoggerFactory.Instance), network ?? new LocalNetwork(), NullLoggerFactory.Instance);
    }

    private static SQLiteStore GivenSQLiteStore()
    {
        return new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
    }

    private static LocalNetwork GivenLocalNetwork()
    {
        return new LocalNetwork();
    }
}