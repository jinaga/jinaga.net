using Jinaga.Facts;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Xunit.Abstractions;
using Jinaga.DefaultImplementations;

namespace Jinaga.Store.SQLite.Test;

public class SQLiteStoreCacheTest
{
    private readonly ITestOutputHelper output;
    private readonly TestLogger logger;
    private readonly SQLiteStore store;
    private readonly JinagaClient j;

    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "SQLiteStoreCacheTest.db");

    public SQLiteStoreCacheTest(ITestOutputHelper output)
    {
        this.output = output;
        this.logger = new TestLogger(output);
        var loggerFactory = new TestLoggerFactory(logger);

        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        store = new SQLiteStore(SQLitePath, loggerFactory);
        j = new JinagaClient(store, new LocalNetwork(), [], loggerFactory);
    }

    private class TestLogger : ILogger
    {
        private readonly ITestOutputHelper output;
        public ConcurrentBag<string> SqlQueries { get; } = new();

        public TestLogger(ITestOutputHelper output)
        {
            this.output = output;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            output.WriteLine($"{logLevel}: {message}");

            // Track SQL queries
            if (message.Contains("SELECT") || message.Contains("INSERT"))
            {
                SqlQueries.Add(message);
            }
        }
    }

    private class TestLoggerFactory : ILoggerFactory
    {
        private readonly TestLogger logger;

        public TestLoggerFactory(TestLogger logger)
        {
            this.logger = logger;
        }

        public void AddProvider(ILoggerProvider provider) { }

        public ILogger CreateLogger(string categoryName) => logger;

        public void Dispose() { }
    }

    [Fact]
    public async Task LoadsFactTypesFromSimpleMatch()
    {
        // Arrange
        var site = await j.Fact(new Site("test.com"));
        var post = await j.Fact(new Post(site, "2024-02-21"));

        // Clear the logger
        logger.SqlQueries.Clear();

        // Act
        var specification = Given<Site>.Match((site, facts) =>
            from post in facts.OfType<Post>()
            where post.site == site
            select post
        );
        await j.Query(specification, site);

        // Assert
        var factTypeQueries = logger.SqlQueries
            .Where(q => q.Contains("fact_type"))
            .ToList();

        // First query should load both fact types
        factTypeQueries.Should().ContainSingle()
            .Which.Should().Contain("WHERE name IN");

        // Second query should use cache
        await j.Query(specification, site);
        factTypeQueries = logger.SqlQueries
            .Where(q => q.Contains("fact_type"))
            .ToList();
        factTypeQueries.Should().ContainSingle(); // Count should still be 1

        // Verify cache hit
        var cacheHits = logger.SqlQueries
            .Where(q => q.Contains("Cache hit"))
            .ToList();
        cacheHits.Should().ContainSingle()
            .Which.Should().Contain("Cache hit for fact types");

        await j.Unload();
    }

    [Fact]
    public async Task LoadsFactTypesFromExistentialCondition()
    {
        // Arrange
        var flight = await j.Fact(new Flight(new AirlineDay(new Airline("IA"), DateTime.Today), 4272));
        var cancellation = await j.Fact(new FlightCancellation(flight, DateTime.UtcNow));

        // Clear the logger
        logger.SqlQueries.Clear();

        // Act
        var specification = Given<Flight>.Match((flight, facts) =>
            from f in facts.OfType<Flight>()
            where f == flight
            where !f.IsCancelled
            select f
        );
        await j.Query(specification, flight);

        // Assert
        var factTypeQueries = logger.SqlQueries
            .Where(q => q.Contains("fact_type"))
            .ToList();

        // Should load both Flight and FlightCancellation types
        factTypeQueries.Should().ContainSingle()
            .Which.Should().Contain("WHERE name IN");

        // Second query should use cache
        await j.Query(specification, flight);
        factTypeQueries = logger.SqlQueries
            .Where(q => q.Contains("fact_type"))
            .ToList();
        factTypeQueries.Should().ContainSingle(); // Count should still be 1

        await j.Unload();
    }

    [Fact]
    public async Task LoadsFactTypesFromProjection()
    {
        // Arrange
        var site = await j.Fact(new Site("test.com"));
        var post = await j.Fact(new Post(site, "2024-02-21"));
        var title = await j.Fact(new Title(post, "Test Post", Array.Empty<Title>()));

        // Clear the logger
        logger.SqlQueries.Clear();

        // Act
        var specification = Given<Site>.Match((site, facts) =>
            from post in facts.OfType<Post>()
            where post.site == site
            select new
            {
                post,
                titles = facts.Observable(
                    from title in facts.OfType<Title>()
                    where title.post == post
                    select title.value
                )
            }
        );
        await j.Query(specification, site);

        // Assert
        var factTypeQueries = logger.SqlQueries
            .Where(q => q.Contains("fact_type"))
            .ToList();

        // Should load Site, Post, and Title types in one query
        factTypeQueries.Should().ContainSingle()
            .Which.Should().Contain("WHERE name IN");

        // Second query should use cache
        await j.Query(specification, site);
        factTypeQueries = logger.SqlQueries
            .Where(q => q.Contains("fact_type"))
            .ToList();
        factTypeQueries.Should().ContainSingle(); // Count should still be 1

        await j.Unload();
    }

    [Fact]
    public async Task LoadsRolesFromSimpleMatch()
    {
        // Arrange
        var airline = await j.Fact(new Airline("IA"));
        var airlineDay = await j.Fact(new AirlineDay(airline, DateTime.Today));
        var flight = await j.Fact(new Flight(airlineDay, 4272));

        // Clear the logger
        logger.SqlQueries.Clear();

        // Act
        var specification = Given<AirlineDay>.Match((airlineDay, facts) =>
            from flight in facts.OfType<Flight>()
            where flight.airlineDay == airlineDay
            select flight
        );
        await j.Query(specification, airlineDay);

        // Assert
        var roleQueries = logger.SqlQueries
            .Where(q => q.Contains("role"))
            .ToList();

        // First query should load roles
        roleQueries.Should().ContainSingle()
            .Which.Should().Contain("WHERE defining_fact_type_id IN");

        // Second query should use cache
        await j.Query(specification, airlineDay);
        roleQueries = logger.SqlQueries
            .Where(q => q.Contains("role"))
            .ToList();
        roleQueries.Should().ContainSingle(); // Count should still be 1

        // Verify cache hit
        var cacheHits = logger.SqlQueries
            .Where(q => q.Contains("Cache hit"))
            .ToList();
        cacheHits.Should().ContainSingle()
            .Which.Should().Contain("Cache hit for roles");

        await j.Unload();
    }

    [Fact]
    public async Task LoadsRolesFromNestedSpecification()
    {
        // Arrange
        var site = await j.Fact(new Site("test.com"));
        var post = await j.Fact(new Post(site, "2024-02-21"));
        var user = await j.Fact(new User("user1"));
        var comment = await j.Fact(new Comment(post, user, "Test comment"));

        // Clear the logger
        logger.SqlQueries.Clear();

        // Act
        var specification = Given<Site>.Match((site, facts) =>
            from post in facts.OfType<Post>()
            where post.site == site
            select new
            {
                post,
                comments = facts.Observable(
                    from comment in facts.OfType<Comment>()
                    where comment.post == post
                    select comment
                )
            }
        );
        await j.Query(specification, site);

        // Assert
        var roleQueries = logger.SqlQueries
            .Where(q => q.Contains("role"))
            .ToList();

        // Should load roles for both Post->Site and Comment->Post relationships
        roleQueries.Should().ContainSingle()
            .Which.Should().Contain("WHERE defining_fact_type_id IN");

        // Second query should use cache
        await j.Query(specification, site);
        roleQueries = logger.SqlQueries
            .Where(q => q.Contains("role"))
            .ToList();
        roleQueries.Should().ContainSingle(); // Count should still be 1

        await j.Unload();
    }

    [Fact]
    public async Task HandlesRoleCacheConcurrently()
    {
        // Arrange
        var airline = await j.Fact(new Airline("IA"));
        var airlineDay = await j.Fact(new AirlineDay(airline, DateTime.Today));
        var flight = await j.Fact(new Flight(airlineDay, 4272));
        var tasks = new List<Task>();

        // Clear the logger
        logger.SqlQueries.Clear();

        // Act
        var specification = Given<AirlineDay>.Match((airlineDay, facts) =>
            from flight in facts.OfType<Flight>()
            where flight.airlineDay == airlineDay
            select flight
        );

        // Run 10 concurrent queries
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => j.Query(specification, airlineDay)));
        }

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);
        if (completedTask == timeoutTask)
        {
            await j.Unload();
            throw new TimeoutException("Test timed out after 10 seconds");
        }

        // Assert
        var roleQueries = logger.SqlQueries
            .Where(q => q.Contains("role"))
            .ToList();

        // Should only have one query to load roles, despite concurrent access
        roleQueries.Should().ContainSingle()
            .Which.Should().Contain("WHERE defining_fact_type_id IN");

        await j.Unload();
    }

    [Fact]
    public async Task LoadsFactTypesFromNestedExistentialCondition()
    {
        // Arrange
        var site = await j.Fact(new Site("test.com"));
        var post = await j.Fact(new Post(site, "2024-02-21"));
        var user = await j.Fact(new User("user1"));
        var comment = await j.Fact(new Comment(post, user, "Test comment"));

        // Clear the logger
        logger.SqlQueries.Clear();

        // Act
        var specification = Given<Post>.Match((post, facts) =>
            from p in facts.OfType<Post>()
            where p == post
            where (
                from comment in facts.OfType<Comment>()
                where comment.post == p
                where (
                    from user in facts.OfType<User>()
                    where user == comment.author
                    select user
                ).Any()
                select comment
            ).Any()
            select p
        );
        await j.Query(specification, post);

        // Assert
        var factTypeQueries = logger.SqlQueries
            .Where(q => q.Contains("fact_type"))
            .ToList();

        // Should load Post, Comment, and User types in one query
        factTypeQueries.Should().ContainSingle()
            .Which.Should().Contain("WHERE name IN");

        // Second query should use cache
        await j.Query(specification, post);
        factTypeQueries = logger.SqlQueries
            .Where(q => q.Contains("fact_type"))
            .ToList();
        factTypeQueries.Should().ContainSingle(); // Count should still be 1

        await j.Unload();
    }

    [Fact]
    public async Task HandlesCacheMissesConcurrently()
    {
        // Arrange
        var tasks = new List<Task>();
        var site = await j.Fact(new Site("test.com"));
        var post = await j.Fact(new Post(site, "2024-02-21"));
        var completionSource = new TaskCompletionSource();

        // Clear the logger
        logger.SqlQueries.Clear();

        // Act - Run multiple queries concurrently
        var specification = Given<Site>.Match((site, facts) =>
            from post in facts.OfType<Post>()
            where post.site == site
            select post
        );

        // Run 10 concurrent queries
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => j.Query(specification, site)));
        }

        // Create a timeout task
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
        var allQueriesTask = Task.WhenAll(tasks).ContinueWith(t => completionSource.SetResult());

        // Wait for either completion or timeout
        var completedTask = await Task.WhenAny(completionSource.Task, timeoutTask);
        if (completedTask == timeoutTask)
        {
            await j.Unload();
            throw new TimeoutException("Test timed out after 10 seconds");
        }

        // Assert
        var factTypeQueries = logger.SqlQueries
            .Where(q => q.Contains("fact_type"))
            .ToList();

        // Should only have one query to load fact types, despite concurrent access
        factTypeQueries.Should().ContainSingle()
            .Which.Should().Contain("WHERE name IN");

        await j.Unload();
    }
}
