using LiveAlert.Windows.Services;
using Xunit;

namespace LiveAlert.Windows.Tests;

public sealed class NicoEasterEggPhysicsTests
{
    [Theory]
    [InlineData("しののめにこ", true)]
    [InlineData(" にこち ", true)]
    [InlineData("にこ", true)]
    [InlineData("ニコ", false)]
    [InlineData("sample", false)]
    [InlineData("", false)]
    public void ShouldActivate_MatchesOnlySpecifiedLabels(string label, bool expected)
    {
        var actual = NicoEasterEggPhysics.ShouldActivate(label);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Step_BounceFlipsVerticalVelocityAndUsesReplacementHorizontalVelocity()
    {
        var motion = new NicoSpriteMotion(50d, 10d, 5d, 12d);

        var stepped = NicoEasterEggPhysics.Step(
            motion,
            0.5d,
            8d,
            imageWidthPx: 20,
            imageHeightPx: 30,
            bounce: true,
            bouncedHorizontalVelocity: -40d);

        Assert.Equal(30d, stepped.X, precision: 6);
        Assert.Equal(6d, stepped.Y, precision: 6);
        Assert.Equal(-40d, stepped.HorizontalVelocity, precision: 6);
        Assert.Equal(-8d, stepped.VerticalVelocity, precision: 6);
    }

    [Fact]
    public void Step_CapsVerticalVelocityAtConfiguredLimit()
    {
        var motion = new NicoSpriteMotion(0d, 0d, 0d, 80d);

        var stepped = NicoEasterEggPhysics.Step(
            motion,
            1d,
            100d,
            imageWidthPx: 20,
            imageHeightPx: 30,
            bounce: false,
            bouncedHorizontalVelocity: 0d);

        Assert.Equal(150d, stepped.VerticalVelocity, precision: 6);
        Assert.Equal(150d, stepped.Y, precision: 6);
    }

    [Fact]
    public void GetPlacement_UsesBottomCenterAsSpriteOrigin()
    {
        var placement = NicoEasterEggPhysics.GetPlacement(
            new NicoSpriteMotion(100d, 80d, 0d, 0d),
            imageWidthPx: 40,
            imageHeightPx: 60);

        Assert.Equal(80, placement.Left);
        Assert.Equal(20, placement.Top);
    }

    [Theory]
    [InlineData(-1d, false)]
    [InlineData(0d, false)]
    [InlineData(1d, true)]
    public void ShouldUseMirroredBitmap_UsesOnlyPositiveHorizontalVelocity(double horizontalVelocity, bool expected)
    {
        var actual = NicoEasterEggPhysics.ShouldUseMirroredBitmap(horizontalVelocity);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(150d, 100, 40, true)]
    [InlineData(140d, 100, 40, false)]
    public void ShouldDespawn_UsesDisplayHeightPlusImageHeight(double y, int displayHeight, int imageHeight, bool expected)
    {
        var actual = NicoEasterEggPhysics.ShouldDespawn(y, displayHeight, imageHeight);

        Assert.Equal(expected, actual);
    }
}
