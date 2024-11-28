using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Jinaga.Azure.Functions;

public class JinagaTriggerBinding<TProjection> : IBinding
{
    private readonly string _configKey;

    public JinagaTriggerBinding(string configKey)
    {
        _configKey = configKey;
    }

    public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
    {
        throw new NotImplementedException("Direct binding is not supported.");
    }

    public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
    {
        var jinagaClient = context.FunctionContext.InstanceServices.GetService<JinagaClient>();
        var executor = context.Executor;

        return Task.FromResult<IListener>(
            new JinagaListener<TProjection>(_configKey, jinagaClient, executor)
        );
    }

    public ParameterDescriptor ToParameterDescriptor()
    {
        return new ParameterDescriptor
        {
            Name = _configKey,
            DisplayHints = new ParameterDisplayHints
            {
                Description = $"Jinaga trigger for {_configKey}"
            }
        };
    }
}