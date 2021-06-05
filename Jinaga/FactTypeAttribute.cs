using System;

namespace Jinaga
{
    public class FactTypeAttribute : Attribute
    {
        public string Type { get; set; }

        public FactTypeAttribute(string type)
        {
            Type = type;
        }
    }
}
