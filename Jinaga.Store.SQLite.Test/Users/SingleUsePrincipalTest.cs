using Jinaga.DefaultImplementations;
using Jinaga.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jinaga.Store.SQLite.Test.Users;
public class SingleUsePrincipalTest : SQLiteTest
{
    [Fact]
    public async Task ShouldCreateSingleUsePrincipal()
    {
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
        var localNetwork = GivenLocalNetwork();
        var jinagaClient = GivenJinagaClient(network: localNetwork);
        var publicKey = await jinagaClient.SingleUse(async principal =>
        {
            await jinagaClient.Fact(new EnvironmentFact(principal, "Production"));
            return principal.publicKey;
        });

        await jinagaClient.DisposeAsync();

        var uploadedEnvironment = localNetwork.SavedFactReferences
            .Where(factReference => factReference.Type == "Enterprise.Environment")
            .Select(localNetwork.UploadedGraph.GetFact)
            .Should().ContainSingle().Subject;

        var environmentSignature = localNetwork.UploadedGraph.GetSignatures(uploadedEnvironment.Reference)
            .Should().ContainSingle().Subject;
        
        environmentSignature.PublicKey.Should().Be(publicKey);
    }

    private JinagaClient GivenJinagaClient(IStore? store = null, INetwork? network = null)
    {
        var options = new JinagaClientOptions();
        return Track(new JinagaClient(store ?? new SQLiteStore(DatabasePath, NullLoggerFactory.Instance), network ?? new LocalNetwork(), [], NullLoggerFactory.Instance, options));
    }

    private static LocalNetwork GivenLocalNetwork()
    {
        return new LocalNetwork();
    }
}

[FactType("Enterprise.Environment")]
internal record EnvironmentFact(User creator, string identifier);
