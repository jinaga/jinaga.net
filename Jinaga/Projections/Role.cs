namespace Jinaga.Projections
{
    public class Role
    {
        public Role(string name, string targetType)
        {
            Name = name;
            TargetType = targetType;
        }

        public string Name { get; }
        public string TargetType { get; }
    }
}