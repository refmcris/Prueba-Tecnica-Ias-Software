using RequestsPortal.Core.Enums;

namespace RequestsPortal.Core.Options;

public sealed class ProviderOptions
{
    private const double MaximumDelaySeconds = (uint.MaxValue - 1d) / 1000;

    public ProviderMode Mode { get; init; } = ProviderMode.Success;

    public double DelaySeconds { get; init; } = 3;

    public void Validate()
    {
        if (!Enum.IsDefined(Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(Mode), "El modo del proveedor no es válido.");
        }

        // El máximo corresponde al rango admitido por el temporizador de .NET.
        if (!double.IsFinite(DelaySeconds) || DelaySeconds < 0 || DelaySeconds > MaximumDelaySeconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DelaySeconds),
                "La demora debe ser finita, no negativa y estar dentro del rango del temporizador.");
        }
    }
}
