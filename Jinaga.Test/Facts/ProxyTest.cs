using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Serialization;
using Xunit;

namespace Jinaga.Test.Facts;

public class ProxyTest
{
    [Fact]
    public void CanCreateProxy()
    {
        var original = new User("--- FAKE PUBLIC KEY ---");
        var proxy = ProxyGenerator.CreateProxy(original);
        var proxyType = proxy.GetType();
        proxyType.Should().BeAssignableTo<IFactProxy>();
        IFactProxy proxyFactProxy = (IFactProxy)proxy;
        proxyFactProxy.Fact.Reference.Type.Should().Be("TODO");
        proxyFactProxy.Fact.Reference.Hash.Should().Be("fSS1hK7OGAeSX4ocN3acuFF87jvzCdPN3vLFUtcej0lOAsVV859UIYZLRcHUoMbyd/J31TdVn5QuE7094oqUPg==");
    }
}