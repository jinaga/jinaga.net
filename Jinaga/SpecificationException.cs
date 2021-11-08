using System;
using System.Runtime.Serialization;

namespace Jinaga
{
    public class SpecificationException : Exception
    {
        public SpecificationException()
        {
        }

        public SpecificationException(string message) : base(message)
        {
        }

        public SpecificationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected SpecificationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
