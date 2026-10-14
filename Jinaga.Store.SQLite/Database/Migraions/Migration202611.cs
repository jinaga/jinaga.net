namespace Jinaga.Store.SQLite.Database.Migrations
{
    internal static class Migration202611
    {
        /// <summary>
        /// Creates the table that records which users this replica saw save each fact.
        ///
        /// The outbound queue is one log shared by everybody who logs in, so sending
        /// it needs to know which facts the user who is logged in now may send.
        /// Nothing in the fact itself says: a fact carries no author, and a signature
        /// arrives only after a replicator has one. A row is written each time a
        /// logged-in user saves a fact with it queued, including a fact the store
        /// already holds, and is never serialized, so it stays on this replica.
        ///
        /// The key is the pair, so a fact may have several savers and records each
        /// once. The public key is named through the public_key table, which is where
        /// every other reference to a key resolves, so a key has one spelling in the
        /// database.
        /// </summary>
        public static void CreateFactSaverTable(Conn conn)
        {
            string table = @"CREATE TABLE IF NOT EXISTS main.fact_saver (
                                fact_id INTEGER NOT NULL,
                                public_key_id INTEGER NOT NULL,
                                PRIMARY KEY (fact_id, public_key_id)
                            )";
            conn.ExecuteNonQuery(table);
        }
    }
}
