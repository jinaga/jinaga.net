using System.Linq;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Jinaga.Test.Users;
public class SingleUsePrincipalTest
{
    private ITestOutputHelper output;

    public SingleUsePrincipalTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task ShouldCreateSingleUsePrincipal()
    {
        var jinagaClient = JinagaTest.Create();
        await jinagaClient.SingleUse(principal =>
        {
            principal.publicKey.Should().StartWith("-----BEGIN PUBLIC KEY-----");
            return Task.FromResult(0);
        });
    }

    [Fact]
    public async Task ShouldSignFactsCreatedBySingleUsePrincipal()
    {
        var fakeNetwork = GivenFakeNetwork();
        var jinagaClient = GivenJinagaClient(fakeNetwork);
        var publicKey = await jinagaClient.SingleUse(async principal =>
        {
            await jinagaClient.Fact(new Environment(principal, "Production"));
            return principal.publicKey;
        });

        var uploadedEnvironment = fakeNetwork.UploadedGraph.FactReferences
            .Where(factReference => factReference.Type == "Enterprise.Environment")
            .Select(fakeNetwork.UploadedGraph.GetFact)
            .Should().ContainSingle().Subject;

        var environmentSignature = fakeNetwork.UploadedGraph.GetSignatures(uploadedEnvironment.Reference)
            .Should().ContainSingle().Subject;
        
        environmentSignature.PublicKey.Should().Be(publicKey);
    }

    private FakeNetwork GivenFakeNetwork()
    {
        return new FakeNetwork(output);
    }

    private JinagaClient GivenJinagaClient(FakeNetwork fakeNetwork)
    {
        return new JinagaClient(new MemoryStore(), fakeNetwork, PurgeConditions.Empty, NullLoggerFactory.Instance);
    }
}

[FactType("Enterprise.Environment")]
internal record Environment(User creator, string identifier);
