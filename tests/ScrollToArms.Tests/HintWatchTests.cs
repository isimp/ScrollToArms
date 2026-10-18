using Xunit;

namespace ScrollToArms.Tests;

/// <summary>
/// The message about a tool that changes the wheel is shown once, when a tool the wheel took out
/// arrives in hand. An item with an equip time arrives a moment later, so the watch lasts five
/// seconds; a tool not in hand by then was not equipped, and one taken out another way never had
/// a watch.
/// </summary>
public class HintWatchTests
{
    private const string Hammer = "Hammer";

    [Fact]
    public void AToolTheWheelTookOutIsAnnouncedWhenItArrives()
    {
        var watch = new HintWatch<string>();
        watch.Watch(Hammer, HintKind.WheelRotates, now: 10f);

        Assert.False(watch.Arrived(now: 10.1f, equipped: false, out _, out _));
        Assert.True(watch.Arrived(now: 10.3f, equipped: true, out var item, out var kind));
        Assert.Equal(Hammer, item);
        Assert.Equal(HintKind.WheelRotates, kind);
    }

    [Fact]
    public void ItIsAnnouncedOnlyOnce()
    {
        var watch = new HintWatch<string>();
        watch.Watch(Hammer, HintKind.KeepsTheWheel, now: 10f);
        watch.Arrived(now: 10.2f, equipped: true, out _, out _);

        Assert.False(watch.Arrived(now: 10.3f, equipped: true, out _, out _));
    }

    [Fact]
    public void AToolNotInHandWithinFiveSecondsIsNotAnnounced()
    {
        var watch = new HintWatch<string>();
        watch.Watch(Hammer, HintKind.KeepsTheWheel, now: 10f);
        watch.Arrived(now: 15.5f, equipped: false, out _, out _);

        Assert.False(watch.Arrived(now: 15.6f, equipped: true, out _, out _));
    }

    [Fact]
    public void AnItemThatChangesNothingIsNotWatched()
    {
        var watch = new HintWatch<string>();
        watch.Watch("Sword", HintKind.None, now: 10f);

        Assert.False(watch.Arrived(now: 10.2f, equipped: true, out _, out _));
    }

    [Fact]
    public void ALaterPickReplacesTheWatch()
    {
        var watch = new HintWatch<string>();
        watch.Watch(Hammer, HintKind.WheelRotates, now: 10f);
        watch.Watch("Sword", HintKind.None, now: 10.1f);

        Assert.False(watch.Arrived(now: 10.2f, equipped: true, out _, out _));
    }
}
