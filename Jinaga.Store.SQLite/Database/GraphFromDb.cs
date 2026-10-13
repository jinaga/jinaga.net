namespace Jinaga.Store.SQLite.Database
{
    /// <summary>
    /// One row of the outbound queue: the graph of the queued fact and its
    /// ancestors, and the public key of the user who authored it on this
    /// replica. The author is null for a fact queued while nobody was logged in.
    /// </summary>
    internal class GraphFromDb
    {
        public int fact_id { get; set; }
        public string graph_data { get; set; }
        public string author { get; set; }
    }
}
