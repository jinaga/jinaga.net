using Jinaga;
using Jinaga.DefaultImplementations;
using Jinaga.Services;
using Jinaga.Store.SQLite;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;

public class ReadyToSyncTest
{
    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "ReadyToSyncTest.db");

    [Fact]
    public async Task NotYetReadyToSync_NoneSent()
    {
        if (File.Exists(SQLitePath))
        {
            File.Delete(SQLitePath);
        }

        var network = GivenLocalNetwork();
        var jinagaClient = GivenJinagaClient(network: network);

        var company = await jinagaClient.Local.Fact(new QmaCompany("Qma"));
        var environment = await jinagaClient.Local.Fact(new AppEnvironment(company, "dev"));
        var id = await jinagaClient.Local.Fact(new DeviceId(environment, "device1"));
        var session = await jinagaClient.Local.Fact(new DeviceSession(id, "session1"));
        var username = await jinagaClient.Local.Fact(new UserName("david.schwartz"));
        var order = await jinagaClient.Local.Fact(new Order(environment, "order1"));
        var orderSourceKey = await jinagaClient.Local.Fact(new OrderSourceKey("sourceKey"));
        var orderDetails = await jinagaClient.Local.Fact(new OrderDetails("order1", orderSourceKey, order));
        var savedOrder = await jinagaClient.Local.Fact(new SavedOrder(session, orderDetails, username, DateTime.Now, new SavedOrder[0]));

        await jinagaClient.Unload();

        // No facts should be uploaded.
        network.SavedFactReferences.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadyToSync_AllSent()
    {
        if (File.Exists(SQLitePath))
        {
            File.Delete(SQLitePath);
        }

        var network = GivenLocalNetwork();
        var jinagaClient = GivenJinagaClient(network: network);

        var company = await jinagaClient.Local.Fact(new QmaCompany("Qma"));
        var environment = await jinagaClient.Local.Fact(new AppEnvironment(company, "dev"));
        var id = await jinagaClient.Local.Fact(new DeviceId(environment, "device1"));
        var session = await jinagaClient.Local.Fact(new DeviceSession(id, "session1"));
        var username = await jinagaClient.Local.Fact(new UserName("david.schwartz"));
        var order = await jinagaClient.Local.Fact(new Order(environment, "order1"));
        var orderSourceKey = await jinagaClient.Local.Fact(new OrderSourceKey("sourceKey"));
        var orderDetails = await jinagaClient.Local.Fact(new OrderDetails("order1", orderSourceKey, order));
        var savedOrder = await jinagaClient.Local.Fact(new SavedOrder(session, orderDetails, username, DateTime.Now, new SavedOrder[0]));
        var orderReadyToSync = await jinagaClient.Fact(new OrderReadyToSync(savedOrder));

        await jinagaClient.Unload();

        // All facts should be uploaded.
        network.SavedFactReferences.Should().HaveCount(10);
        network.SavedFactReferences.Select(r => r.Type).Should().BeEquivalentTo(new[]
        {
            "Qma.Company",
            "Qma.AppEnvironment",
            "Qma.DeviceId",
            "Qma.DeviceSession",
            "Qma.UserName",
            "Qma.Order",
            "Qma.Order.Source.Key",
            "Qma.Order.Details",
            "Qma.Order.Saved",
            "Qma.Order.ReadyToSync"
        });
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