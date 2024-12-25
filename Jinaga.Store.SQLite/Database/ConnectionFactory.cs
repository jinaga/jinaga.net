using System;
using System.Threading;
using System.Threading.Tasks;

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
                        Migrations.Migration202412.CreateDb(conn);
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
    }
}
