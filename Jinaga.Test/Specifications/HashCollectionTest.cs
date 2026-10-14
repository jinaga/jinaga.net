namespace Jinaga.Test.Specifications
{
    // These usings sit inside the namespace so the Site alias wins over
    // Jinaga.Test.Site, which the enclosing namespace would otherwise supply.
    using System.Collections.Immutable;
    using System.Linq;
    using Jinaga.Observers;
    using Content = Jinaga.Test.Model.Content;
    using Site = Jinaga.Test.Model.Site;

    public class HashCollectionTest
    {
        private readonly JinagaClient j;

        public HashCollectionTest()
        {
            j = JinagaTest.Create();
        }

        private record SiteContentHashes(Site site, IQueryable<string> hashes);

        private class SiteContentHashesObservable
        {
            public Site Site { get; set; }
            public IObservableCollection<string> Hashes { get; set; }
        }

        [Fact]
        public async Task CanQueryForCollectionOfHashes()
        {
            var creator = new User("--- PUBLIC KEY ---");
            var site = await j.Fact(new Site(creator, "my-blog"));
            var first = await j.Fact(new Content(site, "/first-post"));
            var second = await j.Fact(new Content(site, "/second-post"));

            var specification = Given<User>.Match((u, facts) =>
                from s in facts.OfType<Site>()
                where s.creator == u
                select new SiteContentHashes(s,
                    facts.OfType<Content>().Where(c => c.site == s).Select(c => j.Hash(c)))
            );

            var results = await j.Query(specification, creator);

            var result = results.Should().ContainSingle().Subject;
            result.site.Should().BeEquivalentTo(site);
            result.hashes.Should().BeEquivalentTo(new[]
            {
                j.Hash(first),
                j.Hash(second)
            });
        }

        [Fact]
        public async Task CanWatchForCollectionOfHashes()
        {
            var creator = new User("--- PUBLIC KEY ---");
            var site = await j.Fact(new Site(creator, "my-blog"));
            var first = await j.Fact(new Content(site, "/first-post"));

            var specification = Given<User>.Match((u, facts) =>
                from s in facts.OfType<Site>()
                where s.creator == u
                select new SiteContentHashesObservable
                {
                    Site = s,
                    Hashes = facts.Observable(
                        from c in facts.OfType<Content>()
                        where c.site == s
                        select j.Hash(c)
                    )
                }
            );

            var hashes = ImmutableList<string>.Empty;
            var observer = j.Watch(specification, creator, projection =>
            {
                projection.Hashes.OnAdded(hash =>
                {
                    hashes = hashes.Add(hash);
                    return Task.CompletedTask;
                });
            });
            await observer.Loaded;

            hashes.Should().BeEquivalentTo(new[] { j.Hash(first) });

            var second = await j.Fact(new Content(site, "/second-post"));

            hashes.Should().BeEquivalentTo(new[]
            {
                j.Hash(first),
                j.Hash(second)
            });

            observer.Stop();
        }
    }
}
