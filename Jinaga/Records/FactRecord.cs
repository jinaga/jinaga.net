using System.Collections.Generic;

namespace Jinaga.Records
{
    public class FactRecord
    {
        public string Type { get; set; }
        public string Hash { get; set; }
        public Dictionary<string, PredecessorSet> Predecessors { get; set; }
        public Dictionary<string, FieldValue> Fields { get; set; }
    }
}