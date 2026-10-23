namespace Verx.SharedKernel;

public sealed class ConcorrenciaException : Exception
{
    public ConcorrenciaException(string message, Exception innerException) : base(message, innerException) { }

    public ConcorrenciaException() { }

    public ConcorrenciaException(string message) : base(message) { }
}
