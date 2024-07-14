namespace Jinaga.Maui.Binding;

public interface INavigationLifecycleManager
{
    /// <summary>
    /// Tell the manager that the view model is visible.
    /// Call this method in the OnAppearing method of the view.
    /// </summary>
    /// <param name="viewModel">The view model to manage</param>
    void Visible(ILifecycleManaged viewModel);
    /// <summary>
    /// Tell the manager that the view model is hidden.
    /// Call this method in the OnNavigatedFrom method of the view.
    /// </summary>
    /// <param name="viewModel">The view model to stop managing</param>
    void Hidden(ILifecycleManaged viewModel);
    /// <summary>
    /// Unload all view models that are not visible.
    /// Call this method when the user logs out.
    /// </summary>
    void UnloadInvisible();
}