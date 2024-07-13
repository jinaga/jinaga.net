namespace Jinaga.Maui.Binding;

/// <summary>
/// A view model for which the lifecycle can be managed.
/// </summary>
public interface ILifecycleManaged
{
    /// <summary>
    /// Called when the view model should load its data.
    /// </summary>
    void Load();
    /// <summary>
    /// Called when the view model should unload its data.
    /// </summary>
    void Unload();
}