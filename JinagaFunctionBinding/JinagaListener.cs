using System;
using System.Threading;
using System.Threading.Tasks;
using Jinaga;

namespace JinagaFunctionBinding
{
    public class JinagaListener : IListener
    {
        private readonly string _specification;
        private readonly string _startingPoint;

        public JinagaListener(string specification, string startingPoint)
        {
            _specification = specification;
            _startingPoint = startingPoint;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Implement the logic to subscribe to the Jinaga Subscribe method
            // using the _specification and _startingPoint
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Implement the logic to stop the subscription
            return Task.CompletedTask;
        }
    }
}
