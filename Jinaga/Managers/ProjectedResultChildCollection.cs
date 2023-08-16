using System.Collections.Immutable;

namespace Jinaga.Managers
{
    public class ProjectedResultChildCollection
    {
        public ProjectedResultChildCollection(string name, ImmutableList<ProjectedResult> results)
        {
            Name = name;
            Results = results;
        }

        public string Name { get; }
        public ImmutableList<ProjectedResult> Results { get; }
    }
}