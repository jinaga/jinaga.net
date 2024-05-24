using System;

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

        public override bool Equals(object obj)
        {
            return obj is FactSignature signature &&
                   PublicKey == signature.PublicKey &&
                   Signature == signature.Signature;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PublicKey, Signature);
        }
    }
}