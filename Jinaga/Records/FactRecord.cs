using System.Collections.Generic;

namespace Jinaga.Records
{
    public class FactRecord
    {
        public string Type { get; set; } = "";
        public string Hash { get; set; } = "";
        public Dictionary<string, PredecessorSet> Predecessors { get; set; } = new Dictionary<string, PredecessorSet>();
        public Dictionary<string, FieldValue> Fields { get; set; } = new Dictionary<string, FieldValue>();
    }
}