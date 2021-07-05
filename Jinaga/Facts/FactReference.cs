using System;

namespace Jinaga.Facts
{
    public class FactReference
    {
        public FactReference(string type, string hash)
        {
            Type = type;
            Hash = hash;
        }

        public string Type { get; }
        public string Hash { get; }

        public override string ToString()
        {
            return $"{Type}: {Hash}";
        }

        public override bool Equals(object obj)
        {
            return obj is FactReference reference &&
                   Type == reference.Type &&
                   Hash == reference.Hash;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Hash);
        }

        public static bool operator ==(FactReference a, FactReference b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(FactReference a, FactReference b)
        {
            return !a.Equals(b);
        }
    }
}