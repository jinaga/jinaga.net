namespace Jinaga
{
    /// <summary>
    /// Reports which user is currently authenticated.
    ///
    /// An object configured as <see cref="JinagaClientOptions.HttpAuthenticationProvider"/>
    /// may also implement this interface. When it does, the client records the current user
    /// as a saver of every fact they save, and sends a queued fact that somebody saved only
    /// while one of its savers is current, or while the configured
    /// <see cref="JinagaClientOptions.AuthorizationRules"/> admit it from anyone. The upload
    /// token and the recorded saver come from the same object, so they cannot name different
    /// users. Before every attempt to upload, the client checks that the user is still the one
    /// it chose the facts for, and stops if not.
    ///
    /// Without it the client records no savers and the queue sends every fact under
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
        /// The client processes the outbound queue again, so a returning user's
        /// held facts upload without the application saving anything new.
        /// </summary>
        event CurrentUserChanged? OnCurrentUserChanged;

        /// <summary>
        /// The user who is currently authenticated, or null if nobody is.
        ///
        /// The user's public key is recorded and compared exactly as given; the client does
        /// not normalize its spelling. It must match, character for character, the key the
        /// replicator signs with, including line endings. A source that spells the same key
        /// differently, for instance with CRLF where the replicator uses LF, is treated as a
        /// different user: <see cref="Repository.FactRepository.SignersOf{TFact}(TFact)"/>
        /// then reports two signers for one person, and the queue holds that user's facts
        /// as someone else's.
        /// </summary>
        User? CurrentUser { get; }
    }
}
