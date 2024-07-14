namespace Jinaga.Maui.Authentication;

/// <summary>
/// Provides authentication services, including token management and user authentication.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Initializes the authentication service. Call at application startup.
    /// </summary>
    /// <returns>True if the user is authenticated</returns>
    Task<bool> Initialize();
    /// <summary>
    /// Log the user in.
    /// </summary>
    /// <param name="provider">The identifier of the authentication provider to use</param>
    /// <returns>True if the user successfully authenticated</returns>
    Task<bool> LogIn(string provider);
    /// <summary>
    /// Log the user out.
    /// </summary>
    /// <returns>Resolves when logout is successful</returns>
    Task LogOut();
}