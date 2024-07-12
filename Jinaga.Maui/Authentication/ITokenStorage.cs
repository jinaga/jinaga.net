namespace Jinaga.Maui.Authentication;

public interface ITokenStorage
{
    Task<AuthenticationResult> LoadTokenAndUser();
    Task SaveTokenAndUser(AuthenticationResult result);
}
