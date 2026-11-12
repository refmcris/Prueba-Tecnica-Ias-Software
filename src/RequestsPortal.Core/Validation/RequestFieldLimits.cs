namespace RequestsPortal.Core.Validation;

// Límites técnicos elegidos para esta solución; el enunciado no fija máximos.
public static class RequestFieldLimits
{
    public const int ExternalReference = 128;

    public const int CustomerId = 100;

    public const int ServiceCode = 80;

    public const int Description = 2000;
}
