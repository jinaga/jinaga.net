using System;

namespace Jinaga.Pipelines
{
    public abstract class Step
    {
        public abstract Step Reflect();
        public abstract string ToDescriptiveString();
        public abstract string ToOldDescriptiveString();
        public abstract string InitialType { get; }
        public abstract string TargetType { get; }
    }
}
