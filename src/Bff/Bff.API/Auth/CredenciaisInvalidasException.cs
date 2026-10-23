namespace Bff.API.Auth;

public sealed class CredenciaisInvalidasException(string message) : Exception(message)
{
}
