using Jinaga.Http;
using Jinaga.Maui.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Jinaga.Maui;
public static class ConfigurationExtensions
{
    public static IServiceCollection AddJinagaAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<OAuth2HttpAuthenticationProvider>();
        services.AddSingleton<IHttpAuthenticationProvider>(
            s => s.GetRequiredService<OAuth2HttpAuthenticationProvider>());
        services.AddSingleton<OAuthClient>();
        services.AddSingleton<AuthenticationService>();
        return services;
    }
}
