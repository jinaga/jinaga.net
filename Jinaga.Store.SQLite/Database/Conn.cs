using System.Collections.Generic;
using System.Collections.Immutable;

namespace Jinaga.Store.SQLite.Database
{
    internal class Conn
    {
        //TODO: Use prepared stmts (on startup or via caching ?) to increase performance.
        SQLitePCL.sqlite3 _db;
        string connLog = "";


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
            ConnectionFactory.myLog += connLog;
            if (r != SQLite.Result.OK)
            {
                throw SQLiteException.New(r, $"Close: {r} - {SQLite.GetErrmsg(_db)}");
            }
        }

    }
}
