using Jinaga.DefaultImplementations;
using Jinaga.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jinaga.Store.SQLite.Test.Users;
public class SingleUsePrincipalTest
{
    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "SingleUsePrincipalTest.db");

    [Fact]
    public async Task ShouldCreateSingleUsePrincipal()
    {
        if (File.Exists(SQLitePath))
        {
            File.Delete(SQLitePath);
        }

        var jinagaClient = GivenJinagaClient();
        await jinagaClient.SingleUse(principal =>
        {
            principal.publicKey.Should().StartWith("-----BEGIN PUBLIC KEY-----");
            return Task.FromResult(0);
        });
    }

    [Fact]
    public async Task ShouldSignFactsCreatedBySingleUsePrincipal()
    {
        if (File.Exists(SQLitePath))
        {
            File.Delete(SQLitePath);
        }

        var localNetwork = GivenLocalNetwork();
        var jinagaClient = GivenJinagaClient(network: localNetwork);
        var publicKey = await jinagaClient.SingleUse(async principal =>
        {
            await jinagaClient.Fact(new EnvironmentFact(principal, "Production"));
            return principal.publicKey;
        });

        await jinagaClient.Unload();

        var uploadedEnvironment = localNetwork.SavedFactReferences
            .Where(factReference => factReference.Type == "Enterprise.Environment")
            .Select(localNetwork.UploadedGraph.GetFact)
            .Should().ContainSingle().Subject;

        var environmentSignature = localNetwork.UploadedGraph.GetSignatures(uploadedEnvironment.Reference)
            .Should().ContainSingle().Subject;
        
        environmentSignature.PublicKey.Should().Be(publicKey);
    }

    private static JinagaClient GivenJinagaClient(IStore? store = null, INetwork? network = null)
    {
        return new JinagaClient(store ?? new SQLiteStore(SQLitePath, NullLoggerFactory.Instance), network ?? new LocalNetwork(), PurgeConditions.Empty, NullLoggerFactory.Instance);
    }

    private static LocalNetwork GivenLocalNetwork()
    {
        return new LocalNetwork();
    }
}

[FactType("Enterprise.Environment")]
internal record EnvironmentFact(User creator, string identifier);
