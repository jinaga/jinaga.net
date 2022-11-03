using System.Collections.Generic;

namespace Jinaga.Records
{
    public class PredecessorSetMultiple : PredecessorSet
    {
        public List<FactReference> References { get; set; } = new List<FactReference>();
    }
}