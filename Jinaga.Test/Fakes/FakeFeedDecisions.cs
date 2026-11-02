using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Test.Fakes;

/// <summary>
/// A fake network has no distribution policy to deny anything with, so it reports every
/// feed it returns as authorized. Deriving the decisions from the feeds keeps that true of
/// whatever the fake returns, rather than restating a list beside it.
/// </summary>
internal static class FakeFeedDecisions
{
    public static (ImmutableList<string> feeds, ImmutableList<FeedDecision> decisions) AuthorizeAll(ImmutableList<string> feeds)
    {
        var decisions = feeds
            .Select(feed => new FeedDecision
            {
                Feed = feed,
                Decision = DistributionDecision.Authorized,
                Reason = "Fake network."
            })
            .ToImmutableList();
        return (feeds, decisions);
    }
}
