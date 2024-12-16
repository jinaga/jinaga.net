using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Store.SQLite.Builder;
using Jinaga.Store.SQLite.Database;
using Jinaga.Store.SQLite.Description;
using Jinaga.Store.SQLite.Generation;
using Jinaga.Visualizers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Store.SQLite
{
    public class SQLiteStore : IStore
    {

        private ConnectionFactory connFactory;
        private readonly ILogger logger;

        public SQLiteStore(string dbFullPath, ILoggerFactory loggerFactory)
        {
            // Ensure that the folder exists.
            var folder = Path.GetDirectoryName(dbFullPath);
            Directory.CreateDirectory(folder);
            
            this.connFactory = new ConnectionFactory(dbFullPath);
            this.logger = loggerFactory.CreateLogger<SQLiteStore>();
        }

        public bool IsPersistent => true;

        Task<ImmutableList<Fact>> IStore.Save(FactGraph graph, bool queue, CancellationToken cancellationToken)
        {
            if (graph.FactReferences.IsEmpty)
            {
                return Task.FromResult(ImmutableList<Fact>.Empty);
            }
            else
            {
                ImmutableList<Fact> newFacts = ImmutableList<Fact>.Empty;
                foreach (var factReference in graph.FactReferences)
                {
                    var envelope = graph.GetEnvelope(factReference);

                    connFactory.WithTxn(
                        (conn, id) =>
                            {
                                string sql;

                                // Select or insert into FactType table.  Gets a FactTypeId
                                sql = @"
                                    SELECT fact_type_id 
                                    FROM fact_type 
                                    WHERE name = @0
                                ";
                                var factTypeId = conn.ExecuteScalar(sql, envelope.Fact.Reference.Type);
                                if (factTypeId == "")
                                {
                                    sql = @"
                                        INSERT OR IGNORE INTO fact_type (name) 
                                        VALUES (@0)
                                    ";
                                    conn.ExecuteNonQuery(sql, envelope.Fact.Reference.Type);
                                    sql = @"
                                        SELECT fact_type_id 
                                        FROM fact_type 
                                        WHERE name = @0
                                    ";
                                    factTypeId = conn.ExecuteScalar(sql, envelope.Fact.Reference.Type);
                                }

                                // Select or insert into Fact table.  Gets a FactId
                                sql = @"
                                    SELECT fact_id FROM fact 
                                    WHERE hash = @0 AND fact_type_id = @1
                                ";
                                var factId = conn.ExecuteScalar(sql, envelope.Fact.Reference.Hash, factTypeId);
                                if (factId == "")
                                {
                                    newFacts = newFacts.Add(envelope.Fact);
                                    string data = Fact.Canonicalize(envelope.Fact.Fields, envelope.Fact.Predecessors);
                                    sql = @"
                                        INSERT OR IGNORE INTO fact (fact_type_id, hash, data, queued) 
                                        VALUES (@0, @1, @2, @3)
                                    ";
                                    conn.ExecuteNonQuery(sql, factTypeId, envelope.Fact.Reference.Hash, data, queue ? 1 : 0);
                                    sql = @"
                                        SELECT fact_id 
                                        FROM fact 
                                        WHERE hash = @0 AND fact_type_id = @1
                                    ";
                                    factId = conn.ExecuteScalar(sql, envelope.Fact.Reference.Hash, factTypeId);

                                    // Insert into the outbound_queue table
                                    if (queue)
                                    {
                                        var graphToQueue = graph.GetSubgraph(factReference);
                                        string graphData = JsonSerializer.Serialize(graphToQueue);
                                        sql = @"
                                            INSERT INTO outbound_queue (fact_id, graph_data) 
                                            VALUES (@0, @1)
                                        ";
                                        conn.ExecuteNonQuery(sql, factId, graphData);
                                    }

                                    // For each predecessor of the inserted fact ...
                                    foreach (var predecessor in envelope.Fact.Predecessors)
                                    {
                                        // Select or insert into Role table.  Gets a RoleId
                                        sql = @"
                                            SELECT role_id 
                                            FROM role 
                                            WHERE defining_fact_type_id = @0 AND name = @1
                                        ";
                                        var roleId = conn.ExecuteScalar(sql, factTypeId, predecessor.Role);
                                        if (roleId == "")
                                        {
                                            sql = @"
                                                INSERT OR IGNORE INTO role (defining_fact_type_id, name) 
                                                VALUES (@0, @1)
                                            ";
                                            conn.ExecuteNonQuery(sql, factTypeId, predecessor.Role);
                                            sql = @"
                                                SELECT role_id 
                                                FROM role 
                                                WHERE defining_fact_type_id = @0 AND name = @1
                                            ";
                                            roleId = conn.ExecuteScalar(sql, factTypeId, predecessor.Role);
                                        }

                                        // Insert into Edge and Ancestor tables
                                        string predecessorFactId;
                                        switch (predecessor)
                                        {
                                            case PredecessorSingle s:
                                                predecessorFactId = getFactId(conn, s.Reference);
                                                InsertEdge(conn, roleId, factId, predecessorFactId);
                                                InsertAncestors(conn, factId, predecessorFactId);
                                                break;
                                            case PredecessorMultiple m:
                                                foreach (var predecessorMultipleReference in m.References)
                                                {
                                                    predecessorFactId = getFactId(conn, predecessorMultipleReference);
                                                    InsertEdge(conn, roleId, factId, predecessorFactId);
                                                    InsertAncestors(conn, factId, predecessorFactId);
                                                }
                                                break;
                                            default:
                                                break;
                                        }
                                    }

                                    // Insert ancestors into the outbound_queue table
                                    InsertAncestorsIntoQueue(conn, factId);
                                }

                                foreach (var signature in envelope.Signatures)
                                {
                                    // Select or insert into the public_key table.  Gets a public_key_id.
                                    sql = @"
                                        SELECT public_key_id 
                                        FROM public_key 
                                        WHERE public_key = @0
                                    ";
                                    var publicKeyId = conn.ExecuteScalar(sql, signature.PublicKey);
                                    if (publicKeyId == "")
                                    {
                                        sql = @"
                                            INSERT OR IGNORE INTO public_key (public_key) 
                                            VALUES (@0)
                                        ";
                                        conn.ExecuteNonQuery(sql, signature.PublicKey);
                                        sql = @"
                                            SELECT public_key_id 
                                            FROM public_key 
                                            WHERE public_key = @0
                                        ";
                                        publicKeyId = conn.ExecuteScalar(sql, signature.PublicKey);
                                    }

                                    // Insert into the signature table if it doesn't already exist.
                                    sql = @"
                                        INSERT OR IGNORE INTO signature (fact_id, public_key_id, signature) 
                                        VALUES (@0, @1, @2)
                                    ";
                                    conn.ExecuteNonQuery(sql, factId, publicKeyId, signature.Signature);
                                }
                                return 0;
                            },
                        true
                    );

                }
                logger.LogInformation("SQLite saved {count} facts", newFacts.Count);
                return Task.FromResult(newFacts);
            }

        }

        private void InsertAncestorsIntoQueue(Conn conn, string factId)
        {
            string sql = @"
                INSERT INTO outbound_queue (fact_id, fact_type_id, hash, data, public_key, signature)
                SELECT f.fact_id, f.fact_type_id, f.hash, f.data, s.public_key, s.signature
                FROM ancestor a
                JOIN fact f
                    ON f.fact_id = a.ancestor_fact_id
                LEFT JOIN signature s
                    ON s.fact_id = f.fact_id
                LEFT JOIN public_key p
                    ON p.public_key_id = s.public_key_id
                WHERE a.fact_id = @0
                AND NOT EXISTS (
                    SELECT 1
                    FROM outbound_queue q
                    WHERE q.fact_id = f.fact_id
                )
            ";
            conn.ExecuteNonQuery(sql, factId);
        }

        private string getFactId(Conn conn, FactReference factReference)
        {
            string sql;

            sql = $@"
                SELECT fact_type_id 
                FROM fact_type 
                WHERE name = '{factReference.Type}'
            ";
            var factTypeId = conn.ExecuteScalar(sql);

            sql = $@"
                SELECT fact_id 
                FROM fact 
                WHERE hash = '{factReference.Hash}' AND fact_type_id = {factTypeId}
            ";
            return conn.ExecuteScalar(sql);
        }


        private void InsertAncestors(Conn conn, string factId, string predecessorFactId)
        {
            string sql;

            sql = @"
                INSERT OR IGNORE INTO ancestor 
                    (fact_id, ancestor_fact_id)
                SELECT @0, @1
                UNION
                SELECT @0, ancestor_fact_id
                FROM ancestor
                WHERE fact_id = @1
            ";
            conn.ExecuteNonQuery(sql, factId, predecessorFactId);
        }


        private void InsertEdge(Conn conn, string roleId, string successorFactId, string predecessorFactId)
        {
            string sql;

            sql = @"
                INSERT OR IGNORE INTO edge (role_id, successor_fact_id, predecessor_fact_id) 
                VALUES (@0, @1, @2)
            ";
            conn.ExecuteNonQuery(sql, roleId, successorFactId, predecessorFactId);
        }


        Task<FactGraph> IStore.Load(ImmutableList<FactReference> references, CancellationToken cancellationToken)
        {
            if (references.IsEmpty)
            {
                return Task.FromResult(FactGraph.Empty);
            }
            else
            {
                var factsFromDb = connFactory.WithConn(
                    (conn, id) =>
                        {
                            var referenceValues = references.Select((f, i) => $"(@{2*i}, @{2*i+1})").ToArray();

                            var parameters = new List<object>();
                            for (int i = 0; i < references.Count; i++)
                            {
                                parameters.Add(references[i].Hash);
                                parameters.Add(references[i].Type);
                            }

                            string sql = $@"
                                SELECT
                                    f.fact_id,
                                    f.hash, 
                                    f.data,
                                    t.name,
                                    p.public_key,
                                    s.signature
                                FROM fact f 
                                JOIN fact_type t 
                                    ON f.fact_type_id = t.fact_type_id
                                LEFT JOIN signature s
                                    ON s.fact_id = f.fact_id
                                LEFT JOIN public_key p
                                    ON p.public_key_id = s.public_key_id
                                WHERE (f.hash,t.name) 
                                    IN (VALUES {String.Join(",", referenceValues)} )

                            UNION 

                                SELECT
                                    f2.fact_id,
                                    f2.hash, 
                                    f2.data,
                                    t2.name,
                                    p.public_key,
                                    s.signature
                                FROM fact f1 
                                JOIN fact_type t1 
                                    ON 
                                        f1.fact_type_id = t1.fact_type_id
                                            AND
                                        (f1.hash,t1.name) IN (VALUES {String.Join(",", referenceValues)} ) 
                                JOIN ancestor a 
                                    ON a.fact_id = f1.fact_id 
                                JOIN fact f2 
                                    ON f2.fact_id = a.ancestor_fact_id 
                                JOIN fact_type t2 
                                    ON t2.fact_type_id = f2.fact_type_id
                                LEFT JOIN signature s
                                    ON s.fact_id = f2.fact_id
                                LEFT JOIN public_key p
                                    ON p.public_key_id = s.public_key_id
                            ";

                            return conn.ExecuteQuery<FactWithIdAndSignatureFromDb>(sql, parameters.ToArray());
                        },
                    true   //exponential backoff
                );

                logger.LogTrace("SQLite loaded {count} facts", factsFromDb.Count());

                FactGraphBuilder fb = new FactGraphBuilder() ;
            
                foreach (FactEnvelope envelope in factsFromDb.Deserialize()) 
                {
                    fb.Add(envelope);
                }

                return Task.FromResult(fb.Build());
            }
        }


        public Task<ImmutableList<FactReference>> ListKnown(ImmutableList<FactReference> factReferences)
        {

            if (factReferences.IsEmpty)
            {
                return Task.FromResult(ImmutableList<FactReference>.Empty);
            }
            else
            {
                var referencesFromDb = connFactory.WithConn(
                    (conn, id) =>
                    {
                        string[] referenceValues = factReferences.Select((f) => "('" + f.Hash + "', '" + f.Type + "')").ToArray();
                        string sql;
                        sql = $@"
                                SELECT f.hash, 
                                       t.name
                                FROM fact f 
                                JOIN fact_type t 
                                    ON f.fact_type_id = t.fact_type_id    
                                WHERE (f.hash,t.name) 
                                    IN (VALUES {String.Join(",", referenceValues)} )
                            ";

                        return conn.ExecuteQuery<ReferenceFromDb>(sql);
                    },
                    true   //exponential backoff
                );

                var knownReferences = referencesFromDb
                    .Select(r => new FactReference(r.name, r.hash))
                    .ToImmutableList();

                logger.LogTrace("SQLite listed {knownCount} known facts of {givenCount}", knownReferences.Count, factReferences.Count);
                return Task.FromResult(knownReferences);
            }
        }


        Task<ImmutableList<Product>> IStore.Read(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            var factTypes = LoadFactTypesFromSpecification(specification);
            var factTypeMap = factTypes.Select(factType => KeyValuePair.Create(factType.name, factType.fact_type_id)).ToImmutableDictionary();
            
            var roles = LoadRolesFromSpecification(specification, factTypes);
            var roleMap = roles
                .GroupBy(
                    role => role.defining_fact_type_id, 
                    role => KeyValuePair.Create(role.name, role.role_id)
                )
                .Select(
                    pair => KeyValuePair.Create(pair.Key, pair.ToImmutableDictionary())
                )
                .ToImmutableDictionary();                                    

            var descriptionBuilder = new ResultDescriptionBuilder(factTypeMap, roleMap);
            
            var description = descriptionBuilder.Build(givenTuple, specification);

            if (!description.QueryDescription.IsSatisfiable())
            {
                return Task.FromResult(ImmutableList<Product>.Empty);
            }
            var sqlQueryTree = SqlGenerator.CreateSqlQueryTree(description);

            ResultSetTree resultSets = connFactory.WithConn(
                    (conn, id) =>
                    {
                        return ExecuteQueryTree(sqlQueryTree, conn);
                    },
                    true   //exponential backoff
            );

            var givenProduct = Product.Empty;
            foreach (var given in specification.Givens)
            {
                var reference = givenTuple.Get(given.Label.Name);
                givenProduct = givenProduct.With(
                    given.Label.Name,
                    new SimpleElement(reference)
                );
            }

            logger.LogInformation("SQLite read {count} facts", resultSets.Count);
            return Task.FromResult(sqlQueryTree.ResultsToProducts(resultSets, givenProduct));
        }

        private ResultSetTree ExecuteQueryTree(SqlQueryTree sqlQueryTree, Conn conn)
        {
            var resultSetTree = ExecuteQuery(sqlQueryTree, conn);

            foreach (var childQuery in sqlQueryTree.ChildQueries)
            {
                var childResultSet = ExecuteQueryTree(childQuery.Value, conn);
                resultSetTree.ChildResultSets = resultSetTree.ChildResultSets.Add(childQuery.Key, childResultSet);
            };

            return resultSetTree;
        }

        private static ResultSetTree ExecuteQuery(SqlQueryTree sqlQueryTree, Conn conn)
        {
            var sqlQuery = sqlQueryTree.SqlQuery;
            if (string.IsNullOrEmpty(sqlQuery.Sql))
            {
                return new ResultSetTree();
            }

            var dataRows = conn.ExecuteQueryRaw(sqlQueryTree.SqlQuery.Sql, sqlQueryTree.SqlQuery.Parameters.ToArray());
            var resultSet = dataRows.Select(dataRow =>
            {
                var resultSetRow = sqlQuery.Labels.Aggregate(ImmutableDictionary<int, ResultSetFact>.Empty,
                                                    (acc, next) =>
                                                    {
                                                        var fact = new ResultSetFact();
                                                        fact.Hash = dataRow[$"hash{next.Index}"];
                                                        fact.FactId = int.Parse(dataRow[$"id{next.Index}"]);
                                                        fact.Data = dataRow[$"data{next.Index}"];
                                                        fact.Type = next.Type;
                                                        fact.Name = next.Name;
                                                        return acc.Add(next.Index, fact);
                                                    });

                return resultSetRow;
            });

            var resultSetTree = new ResultSetTree();

            resultSetTree.ResultSet = resultSet.ToImmutableList();
            return resultSetTree;
        }

        private IEnumerable<FactTypeFromDb> LoadFactTypesFromSpecification(Specification specification)
        {
            //TODO: Now we load all factTypes from the DB.  Optimize by caching, and by adding only the factTypes used in the specification
            var factTypeResult = connFactory.WithConn(
                    (conn, id) =>
                    {
                        string sql;
                        sql = $@"
                            SELECT fact_type_id , name 
                            FROM fact_type
                        ";
                        return conn.ExecuteQuery<FactTypeFromDb>(sql);
                    },
                    true   //exponential backoff
                );
            return factTypeResult;
        }

        private IEnumerable<RoleFromDb> LoadRolesFromSpecification(Specification specification, object factTypes)
        {
            //TODO: Now we load all roles from the DB.  Optimize by caching, and by adding only the roles used in the specification
            var rolesResult = connFactory.WithConn(
                    (conn, id) =>
                    {
                        string sql;
                        sql = $@"
                            SELECT role_id, defining_fact_type_id, name
                            FROM role                                             
                        ";
                        return conn.ExecuteQuery<RoleFromDb>(sql);
                    },
                    true   //exponential backoff
                );
            return rolesResult;
        }

        private IEnumerable<FactTypeFromDb> LoadFactTypesFromReferences(ImmutableList<FactReference> references)
        {
            //TODO: Now we load all factTypes from the DB.  Optimize by caching, and by adding only the factTypes appearing in the references
            var factTypeResult = connFactory.WithConn(
                    (conn, id) =>
                    {
                        string sql;
                        sql = $@"
                            SELECT fact_type_id , name 
                            FROM fact_type
                        ";
                        return conn.ExecuteQuery<FactTypeFromDb>(sql);
                    },
                    true   //exponential backoff
                );
            return factTypeResult;
        }

        private class FactTypeFromDb
        {
            public int fact_type_id { get; set; }
            public string name { get; set; }
        }

        private class RoleFromDb
        {
            public int role_id { get; set; }
            public int defining_fact_type_id { get; set; }
            public string name { get; set; }
        }


        public Task SaveBookmark(string feed, string bookmark)
        {
            connFactory.WithTxn(
                 (conn, id) =>
                 {
                     {
                        string sql;

                        sql = @"
                            INSERT OR REPLACE INTO bookmark (feed_hash, bookmark)                        
                            VALUES  (@0, @1)
                        ";
                        return conn.ExecuteNonQuery(sql, feed, bookmark);
                     }
                 },
                 true
             );
            return Task.FromResult("");
        }


        public Task<string> LoadBookmark(string feed)
        {
            var bookMark = connFactory.WithTxn(
                (conn, id) =>
                {                   
                    {
                        string sql;
                        sql = $@"
                            SELECT bookmark
                            FROM bookmark
                            WHERE feed_hash = '{feed}'
                        ";
                        return conn.ExecuteScalar(sql);
                    }                   
                },
                true
            );
            return Task.FromResult(bookMark);
        }


        public Task SetMruDate(string specificationHash, DateTime mruDate)
        {

            //Accepts UTC and local time
            connFactory.WithTxn(
                (conn, id) =>
                {
                    {
                        string sql;

                        sql = @"
                            INSERT OR REPLACE INTO mru (specification_hash, mru_date)                        
                            VALUES  (@0, @1)
                        ";
                        return conn.ExecuteNonQuery(sql, specificationHash, mruDate.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                },
                true
            );
            return Task.FromResult("");
        }


        public Task<DateTime?> GetMruDate(string specificationHash)
        {

            //Has to return UTC time !!!

            string mruDateString = connFactory.WithTxn(
               (conn, id) =>
               {
                   {
                        string sql;

                        sql = @"
                            SELECT mru_date
                            FROM mru
                            WHERE specification_hash = @0
                        ";
                        return conn.ExecuteScalar(sql, specificationHash);
                   }
               },
               true
            );
            DateTime mruDate;
            if (DateTime.TryParseExact(mruDateString, "yyyy-MM-dd HH:mm:ss", null, DateTimeStyles.AssumeUniversal, out mruDate))
            {
                return Task.FromResult((DateTime?)mruDate.ToUniversalTime());
            }
            else
            {
                return Task.FromResult((DateTime?)null);
            }
            
        }

        public Task<QueuedFacts> GetQueue()
        {
            // Load the graphs from the outbound_queue table.
            var graphsFromDb = connFactory.WithConn(
                (conn, id) =>
                {
                    string sql;
                    sql = $@"
                        SELECT q.fact_id, q.graph_data
                        FROM outbound_queue q
                        ORDER BY q.queue_id
                    ";
                    return conn.ExecuteQuery<GraphFromDb>(sql);
                },
                true
            );

            // Convert the graph records to FactGraph objects.
            var factGraphs = graphsFromDb.Select(g => DeserializeFactGraph(g.graph_data)).ToImmutableList();

            // If there are graphs, then the next bookmark is the largest fact ID.
            int lastFactId = 0;
            if (factGraphs.Count > 0)
            {
                lastFactId = graphsFromDb.Max(g => g.fact_id);
            }

            // Merge the graphs.
            var mergedGraph = FactGraph.Empty;
            foreach (var graph in factGraphs)
            {
                mergedGraph = mergedGraph.Merge(graph);
            }

            // Return the graphs and the next bookmark.
            logger.LogTrace("SQLite read {count} queued graphs", factGraphs.Count);
            return Task.FromResult(new QueuedFacts(mergedGraph, lastFactId.ToString()));
        }

        private FactGraph DeserializeFactGraph(string json)
        {
            var envelopes = new List<FactEnvelope>();
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    var factElement = element.GetProperty("fact");
                    var fields = DeserializeFields(factElement.GetProperty("fields"));
                    var predecessors = DeserializePredecessors(factElement.GetProperty("predecessors"));
                    var fact = Fact.Create(factElement.GetProperty("type").GetString(), fields, predecessors);

                    var signatures = element.GetProperty("signatures")
                        .EnumerateArray()
                        .Select(s => new FactSignature(s.GetProperty("publicKey").GetString(), s.GetProperty("signature").GetString()))
                        .ToImmutableList();

                    envelopes.Add(new FactEnvelope(fact, signatures));
                }
            }

            var builder = new FactGraphBuilder();
            foreach (var envelope in envelopes)
            {
                builder.Add(envelope);
            }
            return builder.Build();
        }

        private ImmutableList<Field> DeserializeFields(JsonElement fieldsElement)
        {
            var fields = ImmutableList<Field>.Empty;
            foreach (var field in fieldsElement.EnumerateObject())
            {
                switch (field.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        fields = fields.Add(new Field(field.Name, new FieldValueString(field.Value.GetString())));
                        break;
                    case JsonValueKind.Number:
                        fields = fields.Add(new Field(field.Name, new FieldValueNumber(field.Value.GetDouble())));
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        fields = fields.Add(new Field(field.Name, new FieldValueBoolean(field.Value.GetBoolean())));
                        break;
                    case JsonValueKind.Null:
                        fields = fields.Add(new Field(field.Name, FieldValue.Null));
                        break;
                }
            }
            return fields;
        }

        private ImmutableList<Predecessor> DeserializePredecessors(JsonElement predecessorsElement)
        {
            var predecessors = ImmutableList<Predecessor>.Empty;
            foreach (var predecessor in predecessorsElement.EnumerateObject())
            {
                switch (predecessor.Value.ValueKind)
                {
                    case JsonValueKind.Object:
                        var hash = predecessor.Value.GetProperty("hash").GetString();
                        var type = predecessor.Value.GetProperty("type").GetString();
                        predecessors = predecessors.Add(new PredecessorSingle(predecessor.Name, new FactReference(type, hash)));
                        break;
                    case JsonValueKind.Array:
                        var factReferences = ImmutableList<FactReference>.Empty;
                        foreach (var factReference in predecessor.Value.EnumerateArray())
                        {
                            hash = factReference.GetProperty("hash").GetString();
                            type = factReference.GetProperty("type").GetString();
                            factReferences = factReferences.Add(new FactReference(type, hash));
                        }
                        predecessors = predecessors.Add(new PredecessorMultiple(predecessor.Name, factReferences));
                        break;
                }
            }
            return predecessors;
        }

        public Task SetQueueBookmark(string bookmark)
        {
            // Delete rows from the outbound_queue table where fact_id is less than or equal to the new bookmark.
            connFactory.WithTxn(
                (conn, id) =>
                {
                    string sql;

                    // Delete rows from the outbound_queue table where fact_id is less than or equal to the new bookmark.
                    if (int.TryParse(bookmark, out int lastFactId))
                    {
                        sql = $@"
                            DELETE FROM outbound_queue
                            WHERE fact_id <= {lastFactId}
                        ";
                        conn.ExecuteNonQuery(sql);
                    }

                    return 0;
                },
                true
            );

            return Task.CompletedTask;
        }

        public Task<IEnumerable<Fact>> GetAllFacts()
        {
            var factsFromDb = connFactory.WithConn(
                (conn, id) =>
                {
                    string sql;
                    sql = $@"
                        SELECT f.fact_id, f.hash, f.data, t.name, p.public_key, s.signature
                        FROM fact f
                        JOIN fact_type t
                            ON f.fact_type_id = t.fact_type_id
                        LEFT JOIN signature s
                            ON s.fact_id = f.fact_id
                        LEFT JOIN public_key p
                            ON p.public_key_id = s.public_key_id
                    ";
                    return conn.ExecuteQuery<FactWithIdAndSignatureFromDb>(sql);
                },
                true
            );

            var envelopes = factsFromDb.Deserialize();
            var facts = envelopes.Select(envelope => envelope.Fact);
            return Task.FromResult(facts);
        }

        public Task Purge(ImmutableList<Specification> purgeConditions)
        {
            foreach (var specification in purgeConditions)
            {
                var label = specification.Givens.Single().Label;
                var givenTuple = FactReferenceTuple.Empty
                    .Add(label.Name, new FactReference(label.Type, "xxxx"));
                var factTypes = LoadFactTypesFromSpecification(specification);
                var factTypeMap = factTypes.Select(factType => KeyValuePair.Create(factType.name, factType.fact_type_id)).ToImmutableDictionary();
                
                var roles = LoadRolesFromSpecification(specification, factTypes);
                var roleMap = roles
                    .GroupBy(
                        role => role.defining_fact_type_id, 
                        role => KeyValuePair.Create(role.name, role.role_id)
                    )
                    .Select(
                        pair => KeyValuePair.Create(pair.Key, pair.ToImmutableDictionary())
                    )
                    .ToImmutableDictionary();                                    

                var descriptionBuilder = new ResultDescriptionBuilder(factTypeMap, roleMap);
                
                var description = descriptionBuilder.Build(givenTuple, specification);

                if (!description.QueryDescription.IsSatisfiable())
                {
                    continue;
                }

                (string sql, ImmutableList<object> parameters) = PurgeSqlFromSpecification(description);
                connFactory.WithConn((conn, i) =>
                {
                    return conn.ExecuteNonQuery(sql, parameters.ToArray());
                });
            }
            return Task.CompletedTask;
        }

        private (string sql, ImmutableList<object> parameters) PurgeSqlFromSpecification(ResultDescription description)
        {
            var queryDescription = description.QueryDescription;
            if (queryDescription.ExistentialConditions.Count > 0)
            {
                throw new ArgumentException("Purge conditions should not have existential conditions");
            }

            var columns = queryDescription.Outputs
                .Select((label, index) => $"f{label.FactIndex}.fact_id as trigger{index + 1}")
                .Join(", ");
            var firstEdge = queryDescription.Edges.First();
            var predecessorInput = queryDescription.Inputs.Find(input => input.FactIndex == firstEdge.PredecessorFactIndex);
            var successorInput = queryDescription.Inputs.Find(input => input.FactIndex == firstEdge.SuccessorFactIndex);
            var firstFactIndex = predecessorInput != null ? predecessorInput.FactIndex : successorInput.FactIndex;
            var writtenFactIndexes = new HashSet<int> { firstFactIndex };
            var joins = GenerateJoins(queryDescription.Edges, writtenFactIndexes);
            var inputWhereClauses = queryDescription.Inputs
                .Select(input => $"f{input.FactIndex}.fact_type_id = ?{input.FactTypeParameter}")
                .Join(" AND ");

            var triggerWhereClauses = queryDescription.Outputs
                .Select((label, index) => $"a.fact_id = c2.trigger{index + 1}")
                .Join("\n            OR ");
            var triggerAncestorClauses = queryDescription.Outputs
                .Select((label, index) =>
                    $"    AND NOT EXISTS (\n" +
                    $"        SELECT 1\n" +
                    $"        FROM candidates c2\n" +
                    $"        JOIN ancestor a2\n" +
                    $"            ON a2.fact_id = c2.trigger{index + 1}\n" +
                    $"        WHERE a.fact_id = a2.ancestor_fact_id\n" +
                    $"    )\n"
                )
                .Join("");

            var sql = $@"
WITH candidates AS (
    SELECT
        f{firstFactIndex}.fact_id as purge_root,
        {columns}
    FROM fact f{firstFactIndex}
    {joins.Join("")}
    WHERE {inputWhereClauses}
), targets AS (
    SELECT a.fact_id
    FROM ancestor a
    JOIN candidates c ON c.purge_root = a.ancestor_fact_id
    WHERE NOT EXISTS (
        SELECT 1
        FROM candidates c2
        WHERE {triggerWhereClauses}
    )
    {triggerAncestorClauses}
)
DELETE
FROM fact
WHERE fact_id IN (SELECT fact_id FROM targets);";
            var parameters = queryDescription.Parameters.RemoveAt(1);

            return (sql, parameters);
        }

        private static ImmutableList<string> GenerateJoins(ImmutableList<EdgeDescription> edges, HashSet<int> writtenFactIndexes)
        {
            var joins = ImmutableList<string>.Empty;
            var remainingEdges = edges;
            while (remainingEdges.Count > 0)
            {
                // Find an edge for which either a predecessor or successor fact has been written.
                var edgeIndex = remainingEdges.FindIndex(edge =>
                    writtenFactIndexes.Contains(edge.PredecessorFactIndex) ||
                    writtenFactIndexes.Contains(edge.SuccessorFactIndex)
                );

                // If no such edge exists, then the graph is not connected.
                if (edgeIndex < 0)
                {
                    throw new ArgumentException("The specification is not connected");
                }

                // Write the edge.
                var edge = remainingEdges[edgeIndex];
                remainingEdges = remainingEdges.RemoveAt(edgeIndex);

                if (writtenFactIndexes.Contains(edge.PredecessorFactIndex))
                {
                    if (writtenFactIndexes.Contains(edge.SuccessorFactIndex))
                    {
                        joins = joins.Add(
                            $" JOIN edge e{edge.EdgeIndex}" +
                            $" ON e{edge.EdgeIndex}.predecessor_fact_id = f{edge.PredecessorFactIndex}.fact_id" +
                            $" AND e{edge.EdgeIndex}.successor_fact_id = f{edge.SuccessorFactIndex}.fact_id" +
                            $" AND e{edge.EdgeIndex}.role_id = ?{edge.RoleParameter - 1}"
                        );
                    }
                    else
                    {
                        joins = joins.Add(
                            $" JOIN edge e{edge.EdgeIndex}" +
                            $" ON e{edge.EdgeIndex}.predecessor_fact_id = f{edge.PredecessorFactIndex}.fact_id" +
                            $" AND e{edge.EdgeIndex}.role_id = ?{edge.RoleParameter - 1}"
                        );
                        joins = joins.Add(
                            $" JOIN fact f{edge.SuccessorFactIndex}" +
                            $" ON f{edge.SuccessorFactIndex}.fact_id = e{edge.EdgeIndex}.successor_fact_id"
                        );
                        writtenFactIndexes.Add(edge.SuccessorFactIndex);
                    }
                }
                else if (writtenFactIndexes.Contains(edge.SuccessorFactIndex))
                {
                    joins = joins.Add(
                        $" JOIN edge e{edge.EdgeIndex}" +
                        $" ON e{edge.EdgeIndex}.successor_fact_id = f{edge.SuccessorFactIndex}.fact_id" +
                        $" AND e{edge.EdgeIndex}.role_id = ?{edge.RoleParameter - 1}"
                    );
                    joins = joins.Add(
                        $" JOIN fact f{edge.PredecessorFactIndex}" +
                        $" ON f{edge.PredecessorFactIndex}.fact_id = e{edge.EdgeIndex}.predecessor_fact_id"
                    );
                    writtenFactIndexes.Add(edge.PredecessorFactIndex);
                }
                else
                {
                    throw new ArgumentException("Neither predecessor nor successor fact has been written");
                }
            }
            return joins;
        }

        public Task PurgeDescendants(FactReference purgeRoot, ImmutableList<FactReference> triggers)
        {
            var factTypes = LoadFactTypesFromReferences(new[] { purgeRoot }.Concat(triggers).ToImmutableList())
                .ToImmutableDictionary(ft => ft.name, ft => ft.fact_type_id);
            if (!factTypes.ContainsKey(purgeRoot.Type) || triggers.Any(t => !factTypes.ContainsKey(t.Type)))
            {
                return Task.CompletedTask;
            }

            var parameters = new List<object>
            {
                factTypes[purgeRoot.Type],
                purgeRoot.Hash
            };
            parameters.AddRange(triggers.SelectMany(t => new object[] { factTypes[t.Type], t.Hash }));

            var purgeCommand = PurgeDescendantsSql(triggers.Count);

            connFactory.WithTxn(
                (conn, id) =>
                {
                    conn.ExecuteNonQuery(purgeCommand, parameters.ToArray());
                    return 0;
                },
                true
            );

            return Task.CompletedTask;
        }

        private string PurgeDescendantsSql(int triggerCount)
        {
            var whereClause = "    WHERE (t.fact_type_id = ?3 AND t.hash = ?4)\n";
            for (int i = 1; i < triggerCount; i++)
            {
                whereClause += $"        OR (t.fact_type_id = ?{i * 2 + 3} AND t.hash = ?{i * 2 + 4})\n";
            }

            var sql =
                "WITH purge_root AS (\n" +
                "    SELECT pr.fact_id\n" +
                "    FROM fact pr\n" +
                "    WHERE pr.fact_type_id = ?1\n" +
                "        AND pr.hash = ?2\n" +
                "), triggers AS (\n" +
                "    SELECT t.fact_id\n" +
                "    FROM fact t\n" +
                whereClause +
                "), triggers_and_ancestors AS (\n" +
                "    SELECT t.fact_id\n" +
                "    FROM triggers t\n" +
                "    UNION\n" +
                "    SELECT a.ancestor_fact_id\n" +
                "    FROM ancestor a\n" +
                "    JOIN triggers t\n" +
                "        ON a.fact_id = t.fact_id\n" +
                "), targets AS (\n" +
                "    SELECT a.fact_id\n" +
                "    FROM ancestor a\n" +
                "    JOIN purge_root pr\n" +
                "        ON a.ancestor_fact_id = pr.fact_id\n" +
                "    WHERE a.fact_id NOT IN (SELECT * FROM triggers_and_ancestors)\n" +
                ")\n" +
                "DELETE\n" +
                "FROM fact\n" +
                "WHERE fact_id IN (SELECT fact_id FROM targets)\n";
            return sql;
        }
    }
}
