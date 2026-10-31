namespace Jinaga.Store.SQLite.Description
{
    /// <summary>
    /// A constraint that two fact indexes name one fact.
    /// </summary>
    /// <remarks>
    /// A path condition with no roles on either side says two labels name one fact, and it
    /// contributes no edge because there is no step between facts to walk. Where neither
    /// label is bound yet, or only one of them is, binding the second to the first's fact
    /// index carries the condition on its own. Where both are already bound, and to different
    /// fact indexes, nothing in the query relates them, so the condition needs a constraint of
    /// its own or it is lost and the read answers with rows the specification excludes.
    /// </remarks>
    internal class FactEqualityDescription
    {
        public int LeftFactIndex { get; }
        public int RightFactIndex { get; }

        public FactEqualityDescription(int leftFactIndex, int rightFactIndex)
        {
            LeftFactIndex = leftFactIndex;
            RightFactIndex = rightFactIndex;
        }
    }
}
