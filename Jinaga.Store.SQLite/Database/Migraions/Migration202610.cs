namespace Jinaga.Store.SQLite.Database.Migrations
{
    internal static class Migration202610
    {
        // Each fragment is a WHERE clause over the table it names, so the same
        // definition of "orphaned" serves both the probe and the delete.
        private const string EdgeIsOrphaned = @"
                NOT EXISTS (
                    SELECT 1 FROM fact f WHERE f.fact_id = edge.successor_fact_id
                )
                OR NOT EXISTS (
                    SELECT 1 FROM fact f WHERE f.fact_id = edge.predecessor_fact_id
                )";

        private const string AncestorIsOrphaned = @"
                NOT EXISTS (
                    SELECT 1 FROM fact f WHERE f.fact_id = ancestor.fact_id
                )
                OR NOT EXISTS (
                    SELECT 1 FROM fact f WHERE f.fact_id = ancestor.ancestor_fact_id
                )";

        private const string SignatureIsOrphaned = @"
                NOT EXISTS (
                    SELECT 1 FROM fact f WHERE f.fact_id = signature.fact_id
                )";

        /// <summary>
        /// Deletes the edge, ancestor and signature rows whose fact is gone.
        ///
        /// Purges before this migration deleted only the fact row. The rows derived
        /// from the fact stayed behind, and fact_id is a PRIMARY KEY without
        /// AUTOINCREMENT, so SQLite hands the id of a deleted fact to the next fact
        /// saved. That fact inherits the orphans. Removing them is the only way a
        /// database written by an earlier version stops attaching them.
        /// </summary>
        public static void DeleteOrphanedRows(Conn conn)
        {
            // Look before writing. Every store that opens this database runs this,
            // and the caller runs it without the exponential backoff, so a database
            // with nothing to clean up must not reach for the write lock.
            var probe = $@"
                SELECT EXISTS (SELECT 1 FROM edge WHERE {EdgeIsOrphaned})
                    OR EXISTS (SELECT 1 FROM ancestor WHERE {AncestorIsOrphaned})
                    OR EXISTS (SELECT 1 FROM signature WHERE {SignatureIsOrphaned})";
            if (conn.ExecuteScalar(probe) != "1")
            {
                return;
            }

            conn.ExecuteNonQuery($"DELETE FROM edge WHERE {EdgeIsOrphaned}");
            conn.ExecuteNonQuery($"DELETE FROM ancestor WHERE {AncestorIsOrphaned}");
            conn.ExecuteNonQuery($"DELETE FROM signature WHERE {SignatureIsOrphaned}");
        }
    }
}
