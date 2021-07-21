namespace Jinaga.Pipelines2
{
    public class Step
    {
        private readonly string role;
        private readonly string targetType;

        public Step(string role, string targetType)
        {
            this.role = role;
            this.targetType = targetType;
        }

        public string Role => role;
        public string TargetType => targetType;
    }
}
