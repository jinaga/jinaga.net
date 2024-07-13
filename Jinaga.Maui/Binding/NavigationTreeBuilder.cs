using System.Collections.Immutable;

namespace Jinaga.Maui.Binding;

public class NavigationTreeBuilder
{
    internal static NavigationTreeBuilder Empty = new NavigationTreeBuilder(
        ImmutableDictionary<Type, NavigationTree>.Empty);
    private ImmutableDictionary<Type, NavigationTree> children;

    internal NavigationTreeBuilder(ImmutableDictionary<Type, NavigationTree> children)
    {
        this.children = children;
    }

    public NavigationTreeBuilder Add<T>() where T: ILifecycleManaged
    {
        return new NavigationTreeBuilder(children.Add(typeof(T),
            new NavigationTree(ImmutableDictionary<Type, NavigationTree>.Empty)));
    }

    public NavigationTreeBuilder Add<T>(Func<NavigationTreeBuilder, NavigationTreeBuilder> pages) where T: ILifecycleManaged
    {
        return new NavigationTreeBuilder(children.Add(typeof(T),
            pages(Empty).Build()));
    }

    internal NavigationTree Build()
    {
        return new NavigationTree(children);
    }
}
