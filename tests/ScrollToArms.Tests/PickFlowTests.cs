using Xunit;

namespace ScrollToArms.Tests;

/// <summary>
/// Plays the game frame by frame for the picker: keys, wheel and time.
/// </summary>
internal sealed class Play
{
    public Play(params string[] bar)
    {
        for (var i = 0; i < bar.Length; i++) World.Bar[i] = bar[i];
        Flow = new PickFlow<string>(World);
    }

    public FakeWorld World { get; } = new FakeWorld();

    public PickFlow<string> Flow { get; }

    public float Now { get; private set; } = 10f;

    public void Frame(float dt = 0.02f)
    {
        Now += dt;
        Flow.Update(Now);
        World.ModifierDown = false;
        World.ModifierUp = false;
        World.OtherKeyDown = false;
        World.Wheel = 0f;
    }

    public void Frames(float seconds)
    {
        var until = Now + seconds;
        while (Now < until - 1e-4f) Frame();
    }

    public void Press()
    {
        World.ModifierDown = true;
        World.ModifierHeld = true;
        Frame();
    }

    public void Release()
    {
        World.ModifierHeld = false;
        World.ModifierUp = true;
        Frame();
    }

    /// <summary>One notch of the wheel: 1 scrolls down, which moves right, -1 scrolls up.</summary>
    public void Notch(int direction)
    {
        World.Wheel = direction > 0 ? -0.1f : 0.1f;
        Frame();
    }

    /// <summary>Holds the modifier, turns the wheel a notch each way given, and lets go.</summary>
    public void Pick(params int[] notches)
    {
        Press();
        foreach (var notch in notches) Notch(notch);
        Release();
    }

    public void Tap()
    {
        Press();
        Release();
    }
}

/// <summary>
/// The pick, from the first notch until the item is in hand. With PlainScroll on Zoom the wheel
/// picks while the modifier is held, and letting go takes the framed item in hand; with Commit on
/// AfterPause, or PlainScroll on Hotbar, the pick goes in once the wheel has been still for
/// PauseTime. The game decides when the item arrives: it queues an item with an equip time behind
/// its equip bar, refuses any equip while attacking, dodging or swimming and drops a queued one on
/// a run, a jump, a dodge or an attack. Such a pick is held for WaitWhileBusy seconds and asked for
/// again as soon as the game allows; one the game refuses outright is dropped. The latest pick
/// wins, a pick landing on the held slot puts that item away as its number key would, and a tap on
/// the modifier goes back to what was held before.
/// </summary>
public class PickFlowTests
{
    private static Play Bar() => new Play("sword", "meat", "axe", null, "bow", "meat_cooked", "club", "torch");

    [Fact]
    public void TurningTheWheelWithTheModifierHeldMovesTheFrameFromStopToStop()
    {
        var play = Bar();
        play.World.Right = "sword";

        play.Press();
        play.Notch(1);
        Assert.True(play.Flow.Choosing);
        Assert.Equal(2, play.Flow.Cursor);

        play.Notch(1);
        Assert.Equal(4, play.Flow.Cursor);
    }

    [Fact]
    public void LettingGoOfTheModifierTakesTheFramedItemInHand()
    {
        var play = Bar();
        play.World.Right = "sword";

        play.Pick(1);
        Assert.Equal(1, play.World.Count("equip axe"));
        Assert.Equal("axe", play.Flow.Pending);

        play.World.Finish();
        play.Frame();
        Assert.Equal("axe", play.World.Right);
        Assert.Null(play.Flow.Pending);
        Assert.Equal(1, play.World.Count("equip axe"));
    }

    [Fact]
    public void NothingGoesInHandWhileTheModifierIsStillHeld()
    {
        var play = Bar();
        play.World.Right = "sword";

        play.Press();
        play.Notch(1);
        play.Frames(2f);

        Assert.Empty(play.World.Calls);
        Assert.True(play.Flow.Choosing);
    }

    [Fact]
    public void EveryMoveOfTheFrameTicks()
    {
        var play = Bar();
        play.World.Right = "sword";

        play.Press();
        play.Notch(1);
        play.Notch(1);

        Assert.Equal(2, play.World.Ticks);
    }

    [Fact]
    public void ANotchThatCannotMoveTheFrameDoesNotTick()
    {
        var play = new Play("meat", "axe", "meat");
        play.World.Right = "axe";

        play.Press();
        play.Notch(1);

        Assert.Equal(0, play.World.Ticks);
        Assert.Equal(1, play.Flow.Cursor);
    }

    [Fact]
    public void WithNothingInHandTheFirstNotchStartsFromTheEndOfTheBarItMovesAwayFrom()
    {
        var right = Bar();
        right.Press();
        right.Notch(1);
        Assert.Equal(0, right.Flow.Cursor);

        var left = Bar();
        left.Press();
        left.Notch(-1);
        Assert.Equal(7, left.Flow.Cursor);
    }

    [Fact]
    public void AfterTheHideKeyTheFrameStartsFromThePutAwayItem()
    {
        var play = Bar();
        play.World.StowedRight = "axe";

        play.Press();
        play.Notch(1);

        Assert.Equal(4, play.Flow.Cursor);
    }

    [Fact]
    public void TheFrameStartsFromTheLeftHandWhenTheRightHoldsNothingOnTheBar()
    {
        var play = new Play("sword", "meat", "shield", null, "bow");
        play.World.Left = "shield";

        play.Press();
        play.Notch(1);

        Assert.Equal(4, play.Flow.Cursor);
    }

    [Fact]
    public void ABarWithNothingToTakeInHandStartsNoPick()
    {
        var play = new Play("meat", null, "meat_cooked");

        play.Press();
        play.Notch(1);
        Assert.False(play.Flow.Choosing);

        play.Release();
        Assert.Equal(0, play.World.Ticks);
        Assert.Empty(play.World.Calls);
    }

    [Fact]
    public void WithNothingElseToStopOnAHeldItemTheWheelPassesOverIsNotPutAway()
    {
        var play = new Play("meat", "meat_hammer", "meat_cooked");
        play.World.Right = "meat_hammer";

        play.Press();
        play.Notch(1);
        play.Release();

        Assert.Empty(play.World.Calls);
        Assert.Equal("meat_hammer", play.World.Right);
    }

    [Fact]
    public void WithCommitOnAfterPauseThePickGoesInOnceTheWheelHasBeenStillForPauseTime()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.World.Commit = Commit.AfterPause;

        play.Press();
        play.Notch(1);
        play.Frames(0.36f);
        Assert.Empty(play.World.Calls);

        play.Frames(0.06f);
        Assert.Equal(1, play.World.Count("equip axe"));
        Assert.False(play.Flow.Choosing);
    }

    [Fact]
    public void AnotherNotchStartsThePauseAgain()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.World.Commit = Commit.AfterPause;

        play.Press();
        play.Notch(1);
        play.Frames(0.3f);
        play.Notch(1);
        play.Frames(0.3f);
        Assert.Empty(play.World.Calls);

        play.Frames(0.12f);
        Assert.Equal(1, play.World.Count("equip bow"));
    }

    [Fact]
    public void WithPlainScrollOnHotbarThePlainWheelPicksAndThePauseTakesItInHand()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.World.PlainScroll = PlainScroll.Hotbar;
        play.World.Commit = Commit.AfterPause;

        play.Notch(1);
        Assert.Equal(2, play.Flow.Cursor);

        play.Frames(0.42f);
        Assert.Equal(1, play.World.Count("equip axe"));
    }

    [Fact]
    public void InvertDirectionTurnsTheWheelTheOtherWay()
    {
        var play = Bar();
        play.World.Right = "axe";
        play.World.Invert = true;

        play.Press();
        play.Notch(1);

        Assert.Equal(0, play.Flow.Cursor);
    }

    [Fact]
    public void WhileTheWheelIsTheGamesItMovesNoFrame()
    {
        var play = Bar();
        play.World.Right = "sword";

        play.Notch(1);

        Assert.False(play.Flow.Choosing);
        Assert.Equal(0, play.World.Ticks);
    }

    [Fact]
    public void TheLatestPickWins()
    {
        var play = Bar();
        play.World.Right = "sword";

        play.Pick(1);
        play.Pick(1);

        Assert.Equal(1, play.World.Count("withdraw axe"));
        Assert.Equal(1, play.World.Count("equip bow"));
        Assert.Equal("bow", play.Flow.Pending);
    }

    [Fact]
    public void ScrollingOnWhileAPickIsOnItsWayStartsFromThatPick()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Pick(1);

        play.Press();
        play.Notch(1);

        Assert.Equal(4, play.Flow.Cursor);
    }

    [Fact]
    public void ScrollingBackOntoThePickOnItsWayKeepsIt()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Pick(1);

        play.Pick(1, -1);

        Assert.Equal(0, play.World.Count("withdraw axe"));
        Assert.Equal(1, play.World.Count("equip axe"));
        Assert.Equal("axe", play.Flow.Pending);
    }

    [Fact]
    public void GoingBackToTheHeldItemOnlyWithdrawsThePickOnItsWay()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Pick(1);

        play.Pick(-1);

        Assert.Equal(1, play.World.Count("withdraw axe"));
        Assert.Equal(0, play.World.Count("equip sword"));
        Assert.Equal("sword", play.World.Right);
        Assert.Null(play.Flow.Pending);
    }

    [Fact]
    public void APickLandingOnTheHeldSlotPutsThatItemAwayAsItsNumberKeyWould()
    {
        var play = Bar();
        play.World.Right = "sword";

        play.Pick(1, -1);
        Assert.Equal(1, play.World.Count("equip sword"));
        Assert.Equal("sword", play.Flow.Pending);

        play.World.Finish();
        play.Frame();
        Assert.Null(play.World.Right);
        Assert.Null(play.Flow.Pending);
    }

    [Fact]
    public void ThePickIsFollowedThroughTheGamesEquipBarUntilTheItemIsInHand()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Pick(1);

        play.Frames(1f);
        Assert.Equal("axe", play.Flow.Pending);
        Assert.False(play.Flow.Waiting);

        play.World.Finish();
        play.Frame();
        Assert.Null(play.Flow.Pending);
        Assert.Equal(1, play.World.Count("equip axe"));
    }

    [Fact]
    public void AnItemWithoutAnEquipTimeIsInHandAtOnce()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.World.Instant = true;

        play.Pick(1);

        Assert.Equal("axe", play.World.Right);
        Assert.Null(play.Flow.Pending);
    }

    [Fact]
    public void ANumberKeyDuringAPickEndsIt()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Press();
        play.Notch(1);

        play.World.Right = "club";
        play.Frame();
        Assert.False(play.Flow.Choosing);
        Assert.Equal(-1, play.Flow.Cursor);

        play.Release();
        Assert.Empty(play.World.Calls);
    }

    [Fact]
    public void ANumberKeyWhileAPickIsOnItsWayDropsItWithoutCountingItLost()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Pick(1);

        play.World.Right = "club";
        play.Frame();

        Assert.Null(play.Flow.Pending);
        Assert.Equal(-1, play.Flow.LostSlot);
        Assert.Equal(0, play.World.LostSounds);
    }

    [Fact]
    public void LeavingThePlacesWhereTheHotbarWorksEndsThePick()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Press();
        play.Notch(1);

        play.World.CanPick = false;
        play.Frame();

        Assert.False(play.Flow.Choosing);
        Assert.Null(play.Flow.Pending);
    }

    [Fact]
    public void APickRefusedDuringAnAttackGoesInOnceTheAttackEnds()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.World.BusyReason = "attacking";

        play.Pick(1);
        var waitBegan = play.Now;
        play.Frames(1f);
        Assert.Empty(play.World.Calls);
        Assert.True(play.Flow.Waiting);
        Assert.Equal(waitBegan, play.Flow.WaitingSince, 3);

        play.World.BusyReason = null;
        play.Frame();
        Assert.Equal(1, play.World.Count("equip axe"));
        Assert.False(play.Flow.Waiting);
    }

    [Fact]
    public void APickHeldBackLongerThanWaitWhileBusyIsLost()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.World.BusyReason = "dodging";

        play.Pick(1);
        play.Frames(1.9f);
        Assert.Equal("axe", play.Flow.Pending);

        play.Frames(0.2f);
        Assert.Null(play.Flow.Pending);
        Assert.Equal(2, play.Flow.LostSlot);
        Assert.InRange(play.Flow.LostAt, play.Now - 0.25f, play.Now);
        Assert.Equal(1, play.World.LostSounds);
        Assert.Empty(play.World.Calls);
    }

    [Fact]
    public void WithWaitWhileBusyZeroAHeldBackPickIsLostAtOnceAsWithTheNumberKeys()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.World.WaitWhileBusy = 0f;
        play.World.BusyReason = "attacking";

        play.Pick(1);

        Assert.Null(play.Flow.Pending);
        Assert.Equal(2, play.Flow.LostSlot);
    }

    [Fact]
    public void TheGamesEquipBarDoesNotCountAgainstTheWait()
    {
        var play = Bar();
        play.World.Right = "sword";

        play.Pick(1);
        play.Frames(3f);
        Assert.Equal("axe", play.Flow.Pending);

        play.World.Finish();
        play.Frame();
        Assert.Equal("axe", play.World.Right);
        Assert.Equal(-1, play.Flow.LostSlot);
    }

    [Fact]
    public void APickARunClearedFromTheQueueIsAskedForAgainOnceTheRunEnds()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Pick(1);
        play.Frames(0.1f);

        play.World.ClearQueue();
        play.World.BusyReason = "running";
        play.Frames(1f);
        Assert.Equal(1, play.World.Count("equip axe"));
        Assert.True(play.Flow.Waiting);

        play.World.BusyReason = null;
        play.Frame();
        Assert.Equal(2, play.World.Count("equip axe"));

        play.World.Finish();
        play.Frame();
        Assert.Equal("axe", play.World.Right);
        Assert.Equal(new[] { "axe" }, play.World.Watched);
    }

    [Fact]
    public void TheWaitStartsWhenTheQueueWasLastSeenNotWhenThePickWasMade()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Pick(1);
        play.Frames(3f);

        play.World.ClearQueue();
        play.World.BusyReason = "running";
        play.Frames(1.5f);

        Assert.Equal("axe", play.Flow.Pending);
    }

    [Fact]
    public void APickTheGameRefusesWithoutQueueingIsDroppedRatherThanAskedForEveryFrame()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.World.Refuses = true;

        play.Pick(1);
        play.Frames(1f);

        Assert.Equal(1, play.World.Count("equip axe"));
        Assert.Null(play.Flow.Pending);
        Assert.Equal(2, play.Flow.LostSlot);
        Assert.Equal(1, play.World.LostSounds);
    }

    [Fact]
    public void PickingThePutAwayItemBringsBothHandsBackAsTheHideKeyWould()
    {
        var play = new Play("sword", "meat", "axe", null, "bow");
        play.World.StowedRight = "axe";
        play.World.StowedLeft = "shield";

        play.Pick(1, -1);

        Assert.Equal(1, play.World.Count("unstow"));
        Assert.Equal(0, play.World.Count("equip axe"));
        Assert.Equal("axe", play.World.Right);
        Assert.Equal("shield", play.World.Left);
    }

    [Fact]
    public void TheToolMessageIsAskedForOnceForAnItemTheWheelTakesOutButNotForOneItPutsAway()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.World.Instant = true;

        play.Pick(1);
        Assert.Equal(new[] { "axe" }, play.World.Watched);

        play.Pick(1, -1);
        Assert.Equal(new[] { "axe" }, play.World.Watched);
    }

    [Fact]
    public void ATapOnTheModifierGoesBackToWhatWasHeldBefore()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Frame();
        play.World.Right = "axe";
        play.Frame();

        play.Tap();

        Assert.Equal(1, play.World.Count("equip sword"));
    }

    [Fact]
    public void ATapAfterAWheelPickGoesBackToWhatWasHeldBeforeIt()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.World.Instant = true;
        play.Pick(1);

        play.Tap();

        Assert.Equal("sword", play.World.Right);
    }

    [Fact]
    public void APressLongerThanThreeTenthsOfASecondIsAHoldNotATap()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Frame();
        play.World.Right = "axe";
        play.Frame();

        play.Press();
        play.Frames(0.32f);
        play.Release();

        Assert.Empty(play.World.Calls);
    }

    [Fact]
    public void AnotherKeyDuringThePressMakesItAHold()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Frame();
        play.World.Right = "axe";
        play.Frame();

        play.Press();
        play.World.OtherKeyDown = true;
        play.Frame();
        play.Release();

        Assert.Empty(play.World.Calls);
    }

    [Fact]
    public void TheWheelDuringThePressMakesItAHoldEvenWhenItMovesNothing()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Frame();
        play.World.Right = "axe";
        play.Frame();

        play.Press();
        play.World.Wheel = -0.05f;
        play.Frame();
        play.Release();

        Assert.Empty(play.World.Calls);
    }

    [Fact]
    public void LeavingTheGameWindowDuringThePressMakesItAHold()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Frame();
        play.World.Right = "axe";
        play.Frame();

        play.Press();
        play.World.Focused = false;
        play.Frame();
        play.World.Focused = true;
        play.Release();

        Assert.Empty(play.World.Calls);
    }

    [Fact]
    public void WithFlipBackOffATapDoesNothing()
    {
        var play = Bar();
        play.World.FlipBack = false;
        play.World.Right = "sword";
        play.Frame();
        play.World.Right = "axe";
        play.Frame();

        play.Tap();

        Assert.Empty(play.World.Calls);
    }

    [Fact]
    public void ATapWhileAPickIsOnItsWayWithdrawsItAndKeepsWhatIsInHand()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Pick(1);

        play.Tap();

        Assert.Equal(1, play.World.Count("withdraw axe"));
        Assert.Equal(0, play.World.Count("equip sword"));
        Assert.Equal("sword", play.World.Right);
        Assert.Null(play.Flow.Pending);
    }

    [Fact]
    public void ATapWithEmptyHandsBringsBackTheLastThingHeld()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Frame();
        play.World.Right = null;
        play.World.StowedRight = "sword";
        play.Frame();

        play.Tap();

        Assert.Equal(1, play.World.Count("unstow"));
        Assert.Equal("sword", play.World.Right);
    }

    [Fact]
    public void ATapWithNothingHeldBeforeDoesNothing()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Frame();

        play.Tap();

        Assert.Empty(play.World.Calls);
    }

    [Fact]
    public void ATapOutsideThePlacesWhereTheHotbarWorksDoesNotCount()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Frame();
        play.World.Right = "axe";
        play.Frame();

        play.World.CanPick = false;
        play.Press();
        play.World.CanPick = true;
        play.Release();

        Assert.Empty(play.World.Calls);
    }

    [Fact]
    public void APickWhoseItemLeavesItsSlotIsDroppedWithoutCountingItLost()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Pick(1);

        play.World.Bar[2] = null;
        play.Frame();

        Assert.Null(play.Flow.Pending);
        Assert.Equal(-1, play.Flow.LostSlot);
    }

    [Fact]
    public void APickAskedForAgainAfterARunAndThenRefusedOutrightIsDropped()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Pick(1);
        play.Frames(0.1f);
        play.World.ClearQueue();
        play.World.BusyReason = "running";
        play.Frames(0.5f);

        play.World.Refuses = true;
        play.World.BusyReason = null;
        play.Frames(1f);

        Assert.Equal(2, play.World.Count("equip axe"));
        Assert.Null(play.Flow.Pending);
        Assert.Equal(2, play.Flow.LostSlot);
    }

    [Fact]
    public void AfterTheHideKeyWithOnlyAShieldPutAwayTheFrameStartsFromTheShield()
    {
        var play = new Play("sword", "meat", "shield", null, "bow");
        play.World.StowedLeft = "shield";

        play.Press();
        play.Notch(1);

        Assert.Equal(4, play.Flow.Cursor);
    }

    [Fact]
    public void TakingUpAShieldAnotherWayDuringAPickEndsIt()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Press();
        play.Notch(1);

        play.World.Left = "shield";
        play.Frame();

        Assert.False(play.Flow.Choosing);
    }

    [Fact]
    public void AnEarlierPickArrivingWhileTheNextIsChosenDoesNotEndTheChoice()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Pick(1);

        play.Press();
        play.Notch(1);
        play.World.Finish();
        play.Frame();
        Assert.True(play.Flow.Choosing);
        Assert.Equal(4, play.Flow.Cursor);

        play.Release();
        Assert.Equal(1, play.World.Count("equip bow"));
    }

    [Fact]
    public void PickingAnItemTheGameIsAlreadyEquippingLeavesItToTheGame()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.World.QueuedItems.Add("axe");

        play.Pick(1);

        Assert.Empty(play.World.Calls);
        Assert.Null(play.Flow.Pending);
    }

    [Fact]
    public void ATapNeverPutsAwayWhatIsStillInHand()
    {
        var play = new Play("sword", "meat", "shield");
        play.World.Left = "shield";
        play.Frame();
        play.World.Right = "sword";
        play.Frame();

        play.Tap();

        Assert.Empty(play.World.Calls);
        Assert.Equal("shield", play.World.Left);
    }

    [Fact]
    public void APartOfANotchFromBeforeTheHotbarWasLeftDoesNotCountAfterwards()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Press();
        play.World.Wheel = -0.05f;
        play.Frame();

        play.World.CanPick = false;
        play.Frame();
        play.World.CanPick = true;
        play.World.Wheel = -0.05f;
        play.Frame();

        Assert.False(play.Flow.Choosing);
    }

    [Fact]
    public void ScrollingWithTheModifierHeldIsAPickNotATap()
    {
        var play = Bar();
        play.World.Right = "sword";
        play.Frame();
        play.World.Right = "club";
        play.Frame();

        play.Pick(1);

        Assert.Equal(1, play.World.Count("equip torch"));
        Assert.Equal(0, play.World.Count("equip sword"));
    }
}
