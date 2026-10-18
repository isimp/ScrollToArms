using System.Collections.Generic;
using System.Linq;

namespace ScrollToArms.Tests;

/// <summary>
/// The game as the picker sees it, played by the tests: a hotbar of named items, the two hands,
/// what the Hide key put away, the game's equip queue and the keys and wheel of the frame. Items
/// whose name starts with "meat" are food and never stops. Equipping follows the game's
/// ToggleEquipped: an item in hand is put away, any other is taken in hand, the shield in the left
/// one; with Instant off, both go through the equip queue until <see cref="Finish"/>.
/// </summary>
internal sealed class FakeWorld : IPickWorld<string>
{
    public PlainScroll PlainScroll { get; set; } = PlainScroll.Zoom;
    public Commit Commit { get; set; } = Commit.OnRelease;
    public float PauseTime { get; set; } = 0.4f;
    public bool Wrap { get; set; } = true;
    public bool Invert { get; set; }
    public bool FlipBack { get; set; } = true;
    public float WaitWhileBusy { get; set; } = 2f;

    public bool CanPick { get; set; } = true;

    /// <summary>Whose the wheel is: by the modifier and PlainScroll, as outside build mode, unless set.</summary>
    public bool? Owns { get; set; }

    public bool OwnsWheel => Owns ?? PickRules.OwnsWheel(ModifierHeld, placing: false, placementReady: true, PlainScroll);

    public float Wheel { get; set; }
    public bool ModifierDown { get; set; }
    public bool ModifierHeld { get; set; }
    public bool ModifierUp { get; set; }
    public bool OtherKeyDown { get; set; }
    public bool Focused { get; set; } = true;

    public string Right { get; set; }
    public string Left { get; set; }
    public string StowedRight { get; set; }
    public string StowedLeft { get; set; }

    public string[] Bar { get; } = new string[8];

    public int Width => Bar.Length;

    /// <summary>When true, an equip or unequip happens at once instead of going through the queue.</summary>
    public bool Instant { get; set; }

    /// <summary>When true, the game refuses every equip without queueing it, as for a broken item.</summary>
    public bool Refuses { get; set; }

    /// <summary>Why the game would refuse or lose a pick right now, or null.</summary>
    public string BusyReason { get; set; }

    public HashSet<string> QueuedItems { get; } = new HashSet<string>();
    public List<string> Calls { get; } = new List<string>();
    public List<string> Watched { get; } = new List<string>();
    public int Ticks { get; private set; }
    public int LostSounds { get; private set; }

    public string ItemAt(int slot) => slot >= 0 && slot < Bar.Length ? Bar[slot] : null;

    public bool IsStop(string item) => item != null && !item.StartsWith("meat", System.StringComparison.Ordinal);

    public bool Equipped(string item) => item != null && (item == Right || item == Left);

    public bool Queued(string item) => QueuedItems.Contains(item);

    public bool IsStowed(string item) => Right == null && Left == null && item != null && (item == StowedRight || item == StowedLeft);

    public string Busy(string item) => BusyReason;

    public void Equip(string item)
    {
        Calls.Add("equip " + item);
        if (Refuses) return;
        if (Instant) Toggle(item);
        else QueuedItems.Add(item);
    }

    public void Unstow()
    {
        Calls.Add("unstow");
        Right = StowedRight;
        Left = StowedLeft;
        StowedRight = null;
        StowedLeft = null;
    }

    public void Withdraw(string item)
    {
        Calls.Add("withdraw " + item);
        QueuedItems.Remove(item);
    }

    public void Tick() => Ticks++;

    public void Lost() => LostSounds++;

    public void Watch(string item, float now) => Watched.Add(item);

    /// <summary>The game's equip bar runs out: every queued equip or unequip happens.</summary>
    public void Finish()
    {
        foreach (var item in QueuedItems.ToList()) Toggle(item);
        QueuedItems.Clear();
    }

    /// <summary>The game clears its queue, as it does every frame the player runs.</summary>
    public void ClearQueue() => QueuedItems.Clear();

    public int SlotOf(string item) => System.Array.IndexOf(Bar, item);

    public int Count(string call) => Calls.Count(c => c == call);

    private void Toggle(string item)
    {
        if (Equipped(item))
        {
            if (Right == item) Right = null;
            if (Left == item) Left = null;
            return;
        }

        if (item.StartsWith("shield", System.StringComparison.Ordinal)) Left = item;
        else Right = item;
    }
}
