using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Records;
using Jinaga.Services;
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

        public HttpNetwork(Uri baseUrl)
        {
            webClient = new WebClient(new HttpConnection(baseUrl, ""));
        }

        public Task Save(ImmutableList<Fact> facts, CancellationToken cancellationToken)
        {
            var saveRequest = new SaveRequest
            {
                Facts = facts.Select(fact => CreateFactRecord(fact)).ToList()
            };
            return webClient.Save(saveRequest);
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

        public Task<ImmutableList<string>> Feeds(ImmutableList<Facts.FactReference> startReferences, Specification specification, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ImmutableList<Facts.FactReference>> FetchFeed(string feed, ref string bookmark, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<FactGraph> Load(ImmutableList<Facts.FactReference> factReferences, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
