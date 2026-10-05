using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Test.Managers;

/// <summary>
/// JinagaClientOptions.MaxBatchSize bounds the number of facts in one request
/// to the replicator. Before it was honored, the whole outbound queue went in a
/// single request, so a queue that had grown past a request size limit between
/// the client and the replicator failed as a unit and never drained.
/// </summary>
public class NetworkManagerBatchTest
{
    /// <summary>
    /// Long enough that the debounce never elapses during a test, so the only
    /// thing that sends the queue is an explicit Push.
    /// </summary>
    private const int DelayLongerThanAnyTest = 60000;

    private static JinagaClient CreateClient(RecordingFakeNetwork network, int maxBatchSize)
    {
        var options = new JinagaClientOptions
        {
            MaxBatchSize = maxBatchSize,
            QueueProcessingDelay = DelayLongerThanAnyTest
        };
        return new JinagaClient(
            new PersistentMemoryStore(),
            network,
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            options);
    }

    [Fact]
    public async Task Push_WithMoreFactsThanTheBatchSize_SendsConsecutiveBatches()
    {
        var network = new RecordingFakeNetwork();
        var j = CreateClient(network, maxBatchSize: 10);

        for (int i = 0; i < 25; i++)
        {
            await j.Fact(new TestFact($"fact{i}"));
        }

        await j.Push();

        network.SavedGraphs.Select(graph => graph.FactReferences.Count)
            .Should().Equal(10, 10, 5);
    }

    [Fact]
    public async Task Push_WithFewerFactsThanTheBatchSize_SendsOneBatch()
    {
        var network = new RecordingFakeNetwork();
        var j = CreateClient(network, maxBatchSize: 10);

        for (int i = 0; i < 4; i++)
        {
            await j.Fact(new TestFact($"fact{i}"));
        }

        await j.Push();

        network.SavedGraphs.Select(graph => graph.FactReferences.Count)
            .Should().Equal(4);
    }

    [Fact]
    public async Task Push_WhenABatchFails_ResumesAfterTheBatchThatSucceeded()
    {
        var network = new RecordingFakeNetwork();
        network.FailSaveCall(2);
        var j = CreateClient(network, maxBatchSize: 10);

        for (int i = 0; i < 25; i++)
        {
            await j.Fact(new TestFact($"fact{i}"));
        }

        Func<Task> failingPush = () => j.Push();
        await failingPush.Should().ThrowAsync<InvalidOperationException>();

        network.SavedGraphs.Select(graph => graph.FactReferences.Count)
            .Should().Equal(new[] { 10 }, "the batch that succeeded is the only one sent");

        await j.Push();

        network.SavedGraphs.Select(graph => graph.FactReferences.Count)
            .Should().Equal(new[] { 10, 10, 5 }, "the queue resumes after the batch that succeeded");
        network.SavedGraphs.SelectMany(graph => graph.FactReferences)
            .Should().OnlyHaveUniqueItems("a fact the replicator accepted is not sent again");
    }

    [Fact]
    public async Task Push_ReportsTheQueueLengthFallingAsEachBatchCompletes()
    {
        var network = new RecordingFakeNetwork();
        var j = CreateClient(network, maxBatchSize: 10);

        for (int i = 0; i < 25; i++)
        {
            await j.Fact(new TestFact($"fact{i}"));
        }

        var queueLengths = new List<int>();
        j.OnStatusChanged += status => queueLengths.Add(status.QueueLength);

        await j.Push();

        queueLengths.Should().Equal(25, 15, 5, 0);
    }

    [Fact]
    public async Task Push_WithFactsThatSharePredecessors_SendsEachBatchClosedAndWithinTheBound()
    {
        var network = new RecordingFakeNetwork();
        var j = CreateClient(network, maxBatchSize: 10);

        var company = await j.Fact(new Company("contoso"));
        var cities = new List<City>();
        for (int i = 0; i < 12; i++)
        {
            cities.Add(await j.Fact(new City($"city{i}")));
        }
        var offices = new List<Office>();
        for (int i = 0; i < 12; i++)
        {
            offices.Add(await j.Fact(new Office(company, cities[i])));
        }

        await j.Push();

        foreach (var graph in network.SavedGraphs)
        {
            // The wire format names a predecessor by its position in the same
            // request, so a batch has to carry the ancestors of its facts,
            // in order, even ones an earlier batch already sent.
            var sentSoFar = new HashSet<FactReference>();
            foreach (var reference in graph.FactReferences)
            {
                foreach (var predecessor in graph.GetFact(reference).GetAllPredecessorReferences())
                {
                    sentSoFar.Should().Contain(predecessor,
                        "every predecessor precedes its successor within the batch that names it");
                }
                sentSoFar.Add(reference);
            }

            graph.FactReferences.Count.Should().BeLessThanOrEqualTo(10,
                "the bound is on the whole request, not on the facts that are new to it");
        }

        // Each of the facts saved reaches the replicator.
        var delivered = network.SavedGraphs
            .SelectMany(graph => graph.FactReferences)
            .Distinct();
        delivered.Should().HaveCount(25);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithANonPositiveMaxBatchSize_Throws(int maxBatchSize)
    {
        Action create = () => JinagaClient.Create(options => options.MaxBatchSize = maxBatchSize);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }
}
