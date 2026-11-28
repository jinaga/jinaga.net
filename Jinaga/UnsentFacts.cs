namespace Jinaga
{
    /// <summary>
    /// How many facts are waiting in the outbound queue, and how the queue would route them
    /// for the user who is logged in now.
    ///
    /// The three counts come from one classification of the queue, the same one that decides
    /// what the next save sends, so they cannot disagree with it. They are counts only: no
    /// member names a fact, a reference, or the user who saved one.
    /// </summary>
    public class UnsentFacts
    {
        public UnsentFacts(int total, int held, int savedByCurrentUser)
        {
            Total = total;
            Held = held;
            SavedByCurrentUser = savedByCurrentUser;
        }

        /// <summary>
        /// Every fact in the outbound queue, whoever saved it.
        /// </summary>
        public int Total { get; }

        /// <summary>
        /// The queued facts the client is holding for a user who is not logged in now,
        /// including those held only because a queued ancestor is held. They stay on this
        /// device until one of the users who saved them logs in here again, which may be
        /// never.
        ///
        /// Zero for a store that records no savers, which sends the whole queue under
        /// whoever is logged in.
        /// </summary>
        public int Held { get; }

        /// <summary>
        /// The queued facts the user who is logged in now is recorded as having saved.
        ///
        /// Every one of them stays on this device when that user logs out. Most then become
        /// held for them once another user logs in, but not a <c>Jinaga.User</c>: anybody may
        /// upload one, so it is never held and leaves under the next user instead. This count
        /// includes those, the user's own <c>Jinaga.User</c> fact among them while it is still
        /// queued, so it is an upper bound on how many become held rather than the exact
        /// number. <see cref="Held"/>, read after the next user logs in, is that number.
        ///
        /// Zero when nobody is logged in, and zero for a store that records no savers.
        /// </summary>
        public int SavedByCurrentUser { get; }
    }
}
