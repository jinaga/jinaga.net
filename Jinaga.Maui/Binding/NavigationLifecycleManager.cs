using System.Collections.Immutable;

namespace Jinaga.Maui.Binding;

public class NavigationLifecycleManager : INavigationLifecycleManager
{
    private readonly NavigationTree tree;

    private ImmutableHashSet<ILifecycleManaged> visibleViewModels = ImmutableHashSet<ILifecycleManaged>.Empty;
    private ImmutableHashSet<ILifecycleManaged> loadedViewModels = ImmutableHashSet<ILifecycleManaged>.Empty;

    internal NavigationLifecycleManager(NavigationTree tree)
    {
        this.tree = tree;
    }

    public void Visible(ILifecycleManaged viewModel)
    {
        if (!visibleViewModels.Contains(viewModel))
        {
            visibleViewModels = visibleViewModels.Add(viewModel);
            if (!loadedViewModels.Contains(viewModel))
            {
                viewModel.Load();
                loadedViewModels = loadedViewModels.Add(viewModel);
            }
            RestoreInvariants();
        }
    }

    public void Hidden(ILifecycleManaged viewModel)
    {
        if (visibleViewModels.Contains(viewModel))
        {
            visibleViewModels = visibleViewModels.Remove(viewModel);
            RestoreInvariants();
        }
    }

    // Invariants:
    // All visible view models are loaded.
    // No descendants of a visible view model are loaded, unless
    // they are also visible or have a visible descendant.

    private void RestoreInvariants()
    {
        // Look for loaded view models that are not visible, and
        // have no visible view models below them in the tree.
        foreach (var viewModel in loadedViewModels.Except(visibleViewModels))
        {
            bool isBelowVisible = visibleViewModels.Any(visible =>
                tree.HasPathFrom(visible.GetType(), viewModel.GetType())
            );
            if (!isBelowVisible)
            {
                continue;
            }

            bool isAboveVisible = visibleViewModels.Any(visible =>
                tree.HasPathFrom(viewModel.GetType(), visible.GetType())
            );
            if (!isAboveVisible)
            {
                viewModel.Unload();
                loadedViewModels = loadedViewModels.Remove(viewModel);
            }
        }
    }
}