using System;

namespace Jinaga.Pipelines
{
    public class Label
    {
        private readonly string name;
        private readonly string type;

        public Label(string name, string type)
        {
            this.name = name;
            this.type = type;
        }

        public string Name => name;
        public string Type => type;

        public override string ToString()
        {
            return $"{name}: {type}";
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            Label that = (Label)obj;
            return name == that.name && type == that.type;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(name, type);
        }

        public static bool operator ==(Label a, Label b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Label a, Label b)
        {
            return !a.Equals(b);
        }
    }
}
