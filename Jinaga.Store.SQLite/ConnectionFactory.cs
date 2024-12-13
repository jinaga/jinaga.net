using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Store.SQLite
{


    public sealed class MyStopWatch
    {
        //TODO: Remove this class here and inject it, together with a Logger, to be used during unit-testing
        private static Stopwatch stopwatch;

        private MyStopWatch() { }


        public static string Start()
        {
            if (stopwatch == null)
            {
                stopwatch = new Stopwatch();
            }
            stopwatch.Restart();
            return $"{TimeSpan.Zero:ss\\ fff} ms";
        }

        public static string Elapsed()
        {
            return $"{stopwatch.Elapsed:ss\\ fff} ms";
        }

        public static long ElapsedMilliSeconds()
        {
            return stopwatch.ElapsedMilliseconds;
        }

    }


    internal class ConnectionFactory
    {                   
        bool _dbExist = false;
        string _dbFullName;
        public static string myLog;

        public ConnectionFactory(string dbFullName )
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


        public class Conn
        {
            //TODO: Use prepared stmts (on startup or via caching ?) to increase performance.
            SQLitePCL.sqlite3 _db;
            String connLog = "";


            public Conn(string connectionString, int id)
            {
                var r = SQLite.Open(connectionString, out _db);
                if (r != SQLite.Result.OK)
                {
                    throw SQLiteException.New(r, $"Conn: {r}");               
                }
                
                //TODO: Activate tracing when injected logger requires it
                //Sqlite3.sqlite3_trace(
                //    _db,
                //    (_, statement) => connLog += $"{MyStopWatch.Elapsed()}: {id:D2} -- { statement}\n\r",
                //    null);

                //This will autoRetry for 10ms upon busy/locked, for this connection only
                r = SQLite.BusyTimeout(_db, 10);
                if (r != SQLite.Result.OK)
                {
                    throw SQLiteException.New(r, $"Conn/2: {r} - {SQLite.GetErrmsg(_db)}");
                }
            }


            public int ExecuteNonQuery(string sql, params object[] parameters)
            {
                var stmt = SQLite.Prepare2(_db, sql);
                try
                {
                    int index = 1;
                    foreach (object parameter in parameters)
                    {
                        SQLite.BindParameter(stmt, index, parameter, true, "", true);
                        index++;
                    };
                    var r = SQLite.Step(stmt);
                    if (r == SQLite.Result.Done)
                    {
                        return SQLite.Changes(_db);
                    }
                    throw SQLiteException.New(r, $"ExecuteNonQuery/1: {r} - {SQLite.GetErrmsg(_db)}");
                }
                finally
                {
                    var r2 = SQLite.Finalize(stmt);
                    if (r2 != SQLite.Result.OK)
                    {
                        throw SQLiteException.New(r2, $"ExecuteNonQuery/2: {r2} - {SQLite.GetErrmsg(_db)}");
                    }
                }
            }


            public string ExecuteScalar(string sql, params object[] parameters)
            {
                var stmt = SQLite.Prepare2(_db, sql);
                string result = "";
                try
                {
                    int index = 1;
                    foreach (object parameter in parameters)
                    {
                        SQLite.BindParameter(stmt, index, parameter, true, "", true);
                        index++;
                    };
                    var r = SQLite.Step(stmt);
                    while (r == SQLite.Result.Row)
                    {
                        result = SQLite.ColumnText(stmt, 0);
                        r = SQLite.Step(stmt);
                    }
                    if (r == SQLite.Result.Done)
                    {
                        return result;
                    }
                    throw SQLiteException.New(r, $"ExecuteScalar<T>/1: {r} - {SQLite.GetErrmsg(_db)}");
                }
                finally
                {
                    var r2 = SQLite.Finalize(stmt);
                    if (r2 != SQLite.Result.OK)
                    {
                        throw SQLiteException.New(r2, $"ExecuteScalar<T>/2: {r2} - {SQLite.GetErrmsg(_db)}");
                    }
                }
            }


            public IEnumerable<T> ExecuteQuery<T>(string sql, params object[] parameters) where T : class, new()
            {
                IList<T> result = new List<T>();
                var stmt = SQLite.Prepare2(_db, sql);
                try
                {
                    int index = 1;
                    foreach (object parameter in parameters)
                    {
                        SQLite.BindParameter(stmt, index, parameter, true, "", true);
                        index++;
                    };

                    var r = SQLite.Step(stmt);
                    while (r == SQLite.Result.Row)
                    {
                        //yield return stmt.row<T>();
                        result.Add(stmt.row<T>());
                        r = SQLite.Step(stmt);
                    }
                    if (r == SQLite.Result.Done)
                    {
                        return result;
                    }
                    throw SQLiteException.New(r, $"ExecuteQuery<T>/1: {r} - {SQLite.GetErrmsg(_db)}");                  
                }
                finally
                {
                    var r2 = SQLite.Finalize(stmt);
                    if (r2 != SQLite.Result.OK)
                    {
                        throw SQLiteException.New(r2, $"ExecuteQuery<T>/2: {r2} - {SQLite.GetErrmsg(_db)}");
                    }
                }
            }


            public IEnumerable<ImmutableDictionary<string, string>> ExecuteQueryRaw(string sql, params object[] parameters)
            {
                var result = new List<ImmutableDictionary<string, string>>();
                var stmt = SQLite.Prepare2(_db, sql);
                try
                {
                    int index = 1;
                    foreach (object parameter in parameters)
                    {
                        SQLite.BindParameter(stmt, index, parameter, true, "", true);
                        index++;
                    };

                    var r = SQLite.Step(stmt);
                    while (r == SQLite.Result.Row)
                    {
                        //yield return stmt.row<T>();
                        result.Add(stmt.rawRow());
                        r = SQLite.Step(stmt);
                    }
                    if (r == SQLite.Result.Done)
                    {
                        return result;
                    }
                    throw SQLiteException.New(r, $"ExecuteQueryRaw/1: {r} - {SQLite.GetErrmsg(_db)}");
                }
                finally
                {
                    var r2 = SQLite.Finalize(stmt);
                    if (r2 != SQLite.Result.OK)
                    {
                        throw SQLiteException.New(r2, $"ExecuteQueryRaw/2: {r2} - {SQLite.GetErrmsg(_db)}");
                    }
                }
            }


            public void Close()
            {               
                var r = SQLite.Close(_db);                
                myLog += connLog;
                if (r != SQLite.Result.OK)
                {
                    throw SQLiteException.New(r, $"Close: {r} - {SQLite.GetErrmsg(_db)}");
                }       
            }

        }


        //TODO: Not sure we will still need this.  We will probably create tasks/threads on j.Fact and j.Query level
        public async Task<T> WithTxnAsync<T>(Func<Conn, int, T> callback, bool enableBackoff = true, int id = 0)
        {
            Func<T> dbOp = () =>
            {
                var result = WithTxn(callback, enableBackoff, id);
                return result;
            };
            return await Task<T>.Run(dbOp).ConfigureAwait(false);
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


        public T WithConn<T>(Func<Conn,int, T> callback, bool enableBackoff = true, int id = 0)
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

            // Add a column to the fact table called 'queued'.
            // This is used to determine whether the fact should be sent to the server.
            // Default all existing rows to true.
            string sql = @"PRAGMA table_info(fact)";
            var columns = conn.ExecuteQueryRaw(sql);
            if (!columns.Any(column => column["name"] == "queued"))
            {
                sql = @"ALTER TABLE fact ADD COLUMN queued INTEGER NOT NULL DEFAULT 1 CHECK(queued IN (0, 1))";
                conn.ExecuteNonQuery(sql);
            }


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
            sql = @"SELECT name FROM sqlite_master WHERE type='table' AND name='signature';";
            var tableExists = conn.ExecuteQueryRaw(sql).Any();

            if (tableExists)
            {
                sql = @"PRAGMA table_info(signature)";
                columns = conn.ExecuteQueryRaw(sql);
                if (!columns.Any(column => column["name"] == "signature"))
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
            columns = conn.ExecuteQueryRaw(sql);
            if (columns.Any(column => column["name"] == "queued"))
            {
                // Retrieve the current bookmark from the queue_bookmark table.
                string bookmarkSql = @"SELECT bookmark FROM queue_bookmark WHERE replicator = 'primary'";
                string bookmark = conn.ExecuteScalar(bookmarkSql);
                if (!int.TryParse(bookmark, out int lastFactId))
                    lastFactId = 0;

                // Copy queued facts to the outbound_queue table where fact_id is greater than the bookmark.
                string copySql = $@"
                    INSERT INTO outbound_queue (fact_id, fact_type_id, hash, data, date_learned)
                    SELECT fact_id, fact_type_id, hash, data, date_learned
                    FROM fact
                    WHERE queued = 1 AND fact_id > {lastFactId}
                ";
                conn.ExecuteNonQuery(copySql);

                // Remove the queued column from the fact table.
                sql = @"ALTER TABLE fact DROP COLUMN queued";
                conn.ExecuteNonQuery(sql);

                // Drop the queue_bookmark table as it is no longer needed.
                sql = @"DROP TABLE IF EXISTS queue_bookmark";
                conn.ExecuteNonQuery(sql);
            }
        }
    }
}
