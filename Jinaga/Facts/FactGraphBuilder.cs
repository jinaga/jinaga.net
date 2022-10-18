namespace Jinaga.Facts
{
    public class FactGraphBuilder
    {
        private FactGraph factGraph = FactGraph.Empty;

        public void Add(Fact fact)
        {
            factGraph = factGraph.Add(fact);
        }
        
        public FactGraph Build()
        {
            return factGraph;
        }
    }
}
