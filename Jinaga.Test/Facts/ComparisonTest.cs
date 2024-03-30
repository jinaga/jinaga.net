namespace Jinaga.Test.Facts;

public class ComparisonTest
{
    [Fact]
    public void CanCompareSimilarUserFacts()
    {
        var a = new User("--- FAKE PUBLIC KEY ---");
        var b = new User("--- FAKE PUBLIC KEY ---");

        a.Should().Be(b);
        a.Should().BeEquivalentTo(b);
        a.Should().NotBeSameAs(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void CanCompareDifferentUserFacts()
    {
        var a = new User("--- FAKE PUBLIC KEY ---");
        var b = new User("--- DIFFERENT FAKE PUBLIC KEY ---");

        a.Should().NotBe(b);
        a.Should().NotBeEquivalentTo(b);
        a.Should().NotBeSameAs(b);
        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }

    [Fact]
    public async Task CanCompareUserFactToProxy()
    {
        var jinagaClient = JinagaTest.Create();
        var user = new User("--- FAKE PUBLIC KEY ---");
        var proxy = await jinagaClient.Fact(user);

        user.Should().Be(proxy);
        user.Should().BeEquivalentTo(proxy);
        user.Should().NotBeSameAs(proxy);
        (user == proxy).Should().BeTrue();
        (user != proxy).Should().BeFalse();
        user.GetHashCode().Should().Be(proxy.GetHashCode());
    }

    [Fact]
    public async Task CanCompareProxyToUserFact()
    {
        var jinagaClient = JinagaTest.Create();
        var user = new User("--- FAKE PUBLIC KEY ---");
        var proxy = await jinagaClient.Fact(user);

        proxy.Should().Be(user);
        proxy.Should().BeEquivalentTo(user);
        proxy.Should().NotBeSameAs(user);
        (proxy == user).Should().BeTrue();
        (proxy != user).Should().BeFalse();
        proxy.GetHashCode().Should().Be(user.GetHashCode());
    }

    [Fact]
    public async Task CanCompareProxyToProxy()
    {
        var jinagaClient = JinagaTest.Create();
        var user = new User("--- FAKE PUBLIC KEY ---");
        var proxy = await jinagaClient.Fact(user);
        var proxy2 = await jinagaClient.Fact(user);

        proxy.Should().Be(proxy2);
        proxy.Should().BeEquivalentTo(proxy2);
        proxy.Should().NotBeSameAs(proxy2);
        (proxy == proxy2).Should().BeTrue();
        (proxy != proxy2).Should().BeFalse();
        proxy.GetHashCode().Should().Be(proxy2.GetHashCode());
    }

    [Fact]
    public async Task CanCompareProxyToDifferentUserFact()
    {
        var jinagaClient = JinagaTest.Create();
        var user = new User("--- FAKE PUBLIC KEY ---");
        var proxy = await jinagaClient.Fact(user);
        var user2 = new User("--- DIFFERENT FAKE PUBLIC KEY ---");

        proxy.Should().NotBe(user2);
        proxy.Should().NotBeEquivalentTo(user2);
        proxy.Should().NotBeSameAs(user2);
        (proxy == user2).Should().BeFalse();
        (proxy != user2).Should().BeTrue();
        proxy.GetHashCode().Should().NotBe(user2.GetHashCode());
    }

    [Fact]
    public async Task CanCompareDifferentUserFactToProxy()
    {
        var jinagaClient = JinagaTest.Create();
        var user = new User("--- FAKE PUBLIC KEY ---");
        var proxy = await jinagaClient.Fact(user);
        var user2 = new User("--- DIFFERENT FAKE PUBLIC KEY ---");

        user2.Should().NotBe(proxy);
        user2.Should().NotBeEquivalentTo(proxy);
        user2.Should().NotBeSameAs(proxy);
        (user2 == proxy).Should().BeFalse();
        (user2 != proxy).Should().BeTrue();
        user2.GetHashCode().Should().NotBe(proxy.GetHashCode());
    }

    [Fact]
    public async Task CanCompareRecordFactToProxy()
    {
        var jinagaClient = JinagaTest.Create();
        var blog = new Blog("example.com");
        var proxy = await jinagaClient.Fact(new Blog("example.com"));

        blog.Should().Be(proxy);
        blog.Should().BeEquivalentTo(proxy);
        blog.Should().NotBeSameAs(proxy);
        (blog == proxy).Should().BeTrue();
        (blog != proxy).Should().BeFalse();
        blog.GetHashCode().Should().Be(proxy.GetHashCode());
    }

    [Fact]
    public async Task CanCompareProxyToRecordFact()
    {
        var jinagaClient = JinagaTest.Create();
        var blog = new Blog("example.com");
        var proxy = await jinagaClient.Fact(new Blog("example.com"));

        proxy.Should().Be(blog);
        proxy.Should().BeEquivalentTo(blog);
        proxy.Should().NotBeSameAs(blog);
        (proxy == blog).Should().BeTrue();
        (proxy != blog).Should().BeFalse();
        proxy.GetHashCode().Should().Be(blog.GetHashCode());
    }

    [Fact]
    public async Task CanCompareProxyToProxyRecordFact()
    {
        var jinagaClient = JinagaTest.Create();
        var blog = new Blog("example.com");
        var proxy = await jinagaClient.Fact(new Blog("example.com"));
        var proxy2 = await jinagaClient.Fact(new Blog("example.com"));

        proxy.Should().Be(proxy2);
        proxy.Should().BeEquivalentTo(proxy2);
        proxy.Should().NotBeSameAs(proxy2);
        (proxy == proxy2).Should().BeTrue();
        (proxy != proxy2).Should().BeFalse();
        proxy.GetHashCode().Should().Be(proxy2.GetHashCode());
    }
}

[FactType("Blog")]
public record Blog(string domain) {}