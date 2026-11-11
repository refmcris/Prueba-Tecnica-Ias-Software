namespace RequestsPortal.Core.Options;

public sealed class ProcessingOptions
{
    private const double MaximumTimerSeconds = (uint.MaxValue - 1d) / 1000;

    public double PollIntervalSeconds { get; init; } = 1;

    public double LeaseSeconds { get; init; } = 30;

    public double TimeoutSeconds { get; init; } = 5;

    public int MaxAttempts { get; init; } = 3;

    public void Validate()
    {
        ValidateInterval(PollIntervalSeconds, nameof(PollIntervalSeconds));
        ValidateInterval(LeaseSeconds, nameof(LeaseSeconds));
        ValidateInterval(TimeoutSeconds, nameof(TimeoutSeconds));

        if (LeaseSeconds <= TimeoutSeconds)
        {
            throw new ArgumentException("La reserva debe durar más que el timeout del proveedor.");
        }

        if (MaxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAttempts), "Debe permitirse al menos un fallo confirmado.");
        }
    }

    private static void ValidateInterval(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0 || value > MaximumTimerSeconds)
        {
            throw new ArgumentOutOfRangeException(name, "El intervalo debe ser positivo y válido para un temporizador.");
        }
    }
}
