namespace Verx.SharedKernel;

public sealed class ChaveDuplicadaException : Exception
{
    public ChaveDuplicadaException(string message, Exception innerException) : base(message, innerException) { }

    public ChaveDuplicadaException() { }

    public ChaveDuplicadaException(string message) : base(message) { }
}
