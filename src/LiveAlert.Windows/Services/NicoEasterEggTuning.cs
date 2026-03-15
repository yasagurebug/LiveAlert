namespace LiveAlert.Windows.Services;

internal static class NicoEasterEggTuning
{
    // Tuning values for the Windows-only nico easter egg.
    public const int MaxSprites = 30;
    public const double HorizontalSpeedLimitMultiplier = 2d;
    public const double GravityMultiplier = 2.5d;
    public const double VerticalSpeedLimitMultiplier = 5d;
    public const double BounceVerticalRestitution = 0.5d;
    public const double MinSpawnDelaySeconds = 0d;
    public const double MaxSpawnDelaySeconds = 0.5d;
    public const int BounceChancePerFrameDenominator = 300;
}
