namespace Jinaga.Test.Users;
public class SingleUsePrincipalTest
{
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
}
