namespace RequestsPortal.Core.Enums;

public enum ProviderMode
{
    Success,
    TransientThenSuccess,
    AlwaysTransient,
    ResponseLostAfterCommit
}
