using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Jinaga.Maui.Authentication;

/// <summary>
/// Manage user state within an application. Register the UserProvider
/// as a singleton in the service collection. Inject the UserProvider
/// into the application components that need to set or know the current user.
/// </summary>
public class UserProvider
{
    private readonly ILogger<UserProvider> logger;

    /// <summary>
    /// Create a new UserProvider.
    /// </summary>
    /// <param name="logger">A logger that will receive error messages</param>
    public UserProvider(ILogger<UserProvider> logger)
    {
        this.logger = logger;
    }

    private readonly object syncRoot = new object();
    private User? user;
    private ImmutableList<Handler> handlers = ImmutableList<Handler>.Empty;

    /// <summary>
    /// Save the handler so that it can be removed from the user provider.
    /// </summary>
    public class Handler
    {
        internal Func<User, Action> WithUser { get; }
        private Action Clear { get; set; } = () => { };

        internal Handler(Func<User, Action> withUser)
        {
            WithUser = withUser;
        }

        internal void InvokeClear()
        {
            Clear();
        }

        internal void SetClear(Action clearAction)
        {
            Clear = clearAction;
        }
    }

    /// <summary>
    /// Change the current user. Typically this is called after the user
    /// has been authenticated.
    /// </summary>
    /// <param name="user">The authenticated user</param>
    public void SetUser(User user)
    {
        lock (syncRoot)
        {
            BeforeSetUser();
            this.user = user;
            AfterSetUser();
        }
    }

    /// <summary>
    /// Clear the current user. This is typically called when the user
    /// logs out.
    /// </summary>
    public void ClearUser()
    {
        lock (syncRoot)
        {
            BeforeSetUser();
            user = null;
            AfterSetUser();
        }
    }

    /// <summary>
    /// Register a handler that will be called when the user changes.
    /// The handler should return an action that will clean up any
    /// operations performed by the handler.
    /// </summary>
    /// <param name="withUser"></param>
    /// <returns></returns>
    public Handler AddHandler(Func<User, Action> withUser)
    {
        lock (syncRoot)
        {
            var handler = new Handler(withUser);
            handlers = handlers.Add(handler);
            if (user != null)
            {
                handler.SetClear(handler.WithUser(user));
            }
            return handler;
        }
    }

    /// <summary>
    /// Remove a handler that was previously registered with <see cref="AddHandler(Func{User, Action})"/>.
    /// </summary>
    /// <param name="handler">The handler that was returned from AddHandler.</param>
    public void RemoveHandler(Handler handler)
    {
        lock (syncRoot)
        {
            handlers = handlers.Remove(handler);
            if (user != null)
            {
                handler.InvokeClear();
            }
        }
    }

    private void BeforeSetUser()
    {
        // Assumes this is called within a lock
        if (user != null)
        {
            foreach (var handler in handlers)
            {
                try
                {
                    handler.InvokeClear();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error invoking clear");
                    // Continue with the rest of the handlers
                }
                handler.SetClear(() => { });
            }
        }
    }

    private void AfterSetUser()
    {
        // Assumes this is called within a lock
        if (user != null)
        {
            foreach (var handler in handlers)
            {
                try
                {
                    var clearAction = handler.WithUser(user);
                    handler.SetClear(clearAction);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error setting user");
                    handler.SetClear(() => { });
                    // Continue with the rest of the handlers
                }
            }
        }
    }
}