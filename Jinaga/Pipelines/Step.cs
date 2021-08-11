using System;

namespace Jinaga.Pipelines
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

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var that = (Step)obj;
            return
                that.role == role &&
                that.targetType == targetType;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(role, targetType);
        }
    }
}
