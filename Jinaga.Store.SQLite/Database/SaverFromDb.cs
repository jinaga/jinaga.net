namespace Jinaga.Store.SQLite.Database
{
    /// <summary>
    /// One row of the fact_saver relation: a fact, and the public key of a user
    /// this replica saw save it.
    /// </summary>
    internal class SaverFromDb
    {
        public string hash { get; set; } = "";
        public string name { get; set; } = "";
        public string public_key { get; set; } = "";
    }
}
