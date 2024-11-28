using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Jinaga.Azure.Functions;

public class JinagaTriggerBindingProvider<TProjection> : IBindingProvider
{
    public Task<IBinding> TryCreateAsync(BindingProviderContext context)
    {
        var parameter = context.Parameter;
        var attribute = parameter.GetCustomAttribute<JinagaTriggerAttribute>();

        if (attribute == null)
        {
            return Task.FromResult<IBinding>(null);
        }

        return Task.FromResult<IBinding>(new JinagaTriggerBinding<TProjection>(attribute.ConfigurationKey));
    }
}
