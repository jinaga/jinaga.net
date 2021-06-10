using System;

namespace Jinaga.Pipelines
{
    public abstract class Step
    {
        public abstract Step Reflect();
        public abstract string ToDescriptiveString();
    }
}
