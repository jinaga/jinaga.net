using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    /// <summary>
    /// A listener on the save of one fact, named by reference. It carries no
    /// specification, so nothing is read on its behalf before it is called.
    /// </summary>
    internal class FactListener
    {
        public Func<CancellationToken, Task> OnSaved { get; }

        public FactListener(Func<CancellationToken, Task> onSaved)
        {
            OnSaved = onSaved;
        }
    }
}
