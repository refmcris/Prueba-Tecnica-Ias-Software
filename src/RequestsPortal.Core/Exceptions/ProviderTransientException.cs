namespace RequestsPortal.Core.Exceptions;

public sealed class ProviderTransientException : Exception
{
    public ProviderTransientException()
        : base("Fallo transitorio simulado; no se produjo el efecto.")
    {
    }
}
