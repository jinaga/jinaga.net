using System.Collections.Generic;
using Jinaga.Records;

namespace Jinaga.Http
{
    public class LoadResponse
    {
        public List<FactRecord> Facts { get; set; } = new List<FactRecord>();
    }
}
