using Xunit;

namespace ScrollToArms.Tests;

/// <summary>
/// How the wheel's readings become steps along the hotbar. One notch is the step the game uses
/// to rotate a building piece, 0.1 of the wheel. Scrolling down moves right and scrolling up
/// moves left, swapped by InvertDirection. Less than a notch moves nothing; the readings of a
/// notch that comes in parts add up, and a part left alone is dropped rather than completed by
/// a later turn of the wheel.
/// </summary>
public class NotchTests
{
    private const float Up = 0.1f;
    private const float Down = -0.1f;

    [Fact]
    public void ANotchDownMovesRightAndANotchUpMovesLeft()
    {
        var wheel = new Notches();

        Assert.Equal(1, wheel.Read(Down, now: 1f, invert: false));
        Assert.Equal(-1, wheel.Read(Up, now: 2f, invert: false));
    }

    [Fact]
    public void InvertDirectionSwapsThem()
    {
        var wheel = new Notches();

        Assert.Equal(-1, wheel.Read(Down, now: 1f, invert: true));
        Assert.Equal(1, wheel.Read(Up, now: 2f, invert: true));
    }

    [Fact]
    public void AStillWheelMovesNothing()
    {
        Assert.Equal(0, new Notches().Read(0f, now: 1f, invert: false));
    }

    [Fact]
    public void LessThanANotchMovesNothing()
    {
        Assert.Equal(0, new Notches().Read(Down / 2, now: 1f, invert: false));
    }

    [Fact]
    public void ANotchThatComesInPartsMovesOnceWhenItIsWhole()
    {
        var wheel = new Notches();

        Assert.Equal(0, wheel.Read(Down / 2, now: 1f, invert: false));
        Assert.Equal(1, wheel.Read(Down / 2, now: 1.016f, invert: false));
        Assert.Equal(0, wheel.Read(Down / 4, now: 1.032f, invert: false));
    }

    [Fact]
    public void APartOfANotchLeftAloneIsDroppedRatherThanCompletedLater()
    {
        var wheel = new Notches();

        wheel.Read(Down / 2, now: 10f, invert: false);
        wheel.Read(0f, now: 10.5f, invert: false);

        Assert.Equal(0, wheel.Read(Down / 2, now: 10.6f, invert: false));
    }

    [Fact]
    public void ClearingDropsAPartOfANotch()
    {
        var wheel = new Notches();

        wheel.Read(Down / 2, now: 1f, invert: false);
        wheel.Clear();

        Assert.Equal(0, wheel.Read(Down / 2, now: 1.016f, invert: false));
    }

    [Fact]
    public void TheLastNotchIsWhenTheWheelLastMovedTheCursor()
    {
        var wheel = new Notches();

        wheel.Read(Down, now: 2f, invert: false);
        wheel.Read(Down / 2, now: 3f, invert: false);
        wheel.Read(0f, now: 4f, invert: false);

        Assert.Equal(2f, wheel.LastNotch);
    }
}
