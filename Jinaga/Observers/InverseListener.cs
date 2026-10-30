using Jinaga.Products;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    /// <summary>
    /// A listener on one exact inverse. Its rows are the changes themselves, so the
    /// listener applies them rather than reading again to decide them, and the read that
    /// reported them is what orders its decisions against every other read.
    ///
    /// The read is shared by every listener registered on the same inverse and givens,
    /// and each listener numbers its reads against its own observer. So one number cannot
    /// serve the read: <see cref="BeginRead"/> asks each listener for its own, and
    /// <see cref="OnResult"/> hands that listener back the one it gave.
    /// </summary>
    /// <remarks>
    /// <see cref="BeginRead"/> is called for every listener of a group before the read
    /// touches the store, and never from the handler. A read numbered when its handler
    /// runs is not pending in between, and handlers are awaited one after another, so a
    /// removal that lands in that gap finds no older read pending and records no
    /// withdrawal. The stale add then claims a row the store no longer holds.
    /// </remarks>
    internal class InverseListener
    {
        /// <summary>
        /// Numbers a read that is about to decide rows, and marks it pending. Called
        /// before the read touches the store.
        /// </summary>
        public Func<long> BeginRead { get; }

        /// <summary>
        /// Applies the rows the read reported, under the number this listener was given.
        /// </summary>
        public Func<ImmutableList<Product>, long, CancellationToken, Task> OnResult { get; }

        /// <summary>
        /// Marks the read's rows delivered. Runs whether the read threw, a handler threw,
        /// or the result held no rows at all.
        /// </summary>
        public Action<long> EndRead { get; }

        public InverseListener(
            Func<long> beginRead,
            Func<ImmutableList<Product>, long, CancellationToken, Task> onResult,
            Action<long> endRead)
        {
            BeginRead = beginRead;
            OnResult = onResult;
            EndRead = endRead;
        }
    }
}
