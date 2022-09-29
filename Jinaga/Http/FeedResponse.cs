using Jinaga.Records;
using System.Collections.Generic;

namespace Jinaga.Http
{
    public class FeedResponse
    {
        public List<FactReference> references { get; set; } = new List<FactReference>();
        public string bookmark { get; set; } = "";
    }
}