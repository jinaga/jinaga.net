namespace Jinaga.Test.Specifications
{
    // These usings sit inside the namespace so the aliases win over the fact types
    // Jinaga.Test declares in the enclosing namespace.
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Jinaga.Observers;
    using Comment = Jinaga.Test.Model.Comment;
    using Content = Jinaga.Test.Model.Content;
    using Site = Jinaga.Test.Model.Site;

    /// <summary>
    /// A projection record declares a collection as either <c>IQueryable&lt;T&gt;</c> or
    /// <c>IEnumerable&lt;T&gt;</c>. Both spell the same intent — a materialized collection
    /// read once — so a specification the parser accepts must deserialize either way.
    /// </summary>
    public class EnumerableProjectionTest
    {
        private record SiteContentsQueryable(Site site, IQueryable<Content> contents);

        private record SiteContentsEnumerable(Site site, IEnumerable<Content> contents);

        private record ContentWithComments(Content content, IEnumerable<Comment> comments);

        private record SiteContentsNested(Site site, IEnumerable<ContentWithComments> contents);

        private record SiteContentsUntypedQueryable(Site site, IQueryable contents);

        private static readonly Specification<Site, SiteContentsQueryable> contentsAsQueryable =
            Given<Site>.Match((site, facts) =>
                facts.OfType<Site>(s => s == site)
                    .Select(s => new SiteContentsQueryable(
                        s,
                        facts.OfType<Content>(content => content.site == s)
                    ))
            );

        private static readonly Specification<Site, SiteContentsEnumerable> contentsAsEnumerable =
            Given<Site>.Match((site, facts) =>
                facts.OfType<Site>(s => s == site)
                    .Select(s => new SiteContentsEnumerable(
                        s,
                        facts.OfType<Content>(content => content.site == s)
                    ))
            );

        private static readonly Specification<Site, SiteContentsNested> contentsNested =
            Given<Site>.Match((site, facts) =>
                facts.OfType<Site>(s => s == site)
                    .Select(s => new SiteContentsNested(
                        s,
                        facts.OfType<Content>(content => content.site == s)
                            .Select(content => new ContentWithComments(
                                content,
                                facts.OfType<Comment>(comment => comment.content == content)
                            ))
                    ))
            );

        [Fact]
        public async Task Query_FillsAnEnumerableCollection_UnderJinagaTest()
        {
            var j = JinagaTest.Create();
            var site = await GivenSite(j);
            var content = await j.Fact(new Content(site, "/first-post"));

            var results = await j.Query(contentsAsEnumerable, site);

            var result = results.Should().ContainSingle().Subject;
            result.site.Should().BeEquivalentTo(site);
            result.contents.Should().BeEquivalentTo(new[] { content });
        }

        [Fact]
        public async Task Query_FillsAnEnumerableCollection_UnderJinagaClient()
        {
            var j = JinagaClient.Create();
            var site = await GivenSite(j);
            var content = await j.Fact(new Content(site, "/first-post"));

            var results = await j.Query(contentsAsEnumerable, site);

            var result = results.Should().ContainSingle().Subject;
            result.site.Should().BeEquivalentTo(site);
            result.contents.Should().BeEquivalentTo(new[] { content });
        }

        /// <summary>
        /// The two declarations differ only in the collection interface, so whatever the
        /// supported one returns is what the other must return. Both sides are read from a
        /// running query, so neither can be satisfied by editing a literal.
        /// </summary>
        [Fact]
        public async Task Query_GivesAnEnumerableTheSameElementsAsAQueryable()
        {
            var j = JinagaTest.Create();
            var site = await GivenSite(j);
            await j.Fact(new Content(site, "/first-post"));
            await j.Fact(new Content(site, "/second-post"));

            var queryableResults = await j.Query(contentsAsQueryable, site);
            var enumerableResults = await j.Query(contentsAsEnumerable, site);

            var expected = queryableResults.Should().ContainSingle().Subject;
            var actual = enumerableResults.Should().ContainSingle().Subject;
            actual.site.Should().BeEquivalentTo(expected.site);
            actual.contents.Should().BeEquivalentTo(expected.contents.ToList());
        }

        [Fact]
        public async Task Query_FillsAnEnumerableCollectionWithinAnEnumerableCollection()
        {
            var j = JinagaTest.Create();
            var site = await GivenSite(j);
            var content = await j.Fact(new Content(site, "/first-post"));
            var comment = await j.Fact(new Comment(content, Guid.NewGuid(), site.creator));

            var results = await j.Query(contentsNested, site);

            var result = results.Should().ContainSingle().Subject;
            var projectedContent = result.contents.Should().ContainSingle().Subject;
            projectedContent.content.Should().BeEquivalentTo(content);
            projectedContent.comments.Should().BeEquivalentTo(new[] { comment });
        }

        /// <summary>
        /// A watch materializes a projection record the same way a query does, so the
        /// collection the callback receives carries the elements loaded at that point.
        /// </summary>
        [Fact]
        public async Task Watch_GivesAnEnumerableTheSameElementsAsAQueryable()
        {
            var j = JinagaTest.Create();
            var site = await GivenSite(j);
            await j.Fact(new Content(site, "/first-post"));
            await j.Fact(new Content(site, "/second-post"));

            var fromQueryable = ImmutableList<Content>.Empty;
            var queryableObserver = j.Watch(contentsAsQueryable, site, projection =>
            {
                fromQueryable = fromQueryable.AddRange(projection.contents);
            });
            await queryableObserver.Loaded;

            var fromEnumerable = ImmutableList<Content>.Empty;
            var enumerableObserver = j.Watch(contentsAsEnumerable, site, projection =>
            {
                fromEnumerable = fromEnumerable.AddRange(projection.contents);
            });
            await enumerableObserver.Loaded;

            fromEnumerable.Should().BeEquivalentTo(fromQueryable);

            queryableObserver.Stop();
            enumerableObserver.Stop();
        }

        /// <summary>
        /// A collection type no branch handles reports what to change. The supported type
        /// names are read back out of the message rather than quoted into it, so the
        /// assertion still has bite if the message is reworded.
        /// </summary>
        [Fact]
        public async Task Query_ReportsAnUnsupportedCollectionType()
        {
            var j = JinagaTest.Create();
            var site = await GivenSite(j);
            await j.Fact(new Content(site, "/first-post"));

            var untypedQueryable = Given<Site>.Match((s, facts) =>
                facts.OfType<Site>(x => x == s)
                    .Select(x => new SiteContentsUntypedQueryable(
                        x,
                        facts.OfType<Content>(content => content.site == x)
                    ))
            );

            Func<Task> query = () => j.Query(untypedQueryable, site);

            var exception = (await query.Should().ThrowAsync<Exception>()).Which;
            exception.Should().NotBeOfType<NotImplementedException>();
            exception.Message.Should().Contain(nameof(SiteContentsUntypedQueryable));
            exception.Message.Should().Contain(nameof(SiteContentsUntypedQueryable.contents));
            exception.Message.Should().Contain(UnqualifiedName(typeof(IQueryable<>)));
            exception.Message.Should().Contain(UnqualifiedName(typeof(IEnumerable<>)));
            exception.Message.Should().Contain(UnqualifiedName(typeof(IObservableCollection<>)));
        }

        /// <summary>
        /// The name of a generic type without its arity suffix, read from the type itself so
        /// that renaming the interface fails the assertion.
        /// </summary>
        private static string UnqualifiedName(Type genericTypeDefinition)
        {
            var name = genericTypeDefinition.Name;
            var arity = name.IndexOf('`');
            return arity < 0 ? name : name.Substring(0, arity);
        }

        private static async Task<Site> GivenSite(JinagaClient j)
        {
            var creator = await j.Fact(new User("--- PUBLIC KEY ---"));
            return await j.Fact(new Site(creator, "contoso"));
        }
    }
}
