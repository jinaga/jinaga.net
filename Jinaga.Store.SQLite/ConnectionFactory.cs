using SQLite;
using System;
using System.Threading.Tasks;


namespace Jinaga.Store.SQLite
{

    internal class ConnectionFactory
    {

        string _connectionString;
        bool _dbExist = false;


        public ConnectionFactory(string dbFullName)
        {
            _connectionString = dbFullName;
        }


        public async Task<T> WithTransaction<T>(Func<SQLiteConnection, T> callback, bool enableBackoff = true)
        {
            return await WithConnection(async (asyncConn) =>
                {
                    T result = default(T);
                    void outerCallback(SQLiteConnection conn)
                    {
                        result = callback(conn);
                    }
                    await asyncConn.RunInTransactionAsync(outerCallback);
                    return result;
                },
                enableBackoff
            );
        }


        public async Task<T> WithConnection<T>(Func<SQLiteAsyncConnection, Task<T>> callback, bool enableBackoff = true)
        {
            Func<Task<T>> dbOp = async () =>
            {
                SQLiteAsyncConnection asyncConn = new SQLiteAsyncConnection(_connectionString);
                try
                {
                    if (!_dbExist)
                    {
                        await CreateDb(asyncConn);
                        _dbExist = true;
                    }
                    return await callback(asyncConn);
                }
                finally
                {
                   await asyncConn.CloseAsync();
                   // This "CloseAsync()" seems to close the wrong connection.
                   // Is SQLite-net-pcl mixing-up connections ?
                   // If we don't close the connection, then no error.
                }
            };

            if (enableBackoff)
            {
                return await WithExponentialBackoff(dbOp);
            }
            else
            {
                return await dbOp();
            }
        }


        private async Task<T> WithExponentialBackoff<T>(Func<Task<T>> callback)
        {
            int attempt = 0;
            int[] pause = { 0, 1000, 5000, 15000, 30000 };
            while (attempt <= pause.Length)
            {
                try
                {
                    return await callback();
                }
                catch (Exception e)
                {
                    if (attempt >= pause.Length)
                    {
                        throw (e);
                    }
                    else
                    {
                        await Task.Delay(pause[attempt]);
                    }
                    attempt++;
                }
            }
            throw new Exception("Code should never reach here, but this line is required to avoid compiler error 'Not all code paths return a value'");
        }


        private async Task CreateDb(SQLiteAsyncConnection asyncConn)
        {
            string table;

            //Fact Type
            table = @"CREATE TABLE IF NOT EXISTS main.fact_type (
                        fact_type_id INTEGER NOT NULL PRIMARY KEY,
                        name TEXT NOT NULL                      
                    )";
            string ux_fact_type = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_fact_type ON fact_type (name)";
            await asyncConn.ExecuteAsync(table);
            await asyncConn.ExecuteAsync(ux_fact_type);


            //Role
            table = @"CREATE TABLE IF NOT EXISTS main.role (
                        role_id INTEGER NOT NULL PRIMARY KEY,
                        defining_fact_type_id INTEGER NOT NULL,
                        name TEXT NOT NULL
                    )";
            string ux_role = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_role ON role (defining_fact_type_id, name)";
            await asyncConn.ExecuteAsync(table);
            await asyncConn.ExecuteAsync(ux_role);


            //Fact
            table = @"CREATE TABLE IF NOT EXISTS main.fact (
                        fact_id INTEGER NOT NULL PRIMARY KEY,
                        fact_type_ID INTEGER NOT NULL,
                        hash TEXT,
                        data TEXT,
                        date_learned TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP                         
                    )";
            string ux_fact = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_fact ON fact (hash, fact_type_id)";
            await asyncConn.ExecuteAsync(table);
            await asyncConn.ExecuteAsync(ux_fact);


            //Edge
            table = @"CREATE TABLE IF NOT EXISTS main.edge (
                        role_id INTEGER NOT NULL,
                        successor_fact_id INTEGER NOT NULL,
                        predecessor_fact_id INTEGER NOT NULL     
                    )";
            string ux_edge = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_edge ON edge (successor_fact_id, predecessor_fact_id, role_id)";
            string ix_successor = @"CREATE INDEX IF NOT EXISTS ix_successor ON edge (successor_fact_id, role_id, predecessor_fact_id)";
            string ix_predecessor = @"CREATE INDEX IF NOT EXISTS ix_predecessor ON edge (predecessor_fact_id, role_id, successor_fact_id)";
            await asyncConn.ExecuteAsync(table);
            await asyncConn.ExecuteAsync(ux_edge);
            await asyncConn.ExecuteAsync(ix_successor);
            await asyncConn.ExecuteAsync(ix_predecessor);


            //Ancestor
            table = @"CREATE TABLE IF NOT EXISTS main.ancestor (
                        fact_id INTEGER NOT NULL,
                        ancestor_fact_id INTEGER NOT NULL                        
                    )";
            string ux_ancestor = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_ancestor ON ancestor (fact_id, ancestor_fact_id)";
            await asyncConn.ExecuteAsync(table);
            await asyncConn.ExecuteAsync(ux_ancestor);


            //PublicKey
            table = @"CREATE TABLE IF NOT EXISTS main.public_key (
                        public_key_id INTEGER NOT NULL PRIMARY KEY,
                        public_key TEXT NOT NULL     
                    )";
            string ux_public_key = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_public_key ON public_key (public_key)";
            await asyncConn.ExecuteAsync(table);
            await asyncConn.ExecuteAsync(ux_public_key);


            //Signature
            table = @"CREATE TABLE IF NOT EXISTS main.signature (
                        fact_id INTEGER NOT NULL,
                        public_key_id INTEGER NOT NULL,
                        date_learned TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP     
                    )";
            string ux_signature = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_signature ON signature (fact_id, public_key_id)";
            await asyncConn.ExecuteAsync(table);
            await asyncConn.ExecuteAsync(ux_signature);


            //User
            table = @"CREATE TABLE IF NOT EXISTS main.user (
                        provider TEXT,
                        user_identifier TEXT,
                        private_key TEXT,
                        public_key TEXT
                    )";
            string ux_user = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_user ON user (user_identifier, provider)";
            string ux_user_public_key = @"CREATE UNIQUE INDEX IF NOT EXISTS ux_user_public_key ON user (public_key)";
            await asyncConn.ExecuteAsync(table);
            await asyncConn.ExecuteAsync(ux_user);
            await asyncConn.ExecuteAsync(ux_user_public_key);
        }

    }
}
