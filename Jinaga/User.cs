namespace Jinaga
{
    [FactType("Jinaga.User")]
    public class User
    {
        public string publicKey { get; }

        public User(string publicKey)
        {
            this.publicKey = publicKey;
        }

        public override bool Equals(object obj)
        {
            // Allow the object to inherit from this class.
            // This is necessary for the proxy to work.
            if (obj is User user)
            {
                return user.publicKey == this.publicKey;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return publicKey.GetHashCode();
        }

        public static bool operator ==(User a, User b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            if (a is null || b is null)
            {
                return false;
            }
            return a.Equals(b);
        }

        public static bool operator !=(User a, User b)
        {
            if (ReferenceEquals(a, b))
            {
                return false;
            }
            if (a is null || b is null)
            {
                return true;
            }
            return !a.Equals(b);
        }
    }
}