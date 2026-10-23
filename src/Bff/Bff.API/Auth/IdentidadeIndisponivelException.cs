namespace Bff.API.Auth;

public sealed class IdentidadeIndisponivelException(string message, Exception? inner = null) : Exception(message, inner)
{
}
