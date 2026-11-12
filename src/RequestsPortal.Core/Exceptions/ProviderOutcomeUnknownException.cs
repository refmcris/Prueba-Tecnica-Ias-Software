namespace RequestsPortal.Core.Exceptions;

public sealed class ProviderOutcomeUnknownException : Exception
{
    public ProviderOutcomeUnknownException()
        : base("Se perdió la confirmación después de guardar el efecto.")
    {
    }
}
