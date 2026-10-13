namespace Jinaga.Store.SQLite.Database.Migrations
{
    internal static class Migration202611
    {
        /// <summary>
        /// Creates the table that records who authored a fact on this replica.
        ///
        /// The outbound queue is one log shared by everybody who logs in, so sending
        /// it needs to know which facts belong to the user who is logged in now.
        /// Nothing in the fact itself says: a fact carries no author, and a signature
        /// arrives only after a replicator has one. The record is written when the
        /// fact is queued and is never serialized, so it stays on this replica.
        ///
        /// fact_id is the primary key, so a fact has at most one local author. The
        /// public key is named through the public_key table, which is where every
        /// other reference to a key resolves, so a key has one spelling in the
        /// database.
        /// </summary>
        public static void CreateFactAuthorTable(Conn conn)
        {
            string table = @"CREATE TABLE IF NOT EXISTS main.fact_author (
                                fact_id INTEGER NOT NULL PRIMARY KEY,
                                public_key_id INTEGER NOT NULL
                            )";
            conn.ExecuteNonQuery(table);
        }
    }
}
