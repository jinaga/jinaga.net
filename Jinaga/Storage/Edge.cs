using Jinaga.Facts;

namespace Jinaga.Storage
{
    class Edge
    {
        public Edge(FactReference predecessor, string role, FactReference successor)
        {
            Predecessor = predecessor;
            Role = role;
            Successor = successor;
        }

        public FactReference Predecessor { get; }
        public string Role { get; }
        public FactReference Successor { get; }
    }
}