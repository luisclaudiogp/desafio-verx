namespace Verx.Web.Http;

public sealed class RequisicaoInvalidaException : Exception
{
    public RequisicaoInvalidaException(string message) : base(message) { }

    public RequisicaoInvalidaException() { }

    public RequisicaoInvalidaException(string message, Exception innerException) : base(message, innerException) { }
}
