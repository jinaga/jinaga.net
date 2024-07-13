using System.Collections.Immutable;

namespace Jinaga.Maui.Binding;

internal class NavigationTree
{
    private ImmutableDictionary<Type, NavigationTree> children;

    public NavigationTree(ImmutableDictionary<Type, NavigationTree> children)
    {
        this.children = children;
    }

    public bool HasPathFrom(Type ancestor, Type descendant)
    {
        return children.Any(child =>
            child.Key.IsAssignableFrom(ancestor)
                ? child.Value.HasPathTo(descendant)
                : child.Value.HasPathFrom(ancestor, descendant)
        );
    }

    private bool HasPathTo(Type descendant)
    {
        return children.Any(child =>
            child.Key.IsAssignableFrom(descendant) ||
            child.Value.HasPathTo(descendant)
        );
    }
}