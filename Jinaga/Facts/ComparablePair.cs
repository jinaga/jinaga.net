using System;
namespace Jinaga.Facts
{
    internal static class ComparablePair
    {
        public static ComparablePair<TKey, TValue> From<TKey, TValue>(TKey key, TValue value)
        {
            return new ComparablePair<TKey, TValue>(key, value);
        }
    }
    internal class ComparablePair<TKey, TValue>
    {
        private TKey key;
        private TValue value;

        public ComparablePair(TKey key, TValue value)
        {
            this.key = key;
            this.value = value;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var that = (ComparablePair<TKey, TValue>)obj;
            return
                AreEqual(this.key, that.key) &&
                AreEqual(this.value, that.value);

            static bool AreEqual<T>(T a, T b)
            {
                return
                    a != null && b != null ? a.Equals(b) :
                    a == null && b == null;
            }
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(key, value);
        }
    }
}