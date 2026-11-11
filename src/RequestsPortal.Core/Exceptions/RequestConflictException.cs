namespace RequestsPortal.Core.Exceptions;

public sealed class RequestConflictException : Exception
{
    public RequestConflictException()
        : base("Ya existe una solicitud con el mismo cliente y referencia, pero con contenido diferente.")
    {
    }
}
