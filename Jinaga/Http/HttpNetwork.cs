﻿using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Http
{
    public class HttpNetwork : INetwork
    {
        private readonly WebClient webClient;

        public event INetwork.AuthenticationStateChanged? OnAuthenticationStateChanged;

        public HttpNetwork(Uri baseUrl, IHttpAuthenticationProvider? authenticationProvider, ILoggerFactory loggerFactory, RetryConfiguration retryConfiguration)
        {
            webClient = new WebClient(new HttpConnection(baseUrl, loggerFactory,
                headers =>
                {
                    if (authenticationProvider != null)
                        authenticationProvider.SetRequestHeaders(headers);
                },
                () => authenticationProvider != null
                    ? authenticationProvider.Reauthenticate()
                    : Task.FromResult(JinagaAuthenticationState.NotAuthenticated),
                authenticationState =>
                {
                    OnAuthenticationStateChanged?.Invoke(authenticationState);
                },
                retryConfiguration
            ));
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

        public async Task<ImmutableList<string>> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            string declarationString = specification.GenerateDeclarationString(givenTuple);
            string specificationString = GenerateSpecificationString(specification);
            string request = $"{declarationString}\n{specificationString}";
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
            webClient.StreamFeed(feed, bookmark, cancellationToken, async (FeedResponse response) => {
                var references = response.references
                    .Select(r => FactReader.ReadFactReference(r))
                    .ToImmutableList();
                await onResponse(references, response.bookmark).ConfigureAwait(false);
            }, onError);
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

        private static string GenerateSpecificationString(Specification specification)
        {
            var specificationWithOnlyCollections = new Specification(
                specification.Givens,
                specification.Matches,
                ProjectionWithOnlyCollections(specification.Projection));
            return specificationWithOnlyCollections.ToDescriptiveString();
        }

        private static Projection ProjectionWithOnlyCollections(Projection projection)
        {
            return MaybeProjectionWithOnlyCollections(projection) ??
                new CompoundProjection(ImmutableDictionary<string, Projection>.Empty, typeof(object));
        }

        private static Projection? MaybeProjectionWithOnlyCollections(Projection projection)
        {
            if (projection is CollectionProjection collectionProjection)
            {
                return new CollectionProjection(
                    collectionProjection.Matches,
                    ProjectionWithOnlyCollections(collectionProjection.Projection),
                    projection.Type
                );
            }
            else if (projection is CompoundProjection compoundProjection)
            {
                var namedProjections = compoundProjection.Names
                    .Select(name => (name, projection: MaybeProjectionWithOnlyCollections(compoundProjection.GetProjection(name))))
                    .Where(p => p.projection != null)
                    .ToImmutableDictionary(
                        p => p.name,
                        p => p.projection!
                    );
                return new CompoundProjection(namedProjections, projection.Type);
            }
            else
            {
                return null;
            }
        }
    }
}
