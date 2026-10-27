using Jinaga.Facts;
using Jinaga.Observers;
using Jinaga.Projections;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Jinaga.Test.Specifications;

using Content = Jinaga.Test.Model.Content;
using Site = Jinaga.Test.Model.Site;

/// <summary>
/// A specification that walks only predecessors is evaluated from the fact graph by
/// <c>Specification.Execute</c>, and any other by the store. Both build a product that holds
/// every label of the tuple, plus one collection per collection projection, and the
/// deserializer reads the rest of the projection from those labels. These tests hold the
/// two paths to the same products and the same deserialized results.
/// </summary>
public class ProjectionOnGraphTest
{
    private readonly MemoryStore store = new MemoryStore();
    private readonly JinagaClient j;

    public ProjectionOnGraphTest(Xunit.Abstractions.ITestOutputHelper output)
    {
        j = new JinagaClient(
            store,
            new FakeNetwork(output),
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            new JinagaClientOptions());
    }

    [Fact]
    public async Task FieldProjection_PredecessorQueryReturnsTheSameFieldAsTheStorePath()
    {
        var site = await GivenSite();
        var content = await j.Fact(new Content(site, "/a"));

        var fromGraphSpecification = Given<Content>.Match((c, facts) =>
            from s in facts.OfType<Site>()
            where c.site == s
            select new SiteIdentifier(s, s.identifier, j.Hash(s))
        );
        fromGraphSpecification.CanRunOnGraph.Should().BeTrue();

        var fromStoreSpecification = Given<Site>.Match((s, facts) =>
            from c in facts.OfType<Content>()
            where c.site == s
            select new SiteIdentifier(s, s.identifier, j.Hash(s))
        );
        fromStoreSpecification.CanRunOnGraph.Should().BeFalse();

        var fromGraph = await j.Query(fromGraphSpecification, content);
        var fromStore = await j.Query(fromStoreSpecification, site);

        fromGraph.Should().ContainSingle().Which.Should().Be(
            fromStore.Should().ContainSingle().Subject);
        fromGraph.Single().identifier.Should().Be("michaelperry.net");
    }

    [Fact]
    public async Task CollectionProjection_PredecessorQueryReturnsTheCollection()
    {
        var creator = await j.Fact(new User("--- PUBLIC KEY ---"));
        var site = await j.Fact(new Site(creator, "michaelperry.net"));
        var content = await j.Fact(new Content(site, "/a"));

        var creatorsOfSite = Given<Site>.Match((s, facts) =>
            from u in facts.OfType<User>()
            where s.creator == u
            select u
        );
        var specification = Given<Content>.Match((c, facts) =>
            from s in facts.OfType<Site>()
            where c.site == s
            select new SiteCreators(s, facts.Observable(s, creatorsOfSite))
        );
        specification.CanRunOnGraph.Should().BeTrue();

        var results = await j.Query(specification, content);

        var result = results.Should().ContainSingle().Subject;
        result.site.Should().Be(site);
        result.creators.Should().ContainSingle().Which.Should().Be(creator);
    }

    [Fact]
    public async Task AliasedProjection_PredecessorQueryReturnsTheSameResultAsTheStorePath()
    {
        var site = await GivenSite();
        var content = await j.Fact(new Content(site, "/a"));

        // Each name in the projection is the other label's name, so a product keyed by
        // projection name rather than by label would hand each property the wrong fact.
        var fromGraphSpecification = Given<Content>.Match((c, facts) =>
            from s in facts.OfType<Site>()
            where c.site == s
            select new { c = s, s = c, p = s }
        );
        fromGraphSpecification.CanRunOnGraph.Should().BeTrue();

        var fromStoreSpecification = Given<Site>.Match((s, facts) =>
            from c in facts.OfType<Content>()
            where c.site == s
            select new { c = s, s = c, p = s }
        );
        fromStoreSpecification.CanRunOnGraph.Should().BeFalse();

        var fromGraph = await j.Query(fromGraphSpecification, content);
        var fromStore = await j.Query(fromStoreSpecification, site);

        var result = fromGraph.Should().ContainSingle().Subject;
        result.Should().Be(fromStore.Should().ContainSingle().Subject);
        result.c.Should().Be(site);
        result.s.Should().Be(content);
        result.p.Should().Be(site);
    }

    [Fact]
    public async Task CompoundProjection_GraphBuildsTheSameProductsAsTheStore()
    {
        var site = await GivenSite();
        var content = await j.Fact(new Content(site, "/a"));

        var creatorsOfSite = Given<Site>.Match((s, facts) =>
            from u in facts.OfType<User>()
            where s.creator == u
            select u
        );
        // Every kind of child a compound can hold that the graph carries, under names
        // that are not the labels they read.
        var specification = Given<Content>.Match((c, facts) =>
            from s in facts.OfType<Site>()
            where c.site == s
            select new
            {
                p = s,
                identifier = s.identifier,
                hash = j.Hash(s),
                creators = facts.Observable(s, creatorsOfSite)
            }
        );
        specification.CanRunOnGraph.Should().BeTrue();

        var graph = j.Graph(content);
        var givenTuple = FactReferenceTuple.Empty
            .Add(specification.Givens.Single().Label.Name, graph.Last);

        var fromGraph = specification.Execute(givenTuple, graph);
        var fromStore = await store.Read(givenTuple, specification, CancellationToken.None);

        // Compared element by element: CollectionElement.GetHashCode is by reference, so
        // Product.Equals does not hold across two products that each carry a collection.
        var graphProduct = fromGraph.Should().ContainSingle().Subject;
        var storeProduct = fromStore.Should().ContainSingle().Subject;
        graphProduct.Names.Should().BeEquivalentTo(storeProduct.Names);
        foreach (var name in storeProduct.Names)
        {
            graphProduct.GetElement(name).Should().Be(storeProduct.GetElement(name), because: name);
        }
    }

    private async Task<Site> GivenSite()
    {
        var creator = await j.Fact(new User("--- PUBLIC KEY ---"));
        return await j.Fact(new Site(creator, "michaelperry.net"));
    }

    record SiteIdentifier(Site site, string identifier, string hash);

    record SiteCreators(Site site, IObservableCollection<User> creators);
}
