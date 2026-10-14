#nullable enable

using Jinaga.Authorization;
using Jinaga.Facts;
using Jinaga.Serialization;
using Jinaga.Storage;
using Jinaga.Test.Managers;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Jinaga.Test;

/// <summary>
/// A replicator keeps a signature only for a key the rules name, and an Any rule
/// names none. These are the two questions the client asks of the same rules: may
/// a fact travel under anyone, and whose key would it be signed with.
/// </summary>
public class AuthorizationEngineSigningTest
{
    private static readonly User Creator = new User("---FAKE PUBLIC KEY CREATOR---");
    private static readonly User Stranger = new User("---FAKE PUBLIC KEY STRANGER---");

    [Fact]
    public async Task AdmitsWithoutSignature_UnderAnAnyRule_IsTrue()
    {
        var engine = new AuthorizationEngine(RoutingModel.Rules, new MemoryStore());
        var (graph, user) = Serialize(Creator);

        var admitted = await engine.AdmitsWithoutSignature(graph, user, CancellationToken.None);

        admitted.Should().BeTrue();
    }

    [Fact]
    public async Task AdmitsWithoutSignature_UnderASpecificationRule_IsFalse()
    {
        var engine = new AuthorizationEngine(RoutingModel.Rules, new MemoryStore());
        var (graph, project) = Serialize(new RoutingProject(Creator, Guid.NewGuid()));

        var admitted = await engine.AdmitsWithoutSignature(graph, project, CancellationToken.None);

        admitted.Should().BeFalse();
    }

    [Fact]
    public async Task AdmitsWithoutSignature_ForATypeWithNoRule_IsFalse()
    {
        var engine = new AuthorizationEngine(RoutingModel.Rules, new MemoryStore());
        var (graph, school) = Serialize(new Model.School(Guid.NewGuid()));

        var admitted = await engine.AdmitsWithoutSignature(graph, school, CancellationToken.None);

        admitted.Should().BeFalse();
    }

    [Fact]
    public async Task WouldSignWith_TheUserTheRuleNames_IsTrue()
    {
        var engine = new AuthorizationEngine(RoutingModel.Rules, new MemoryStore());
        var (graph, project) = Serialize(new RoutingProject(Creator, Guid.NewGuid()));
        var (_, creator) = Serialize(Creator);

        var signed = await engine.WouldSignWith(graph, project, creator, CancellationToken.None);

        signed.Should().BeTrue();
    }

    [Fact]
    public async Task WouldSignWith_AnotherUser_IsFalse()
    {
        var engine = new AuthorizationEngine(RoutingModel.Rules, new MemoryStore());
        var (graph, project) = Serialize(new RoutingProject(Creator, Guid.NewGuid()));
        var (_, stranger) = Serialize(Stranger);

        var signed = await engine.WouldSignWith(graph, project, stranger, CancellationToken.None);

        signed.Should().BeFalse();
    }

    [Fact]
    public async Task WouldSignWith_AFactAnAnyRuleAdmits_IsFalseEvenForItsOwnUser()
    {
        var engine = new AuthorizationEngine(RoutingModel.Rules, new MemoryStore());
        var (graph, creator) = Serialize(Creator);

        var signed = await engine.WouldSignWith(graph, creator, creator, CancellationToken.None);

        signed.Should().BeFalse();
    }

    private static (FactGraph graph, FactReference reference) Serialize(object fact)
    {
        var collector = new Collector(SerializerCache.Empty, new ConditionalWeakTable<object, FactGraph>());
        collector.Serialize(fact);
        return (collector.Graph, collector.Graph.Last);
    }
}
