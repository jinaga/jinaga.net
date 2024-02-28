using System;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    interface OnAddedHandler
    {
        Task<Func<Task>> Handle(object projection);
    }

    /// <summary>
    /// Respond to a new projection using a lambda.
    /// </summary>
    /// <typeparam name="TProjection">The type of the projection</typeparam>
    public class OnAddedHandler<TProjection> : OnAddedHandler
    {
        public delegate void Synchronous(TProjection projection);
        public delegate Task Asynchronous(TProjection projection);
        public delegate Action SynchronousRemoval(TProjection projection);
        public delegate Func<Task> AsynchronousRemoval(TProjection projection);

        OnAddedHandler(Func<TProjection, Task<Func<Task>>> handler)
        {
            this.handler = handler;
        }

        /// <summary>
        /// Respond to a new projection synchronously.
        /// (projection) => { ... }
        /// </summary>
        /// <param name="handler">Lambda to respond to a new projection</param>
        public static implicit operator OnAddedHandler<TProjection>(Synchronous handler)
        {
            return new OnAddedHandler<TProjection>(projection =>
            {
                handler(projection);
                Func<Task> result = () => Task.CompletedTask;
                return Task.FromResult(result);
            });
        }

        // /// <summary>
        // /// Respond to a new projection asynchronously.
        // /// async (projection) => { ... }
        // /// </summary>
        // /// <param name="handler">Lambda to respond to a new projection</param>
        // public static implicit operator OnAddedHandler<TProjection>(Func<TProjection, Task> handler)
        // {
        //     return new OnAddedHandler<TProjection>(async projection =>
        //     {
        //         await handler(projection);
        //         Func<Task> result = () => Task.CompletedTask;
        //         return result;
        //     });
        // }

        // /// <summary>
        // /// Respond to a new projection synchronously, and provide a synchronous
        // /// method to remove the projection.
        // /// (projection) => { ... return () => { ... }; }
        // /// </summary>
        // /// <param name="handler">Lambda to respond to a new projection</param>
        // public static implicit operator OnAddedHandler<TProjection>(Func<TProjection, Action> handler)
        // {
        //     return new OnAddedHandler<TProjection>(projection =>
        //     {
        //         var removed = handler(projection);
        //         Func<Task> result = () =>
        //         {
        //             removed();
        //             return Task.CompletedTask;
        //         };
        //         return Task.FromResult(result);
        //     });
        // }

        // /// <summary>
        // /// Respond to a new projection asynchronously, and provide an asynchronous
        // /// method to remove the projection.
        // /// async (projection) => { ... return async () => { ... }; }
        // /// </summary>
        // /// <param name="handler">Lambda to respond to a new projection</param>
        // public static implicit operator OnAddedHandler<TProjection>(Func<TProjection, Task<Func<Task>>> handler)
        // {
        //     return new OnAddedHandler<TProjection>(handler);
        // }

        private Func<TProjection, Task<Func<Task>>> handler;

        Task<Func<Task>> OnAddedHandler.Handle(object projection)
        {
            return handler((TProjection)projection);
        }
    }
}