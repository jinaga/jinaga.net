using Jinaga.DefaultImplementations;
using Jinaga.Facts;
using Jinaga.Http;
using Jinaga.Managers;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
    /// <summary>
    /// Options for creating a Jinaga client.
    /// </summary>
    public class JinagaClientOptions
    {
        /// <summary>
        /// http://localhost:8080/jinaga/
        /// </summary>
        public static Uri DefaultReplicatorEndpoint = new Uri("http://localhost:8080/jinaga/");

        /// <summary>
        /// The endpoint of the Jinaga replicator, or null for local operation.
        /// </summary>
        public Uri? HttpEndpoint { get; set; }

        /// <summary>
        /// The strategy to use for authenticating with the Jinaga replicator.
        /// </summary>
        public IHttpAuthenticationProvider? HttpAuthenticationProvider { get; set; }

        /// <summary>
        /// A factory configured for logging.
        /// If not provided, logging is disabled.
        /// </summary>
        public ILoggerFactory? LoggerFactory { get; set; }
    }

    /// <summary>
    /// Information about Jinaga background processes.
    /// </summary>
    public class JinagaStatus
    {
        public static readonly JinagaStatus Default = new JinagaStatus(false, null, false, null, 0);

        public JinagaStatus(bool isLoading, Exception? lastLoadError, bool isSaving, Exception? lastSaveError, int queueLength)
        {
            IsLoading = isLoading;
            LastLoadError = lastLoadError;
            IsSaving = isSaving;
            LastSaveError = lastSaveError;
            QueueLength = queueLength;
        }

        /// <summary>
        /// True if the client is currently loading facts from the remote replicator.
        /// </summary>
        public bool IsLoading { get; }
        /// <summary>
        /// The last error that occurred while loading facts from the remote replicator.
        /// </summary>
        public Exception? LastLoadError { get; }

        /// <summary>
        /// True if the client is currently saving facts to the remote replicator.
        /// </summary>
        public bool IsSaving { get; }
        /// <summary>
        /// The last error that occurred while saving facts to the remote replicator.
        /// </summary>
        public Exception? LastSaveError { get; }
        /// <summary>
        /// The number of facts that are currently queued to be saved to the remote replicator.
        /// </summary>
        public int QueueLength { get; }

        public JinagaStatus WithLoadStatus(bool isLoading, Exception? lastLoadError)
        {
            return new JinagaStatus(isLoading, lastLoadError, IsSaving, LastSaveError, QueueLength);
        }

        public JinagaStatus WithSaveStatus(bool isSaving, Exception? lastSaveError, int queueLength)
        {
            return new JinagaStatus(IsLoading, LastLoadError, isSaving, lastSaveError, queueLength);
        }
    }

    public delegate void JinagaStatusChanged(JinagaStatus status);

    /// <summary>
    /// Provides access to Jinaga facts and results.
    /// Treat this object as a singleton.
    /// </summary>
    public class JinagaClient
    {
        /// <summary>
        /// Creates a Jinaga client with no persistent storage or network connection.
        /// </summary>
        /// <returns>A Jinaga client</returns>
        public static JinagaClient Create()
        {
            return Create(_ => { });
        }

        /// <summary>
        /// Creates a Jinaga client using the provided configuration.
        /// </summary>
        /// <param name="configure">Lambda that sets properties on the options object.</param>
        /// <returns>A Jinaga client</returns>
        public static JinagaClient Create(Action<JinagaClientOptions> configure)
        {
            var options = new JinagaClientOptions();
            configure(options);
            var loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            IStore store = new MemoryStore();
            INetwork network = options.HttpEndpoint == null
                ? (INetwork)new LocalNetwork()
                : new HttpNetwork(options.HttpEndpoint, options.HttpAuthenticationProvider, loggerFactory);
            return new JinagaClient(store, network, loggerFactory);
        }

        private readonly FactManager factManager;
        private readonly NetworkManager networkManager;
        private readonly ILogger<JinagaClient> logger;

        /// <summary>
        /// Event that fires when the status of the client changes.
        /// </summary>
        public event JinagaStatusChanged OnStatusChanged
        {
            add
            {
                networkManager.OnStatusChanged += value;
            }
            remove
            {
                networkManager.OnStatusChanged -= value;
            }
        }

        /// <summary>
        /// Use factory methods such as JinagaClient.Create or
        /// JinagaSQLiteClient.Create instead of this constructor.
        /// </summary>
        /// <param name="store">A strategy to store facts locally</param>
        /// <param name="network">A strategy to communicate with a remote replicator</param>
        /// <param name="loggerFactory">A factory configured for logging</param>
        public JinagaClient(IStore store, INetwork network, ILoggerFactory loggerFactory)
        {
            networkManager = new NetworkManager(network, store, loggerFactory, async (graph, added, cancellationToken) =>
            {
                if (factManager != null)
                {
                    await factManager.NotifyObservers(graph, added, cancellationToken).ConfigureAwait(false);
                }
            });
            factManager = new FactManager(store, networkManager, loggerFactory);
            logger = loggerFactory.CreateLogger<JinagaClient>();

            Local = new LocalJinagaClient(factManager, loggerFactory);
        }

        public LocalJinagaClient Local { get; }

        /// <summary>
        /// Get information about the logged in user.
        /// </summary>
        /// <param name="cancellationToken">To cancel the operation</param>
        /// <returns>The user fact and profile information from the identity provider</returns>
        public async Task<(User user, UserProfile profile)> Login(CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInformation("Login starting");

                var (graph, profile) = await factManager.Login(cancellationToken);
                var user = factManager.Deserialize<User>(graph, graph.Last);

                logger.LogInformation("Login succeeded for {DisplayName}", profile.DisplayName);

                return (user, profile);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Login failed");
                throw;
            }
        }

        /// <summary>
        /// If any facts are in the queue, send them to the replicator.
        /// </summary>
        /// <param name="cancellationToken">To cancel the operation</param>
        /// <returns>Resolved when the queue has been emptied</returns>
        public async Task Push(CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInformation("Push starting");

                await factManager.Push(cancellationToken).ConfigureAwait(false);

                logger.LogInformation("Push succeeded");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Push failed");
                throw;
            }
        }

        /// <summary>
        /// Process a new fact.
        /// The fact is stored locally and queued for sending upstream.
        /// Active observers are notified to update the user interface.
        /// </summary>
        /// <typeparam name="TFact">The type of the new fact</typeparam>
        /// <param name="prototype">The new fact</param>
        /// <returns>A copy of the fact as saved</returns>
        /// <exception cref="ArgumentNullException">If the fact is null</exception>
        public async Task<TFact> Fact<TFact>(TFact prototype) where TFact : class
        {
            try
            {
                logger.LogInformation("Fact creation starting");

                if (prototype == null)
                {
                    throw new ArgumentNullException(nameof(prototype));
                }

                var graph = factManager.Serialize(prototype);
                using (var source = new CancellationTokenSource())
                {
                    var token = source.Token;
                    await factManager.Save(graph, token).ConfigureAwait(false);
                }

                var fact = factManager.Deserialize<TFact>(graph, graph.Last);

                logger.LogInformation("Fact creation succeeded");

                return fact;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fact creation failed");
                throw;
            }
        }

        /// <summary>
        /// Compute the hash of a fact.
        /// </summary>
        /// <typeparam name="TFact">The type of the fact</typeparam>
        /// <param name="fact">The fact of which to compute the hash</param>
        /// <returns>Base 64 encoded SHA-512 hash of the fact</returns>
        public string Hash<TFact>(TFact fact)
        {
            var graph = Graph(fact);
            return graph.Last.Hash;
        }

        /// <summary>
        /// Get the graph of a fact. It is rare for an application to need this.
        /// </summary>
        /// <typeparam name="TFact">The type of the fact</typeparam>
        /// <param name="fact">The fact of which to get the graph</param>
        /// <returns>The graph of the fact</returns>
        public FactGraph Graph<TFact>(TFact fact)
        {
            if (fact == null)
            {
                throw new ArgumentNullException(nameof(fact));
            }

            var graph = factManager.Serialize(fact);
            return graph;
        }

        /// <summary>
        /// Retrieve results of a specification.
        /// Unlike Watch, results of Query are not updated with new facts.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="cancellationToken">To cancel the operation</param>
        /// <returns>The results</returns>
        /// <exception cref="ArgumentNullException">If the given is null</exception>
        public async Task<ImmutableList<TProjection>> Query<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            CancellationToken cancellationToken = default) where TFact : class
        {
            if (given == null)
            {
                throw new ArgumentNullException(nameof(given));
            }

            var graph = factManager.Serialize(given);
            var givenReference = graph.Last;
            var givenTuple = FactReferenceTuple.Empty
                .Add(specification.Givens.Single().Label.Name, givenReference);
            return await RunSpecification<TProjection>(specification, graph, givenReference, givenTuple, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve results of a specification.
        /// Unlike Watch, results of Query are not updated with new facts.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="cancellationToken">To cancel the operation</param>
        /// <returns>The results</returns>
        /// <exception cref="ArgumentNullException">If either given is null</exception>
        public async Task<ImmutableList<TProjection>> Query<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            CancellationToken cancellationToken = default) where TFact1 : class where TFact2 : class
        {
            if (given1 == null)
            {
                throw new ArgumentNullException(nameof(given1));
            }
            if (given2 == null)
            {
                throw new ArgumentNullException(nameof(given2));
            }

            var graph1 = factManager.Serialize(given1);
            var givenReference1 = graph1.Last;
            var graph2 = factManager.Serialize(given2);
            var givenReference2 = graph2.Last;
            var givenTuple = FactReferenceTuple.Empty
                .Add(specification.Givens[0].Label.Name, givenReference1)
                .Add(specification.Givens[1].Label.Name, givenReference2);
            return await RunSpecification<TProjection>(specification, graph1.AddGraph(graph2), givenReference1, givenTuple, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve results of a specification from the local cache.
        /// Unlike Query, QueryLocal does not fetch new facts from the remote replicator.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="cancellationToken">To cancel the operation</param>
        /// <returns>The results</returns>
        /// <exception cref="ArgumentNullException">If the given is null</exception>
        [Obsolete("Use Local.Query instead.")]
        public Task<ImmutableList<TProjection>> QueryLocal<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            CancellationToken cancellationToken = default) where TFact : class
        {
            return Local.Query(specification, given, cancellationToken);
        }

        /// <summary>
        /// Retrieve results of a specification from the local cache.
        /// Unlike Query, QueryLocal does not fetch new facts from the remote replicator.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="cancellationToken">To cancel the operation</param>
        /// <returns>The results</returns>
        /// <exception cref="ArgumentNullException">If either given is null</exception>
        [Obsolete("Use Local.Query instead.")]
        public Task<ImmutableList<TProjection>> QueryLocal<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            CancellationToken cancellationToken = default) where TFact1 : class where TFact2 : class
        {
            return Local.Query(specification, given1, given2, cancellationToken);
        }

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        public IObserver Watch<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Action<TProjection> added)
            where TFact : class
        {
            return Watch<TFact, TProjection>(specification, given,
                projection =>
                {
                    added(projection);
                    Func<Task> result = () => Task.CompletedTask;
                    return Task.FromResult(result);
                }
            );
        }

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        public IObserver Watch<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Func<TProjection, Action> added)
            where TFact : class
        {
            return Watch<TFact, TProjection>(specification, given,
                projection =>
                {
                    var removed = added(projection);
                    Func<Task> result = () =>
                    {
                        removed();
                        return Task.CompletedTask;
                    };
                    return Task.FromResult(result);
                }
            );
        }

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        public IObserver Watch<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Func<TProjection, Task> added)
            where TFact : class
        {
            return Watch<TFact, TProjection>(specification, given,
                async projection =>
                {
                    await added(projection).ConfigureAwait(false);
                    return () => Task.CompletedTask;
                }
            );
        }

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        public IObserver Watch<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Func<TProjection, Task<Func<Task>>> added)
            where TFact : class
        {
            if (given == null)
            {
                throw new ArgumentNullException(nameof(given));
            }

            var graph = factManager.Serialize(given);
            var givenReference = graph.Last;
            var givenTuple = FactReferenceTuple.Empty
                .Add(specification.Givens.Single().Label.Name, givenReference);
            Func<object, Task<Func<Task>>> onAdded = (object obj) => added((TProjection)obj);
            var observer = factManager.StartObserver(givenTuple, specification, onAdded, keepAlive: false);
            return observer;
        }

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        public IObserver Watch<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            Action<TProjection> added)
            where TFact1 : class
            where TFact2 : class
        {
            return Watch<TFact1, TFact2, TProjection>(specification, given1, given2,
                projection =>
                {
                    added(projection);
                    Func<Task> result = () => Task.CompletedTask;
                    return Task.FromResult(result);
                }
            );
        }

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        public IObserver Watch<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            Func<TProjection, Action> added)
            where TFact1 : class
            where TFact2 : class
        {
            return Watch<TFact1, TFact2, TProjection>(specification, given1, given2,
                projection =>
                {
                    var removed = added(projection);
                    Func<Task> result = () =>
                    {
                        removed();
                        return Task.CompletedTask;
                    };
                    return Task.FromResult(result);
                }
            );
        }

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        public IObserver Watch<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            Func<TProjection, Task> added)
            where TFact1 : class
            where TFact2 : class
        {
            return Watch<TFact1, TFact2, TProjection>(specification, given1, given2,
                async projection =>
                {
                    await added(projection).ConfigureAwait(false);
                    return () => Task.CompletedTask;
                }
            );
        }

        /// <summary>
        /// Observe the results of a specification, including changes.
        /// Unlike Query, Watch sets up an observer which responds to new facts.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        public IObserver Watch<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            Func<TProjection, Task<Func<Task>>> added)
            where TFact1 : class
            where TFact2 : class
        {
            if (given1 == null)
            {
                throw new ArgumentNullException(nameof(given1));
            }

            if (given2 == null)
            {
                throw new ArgumentNullException(nameof(given2));
            }

            var graph1 = factManager.Serialize(given1);
            var givenReference1 = graph1.Last;
            var graph2 = factManager.Serialize(given2);
            var givenReference2 = graph2.Last;
            var givenTuple = FactReferenceTuple.Empty
                .Add(specification.Givens[0].Label.Name, givenReference1)
                .Add(specification.Givens[1].Label.Name, givenReference2);
            Func<object, Task<Func<Task>>> onAdded = (object obj) => added((TProjection)obj);
            var observer = factManager.StartObserver(givenTuple, specification, onAdded, keepAlive: false);
            return observer;
        }

        /// <summary>
        /// Observe the results of a specification, including changes from the remote replicator.
        /// Unlike Watch, Subscribe creates a persistent connection to the replicator.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        public IObserver Subscribe<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Action<TProjection> added)
            where TFact : class
        {
            return Subscribe<TFact, TProjection>(specification, given,
                projection =>
                {
                    added(projection);
                    Func<Task> result = () => Task.CompletedTask;
                    return Task.FromResult(result);
                }
            );
        }

        /// <summary>
        /// Observe the results of a specification, including changes from the remote replicator.
        /// Unlike Watch, Subscribe creates a persistent connection to the replicator.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        public IObserver Subscribe<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Func<TProjection, Action> added)
            where TFact : class
        {
            return Subscribe<TFact, TProjection>(specification, given,
                projection =>
                {
                    var removed = added(projection);
                    Func<Task> result = () =>
                    {
                        removed();
                        return Task.CompletedTask;
                    };
                    return Task.FromResult(result);
                }
            );
        }

        /// <summary>
        /// Observe the results of a specification, including changes from the remote replicator.
        /// Unlike Watch, Subscribe creates a persistent connection to the replicator.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        public IObserver Subscribe<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Func<TProjection, Task> added)
            where TFact : class
        {
            return Subscribe<TFact, TProjection>(specification, given,
                async projection =>
                {
                    await added(projection).ConfigureAwait(false);
                    return () => Task.CompletedTask;
                }
            );
        }

        /// <summary>
        /// Observe the results of a specification, including changes from the remote replicator.
        /// Unlike Watch, Subscribe creates a persistent connection to the replicator.
        /// </summary>
        /// <typeparam name="TFact">The type of the starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given">The starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns>To control the observer</returns>
        public IObserver Subscribe<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Func<TProjection, Task<Func<Task>>> added)
            where TFact : class
        {
            if (given == null)
            {
                throw new ArgumentNullException(nameof(given));
            }

            var graph = factManager.Serialize(given);
            var givenReference = graph.Last;
            var givenTuple = FactReferenceTuple.Empty
                .Add(specification.Givens.Single().Label.Name, givenReference);
            Func<object, Task<Func<Task>>> onAdded = (object obj) => added((TProjection)obj);
            var observer = factManager.StartObserver(givenTuple, specification, onAdded, keepAlive: true);
            return observer;
        }

        /// <summary>
        /// Observe the results of a specification, including changes from the remote replicator.
        /// Unlike Watch, Subscribe creates a persistent connection to the replicator.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns></returns>
        public IObserver Subscribe<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            Action<TProjection> added)
            where TFact1 : class
            where TFact2 : class
        {
            return Subscribe<TFact1, TFact2, TProjection>(specification, given1, given2,
                projection =>
                {
                    added(projection);
                    Func<Task> result = () => Task.CompletedTask;
                    return Task.FromResult(result);
                }
            );
        }

        /// <summary>
        /// Observe the results of a specification, including changes from the remote replicator.
        /// Unlike Watch, Subscribe creates a persistent connection to the replicator.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns></returns>
        public IObserver Subscribe<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            Func<TProjection, Action> added)
            where TFact1 : class
            where TFact2 : class
        {
            return Subscribe<TFact1, TFact2, TProjection>(specification, given1, given2,
                projection =>
                {
                    var removed = added(projection);
                    Func<Task> result = () =>
                    {
                        removed();
                        return Task.CompletedTask;
                    };
                    return Task.FromResult(result);
                }
            );
        }

        /// <summary>
        /// Observe the results of a specification, including changes from the remote replicator.
        /// Unlike Watch, Subscribe creates a persistent connection to the replicator.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns></returns>
        public IObserver Subscribe<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            Func<TProjection, Task> added)
            where TFact1 : class
            where TFact2 : class
        {
            return Subscribe<TFact1, TFact2, TProjection>(specification, given1, given2,
                async projection =>
                {
                    await added(projection).ConfigureAwait(false);
                    return () => Task.CompletedTask;
                }
            );
        }

        /// <summary>
        /// Observe the results of a specification, including changes from the remote replicator.
        /// Unlike Watch, Subscribe creates a persistent connection to the replicator.
        /// </summary>
        /// <typeparam name="TFact1">The type of the first starting point</typeparam>
        /// <typeparam name="TFact2">The type of the second starting point</typeparam>
        /// <typeparam name="TProjection">The type of the results</typeparam>
        /// <param name="specification">Defines which facts to match and how to project them</param>
        /// <param name="given1">The first starting point for the query</param>
        /// <param name="given2">The second starting point for the query</param>
        /// <param name="added">Called when a result is added. Optionally return a function to be called when it is removed.</param>
        /// <returns></returns>
        public IObserver Subscribe<TFact1, TFact2, TProjection>(
            Specification<TFact1, TFact2, TProjection> specification,
            TFact1 given1,
            TFact2 given2,
            Func<TProjection, Task<Func<Task>>> added)
            where TFact1 : class
            where TFact2 : class
        {
            if (given1 == null)
            {
                throw new ArgumentNullException(nameof(given1));
            }

            if (given2 == null)
            {
                throw new ArgumentNullException(nameof(given2));
            }

            var graph1 = factManager.Serialize(given1);
            var givenReference1 = graph1.Last;
            var graph2 = factManager.Serialize(given2);
            var givenReference2 = graph2.Last;
            var givenTuple = FactReferenceTuple.Empty
                .Add(specification.Givens[0].Label.Name, givenReference1)
                .Add(specification.Givens[1].Label.Name, givenReference2);
            Func<object, Task<Func<Task>>> onAdded = (object obj) => added((TProjection)obj);
            var observer = factManager.StartObserver(givenTuple, specification, onAdded, keepAlive: true);
            return observer;
        }

        /// <summary>
        /// Wait for any background processes to stop.
        /// </summary>
        /// <returns>Resolved when background processes are finished</returns>
        public async Task Unload()
        {
            try
            {
                logger.LogInformation("Unload starting");

                await factManager.Unload().ConfigureAwait(false);

                logger.LogInformation("Unload succeeded");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unload failed");
                throw;
            }
        }

        private async Task<ImmutableList<TProjection>> RunSpecification<TProjection>(
            Specification specification,
            FactGraph graph,
            FactReference givenReference,
            FactReferenceTuple givenTuple,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                logger.LogInformation("Query starting for {Specification}", specification.ToDescriptiveString());

                var givenReferences = ImmutableList.Create(givenReference);
                if (specification.CanRunOnGraph)
                {
                    var products = specification.Execute(givenTuple, graph);
                    var productAnchorProjections = factManager.DeserializeProductsFromGraph(
                        graph, specification.Projection, products, typeof(TProjection), "", null);

                    stopwatch.Stop();
                    logger.LogInformation("Query was able to run on graph in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

                    return productAnchorProjections.Select(pap => (TProjection)pap.Projection).ToImmutableList();
                }
                else
                {
                    var products = await factManager.Query(givenTuple, specification, cancellationToken).ConfigureAwait(false);
                    var productProjections = await factManager.ComputeProjections(specification.Projection, products, typeof(TProjection), null, string.Empty, cancellationToken).ConfigureAwait(false);
                    var projections = productProjections
                        .Select(pair => (TProjection)pair.Projection)
                        .ToImmutableList();

                    stopwatch.Stop();
                    logger.LogInformation("Query succeeded after {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

                    return projections;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger.LogError(ex, "Query failed after {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
