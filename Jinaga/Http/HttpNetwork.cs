﻿using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Http
{
    public class HttpNetwork : INetwork
    {
        private readonly WebClient webClient;
        private readonly IIdentitySource? identitySource;

        public event INetwork.AuthenticationStateChanged? OnAuthenticationStateChanged;

        public HttpNetwork(Uri baseUrl, IHttpAuthenticationProvider? authenticationProvider, ILoggerFactory loggerFactory, RetryConfiguration retryConfiguration)
            : this(null, baseUrl, authenticationProvider, loggerFactory, retryConfiguration)
        {
        }

        /// <summary>
        /// Sends requests through the given message handler, or the network when it is null.
        /// The handler is the seam that lets a test script the replicator's responses.
        /// </summary>
        internal HttpNetwork(HttpMessageHandler? messageHandler, Uri baseUrl, IHttpAuthenticationProvider? authenticationProvider, ILoggerFactory loggerFactory, RetryConfiguration retryConfiguration)
        {
            // The user an upload checks for is read from the object that sets
            // its token, so the two cannot name different users.
            identitySource = authenticationProvider as IIdentitySource;

            Action<HttpRequestHeaders> setRequestHeaders = headers =>
            {
                if (authenticationProvider != null)
                    authenticationProvider.SetRequestHeaders(headers);
            };
            Func<Task<JinagaAuthenticationState>> reauthenticate = () => authenticationProvider != null
                ? authenticationProvider.Reauthenticate()
                : Task.FromResult(JinagaAuthenticationState.NotAuthenticated);
            Action<JinagaAuthenticationState> setAuthenticationState = authenticationState =>
            {
                OnAuthenticationStateChanged?.Invoke(authenticationState);
            };
            webClient = new WebClient(messageHandler == null
                ? new HttpConnection(baseUrl, loggerFactory, setRequestHeaders, reauthenticate, setAuthenticationState, retryConfiguration)
                : new HttpConnection(messageHandler, baseUrl, loggerFactory, setRequestHeaders, reauthenticate, setAuthenticationState, retryConfiguration));
        }

        public async Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken)
        {
            var response = await webClient.Login(cancellationToken).ConfigureAwait(false);
            var userFact = FactReader.ReadFact(response.UserFact);
            var graph = FactGraph.Empty
                .Add(new FactEnvelope(userFact, ImmutableList<FactSignature>.Empty));
            var profile = new UserProfile(response.Profile.DisplayName);
            return (graph, profile);
        }

        public Task Save(FactGraph graph, CancellationToken cancellationToken)
        {
            return webClient.Save(graph);
        }

        /// <summary>
        /// Uploads the facts, checking before every attempt to send them, including a retry
        /// and the attempt after re-authenticating, that <paramref name="uploader"/> is still
        /// the user logged in. A refreshed token for the same user goes through. A different
        /// user, or nobody, stops the upload before anything more is sent.
        /// </summary>
        public Task Save(FactGraph graph, string? uploader, CancellationToken cancellationToken)
        {
            return webClient.Save(graph, () => UploaderChangedException.ThrowIfChanged(identitySource, uploader));
        }

        public async Task<ImmutableList<string>> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            string request = CreateFeedsRequest(givenTuple, specification);
            var response = await webClient.Feeds(request).ConfigureAwait(false);
            var feeds = response.Feeds.ToImmutableList();
            return feeds;
        }

        public async Task<(ImmutableList<Facts.FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
        {
            var response = await webClient.Feed(feed, bookmark, cancellationToken).ConfigureAwait(false);
            bookmark = response.bookmark;
            var references = response.references
                .Select(r => FactReader.ReadFactReference(r))
                .ToImmutableList();
            return (references, bookmark);
        }

        public void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<Facts.FactReference>, string, Task> onResponse, Action<Exception> onError)
        {
            // StreamFeed runs for the lifetime of the stream. It is intentionally not awaited here,
            // but any exception that escapes it is caught and passed to onError so it is not lost
            // as an unobserved task exception. If onError itself throws, that exception is swallowed
            // (after logging via the fallback below is not available here, so it is intentionally
            // dropped) rather than faulting this fire-and-forget task.
            _ = StreamFeedAsync(feed, bookmark, cancellationToken, onResponse, onError);
        }

        private async Task StreamFeedAsync(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<Facts.FactReference>, string, Task> onResponse, Action<Exception> onError)
        {
            try
            {
                await webClient.StreamFeed(feed, bookmark, cancellationToken, async (FeedResponse response) => {
                    var references = response.references
                        .Select(r => FactReader.ReadFactReference(r))
                        .ToImmutableList();
                    await onResponse(references, response.bookmark).ConfigureAwait(false);
                }, onError).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try
                {
                    onError(ex);
                }
                catch
                {
                    // Swallow exceptions from onError itself so they cannot fault this
                    // fire-and-forget task and surface as an unobserved task exception.
                }
            }
        }

        public async Task<FactGraph> Load(ImmutableList<Facts.FactReference> factReferences, CancellationToken cancellationToken)
        {
            var request = new LoadRequest
            {
                References = factReferences.Select(r => CreateFactReference(r)).ToList()
            };
            FactGraph graph = await webClient.Load(request, cancellationToken).ConfigureAwait(false);
            return graph;
        }

        private static Records.FactReference CreateFactReference(Facts.FactReference reference)
        {
            return new Records.FactReference
            {
                Type = reference.Type,
                Hash = reference.Hash
            };
        }

        /// <summary>
        /// The body of the feeds request: the givens, then the reduced specification. The
        /// replicator matches facts and does not project, so a projection that only shapes
        /// the local result never reaches the wire. Which projections those are is
        /// <see cref="Specification.Reduce"/>'s to decide; this method only asks.
        /// </summary>
        internal static string CreateFeedsRequest(FactReferenceTuple givenTuple, Specification specification)
        {
            string declarationString = specification.GenerateDeclarationString(givenTuple);
            string specificationString = GenerateSpecificationString(specification);
            return $"{declarationString}\n{specificationString}";
        }

        private static string GenerateSpecificationString(Specification specification)
        {
            return specification.Reduce().ToDescriptiveString();
        }
    }
}
