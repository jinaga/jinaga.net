namespace Jinaga.Store.SQLite.Database
{
    internal class FactWithIdAndSignatureFromDb : FactWithIdFromDb
    {
        public string public_key { get; set; }
        public string signature { get; set; }
    }
}
