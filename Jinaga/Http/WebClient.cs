using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Records;

namespace Jinaga.Http
{
    public class WebClient
    {
        private readonly IHttpConnection httpConnection;

        private ImmutableList<string>? saveContentTypes;

        public WebClient(IHttpConnection httpConnection)
        {
            this.httpConnection = httpConnection;
        }

        public Task<LoginResponse> Login(CancellationToken cancellationToken)
        {
            return httpConnection.Get<LoginResponse>("login");
        }
        
        public async Task Save(FactGraph graph)
        {
            // Cache the accepted content types for the save endpoint.
            if (saveContentTypes == null)
            {
                saveContentTypes = await httpConnection.GetAcceptedContentTypes("save");
            }

            if (saveContentTypes.Contains("application/x-jinaga-graph-v1"))
            {
                await httpConnection.PostGraph("save", graph);
            }
            else
            {
                var saveRequest = new SaveRequest
                {
                    Facts = graph.FactReferences.Select(reference =>
                        CreateFactRecord(graph.GetFact(reference))
                    ).ToList()
                };
                await httpConnection.PostJson("save", saveRequest);
            }
        }

        public Task<FeedsResponse> Feeds(string request)
        {
            return httpConnection.PostStringExpectingJson<FeedsResponse>("feeds", request);
        }

        public Task<FeedResponse> Feed(string feed, string bookmark, CancellationToken cancellationToken)
        {
            string queryString = bookmark == null ? "" : $"?b={bookmark}";
            return httpConnection.Get<FeedResponse>($"feeds/{feed}{queryString}");
        }

        public async void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<FeedResponse, Task> onResponse, Action<Exception> onError)
        {
            string queryString = bookmark == null ? "" : $"?b={bookmark}";

            try
            {
                await httpConnection.GetStream<FeedResponse>($"feeds/{feed}{queryString}", onResponse, onError, cancellationToken);
            }
            catch (Exception ex)
            {
                onError(ex);
            }
        }

        public Task<FactGraph> Load(LoadRequest request, CancellationToken cancellationToken)
        {
            return httpConnection.PostLoad("load", request);
        }

        private static FactRecord CreateFactRecord(Fact fact)
        {
            var record = new FactRecord
            {
                Type = fact.Reference.Type,
                Hash = fact.Reference.Hash,
                Fields = fact.Fields.ToDictionary(field => field.Name, field => CreateFieldValue(field.Value)),
                Predecessors = fact.Predecessors.ToDictionary(predecessor => predecessor.Role, predecessor => CreatePredecessorSet(predecessor))
            };
            return record;
        }

        private static Records.FieldValue CreateFieldValue(Facts.FieldValue value)
        {
            if (value is Facts.FieldValueString fieldValueString)
            {
                return Records.FieldValue.From(fieldValueString.StringValue);
            }
            else if (value is Facts.FieldValueNumber fieldValueNumber)
            {
                return Records.FieldValue.From(fieldValueNumber.DoubleValue);
            }
            else if (value is Facts.FieldValueBoolean fieldValueBoolean)
            {
                return Records.FieldValue.From(fieldValueBoolean.BoolValue);
            }
            else if (value is Facts.FieldValueNull)
            {
                return Records.FieldValue.Null;
            }
            else
            {
                throw new ArgumentException($"Unknown field value type: {value.GetType().Name}");
            }
        }

        private static PredecessorSet CreatePredecessorSet(Predecessor predecessor)
        {
            if (predecessor is PredecessorSingle predecessorSingle)
            {
                return new PredecessorSetSingle
                {
                    Reference = CreateFactReference(predecessorSingle.Reference)
                };
            }
            else if (predecessor is PredecessorMultiple predecessorMultiple)
            {
                return new PredecessorSetMultiple
                {
                    References = predecessorMultiple.References.Select(reference => CreateFactReference(reference)).ToList()
                };
            }
            else
            {
                throw new ArgumentException($"Unknown predecessor type: {predecessor.GetType().Name}");
            }
        }

        private static Records.FactReference CreateFactReference(Facts.FactReference reference)
        {
            return new Records.FactReference
            {
                Type = reference.Type,
                Hash = reference.Hash
            };
        }
    }
}
