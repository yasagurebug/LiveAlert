namespace LiveAlert.Windows.Services;

internal static class NicoEasterEggPhysics
{
    private static readonly string[] ActivationLabels = ["しののめにこ", "にこち", "にこ"];

    public static bool ShouldActivate(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        return ActivationLabels.Contains(label.Trim(), StringComparer.Ordinal);
    }

    public static double NextSpawnDelaySeconds(Random random)
    {
        var range = Math.Max(0d, NicoEasterEggTuning.MaxSpawnDelaySeconds - NicoEasterEggTuning.MinSpawnDelaySeconds);
        return NicoEasterEggTuning.MinSpawnDelaySeconds + (random.NextDouble() * range);
    }

    public static NicoSpriteMotion CreateInitialMotion(Random random, int displayWidthPx, int imageWidthPx)
    {
        var safeWidth = Math.Max(1, displayWidthPx);
        return new NicoSpriteMotion(
            random.NextDouble() * safeWidth,
            0d,
            RandomHorizontalVelocity(random, imageWidthPx),
            0d);
    }

    public static double RandomHorizontalVelocity(Random random, int imageWidthPx)
    {
        var maxHorizontalSpeed = Math.Max(1d, imageWidthPx * NicoEasterEggTuning.HorizontalSpeedLimitMultiplier);
        var direction = random.Next(0, 2) == 0 ? -1d : 1d;
        return direction * random.NextDouble() * maxHorizontalSpeed;
    }

    public static bool ShouldUseMirroredBitmap(double horizontalVelocity)
    {
        return horizontalVelocity > 0d;
    }

    public static NicoSpriteMotion Step(
        NicoSpriteMotion motion,
        double deltaSeconds,
        double gravityPerSecondSquared,
        int imageWidthPx,
        int imageHeightPx,
        bool bounce,
        double bouncedHorizontalVelocity)
    {
        var maxHorizontalSpeed = Math.Max(1d, imageWidthPx * NicoEasterEggTuning.HorizontalSpeedLimitMultiplier);
        var maxVerticalSpeed = Math.Max(1d, imageHeightPx * NicoEasterEggTuning.VerticalSpeedLimitMultiplier);

        var nextHorizontalVelocity = Math.Clamp(motion.HorizontalVelocity, -maxHorizontalSpeed, maxHorizontalSpeed);
        var nextVerticalVelocity = Math.Clamp(
            motion.VerticalVelocity + (gravityPerSecondSquared * deltaSeconds),
            -maxVerticalSpeed,
            maxVerticalSpeed);

        if (bounce)
        {
            nextVerticalVelocity = -Math.Abs(nextVerticalVelocity) * NicoEasterEggTuning.BounceVerticalRestitution;
            nextHorizontalVelocity = Math.Clamp(bouncedHorizontalVelocity, -maxHorizontalSpeed, maxHorizontalSpeed);
        }

        return motion with
        {
            X = motion.X + (nextHorizontalVelocity * deltaSeconds),
            Y = motion.Y + (nextVerticalVelocity * deltaSeconds),
            HorizontalVelocity = nextHorizontalVelocity,
            VerticalVelocity = nextVerticalVelocity
        };
    }

    public static NicoWindowPlacement GetPlacement(NicoSpriteMotion motion, int imageWidthPx, int imageHeightPx)
    {
        return new NicoWindowPlacement(
            (int)Math.Round(motion.X - (imageWidthPx / 2d)),
            (int)Math.Round(motion.Y - imageHeightPx));
    }

    public static bool ShouldDespawn(double y, int displayHeightPx, int imageHeightPx)
    {
        return y > displayHeightPx + imageHeightPx;
    }
}

internal readonly record struct NicoSpriteMotion(
    double X,
    double Y,
    double HorizontalVelocity,
    double VerticalVelocity);

internal readonly record struct NicoWindowPlacement(int Left, int Top);
