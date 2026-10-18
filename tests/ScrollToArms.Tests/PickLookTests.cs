using Xunit;

namespace ScrollToArms.Tests;

/// <summary>
/// How the hotbar marks a pick. The frame sits on the cursor while choosing and on the pick still
/// on its way after that. A pick held back while the game would refuse it pulses twice a second,
/// starting dim the moment the wait begins. A lost pick flashes red on its slot and fades out over
/// half a second, unless the frame is back on that slot. The name above the bar follows the frame,
/// or the fading flash, and is faint.
/// </summary>
public class PickLookTests
{
    [Fact]
    public void WhileChoosingTheFrameFollowsTheCursor()
    {
        Assert.Equal(3, PickLook.Frame(choosing: true, cursor: 3, pendingSlot: 1));
    }

    [Fact]
    public void AfterThePickTheFrameStaysOnItUntilItArrives()
    {
        Assert.Equal(1, PickLook.Frame(choosing: false, cursor: -1, pendingSlot: 1));
        Assert.Equal(-1, PickLook.Frame(choosing: false, cursor: -1, pendingSlot: -1));
    }

    [Fact]
    public void ALostPickFlashesOnItsSlotForHalfASecond()
    {
        Assert.Equal(4, PickLook.Flash(lostSlot: 4, age: 0f, frame: -1));
        Assert.Equal(4, PickLook.Flash(lostSlot: 4, age: 0.49f, frame: -1));
        Assert.Equal(-1, PickLook.Flash(lostSlot: 4, age: 0.5f, frame: -1));
    }

    [Fact]
    public void NoFlashWhereTheFrameIsBack()
    {
        Assert.Equal(-1, PickLook.Flash(lostSlot: 4, age: 0.1f, frame: 4));
        Assert.Equal(4, PickLook.Flash(lostSlot: 4, age: 0.1f, frame: 2));
    }

    [Fact]
    public void NothingLostNothingFlashes()
    {
        Assert.Equal(-1, PickLook.Flash(lostSlot: -1, age: 0f, frame: -1));
    }

    [Fact]
    public void TheFlashFadesFromFullToNothing()
    {
        Assert.Equal(1f, PickLook.Fade(age: 0f), 3);
        Assert.Equal(0.5f, PickLook.Fade(age: 0.25f), 3);
        Assert.Equal(0f, PickLook.Fade(age: 0.5f), 3);
        Assert.Equal(0f, PickLook.Fade(age: 2f), 3);
    }

    [Fact]
    public void OnlyAPickWaitingForTheGamePulsesNotOneBeingChosen()
    {
        Assert.True(PickLook.Pulses(choosing: false, waiting: true));
        Assert.False(PickLook.Pulses(choosing: true, waiting: true));
        Assert.False(PickLook.Pulses(choosing: false, waiting: false));
    }

    [Fact]
    public void ThePulseStartsDimTheMomentTheWaitBeginsAndPeaksAQuarterSecondLater()
    {
        Assert.Equal(0.15f, PickLook.PulseAlpha(sinceWait: 0f), 3);
        Assert.Equal(1f, PickLook.PulseAlpha(sinceWait: 0.25f), 3);
        Assert.Equal(0.15f, PickLook.PulseAlpha(sinceWait: 0.5f), 3);
    }

    [Fact]
    public void ThePulseNeverGoesBelowItsDimLevelOrAboveFull()
    {
        for (var t = 0f; t < 2f; t += 0.01f)
        {
            var alpha = PickLook.PulseAlpha(t);
            Assert.InRange(alpha, 0.15f - 1e-4f, 1f + 1e-4f);
        }
    }

    [Fact]
    public void TheBarIsLeftToTheGameWhenNothingIsPickedWaitingOrFlashing()
    {
        Assert.False(PickLook.Shows(frame: -1, waiting: -1, flash: -1));
        Assert.True(PickLook.Shows(frame: 2, waiting: -1, flash: -1));
        Assert.True(PickLook.Shows(frame: -1, waiting: 2, flash: -1));
        Assert.True(PickLook.Shows(frame: -1, waiting: -1, flash: 2));
    }

    [Fact]
    public void ASlotShowsQueuedForThePickOnItsWayOrAnyItemTheGameQueued()
    {
        Assert.True(PickLook.Queued(slot: 2, waitingSlot: 2, itemQueued: false));
        Assert.True(PickLook.Queued(slot: 5, waitingSlot: 2, itemQueued: true));
        Assert.False(PickLook.Queued(slot: 5, waitingSlot: 2, itemQueued: false));
    }

    [Fact]
    public void TheNameFollowsTheFrameOrElseTheFlash()
    {
        Assert.Equal(3, PickLook.NamedSlot(frame: 3, flash: 5));
        Assert.Equal(5, PickLook.NamedSlot(frame: -1, flash: 5));
        Assert.Equal(-1, PickLook.NamedSlot(frame: -1, flash: -1));
    }

    [Fact]
    public void TheNameIsFaintAndALostPicksNameFadesInRedWithItsFrame()
    {
        Assert.Equal(0.55f, PickLook.NameAlpha(lostName: false, fade: 0.2f), 3);
        Assert.Equal(0.55f * 0.2f, PickLook.NameAlpha(lostName: true, fade: 0.2f), 3);
        Assert.True(PickLook.LostName(hasItem: true, frame: -1));
        Assert.False(PickLook.LostName(hasItem: true, frame: 3));
        Assert.False(PickLook.LostName(hasItem: false, frame: -1));
    }
}
