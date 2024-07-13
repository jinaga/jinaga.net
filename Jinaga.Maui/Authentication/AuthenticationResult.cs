namespace Jinaga.Maui.Authentication;

public record AuthenticationResult(AuthenticationToken? Token, User? User)
{
    public static AuthenticationResult Empty = new(null, null);
}
