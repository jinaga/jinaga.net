namespace Jinaga
{
    /// <summary>
    /// Reports which user is currently authenticated.
    ///
    /// An object configured as <see cref="JinagaClientOptions.HttpAuthenticationProvider"/>
    /// may also implement this interface. When it does, the client records the current user
    /// as the local author of every fact it queues, and sends a queued fact only while its
    /// author is current. The upload token and the recorded author then come from the same
    /// object, so they cannot name different users.
    ///
    /// Without it the client records no authors and the queue sends every fact under
    /// whoever is logged in, which is what a client with no notion of a current user does.
    /// </summary>
    public interface IIdentitySource
    {
        /// <summary>
        /// Called when <see cref="CurrentUser"/> changes.
        /// </summary>
        /// <param name="user">The user who is now authenticated, or null if nobody is</param>
        delegate void CurrentUserChanged(User? user);

        /// <summary>
        /// Raised when the authenticated user changes, including on log out.
        /// The client processes the outbound queue again, so a returning author's
        /// held facts upload without the application saving anything new.
        /// </summary>
        event CurrentUserChanged? OnCurrentUserChanged;

        /// <summary>
        /// The user who is currently authenticated, or null if nobody is.
        /// </summary>
        User? CurrentUser { get; }
    }
}
