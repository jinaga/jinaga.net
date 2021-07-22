using System;
namespace Jinaga.Pipelines2
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
            return this.name == that.name && this.type == that.type;
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(this.name, this.type);
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
