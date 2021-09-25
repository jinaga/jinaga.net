using System.Collections.Generic;
using Jinaga.Facts;

namespace Jinaga.Products
{
    public class SimpleElement : Element
    {
        public FactReference FactReference { get; }

        public SimpleElement(FactReference factReference)
        {
            FactReference = factReference;
        }

        public override IEnumerable<FactReference> GetFactReferences()
        {
            return new [] { FactReference };
        }

        public override bool Equals(object obj)
        {
            return obj is SimpleElement product &&
                FactReference.Equals(product.FactReference);
        }

        public override int GetHashCode()
        {
            return FactReference.GetHashCode();
        }
    }
}