using Xunit;

namespace ScrollToArms.Tests;

/// <summary>
/// Where one notch of the wheel puts the cursor on the hotbar. It stops only on slots holding
/// something to take in hand, such as a weapon, a shield, a tool or a torch, and passes over the
/// rest. Scrolling past one end continues at the other while Wrap is on, and stays at the end
/// while it is off. With nothing in hand the first notch starts from the end of the bar it moves
/// away from.
/// </summary>
public class NextStopTests
{
    private const int Right = 1;
    private const int Left = -1;
    private const int NothingInHand = -1;

    /// <summary>A bar written as its slots, W for something to stop on and . for anything else: "W..W....".</summary>
    private static int Next(string bar, int from, int step, bool wrap = true)
        => HotbarWalk.NextStop(bar.Length, from, step, wrap, slot => bar[slot] == 'W');

    [Fact]
    public void ANotchMovesToTheNextWeaponPassingOverWhatIsNotOne()
    {
        Assert.Equal(3, Next("W..W....", from: 0, Right));
    }

    [Fact]
    public void ANotchTheOtherWayMovesBackAlongTheBar()
    {
        Assert.Equal(0, Next("W..W....", from: 3, Left));
    }

    [Fact]
    public void PastTheLastWeaponTheCursorContinuesAtTheFirstWithWrapOn()
    {
        Assert.Equal(1, Next(".W....W.", from: 6, Right));
    }

    [Fact]
    public void PastTheFirstWeaponTheCursorContinuesAtTheLastWithWrapOn()
    {
        Assert.Equal(6, Next(".W....W.", from: 1, Left));
    }

    [Fact]
    public void WithWrapOffTheCursorStaysOnTheLastWeaponAtEitherEnd()
    {
        Assert.Equal(6, Next(".W....W.", from: 6, Right, wrap: false));
        Assert.Equal(1, Next(".W....W.", from: 1, Left, wrap: false));
    }

    [Fact]
    public void WithNothingInHandScrollingRightStartsFromTheLeftEnd()
    {
        Assert.Equal(2, Next("..W..W..", NothingInHand, Right));
        Assert.Equal(2, Next("..W..W..", NothingInHand, Right, wrap: false));
    }

    [Fact]
    public void WithNothingInHandScrollingLeftStartsFromTheRightEnd()
    {
        Assert.Equal(5, Next("..W..W..", NothingInHand, Left));
        Assert.Equal(5, Next("..W..W..", NothingInHand, Left, wrap: false));
    }

    [Fact]
    public void AWeaponInTheFirstOrLastSlotIsReachedFromNothingInHand()
    {
        Assert.Equal(0, Next("W......W", NothingInHand, Right));
        Assert.Equal(7, Next("W......W", NothingInHand, Left));
    }

    [Fact]
    public void WithNothingInHandTheFirstNotchFindsAWeaponAtTheFarEnd()
    {
        Assert.Equal(7, Next(".......W", NothingInHand, Right));
        Assert.Equal(0, Next("W.......", NothingInHand, Left));
    }

    [Fact]
    public void TheOnlyWeaponOnTheBarKeepsTheCursorOnIt()
    {
        Assert.Equal(3, Next("...W....", from: 3, Right));
        Assert.Equal(3, Next("...W....", from: 3, Left));
        Assert.Equal(3, Next("...W....", from: 3, Right, wrap: false));
    }

    [Fact]
    public void ABarWithNothingToTakeInHandGivesTheCursorNowhereToGo()
    {
        Assert.Equal(-1, Next("........", NothingInHand, Right));
        Assert.Equal(-1, Next("........", NothingInHand, Left, wrap: false));
    }

    [Fact]
    public void ANarrowerBarWrapsWithinItsOwnSlots()
    {
        Assert.Equal(0, Next("W..W", from: 3, Right));
        Assert.Equal(3, Next("W..W", from: 0, Left));
    }

    [Fact]
    public void ABarWithNoSlotsGivesNoStop()
    {
        Assert.Equal(-1, Next("", NothingInHand, Right));
    }
}
