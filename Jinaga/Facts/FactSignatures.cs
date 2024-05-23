namespace Jinaga.Facts
{
    public class FactSignature
    {
        public string PublicKey { get; }
        public string Signature { get; }

        public FactSignature(string publicKey, string signature)
        {
            PublicKey = publicKey;
            Signature = signature;
        }
    }
}