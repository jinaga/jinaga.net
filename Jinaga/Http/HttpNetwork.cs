using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Records;
using Jinaga.Services;
using System;
using System.Collections.Generic;
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

        public async Task<ImmutableList<string>> Feeds(ImmutableList<Facts.FactReference> givenReferences, Specification specification, CancellationToken cancellationToken)
        {
            string startString = GenerateDeclarationString(specification.Given, givenReferences);
            string specificationString = GenerateSpecificationString(specification);
            var response = await webClient.Feeds(startString + specificationString);
            var feeds = response.Feeds.ToImmutableList();
            return feeds;
        }

        public async Task<(ImmutableList<Facts.FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
        {
            var response = await webClient.Feed(feed, bookmark, cancellationToken);
            bookmark = response.bookmark;
            var references = response.references
                .Select(r => ReadFactReference(r))
                .ToImmutableList();
            return (references, bookmark);
        }

        public async Task<FactGraph> Load(ImmutableList<Facts.FactReference> factReferences, CancellationToken cancellationToken)
        {
            var request = new LoadRequest
            {
                References = factReferences.Select(r => CreateFactReference(r)).ToList()
            };
            LoadResponse response = await webClient.Load(request, cancellationToken);
            var builder = new FactGraphBuilder();
            foreach (var factRecord in response.Facts)
            {
                var fact = ReadFact(factRecord);
                builder.Add(fact);
            }
            return builder.Build();
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

        private static string GenerateDeclarationString(ImmutableList<Label> given, ImmutableList<Facts.FactReference> givenReferences)
        {
            var startStrings = given.Zip(givenReferences, (label, reference) =>
                $"let {label.Name}:{reference.Type}=#{reference.Hash}\n");
            return string.Join("", startStrings);
        }

        private static string GenerateSpecificationString(Specification specification)
        {
            var specificationWithOnlyCollections = new Specification(
                specification.Given,
                specification.Matches,
                ProjectionWithOnlyCollections(specification.Projection));
            return specificationWithOnlyCollections.ToDescriptiveString();
        }

        private static Projection ProjectionWithOnlyCollections(Projection projection)
        {
            return MaybeProjectionWithOnlyCollections(projection) ??
                new CompoundProjection(ImmutableDictionary<string, Projection>.Empty);
        }

        private static Projection? MaybeProjectionWithOnlyCollections(Projection projection)
        {
            if (projection is CollectionProjection collectionProjection)
            {
                return new CollectionProjection(
                    collectionProjection.Matches,
                    ProjectionWithOnlyCollections(collectionProjection.Projection)
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
                return new CompoundProjection(namedProjections);
            }
            else
            {
                return null;
            }
        }

        private static Fact ReadFact(FactRecord fact)
        {
            return Fact.Create(
                fact.Type,
                fact.Fields.Select(field => ReadField(field)).ToImmutableList(),
                fact.Predecessors.Select(predecessor => ReadPredecessor(predecessor)).ToImmutableList()
            );
        }

        private static Field ReadField(KeyValuePair<string, Records.FieldValue> field)
        {
            return new Field(field.Key, ReadFieldValue(field.Value));
        }

        private static Facts.FieldValue ReadFieldValue(Records.FieldValue value)
        {
            if (value is Records.FieldValueString stringValue)
            {
                return new Facts.FieldValueString(stringValue.Value);
            }
            else if (value is Records.FieldValueNumber numberValue)
            {
                return new Facts.FieldValueNumber(numberValue.Value);
            }
            else if (value is Records.FieldValueBoolean booleanValue)
            {
                return new Facts.FieldValueBoolean(booleanValue.Value);
            }
            else
            {
                throw new ArgumentException($"Unknown value type {value.GetType().Name}");
            }
        }

        private static Predecessor ReadPredecessor(KeyValuePair<string, PredecessorSet> pair)
        {
            if (pair.Value is PredecessorSetSingle single)
            {
                return new PredecessorSingle(pair.Key, ReadFactReference(single.Reference));
            }
            else if (pair.Value is PredecessorSetMultiple multiple)
            {
                return new PredecessorMultiple(pair.Key, multiple.References
                    .Select(r => ReadFactReference(r)).ToImmutableList());
            }
            else
            {
                throw new ArgumentException($"Unknown predecessor set type {pair.Value.GetType().Name}");
            }
        }

        private static Facts.FactReference ReadFactReference(Records.FactReference reference)
        {
            return new Facts.FactReference(reference.Type, reference.Hash);
        }
    }
}
