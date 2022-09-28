﻿using Jinaga.Facts;
using Jinaga.Pipelines;
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

        public async Task<ImmutableList<string>> Feeds(ImmutableList<Facts.FactReference> startReferences, Specification specification, CancellationToken cancellationToken)
        {
            string startString = GenerateStartString(specification.Given, startReferences);
            string specificationString = GenerateSpecificationString(specification);
            var response = await webClient.Feeds(startString + specificationString);
            var feeds = response.Feeds.ToImmutableList();
            return feeds;
        }

        public Task<ImmutableList<Facts.FactReference>> FetchFeed(string feed, ref string bookmark, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<FactGraph> Load(ImmutableList<Facts.FactReference> factReferences, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
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

        private string GenerateStartString(ImmutableList<Label> given, ImmutableList<Facts.FactReference> startReferences)
        {
            var startStrings = given.Zip(startReferences, (label, reference) =>
                $"{label.Name}=#{reference.Hash}\n");
            return string.Join("", startStrings);
        }

        private string GenerateSpecificationString(Specification specification)
        {
            var specificationWithOnlyCollections = new Specification(
                specification.Given,
                specification.Matches,
                ProjectionWithOnlyCollections(specification.Projection));
            return specificationWithOnlyCollections.ToDescriptiveString();
        }

        private Projection ProjectionWithOnlyCollections(Projection projection)
        {
            return MaybeProjectionWithOnlyCollections(projection) ??
                new CompoundProjection(ImmutableDictionary<string, Projection>.Empty);
        }

        private static Projection? MaybeProjectionWithOnlyCollections(Projection projection)
        {
            if (projection is CollectionProjection)
            {
                return projection;
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
    }
}
