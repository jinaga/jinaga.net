using System.Linq;

namespace Jinaga.Store.SQLite.Database.Migrations
{
    internal static class Migration202405
    {
        public static void CreateDb(Conn conn)
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

            //QueueBookmark
            table = @"CREATE TABLE IF NOT EXISTS main.queue_bookmark (
                                replicator TEXT NOT NULL PRIMARY KEY,
                                bookmark TEXT                           
                            )";
            conn.ExecuteNonQuery(table);


        }
    }
}