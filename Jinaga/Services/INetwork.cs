using Jinaga.Facts;
using Jinaga.Projections;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Services
{
    public interface INetwork
    {
        delegate void AuthenticationStateChanged(JinagaAuthenticationState state);
        event AuthenticationStateChanged? OnAuthenticationStateChanged;

        Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken);

        /// <summary>
        /// Declares a specification to the replicator and reads back the feeds it resolves to.
        /// </summary>
        /// <remarks>
        /// The decisions are the replicator's own, one per feed it considered, and they carry
        /// the distinctions the feed list cannot: a feed the distribution policy denied is not
        /// a feed with no facts. An implementation that answers from its own store rather than
        /// from a replicator reports every feed it returns as
        /// <see cref="DistributionDecision.Authorized"/>.
        /// </remarks>
        /// <param name="givenTuple">The starting points of the specification</param>
        /// <param name="specification">The specification to declare</param>
        /// <param name="cancellationToken">To cancel the operation</param>
        /// <returns>The feeds to fetch, and what the replicator decided about each one it considered</returns>
        Task<(ImmutableList<string> feeds, ImmutableList<FeedDecision> decisions)> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken);
        Task<(ImmutableList<Facts.FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken);
        void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError);
        Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken);
        Task Save(FactGraph graph, CancellationToken cancellationToken);

        /// <summary>
        /// Uploads the facts under the given user.
        ///
        /// The client chose these facts because <paramref name="uploader"/> was logged in.
        /// An implementation that can tell who is logged in checks, before every attempt to
        /// send, including a retry, that the same user still is, and sends nothing if not.
        /// By default it sends the graph without checking.
        /// </summary>
        /// <param name="graph">The facts to upload, with their ancestors</param>
        /// <param name="uploader">Public key of the user the facts are sent under, or null if the client cannot tell</param>
        /// <param name="cancellationToken">To cancel the operation</param>
        Task Save(FactGraph graph, string? uploader, CancellationToken cancellationToken)
            => Save(graph, cancellationToken);
    }
}
