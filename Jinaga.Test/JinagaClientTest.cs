using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Jinaga.Test;

public class JinagaClientTest
{
    private readonly ITestOutputHelper output;

    public JinagaClientTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task JinagaClient_SimulatedUser()
    {
        var j = JinagaTest.Create(options =>
        {
            options.User = new User("Simulated User");
        });

        var (user, profile) = await j.Login();
        user.publicKey.Should().Be("Simulated User");
    }

    private const string DeniedFeed = "4rJh7qM7Rx";

    /// <summary>
    /// Matches successors, so it cannot run on the graph the caller already holds and
    /// therefore asks the replicator for a feed.
    /// </summary>
    private static readonly Specification<AirlineDay, Flight> flightsOfDay =
        Given<AirlineDay>.Match((airlineDay, facts) =>
            from flight in facts.OfType<Flight>()
            where flight.airlineDay == airlineDay
            select flight);

    private (JinagaClient j, FakeNetwork network, List<DistributionDiagnostic> diagnostics) CreateClient()
    {
        var network = new FakeNetwork(output);
        var j = new JinagaClient(new MemoryStore(), network, ImmutableList<Jinaga.Projections.Specification>.Empty,
            NullLoggerFactory.Instance, new JinagaClientOptions());
        var diagnostics = new List<DistributionDiagnostic>();
        j.OnDistributionDiagnostic += diagnostic => diagnostics.Add(diagnostic);
        return (j, network, diagnostics);
    }

    private static FeedDecision Denial(DistributionDenialCode code, string reason)
    {
        return new FeedDecision
        {
            Feed = DeniedFeed,
            Decision = DistributionDecision.Denied,
            Code = code,
            Reason = reason
        };
    }

    [Theory]
    [InlineData(DistributionDenialCode.NoMatchingRule)]
    [InlineData(DistributionDenialCode.SpecMoreRestrictiveThanRule)]
    public async Task Query_ThrowsOnAStructuralDenial(DistributionDenialCode code)
    {
        var (j, network, _) = CreateClient();
        network.ReportDecisions(Denial(code, "The replicator said so."));
        var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));

        Func<Task> query = () => j.Query(flightsOfDay, airlineDay);

        var exception = (await query.Should().ThrowAsync<DistributionDeniedException>()).Which;
        var diagnostic = exception.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Feed.Should().Be(DeniedFeed);
        diagnostic.Code.Should().Be(code);
        diagnostic.Reason.Should().Be("The replicator said so.");
        diagnostic.Operation.Should().Be(DistributionOperation.Query);
        diagnostic.IsStructural.Should().BeTrue();
    }

    [Theory]
    [InlineData(DistributionDenialCode.NoMatchingRule)]
    [InlineData(DistributionDenialCode.SpecMoreRestrictiveThanRule)]
    public async Task QueryWithDiagnostics_ReportsAStructuralDenialWithoutThrowing(DistributionDenialCode code)
    {
        var (j, network, _) = CreateClient();
        network.ReportDecisions(Denial(code, "The replicator said so."));
        var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));

        var (results, diagnostics) = await j.QueryWithDiagnostics(flightsOfDay, airlineDay);

        results.Should().BeEmpty();
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Feed.Should().Be(DeniedFeed);
        diagnostic.Code.Should().Be(code);
        diagnostic.Reason.Should().Be("The replicator said so.");
        diagnostic.Operation.Should().Be(DistributionOperation.Query);
        diagnostic.IsStructural.Should().BeTrue();
    }

    [Theory]
    [InlineData(DistributionDenialCode.PrincipalExcluded)]
    [InlineData(DistributionDenialCode.NotAuthenticated)]
    public async Task Query_ReportsANonStructuralDenialWithoutThrowing(DistributionDenialCode code)
    {
        var (j, network, diagnostics) = CreateClient();
        network.ReportDecisions(Denial(code, "A later fact can change this."));
        var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));

        var flights = await j.Query(flightsOfDay, airlineDay);

        flights.Should().BeEmpty();
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Feed.Should().Be(DeniedFeed);
        diagnostic.Decision.Should().Be(DistributionDecision.Denied);
        diagnostic.Code.Should().Be(code);
        diagnostic.IsStructural.Should().BeFalse();
    }

    [Fact]
    public async Task Query_ReportsAReactiveDecisionWithoutThrowing()
    {
        var (j, network, diagnostics) = CreateClient();
        network.ReportDecisions(new FeedDecision
        {
            Feed = DeniedFeed,
            Decision = DistributionDecision.Reactive,
            Reason = "Permitted while a condition holds."
        });
        var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));

        var flights = await j.Query(flightsOfDay, airlineDay);

        flights.Should().BeEmpty();
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Decision.Should().Be(DistributionDecision.Reactive);
        diagnostic.Code.Should().BeNull();
        diagnostic.IsStructural.Should().BeFalse();
    }

    [Theory]
    [InlineData(DistributionDenialCode.NoMatchingRule)]
    [InlineData(DistributionDenialCode.SpecMoreRestrictiveThanRule)]
    public async Task Query_ThrowsOnAStructuralCodeTheReplicatorCallsReactive(DistributionDenialCode code)
    {
        // A replicator can report a structural code with decision "reactive" (jinaga.js#242).
        // No later fact can heal a structural code, so the code decides.
        var (j, network, _) = CreateClient();
        network.ReportDecisions(new FeedDecision
        {
            Feed = DeniedFeed,
            Decision = DistributionDecision.Reactive,
            Code = code,
            Reason = "The shapes never matched."
        });
        var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));

        Func<Task> query = () => j.Query(flightsOfDay, airlineDay);

        var exception = (await query.Should().ThrowAsync<DistributionDeniedException>()).Which;
        var diagnostic = exception.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Decision.Should().Be(DistributionDecision.Reactive);
        diagnostic.Code.Should().Be(code);
        diagnostic.IsStructural.Should().BeTrue();
    }

    [Fact]
    public async Task Query_AsksTheReplicatorAgainAfterADenial()
    {
        var (j, network, diagnostics) = CreateClient();
        network.ReportDecisions(Denial(DistributionDenialCode.NotAuthenticated, "Log in first."));
        var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));
        await j.Query(flightsOfDay, airlineDay);
        var requestsAfterDenial = network.RequestedFeeds.Count;

        // The user logs in, and the replicator now authorizes the feed.
        network.ReportDecisions();
        diagnostics.Clear();
        await j.Query(flightsOfDay, airlineDay);

        network.RequestedFeeds.Count.Should().Be(requestsAfterDenial + 1);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Query_ReusesAnAuthorizedDeclaration()
    {
        var (j, network, _) = CreateClient();
        var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));
        await j.Query(flightsOfDay, airlineDay);
        var requestsAfterFirstQuery = network.RequestedFeeds.Count;

        await j.Query(flightsOfDay, airlineDay);

        network.RequestedFeeds.Count.Should().Be(requestsAfterFirstQuery);
    }

    [Fact]
    public async Task Query_ReportsNothingWhenTheReplicatorReportsNoDecisions()
    {
        var (j, network, diagnostics) = CreateClient();
        network.ReportDecisions();
        var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));

        var flights = await j.Query(flightsOfDay, airlineDay);

        flights.Should().BeEmpty();
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Watch_ReportsADeniedFeed()
    {
        var (j, network, diagnostics) = CreateClient();
        network.ReportDecisions(Denial(DistributionDenialCode.NoMatchingRule, "The replicator said so."));
        var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));

        var observer = j.Watch(flightsOfDay, airlineDay, flight => { });
        await observer.Loaded;
        observer.Stop();

        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Feed.Should().Be(DeniedFeed);
        diagnostic.Operation.Should().Be(DistributionOperation.Watch);
    }

    [Fact]
    public async Task Subscribe_ReportsADeniedFeed()
    {
        var (j, network, diagnostics) = CreateClient();
        network.ReportDecisions(Denial(DistributionDenialCode.NoMatchingRule, "The replicator said so."));
        var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));

        var observer = j.Subscribe(flightsOfDay, airlineDay, flight => { });
        await observer.Loaded;
        observer.Stop();

        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Feed.Should().Be(DeniedFeed);
        diagnostic.Operation.Should().Be(DistributionOperation.Subscribe);
    }

    [Fact]
    public async Task Query_ReportsTheSpecificationTheReplicatorReceived()
    {
        var (j, network, diagnostics) = CreateClient();
        network.ReportDecisions(Denial(DistributionDenialCode.PrincipalExcluded, "A later fact can change this."));
        var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));

        await j.Query(flightsOfDay, airlineDay);

        // Read the specification back from the one the client handed to the network, so
        // this fails when the two diverge rather than when a literal is edited.
        var requested = network.RequestedFeeds.Should().ContainSingle().Which;
        diagnostics.Should().ContainSingle().Which
            .Specification.Should().Be(requested.specification.ToDescriptiveString());
    }

    [Fact]
    public async Task Query_IsolatesAHandlerThatThrows()
    {
        var (j, network, diagnostics) = CreateClient();
        network.ReportDecisions(Denial(DistributionDenialCode.PrincipalExcluded, "A later fact can change this."));
        j.OnDistributionDiagnostic += diagnostic => throw new InvalidOperationException("The handler failed.");
        var airlineDay = await j.Fact(new AirlineDay(new Airline("IA"), DateTime.Today));

        var flights = await j.Query(flightsOfDay, airlineDay);

        flights.Should().BeEmpty();
        diagnostics.Should().ContainSingle();
    }
}
