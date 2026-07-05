using Jinaga.Extensions;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;

namespace Jinaga.Test.Managers;

/// <summary>
/// Regression test for a review comment on https://github.com/jinaga/jinaga.net/pull/193:
/// when a feed fetch returns zero facts, NetworkManager.ProcessFeed must still save the
/// (possibly advanced) bookmark returned by the network, instead of discarding it. Otherwise
/// a subsequent fetch redundantly re-requests using a stale bookmark.
/// </summary>
public class NetworkManagerBookmarkTest
{
    private static readonly Specification<Company, Office> officesInCompany = Given<Company>.Match((company, facts) =>
        from office in facts.OfType<Office>()
        where office.company == company
        select office
    );

    [Fact]
    public async Task Fetch_WithEmptyResult_SavesAdvancedBookmark()
    {
        var network = new EmptyResultAdvancingBookmarkNetwork();
        var store = new MemoryStore();
        var options = new JinagaClientOptions();
        var j = new JinagaClient(store, network, [], NullLoggerFactory.Instance, options);

        var contoso = new Company("contoso");
        await j.Query(officesInCompany, contoso);

        var bookmark = await store.LoadBookmark("feed");

        bookmark.Should().Be("bookmark-1");
    }
}
