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
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var other = (User)obj;
            return other.publicKey == this.publicKey;
        }

        public override int GetHashCode()
        {
            return publicKey.GetHashCode();
        }
    }
}