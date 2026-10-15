using System;

namespace Jinaga.Services
{
    /// <summary>
    /// Stops an upload because the user logged in is no longer the one the facts are being
    /// sent under.
    ///
    /// Facts are chosen for an upload by who is logged in when it starts, and the token is
    /// read again on every attempt to send them. Checking before each attempt is what keeps a
    /// user who logs in part way through, during a retry or between batches, from carrying
    /// another user's facts up under their own token. Nothing is sent once this is thrown;
    /// the facts stay queued for the next pass.
    /// </summary>
    internal class UploaderChangedException : Exception
    {
        public UploaderChangedException()
            : base("The user logged in changed while queued facts were being sent, so the rest were not sent.")
        {
        }

        /// <summary>
        /// Throws unless the identity source still reports <paramref name="uploader"/>.
        /// A client that cannot tell who is logged in has no identity source, and was given no
        /// uploader, so it never stops.
        /// </summary>
        public static void ThrowIfChanged(IIdentitySource? identitySource, string? uploader)
        {
            var current = identitySource?.CurrentUser?.publicKey;
            if (current != uploader)
            {
                throw new UploaderChangedException();
            }
        }
    }
}
