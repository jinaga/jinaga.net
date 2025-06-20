using System;

namespace JinagaFunctionBinding
{
    [AttributeUsage(AttributeTargets.Method)]
    public class JinagaTriggerAttribute : Attribute
    {
        public string Specification { get; }
        public string StartingPoint { get; }

        public JinagaTriggerAttribute(string specification, string startingPoint)
        {
            Specification = specification;
            StartingPoint = startingPoint;
        }
    }
}
