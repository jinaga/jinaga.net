namespace Jinaga.Authorization
{
    /// <summary>
    /// Decides which user a save is checked as, and whether it is checked at all.
    ///
    /// A client has two notions of "the user" a save could be checked as: the one a test
    /// fixes, and the one an identity source reports as logged in. This is the one place that
    /// chooses between them, so that <see cref="Managers.FactManager"/> never has to.
    /// </summary>
    internal abstract class AuthorizingUser
    {
        /// <summary>
        /// Every save is checked as this user, as <c>Jinaga.UnitTest</c> checks it. Null means
        /// nobody, so every rule that names a user refuses.
        /// </summary>
        public static AuthorizingUser Fixed(User? user) => new FixedUser(user);

        /// <summary>
        /// Each save is checked as whoever the identity source reports is logged in. With no
        /// identity source, or nobody logged in, there is no user to check as and the save is
        /// not checked.
        /// </summary>
        public static AuthorizingUser LoggedIn(IIdentitySource? identitySource) => new LoggedInUser(identitySource);

        /// <summary>
        /// Whether to check a save, and if so, as whom.
        /// </summary>
        /// <param name="signingAsSingleUsePrincipal">
        /// True inside <see cref="JinagaClient.SingleUse{T}"/>, where the facts are signed by the
        /// single-use principal
        /// </param>
        /// <param name="user">The user to check as, or null for nobody</param>
        /// <returns>False if the save is not to be checked</returns>
        public abstract bool TryGetUser(bool signingAsSingleUsePrincipal, out User? user);

        private class FixedUser : AuthorizingUser
        {
            private readonly User? user;

            public FixedUser(User? user)
            {
                this.user = user;
            }

            public override bool TryGetUser(bool signingAsSingleUsePrincipal, out User? user)
            {
                user = this.user;
                return true;
            }
        }

        private class LoggedInUser : AuthorizingUser
        {
            private readonly IIdentitySource? identitySource;

            public LoggedInUser(IIdentitySource? identitySource)
            {
                this.identitySource = identitySource;
            }

            public override bool TryGetUser(bool signingAsSingleUsePrincipal, out User? user)
            {
                // A replicator authorizes a signed fact by its signatures as well as by the
                // uploader. A single-use fact carries the principal's signature, and the engine
                // checks as one user, so checking as the logged-in user alone could refuse a
                // fact the replicator accepts. Leave it to the replicator.
                user = signingAsSingleUsePrincipal ? null : identitySource?.CurrentUser;
                return user != null;
            }
        }
    }
}
