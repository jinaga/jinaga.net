using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Jinaga.Maui.Binding;

public class UserProvider
{
    private readonly ILogger<UserProvider> logger;

    public UserProvider(ILogger<UserProvider> logger)
    {
        this.logger = logger;
    }

    private readonly object syncRoot = new object();
    private User? user;
    private ImmutableList<Handler> handlers = ImmutableList<Handler>.Empty;

    public class Handler
    {
        public Func<User, Action> WithUser { get; }
        private Action Clear { get; set; } = () => { };

        public Handler(Func<User, Action> withUser)
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

    public void SetUser(User user)
    {
        lock (syncRoot)
        {
            BeforeSetUser();
            this.user = user;
            AfterSetUser();
        }
    }

    public void ClearUser()
    {
        lock (syncRoot)
        {
            BeforeSetUser();
            user = null;
            AfterSetUser();
        }
    }

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