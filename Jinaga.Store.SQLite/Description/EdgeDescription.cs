namespace Jinaga.Store.SQLite.Description
{
    internal class EdgeDescription
    {
        public int EdgeIndex { get; }
        public int PredecessorFactIndex { get; }
        public int SuccessorFactIndex { get; }
        public int RoleParameter { get; }

        public EdgeDescription(int edgeIndex, int predecessorFactIndex, int successorFactIndex, int roleParameter)
        {
            EdgeIndex = edgeIndex;
            PredecessorFactIndex = predecessorFactIndex;
            SuccessorFactIndex = successorFactIndex;
            RoleParameter = roleParameter;
        }
    }
}