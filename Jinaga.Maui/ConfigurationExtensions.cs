using Jinaga.Http;
using Jinaga.Maui.Authentication;
using Jinaga.Maui.Binding;
using Microsoft.Extensions.DependencyInjection;

namespace Jinaga.Maui;
public static class ConfigurationExtensions
{
    /// <summary>
    /// Add Jinaga authentication services to the application.
    /// This registers the following services:
    /// - ITokenStorage
    /// - UserProvider
    /// - AuthenticationProviderProxy
    /// - IHttpAuthenticationProvider
    /// - OAuthClient
    /// - AuthenticationService
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddJinagaAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<ITokenStorage, SecureTokenStorage>();
        services.AddSingleton<UserProvider>();
        services.AddSingleton<AuthenticationProviderProxy>();
        services.AddSingleton<IHttpAuthenticationProvider>(
            s => s.GetRequiredService<AuthenticationProviderProxy>());
        services.AddSingleton<OAuthClient>();
        services.AddSingleton<AuthenticationService>();
        return services;
    }

    /// <summary>
    /// Add Jinaga navigation services to the application.
    /// This registers the INavigationLifecycleManager service.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure">A tree of view models to control lifecycle</param>
    /// <returns></returns>
    public static IServiceCollection AddJinagaNavigation(this IServiceCollection services, Func<NavigationTreeBuilder, NavigationTreeBuilder> configure)
    {
        services.AddSingleton<INavigationLifecycleManager>(s =>
        {
            var tree = configure(NavigationTreeBuilder.Empty).Build();
            return new NavigationLifecycleManager(tree);
        });
        return services;
    }
}
