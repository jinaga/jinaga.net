namespace Jinaga.Facts
{
    public abstract class Predecessor
    {
        protected Predecessor(string role)
        {
            Role = role;
        }

        public string Role { get; }
    }

    public class PredecessorSingle : Predecessor
    {
        public PredecessorSingle(string role, FactReference reference) :
            base(role)
        {
            Reference = reference;
        }

        public FactReference Reference { get; }
    }
}