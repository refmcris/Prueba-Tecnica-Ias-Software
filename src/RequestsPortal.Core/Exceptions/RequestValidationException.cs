namespace RequestsPortal.Core.Exceptions;

public sealed class RequestValidationException : Exception
{
    public RequestValidationException(Dictionary<string, string[]> errors)
        : base("Los datos de la solicitud no son válidos.")
    {
        Errors = errors;
    }

    public Dictionary<string, string[]> Errors { get; }
}
