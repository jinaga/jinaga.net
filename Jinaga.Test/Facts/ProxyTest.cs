using System.Linq;
using System.Reflection;
using FluentAssertions;
using Jinaga.Facts;
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
        var proxyProperties = proxyType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        proxyProperties.Should().HaveCount(2);
        var proxyReference = proxyProperties.Single(p => p.Name == nameof(IFactProxy.Reference));
        proxyReference.PropertyType.Should().BeAssignableTo<FactReference>();
        var proxyReferenceValue = proxyReference.GetValue(proxy);
        proxyReferenceValue.Should().BeAssignableTo<FactReference>();
        var proxyReferenceFactReference = (FactReference)proxyReferenceValue;
        proxyReferenceFactReference.Type.Should().Be("TODO");
        proxyReferenceFactReference.Hash.Should().Be("hash");
    }
}