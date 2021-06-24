namespace Jinaga.Pipelines
{
    public class SimpleProjection : Projection
    {
        public string Tag { get; }

        public SimpleProjection(string tag)
        {
            Tag = tag;
        }

        public override string ToDescriptiveString()
        {
            return Tag;
        }
    }
}