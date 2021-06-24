using System.Collections.Generic;

namespace Jinaga.Pipelines
{
    class PathTagComparer : IEqualityComparer<Path>
    {
        public bool Equals(Path x, Path y)
        {
            return string.Equals(x.Tag, y.Tag);
        }

        public int GetHashCode(Path obj)
        {
            return obj.Tag.GetHashCode();
        }
    }
}