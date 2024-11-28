using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace JinagaFunctionBinding
{
    public class JinagaTriggerBinding : ITriggerBinding
    {
        private readonly string _specification;
        private readonly string _startingPoint;

        public JinagaTriggerBinding(string specification, string startingPoint)
        {
            _specification = specification;
            _startingPoint = startingPoint;
        }

        public Type TriggerValueType => typeof(JinagaListener);

        public IReadOnlyDictionary<string, Type> BindingDataContract => new Dictionary<string, Type>();

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            var listener = (JinagaListener)value;
            var bindingData = new Dictionary<string, object>();
            return Task.FromResult<ITriggerData>(new TriggerData(new ValueProvider(listener), bindingData));
        }

        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            var listener = new JinagaListener(_specification, _startingPoint);
            return Task.FromResult<IListener>(listener);
        }
    }

    public class ValueProvider : IValueProvider
    {
        private readonly JinagaListener _listener;

        public ValueProvider(JinagaListener listener)
        {
            _listener = listener;
        }

        public Type Type => typeof(JinagaListener);

        public Task<object> GetValueAsync()
        {
            return Task.FromResult<object>(_listener);
        }

        public string ToInvokeString()
        {
            return _listener.ToString();
        }
    }
}
