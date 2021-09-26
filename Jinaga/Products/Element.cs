using System.Collections.Generic;
using Jinaga.Facts;

namespace Jinaga.Products
{
    public abstract class Element
    {
        public abstract IEnumerable<FactReference> GetFactReferences();
    }
}