using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(JinagaFunctionBinding.Startup))]

namespace JinagaFunctionBinding
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton<JinagaBindingConfig>();
            builder.Services.AddSingleton<ITriggerBindingProvider, JinagaTriggerBindingProvider>();
        }
    }

    public static class JinagaFunctionsExtensions
    {
        public static IFunctionsHostBuilder AddJinaga(this IFunctionsHostBuilder builder, Action<JinagaBindingConfig> configure)
        {
            var config = new JinagaBindingConfig();
            configure(config);
            builder.Services.AddSingleton(config);
            return builder;
        }
    }
}
