using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Services;

namespace Jinaga.Store.SQLite.Database
{
    internal class ConnectionFactory
    {
        bool _dbExist = false;
        string _dbFullName;
        public static string myLog;

        public ConnectionFactory(string dbFullName)
        {
            SQLite.InitLib();
            _dbFullName = dbFullName;
            if (!_dbExist)
            {
                WithConn((conn, id) =>
                    {
                        EnableWalMode(conn);
                        return 0;
                    },
                    false
                );
                WithTxn((conn, id) =>
                    {
                        CreateDb(conn);
                        return 0;
                    },
                    false
                );
                _dbExist = true;
            }
        }


        private void EnableWalMode(Conn conn)
        {
            string sql;
            sql = "PRAGMA journal_mode=WAL";
            conn.ExecuteScalar(sql);
        }


        //TODO: Not sure we will still need this.  We will probably create tasks/threads on j.Fact and j.Query level
        public async Task<T> WithTxnAsync<T>(Func<Conn, int, T> callback, bool enableBackoff = true, int id = 0)
        {
            Func<T> dbOp = () =>
            {
                var result = WithTxn(callback, enableBackoff, id);
                return result;
            };
            return await Task.Run(dbOp).ConfigureAwait(false);
            //return await new TaskFactory().StartNew(dbOp).ConfigureAwait(false);
            //return await new TaskFactory().StartNew(dbOp,TaskCreationOptions.LongRunning ).ConfigureAwait(false);
        }



        public T WithTxn<T>(Func<Conn, int, T> callback, bool enableBackoff = true, int id = 0)
        {
            Func<Conn, int, T> dbOp = (conn, id2) =>
            {
                try
                {
                    conn.ExecuteNonQuery("BEGIN TRANSACTION");
                    var result = callback(conn, id2);
                    conn.ExecuteNonQuery("END TRANSACTION");
                    return result;
                }
                catch
                {
                    conn.ExecuteNonQuery("ROLLBACK TRANSACTION");
                    //myLog += $"{MyStopWatch.Elapsed()}: {id:D2} -- {e.ToString()}\n\r";
                    throw;
                }
            };

            return WithConn(dbOp, enableBackoff, id);
        }


        public T WithConn<T>(Func<Conn, int, T> callback, bool enableBackoff = true, int id = 0)
        {
            Func<T> dbOp = () =>
           {
               Conn conn = new Conn(_dbFullName, id);
               try
               {
                   return callback(conn, id);
               }
               finally
               {
                   conn.Close();
               }
           };

            if (enableBackoff)
            {
                return WithExponentialBackoff(dbOp);
            }
            else
            {
                return dbOp();
            }
        }


        private T WithExponentialBackoff<T>(Func<T> callback)
        {
            int attempt = 0;
            int[] pause = { 0, 100, 200, 500, 1000, 2000, 5000 };
            while (attempt <= pause.Length)
            {
                try
                {
                    return callback();
                }
                catch
                {
                    if (attempt >= pause.Length)
                    {
                        throw;
                    }
                    else
                    {
                        Thread.Sleep(pause[attempt]);
                    }
                    attempt++;
                }
            }
            throw new Exception("Code should never reach here, but this line is required to avoid compiler error 'Not all code paths return a value'");
        }


        private void CreateDb(Conn conn)
        {
            //TODO: Add constraints

            string table;

            //Fact Type
            table = @"CREATE TABLE IF NOT EXISTS main.fact_type (
                                fact_type_id INTEGER NOT NULL PRIMARY KEY,
                                name TEXT NOT NULL                      
                            )";
            string ux_fact_type = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_fact_type ON fact_type (name)";
            conn.ExecuteNonQuery(table);
            conn.ExecuteNonQuery(ux_fact_type);


            //Role
            table = @"CREATE TABLE IF NOT EXISTS main.role (
                                role_id INTEGER NOT NULL PRIMARY KEY,
                                defining_fact_type_id INTEGER NOT NULL,
                                name TEXT NOT NULL
                            )";
            string ux_role = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_role ON role (defining_fact_type_id, name)";
            conn.ExecuteNonQuery(table);
            conn.ExecuteNonQuery(ux_role);


            //Fact
            table = @"CREATE TABLE IF NOT EXISTS main.fact (
                                fact_id INTEGER NOT NULL PRIMARY KEY,
                                fact_type_id INTEGER NOT NULL,
                                hash TEXT,
                                data TEXT,
                                date_learned TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                queued INTEGER NOT NULL DEFAULT 1 CHECK(queued IN (0, 1))
                            )";
            string ux_fact = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_fact ON fact (hash, fact_type_id)";
            conn.ExecuteNonQuery(table);
            conn.ExecuteNonQuery(ux_fact);


            //Edge
            table = @"CREATE TABLE IF NOT EXISTS main.edge (
                                role_id INTEGER NOT NULL,
                                successor_fact_id INTEGER NOT NULL,
                                predecessor_fact_id INTEGER NOT NULL     
                            )";
            string ux_edge = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_edge ON edge (successor_fact_id, predecessor_fact_id, role_id)";
            string ix_successor = @"CREATE INDEX IF NOT EXISTS ix_successor ON edge (successor_fact_id, role_id, predecessor_fact_id)";
            string ix_predecessor = @"CREATE INDEX IF NOT EXISTS ix_predecessor ON edge (predecessor_fact_id, role_id, successor_fact_id)";
            conn.ExecuteNonQuery(table);
            conn.ExecuteNonQuery(ux_edge);
            conn.ExecuteNonQuery(ix_successor);
            conn.ExecuteNonQuery(ix_predecessor);


            //Ancestor
            table = @"CREATE TABLE IF NOT EXISTS main.ancestor (
                                fact_id INTEGER NOT NULL,
                                ancestor_fact_id INTEGER NOT NULL                        
                            )";
            string ux_ancestor = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_ancestor ON ancestor (fact_id, ancestor_fact_id)";
            conn.ExecuteNonQuery(table);
            conn.ExecuteNonQuery(ux_ancestor);


            //PublicKey
            table = @"CREATE TABLE IF NOT EXISTS main.public_key (
                                public_key_id INTEGER NOT NULL PRIMARY KEY,
                                public_key TEXT NOT NULL     
                            )";
            string ux_public_key = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_public_key ON public_key (public_key)";
            conn.ExecuteNonQuery(table);
            conn.ExecuteNonQuery(ux_public_key);


            //Signature
            // An earlier version of the schema did not have the signature column.
            // If the signature column does not exist, drop the table and recreate it.
            var sql = @"SELECT name FROM sqlite_master WHERE type='table' AND name='signature';";
            var tableExists = conn.ExecuteQueryRaw(sql).Any();

            if (tableExists)
            {
                sql = @"PRAGMA table_info(signature)";
                var signatureColumns = conn.ExecuteQueryRaw(sql);
                if (!signatureColumns.Any(column => column["name"] == "signature"))
                {
                    sql = @"DROP TABLE signature";
                    conn.ExecuteNonQuery(sql);
                }
            }

            table = @"CREATE TABLE IF NOT EXISTS main.signature (
                                fact_id INTEGER NOT NULL,
                                public_key_id INTEGER NOT NULL,
                                signature TEXT NOT NULL,
                                date_learned TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP     
                            )";
            string ux_signature = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_signature ON signature (fact_id, public_key_id)";
            conn.ExecuteNonQuery(table);
            conn.ExecuteNonQuery(ux_signature);


            //User
            table = @"CREATE TABLE IF NOT EXISTS main.user (
                                provider TEXT,
                                user_identifier TEXT,
                                private_key TEXT,
                                public_key TEXT
                            )";
            string ux_user = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_user ON user (user_identifier, provider)";
            string ux_user_public_key = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_user_public_key ON user (public_key)";
            conn.ExecuteNonQuery(table);
            conn.ExecuteNonQuery(ux_user);
            conn.ExecuteNonQuery(ux_user_public_key);


            //Bookmark
            table = @"CREATE TABLE IF NOT EXISTS main.bookmark (
                                feed_hash TEXT NOT NULL PRIMARY KEY,
                                bookmark TEXT                           
                            )";
            conn.ExecuteNonQuery(table);


            //MruDate
            table = @"CREATE TABLE IF NOT EXISTS main.mru (
                                specification_hash TEXT NOT NULL PRIMARY KEY,
                                mru_date TEXT NOT NULL              
                            )";
            conn.ExecuteNonQuery(table);

            //OutboundQueue
            table = @"CREATE TABLE IF NOT EXISTS main.outbound_queue (
                            queue_id INTEGER NOT NULL PRIMARY KEY,
                            fact_id INTEGER NOT NULL,
                            graph_data TEXT NOT NULL
                        )";
            conn.ExecuteNonQuery(table);

            // Check if the queued column exists in the fact table.
            sql = @"PRAGMA table_info(fact)";
            var columns = conn.ExecuteQueryRaw(sql);
            bool queuedColumnExists = columns.Any(column => column["name"] == "queued");

            // Check if the queue_bookmark table exists.
            sql = @"SELECT name FROM sqlite_master WHERE type='table' AND name='queue_bookmark'";
            var queueBookmarkExists = conn.ExecuteQueryRaw(sql).Any();

            if (queuedColumnExists && queueBookmarkExists)
            {
                MigrateQueuedFactsToOutboundQueue(conn);
            }
            if (queuedColumnExists)
            {
                // Remove the queued column from the fact table.
                sql = @"ALTER TABLE fact DROP COLUMN queued";
                conn.ExecuteNonQuery(sql);
            }
            if (queueBookmarkExists)
            {
                // Drop the queue_bookmark table as it is no longer needed.
                sql = @"DROP TABLE IF EXISTS queue_bookmark";
                conn.ExecuteNonQuery(sql);
            }
        }

        private void MigrateQueuedFactsToOutboundQueue(Conn conn)
        {
            var queuedGraph = LoadFactGraphsFromQueueBookmark(conn);
            if (queuedGraph.Any())
            {
                SaveFactGraphsToOutboundQueue(conn, queuedGraph);
            }
        }

        private List<QueuedFacts> LoadFactGraphsFromQueueBookmark(Conn conn)
        {
            // Load the current bookmark from the bookmark table.
            string bookmark = WithConn(
                (conn, id) =>
                {
                    string sql;
                    sql = $@"
                        SELECT bookmark
                        FROM queue_bookmark
                        WHERE replicator = 'primary'
                    ";
                    return conn.ExecuteScalar(sql);
                },
                true
            );

            // Interpret the bookmark as a fact ID.
            if (!int.TryParse(bookmark, out int lastFactId))
                lastFactId = 0;

            // Load the facts from the fact table.
            var factsFromDb = WithConn(
                (conn, id) =>
                {
                    string sql;
                    sql = $@"
                        SELECT f.fact_id as bookmark, f.fact_id, f.hash, f.data, t.name, p.public_key, s.signature
                        FROM fact f
                        JOIN fact_type t
                            ON f.fact_type_id = t.fact_type_id
                        LEFT JOIN signature s
                            ON s.fact_id = f.fact_id
                        LEFT JOIN public_key p
                            ON p.public_key_id = s.public_key_id
                        WHERE f.fact_id > {lastFactId}
                            AND queued = 1

                        UNION

                        SELECT f1.fact_id as bookmark, f2.fact_id, f2.hash, f2.data, t2.name, p.public_key, s.signature
                        FROM fact f1
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
                        WHERE f1.fact_id > {lastFactId}
                            AND f1.queued = 1

                        ORDER BY 1, 2
                    ";
                    return conn.ExecuteQuery<FactWithBookmarkIdAndSignatureFromDb>(sql);
                },
                true
            );

            // Produce a graph for each bookmark.
            var graphs = new List<QueuedFacts>();
            FactGraphBuilder graphBuilder = null;
            int lastBookmark = 0;
            foreach (var fact in factsFromDb)
            {
                if (lastBookmark != fact.bookmark)
                {
                    if (graphBuilder != null)
                    {
                        graphs.Add(new QueuedFacts(graphBuilder.Build(), lastBookmark.ToString()));
                    }
                    graphBuilder = new FactGraphBuilder();
                    lastBookmark = fact.bookmark;
                }
                var envelope = Deserializer.LoadEnvelope(fact);
                graphBuilder.Add(envelope);
            }
            if (graphBuilder != null)
            {
                graphs.Add(new QueuedFacts(graphBuilder.Build(), lastBookmark.ToString()));
            }
            return graphs;
        }

        private void SaveFactGraphsToOutboundQueue(Conn conn, List<QueuedFacts> queuedGraph)
        {
            var sql = @"
                INSERT INTO outbound_queue (fact_id, graph_data) 
                VALUES (@0, @1)
            ";
            foreach (var graph in queuedGraph)
            {
                var graphData = graph.Graph.ToJson();
                conn.ExecuteNonQuery(sql, graph.NextBookmark, graphData);
            }
        }
    }
}
