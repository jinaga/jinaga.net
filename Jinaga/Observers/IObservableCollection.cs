using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    public interface IObservableCollection<TProjection> : IEnumerable<TProjection>
    {
        void OnAdded(Func<TProjection, Task<Func<Task>>> added);

        void OnAdded(Func<TProjection, Task> added)
        {
            this.OnAdded(async projection =>
            {
                await added(projection).ConfigureAwait(false);
                return () => Task.CompletedTask;
            });
        }

        void OnAdded(Func<TProjection, Action> added)
        {
            this.OnAdded(projection =>
            {
                var removal = added(projection);
                return Task.FromResult<Func<Task>>(() =>
                {
                    removal();
                    return Task.CompletedTask;
                });
            });
        }

        void OnAdded(Action<TProjection> added)
        {
            this.OnAdded(projection =>
            {
                added(projection);
                return Task.FromResult<Func<Task>>(() => Task.CompletedTask);
            });
        }
    }
}
