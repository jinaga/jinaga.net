using Jinaga.DefaultImplementations;
using Jinaga.Facts;
using Jinaga.Http;
using Jinaga.Managers;
using Jinaga.Services;
using Jinaga.Storage;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
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
    }

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
            IStore store = new MemoryStore();
            INetwork network = options.HttpEndpoint == null
                ? (INetwork)new LocalNetwork()
                : new HttpNetwork(options.HttpEndpoint, options.HttpAuthenticationProvider);
            return new JinagaClient(store, network);
        }

        private readonly FactManager factManager;
        private readonly NetworkManager networkManager;

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

        public JinagaClient(IStore store, INetwork network)
        {
            networkManager = new NetworkManager(network, store, async (graph, added, cancellationToken) =>
            {
                if (factManager != null)
                {
                    await factManager.NotifyObservers(graph, added, cancellationToken).ConfigureAwait(false);
                }
            });
            factManager = new FactManager(store, networkManager);
        }

        public async Task<(User user, UserProfile profile)> Login(CancellationToken cancellationToken = default)
        {
            var (graph, profile) = await factManager.Login(cancellationToken);
            var user = factManager.Deserialize<User>(graph, graph.Last);
            return (user, profile);
        }

        public async Task Push(CancellationToken cancellationToken = default)
        {
            await factManager.Push(cancellationToken).ConfigureAwait(false);
        }

        public async Task<TFact> Fact<TFact>(TFact prototype) where TFact: class
        {
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

            return factManager.Deserialize<TFact>(graph, graph.Last);
        }

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
                .Add(specification.Given.Single().Name, givenReference);
            var givenReferences = ImmutableList.Create(givenReference);
            if (specification.CanRunOnGraph)
            {
                var products = specification.Execute(givenTuple, graph);
                var productAnchorProjections = factManager.DeserializeProductsFromGraph(
                    graph, specification.Projection, products, typeof(TProjection), "", null);
                return productAnchorProjections.Select(pap => (TProjection)pap.Projection).ToImmutableList();
            }
            else
            {
                await factManager.Fetch(givenTuple, specification, cancellationToken).ConfigureAwait(false);
                var products = await factManager.Query(givenTuple, specification, cancellationToken).ConfigureAwait(false);
                var productProjections = await factManager.ComputeProjections(specification.Projection, products, typeof(TProjection), null, string.Empty, cancellationToken).ConfigureAwait(false);
                var projections = productProjections
                    .Select(pair => (TProjection)pair.Projection)
                    .ToImmutableList();
                return projections;
            }
        }

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

        public IObserver Watch<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Func<TProjection, Task> added)
            where TFact: class
        {
            return Watch<TFact, TProjection>(specification, given,
                async projection =>
                {
                    await added(projection).ConfigureAwait(false);
                    return () => Task.CompletedTask;
                }
            );
        }

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
                .Add(specification.Given.Single().Name, givenReference);
            Func<object, Task<Func<Task>>> onAdded = (object obj) => added((TProjection)obj);
            var observer = factManager.StartObserver(givenTuple, specification, onAdded);
            return observer;
        }
    }
}
