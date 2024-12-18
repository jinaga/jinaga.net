namespace Jinaga.Store.SQLite.Database
{
    internal class FactWithBookmarkIdAndSignatureFromDb : FactWithIdAndSignatureFromDb
    {
        public int bookmark { get; set; }
    }
}
