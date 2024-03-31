namespace Jinaga.Test;

public class JinagaClientTest
{
    [Fact]
    public async Task JinagaClient_SimulatedUser()
    {
        var j = JinagaTest.Create(options =>
        {
            options.User = new User("Simulated User");
        });

        var (user, profile) = await j.Login();
        user.publicKey.Should().Be("Simulated User");
    }
}
