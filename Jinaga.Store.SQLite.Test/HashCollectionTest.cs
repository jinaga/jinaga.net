using Jinaga.DefaultImplementations;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;

namespace Jinaga.Store.SQLite.Test;

public class HashCollectionTest
{
    private readonly JinagaClient j;

    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "HashCollectionTest.db");

    public HashCollectionTest()
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        var options = new JinagaClientOptions();
        j = new JinagaClient(new SQLiteStore(SQLitePath, NullLoggerFactory.Instance), new LocalNetwork(), [], NullLoggerFactory.Instance, options);
    }

    private record PostCommentHashes(Post post, IQueryable<string> hashes);

    [Fact]
    public async Task CanQueryForCollectionOfHashes()
    {
        var site = await j.Fact(new Site("michaelperry.net"));
        var post = await j.Fact(new Post(site, "2026-10-14T00:00:00Z"));
        var author = await j.Fact(new User("--- COMMENTER ---"));
        var first = await j.Fact(new Comment(post, author, "First!"));
        var second = await j.Fact(new Comment(post, author, "Second!"));

        var specification = Given<Site>.Match((s, facts) =>
            from p in facts.OfType<Post>()
            where p.site == s
            select new PostCommentHashes(p,
                facts.OfType<Comment>().Where(c => c.post == p).Select(c => j.Hash(c)))
        );

        var results = await j.Query(specification, site);

        var result = results.Should().ContainSingle().Subject;
        result.post.Should().BeEquivalentTo(post);
        result.hashes.Should().BeEquivalentTo(new[]
        {
            j.Hash(first),
            j.Hash(second)
        });

        await j.Unload();
    }
}
