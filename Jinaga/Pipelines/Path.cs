namespace Jinaga.Pipelines
{
    public class Path
    {
        private readonly string tag;
        private readonly string targetType;
        private readonly string startingTag;

        public Path(string tag, string targetType, string startingTag)
        {
            this.tag = tag;
            this.targetType = targetType;
            this.startingTag = startingTag;
        }

        public string ToDescriptiveString()
        {
            return $"    {tag}: {targetType} = {startingTag}\r\n";
        }
    }
}
