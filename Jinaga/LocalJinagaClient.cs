using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Projections;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
    public class LocalJinagaClient
    {
        private readonly FactManager factManager;
        private readonly ILogger logger;

        internal LocalJinagaClient(FactManager factManager, ILoggerFactory loggerFactory)
        {
            this.factManager = factManager;
            this.logger = loggerFactory.CreateLogger<LocalJinagaClient>();
        }

        /// <summary>
        /// Retrieve results of a specification from the local cache.
        /// Unlike Query, Local.Query does not fetch new facts from the remote replicator.
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
        /// Retrieve results of a specification from the local cache.
        /// Unlike Query, Local.Query does not fetch new facts from the remote replicator.
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
                logger.LogInformation("Local.Query starting for {Specification}", specification.ToDescriptiveString());

                var givenReferences = ImmutableList.Create(givenReference);
                if (specification.CanRunOnGraph)
                {
                    var products = specification.Execute(givenTuple, graph);
                    var productAnchorProjections = factManager.DeserializeProductsFromGraph(
                        graph, specification.Projection, products, typeof(TProjection), "", null);

                    stopwatch.Stop();
                    logger.LogInformation("Local.Query was able to run on graph in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

                    return productAnchorProjections.Select(pap => (TProjection)pap.Projection).ToImmutableList();
                }
                else
                {
                    var products = await factManager.QueryLocal(givenTuple, specification, cancellationToken).ConfigureAwait(false);
                    var productProjections = await factManager.ComputeProjections(specification.Projection, products, typeof(TProjection), null, string.Empty, cancellationToken).ConfigureAwait(false);
                    var projections = productProjections
                        .Select(pair => (TProjection)pair.Projection)
                        .ToImmutableList();

                    stopwatch.Stop();
                    logger.LogInformation("Local.Query succeeded after {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

                    return projections;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger.LogError(ex, "Local.Query failed after {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
