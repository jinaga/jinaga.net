using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Store.SQLite
{
    public class SQLiteStore : IStore
    {

        private ConnectionFactory connectionFactory;

        public SQLiteStore()
        {
            string dbFolderName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            this.connectionFactory = new ConnectionFactory(dbFolderName + "\\jinaga.db");
        }


        Task<ImmutableList<Fact>> IStore.Save(FactGraph graph, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
            //only to test the connectionFactory
            //return this.connectionFactory.WithTransaction((connection) =>
            //{
            //    //throw new Exception("creating my own error");
            //    return Task.FromResult(graph.FactReferences.Select(reference => graph.GetFact(reference)).ToImmutableList());
            //});

        }



        //if (graph.FactReferences.IsEmpty) {


        //}
        //else
        //{

        //}



        //return await this.connectionFactory.withTransaction(async (connection) => {
        //    const newFacts = await filterNewFacts(facts, connection);
        //    await insertEdges(newFacts, connection);
        //    await insertFacts(newFacts, connection);
        //    await insertSignatures(envelopes, connection);
        //    return envelopes.filter(envelope => newFacts.some(
        //        factReferenceEquals(envelope.fact)));
        //});



        ////Get new facts
        //var newFacts = graph.FactReferences
        //    .Where(reference => !factsByReference.ContainsKey(reference))
        //     .Select(reference => graph.GetFact(reference))
        //     .ToImmutableList();

        ////Add facts
        //factsByReference = factsByReference.AddRange(newFacts
        //    .Select(fact => new KeyValuePair<FactReference, Fact>(fact.Reference, fact))
        //);

        ////Add edges
        //var newPredecessors = newFacts
        //    .Select(fact => (
        //        factReference: fact.Reference,
        //        edges: fact.Predecessors
        //            .SelectMany(predecessor => CreateEdges(fact, predecessor))
        //            .ToImmutableList()
        //    ))
        //    .ToImmutableList();
        //edges = edges.AddRange(newPredecessors
        //    .SelectMany(pair => pair.edges)
        //);

        ////Add ancestors
        //foreach (var (factReference, edges) in newPredecessors)
        //{
        //    ancestors = ancestors.Add(
        //        factReference,
        //        edges
        //            .SelectMany(edge => ancestors[edge.Predecessor])
        //            .Append(factReference)
        //            .Distinct()
        //            .ToImmutableList()
        //    );
        //}



        //return Task.FromResult(newFacts);






        Task<FactGraph> IStore.Load(ImmutableList<FactReference> references, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<ImmutableList<Product>> IStore.Query(ImmutableList<FactReference> startReferences, Specification specification, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

    }



}
