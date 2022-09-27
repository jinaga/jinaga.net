using System.Collections.Generic;
using Jinaga.Records;

namespace Jinaga.Http
{
    public class LoadRequest
    {
        public List<FactReference> References { get; set; } = new List<FactReference>();
    }
}