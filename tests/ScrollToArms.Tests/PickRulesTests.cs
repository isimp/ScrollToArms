using Xunit;
using ItemType = ItemDrop.ItemData.ItemType;

namespace ScrollToArms.Tests;

/// <summary>
/// What the wheel stops on, what holds a pick back, who the wheel belongs to and when a pick goes
/// into the hand. Stops are things held in the hands that can be equipped now: weapons, bows,
/// shields, tools and the torch; food, armour and materials are passed over, and so is a broken
/// item, a build tool while SkipBuildTools is on, and anything on SkipItems. Attacking, dodging
/// and swimming hold any pick back; running holds back an item with an equip time, since the game
/// clears its equip queue while running, unless it is the stowed item coming back.
/// </summary>
public class PickRulesTests
{
    private static bool Stop(ItemType type, bool equipable = true, bool buildTool = false, bool skipBuildTools = false,
        bool listed = false, bool wears = false, float durability = 100f)
        => PickRules.IsStop(equipable, type, buildTool, skipBuildTools, listed, wears, durability);

    [Theory]
    [InlineData(ItemType.OneHandedWeapon)]
    [InlineData(ItemType.TwoHandedWeapon)]
    [InlineData(ItemType.TwoHandedWeaponLeft)]
    [InlineData(ItemType.Bow)]
    [InlineData(ItemType.Shield)]
    [InlineData(ItemType.Torch)]
    [InlineData(ItemType.Tool)]
    public void WeaponsBowsShieldsToolsAndTheTorchAreStops(ItemType type)
    {
        Assert.True(Stop(type));
    }

    [Theory]
    [InlineData(ItemType.Consumable)]
    [InlineData(ItemType.Helmet)]
    [InlineData(ItemType.Chest)]
    [InlineData(ItemType.Legs)]
    [InlineData(ItemType.Shoulder)]
    [InlineData(ItemType.Utility)]
    [InlineData(ItemType.Material)]
    [InlineData(ItemType.Ammo)]
    [InlineData(ItemType.Trophy)]
    public void FoodArmourMaterialsAndAmmoArePassedOver(ItemType type)
    {
        Assert.False(Stop(type));
    }

    [Fact]
    public void AnItemThatCannotBeEquippedIsPassedOver()
    {
        Assert.False(Stop(ItemType.OneHandedWeapon, equipable: false));
    }

    [Fact]
    public void ABrokenWeaponIsPassedOverAsTheGameWouldRefuseIt()
    {
        Assert.False(Stop(ItemType.OneHandedWeapon, wears: true, durability: 0f));
        Assert.True(Stop(ItemType.OneHandedWeapon, wears: true, durability: 0.5f));
    }

    [Fact]
    public void AnItemThatDoesNotWearIsNeverBroken()
    {
        Assert.True(Stop(ItemType.Torch, wears: false, durability: 0f));
    }

    [Fact]
    public void BuildToolsArePassedOverOnlyWithSkipBuildToolsOn()
    {
        Assert.True(Stop(ItemType.Tool, buildTool: true, skipBuildTools: false));
        Assert.False(Stop(ItemType.Tool, buildTool: true, skipBuildTools: true));
        Assert.True(Stop(ItemType.Tool, buildTool: false, skipBuildTools: true));
    }

    [Fact]
    public void AnythingOnSkipItemsIsPassedOver()
    {
        Assert.False(Stop(ItemType.OneHandedWeapon, listed: true));
    }

    [Fact]
    public void NothingHoldsAPickBackInOrdinaryPlay()
    {
        Assert.Null(PickRules.Busy(attacking: false, dodging: false, swimming: false, onGround: true, equipTime: true, running: false, stowed: false));
    }

    [Fact]
    public void AnAttackOrADodgeHoldsAnyPickBack()
    {
        Assert.NotNull(PickRules.Busy(attacking: true, dodging: false, swimming: false, onGround: true, equipTime: false, running: false, stowed: false));
        Assert.NotNull(PickRules.Busy(attacking: false, dodging: true, swimming: false, onGround: true, equipTime: false, running: false, stowed: false));
    }

    [Fact]
    public void SwimmingHoldsAPickBackUnlessThePlayerStandsOnTheBottom()
    {
        Assert.NotNull(PickRules.Busy(attacking: false, dodging: false, swimming: true, onGround: false, equipTime: false, running: false, stowed: false));
        Assert.Null(PickRules.Busy(attacking: false, dodging: false, swimming: true, onGround: true, equipTime: false, running: false, stowed: false));
    }

    [Fact]
    public void RunningHoldsBackOnlyAnItemWithAnEquipTime()
    {
        Assert.NotNull(PickRules.Busy(attacking: false, dodging: false, swimming: false, onGround: true, equipTime: true, running: true, stowed: false));
        Assert.Null(PickRules.Busy(attacking: false, dodging: false, swimming: false, onGround: true, equipTime: false, running: true, stowed: false));
    }

    [Fact]
    public void RunningDoesNotHoldBackTheStowedItemComingBack()
    {
        Assert.Null(PickRules.Busy(attacking: false, dodging: false, swimming: false, onGround: true, equipTime: true, running: true, stowed: true));
    }

    [Fact]
    public void WithPlainScrollOnZoomTheModifierGivesTheWheelToTheHotbar()
    {
        Assert.True(PickRules.OwnsWheel(modifierHeld: true, placing: false, placementReady: true, PlainScroll.Zoom));
        Assert.False(PickRules.OwnsWheel(modifierHeld: false, placing: false, placementReady: true, PlainScroll.Zoom));
    }

    [Fact]
    public void WithPlainScrollOnHotbarThePlainWheelPicksAndTheModifierZooms()
    {
        Assert.True(PickRules.OwnsWheel(modifierHeld: false, placing: false, placementReady: true, PlainScroll.Hotbar));
        Assert.False(PickRules.OwnsWheel(modifierHeld: true, placing: false, placementReady: true, PlainScroll.Hotbar));
    }

    [Fact]
    public void InBuildModeTheHotbarIsAlwaysOnTheModifierAsThePlainWheelRotatesThePiece()
    {
        Assert.True(PickRules.OwnsWheel(modifierHeld: true, placing: true, placementReady: true, PlainScroll.Hotbar));
        Assert.False(PickRules.OwnsWheel(modifierHeld: false, placing: true, placementReady: true, PlainScroll.Hotbar));
        Assert.False(PickRules.OwnsWheel(modifierHeld: false, placing: true, placementReady: true, PlainScroll.Zoom));
    }

    [Fact]
    public void InBuildModeWithoutTheRotationPatchTheWheelStaysTheGames()
    {
        Assert.False(PickRules.OwnsWheel(modifierHeld: true, placing: true, placementReady: false, PlainScroll.Zoom));
    }

    [Fact]
    public void WithPlainScrollOnHotbarAPickGoesInAfterThePauseWhateverCommitSays()
    {
        Assert.Equal(Commit.AfterPause, PickRules.EffectiveCommit(PlainScroll.Hotbar, Commit.OnRelease));
        Assert.Equal(Commit.AfterPause, PickRules.EffectiveCommit(PlainScroll.Hotbar, Commit.AfterPause));
    }

    [Fact]
    public void WithPlainScrollOnZoomCommitDecides()
    {
        Assert.Equal(Commit.OnRelease, PickRules.EffectiveCommit(PlainScroll.Zoom, Commit.OnRelease));
        Assert.Equal(Commit.AfterPause, PickRules.EffectiveCommit(PlainScroll.Zoom, Commit.AfterPause));
    }
}
