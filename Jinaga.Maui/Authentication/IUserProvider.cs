namespace Jinaga.Maui.Authentication;

public interface IUserProvider
{
    /// <summary>
    /// Change the current user. Typically this is called after the user
    /// has been authenticated.
    /// </summary>
    /// <param name="user">The authenticated user</param>
    void SetUser(User user);
    /// <summary>
    /// Clear the current user. This is typically called when the user
    /// logs out.
    /// </summary>
    void ClearUser();
    /// <summary>
    /// Register a handler that will be called when the user changes.
    /// The handler should return an action that will clean up any
    /// operations performed by the handler.
    /// </summary>
    /// <param name="withUser"></param>
    /// <returns>A handler</returns>
    IUserProviderHandler AddHandler(Func<User, Action> withUser);
    /// <summary>
    /// Remove a handler that was previously registered with <see cref="AddHandler(Func{User, Action})"/>.
    /// </summary>
    /// <param name="handler">The handler that was returned from AddHandler.</param>
    void RemoveHandler(IUserProviderHandler handler);
}