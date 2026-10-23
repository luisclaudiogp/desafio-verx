namespace Verx.SharedKernel;

public sealed class NaoEncontradoException : DomainException
{
    public NaoEncontradoException(string message) : base(message) { }

    public NaoEncontradoException() { }

    public NaoEncontradoException(string message, Exception innerException) : base(message, innerException) { }
}
