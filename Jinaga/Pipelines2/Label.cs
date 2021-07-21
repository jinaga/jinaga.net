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
    }
}
