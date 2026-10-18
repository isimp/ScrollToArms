namespace ScrollToArms
{
    /// <summary>
    /// The game as the picker sees it: the settings, where the hotbar can be used, the frame's keys
    /// and wheel, the player's hands and hotbar, and what the picker does to them. The game's side
    /// is <see cref="Picker"/>; the tests play their own.
    /// </summary>
    internal interface IPickWorld<TItem> where TItem : class
    {
        PlainScroll PlainScroll { get; }

        /// <summary>The commit that applies (see <see cref="PickRules.EffectiveCommit"/>).</summary>
        Commit Commit { get; }

        float PauseTime { get; }

        bool Wrap { get; }

        bool Invert { get; }

        bool FlipBack { get; }

        float WaitWhileBusy { get; }

        /// <summary>Whether the hotbar can be used: the same gate the game applies to its own gamepad cursor.</summary>
        bool CanPick { get; }

        /// <summary>Whether the wheel belongs to the hotbar this frame (see <see cref="PickRules.OwnsWheel"/>).</summary>
        bool OwnsWheel { get; }

        /// <summary>This frame's wheel reading; up reads positive.</summary>
        float Wheel { get; }

        bool ModifierDown { get; }

        bool ModifierHeld { get; }

        bool ModifierUp { get; }

        /// <summary>Whether any key or mouse button went down this frame.</summary>
        bool OtherKeyDown { get; }

        /// <summary>Whether the game window has the focus.</summary>
        bool Focused { get; }

        TItem Right { get; }

        TItem Left { get; }

        /// <summary>What the Hide key put away from the right hand and can still bring back, or null.</summary>
        TItem StowedRight { get; }

        TItem StowedLeft { get; }

        /// <summary>The hotbar's width, at most eight slots.</summary>
        int Width { get; }

        TItem ItemAt(int slot);

        /// <summary>Whether the wheel stops on an item (see <see cref="PickRules.IsStop"/>).</summary>
        bool IsStop(TItem item);

        bool Equipped(TItem item);

        /// <summary>Whether the game has an equip or unequip of the item queued behind its equip bar.</summary>
        bool Queued(TItem item);

        /// <summary>Whether both hands are empty and the item is what the Hide key would bring back.</summary>
        bool IsStowed(TItem item);

        /// <summary>Why the game would refuse or lose a pick of the item now, or null (see <see cref="PickRules.Busy"/>).</summary>
        string Busy(TItem item);

        /// <summary>Equips the item, or puts it away when it is in hand, as the game's ToggleEquipped does.</summary>
        void Equip(TItem item);

        /// <summary>Brings back both items the Hide key put away, as the Hide key does with empty hands.</summary>
        void Unstow();

        /// <summary>Takes a queued equip or unequip of the item off the game's queue.</summary>
        void Withdraw(TItem item);

        /// <summary>The cursor moved.</summary>
        void Tick();

        /// <summary>A pick was lost.</summary>
        void Lost();

        /// <summary>The wheel is asking for an item for the first time, for the tool message.</summary>
        void Watch(TItem item, float now);
    }

    /// <summary>
    /// The pick: which hotbar slot the wheel points at, and whether an item is waiting for the game
    /// to allow it into the player's hand. Driven once a frame through <see cref="Update"/>.
    ///
    /// Items are equipped through the game's ToggleEquipped, never its hotbar path, which uses the
    /// item on whatever the player is looking at, eats food and unequips the item already in hand,
    /// none of which a wheel sweeping past a slot should do. ToggleEquipped keeps the game's own
    /// rules: an item with an equip time is queued with its progress bar, and the hand rules decide
    /// what the new item replaces.
    /// </summary>
    internal sealed class PickFlow<TItem> where TItem : class
    {
        // Longer than this is a hold, not a tap.
        private const float TapTime = 0.3f;

        private readonly IPickWorld<TItem> _world;
        private readonly Notches _wheel = new Notches();

        private float _pendingUntil;
        private bool _unequip;
        private bool _asked;
        private bool _queueSeen;
        private string _lastBusy;
        private TItem _heldRight;
        private TItem _heldLeft;

        // What the hands held most recently and before that, for flipping back. Empty hands, as
        // after the Hide key, do not count as a change.
        private TItem _current;
        private TItem _previous;

        private float _tapStart;
        private bool _tapSpoiled = true;

        public PickFlow(IPickWorld<TItem> world)
        {
            _world = world;
        }

        /// <summary>True from the first notch until the pick is committed or dropped.</summary>
        public bool Choosing { get; private set; }

        /// <summary>The hotbar slot the wheel points at, or -1 while not choosing.</summary>
        public int Cursor { get; private set; } = -1;

        /// <summary>
        /// A committed pick the game has not carried out yet: an item on its way into the hand, or
        /// out of it when the pick landed on the slot already held.
        /// </summary>
        public TItem Pending { get; private set; }

        /// <summary>The hotbar slot of <see cref="Pending"/>.</summary>
        public int PendingSlot { get; private set; } = -1;

        /// <summary>
        /// True while a pick is held back because the game would refuse or lose it, as opposed to
        /// being in the game's own equip bar.
        /// </summary>
        public bool Waiting => Pending != null && _lastBusy != null;

        /// <summary>When the current wait began, so a pulse can start with it.</summary>
        public float WaitingSince { get; private set; }

        /// <summary>
        /// The hotbar slot of the last pick that was lost, because its wait ran out or the game
        /// would not take it, and when. Picks the player cancelled another way are not counted.
        /// </summary>
        public int LostSlot { get; private set; } = -1;

        public float LostAt { get; private set; }

        /// <summary>One frame of the pick, at <paramref name="now"/> seconds of game time.</summary>
        public void Update(float now)
        {
            TrackHands();
            var tapped = ReadTap(now);

            if (!_world.CanPick)
            {
                _tapSpoiled = true;
                EndPick();
                return;
            }

            // A pick that arrived changes the hands itself, so it is settled before that check, and
            // the hands it produced become the ones a pick still being chosen started from.
            if (Pending != null && Done(Pending))
            {
                ClearPending();
                RememberHands();
            }

            // A number key, the inventory or anything else that changes the hands ends the pick:
            // the player has chosen another way.
            if ((Choosing || Pending != null) && HandsChanged())
            {
                EndPick();
                return;
            }

            ReadWheel(now);

            if (Choosing)
            {
                var released = _world.PlainScroll == PlainScroll.Zoom && !_world.ModifierHeld;
                var paused = _world.Commit == Commit.AfterPause && now - _wheel.LastNotch >= _world.PauseTime;
                if (released || paused) CommitPick(now);
            }
            else if (tapped && _world.FlipBack)
            {
                Flip(now);
            }

            if (Pending != null) TryEquipPending(now);
        }

        /// <summary>
        /// Ends the pick: the cursor, a pick still on its way and any part of a notch go. What the
        /// hands held before, for flipping back, and the flash of a lost pick stay.
        /// </summary>
        public void EndPick()
        {
            Choosing = false;
            Cursor = -1;
            ClearPending();
            _wheel.Clear();
        }

        /// <summary>
        /// Follows what the hands hold. A change to another item moves the last one into
        /// <see cref="_previous"/>; empty hands leave both as they are.
        /// </summary>
        private void TrackHands()
        {
            var held = _world.Right ?? _world.Left;
            if (held == null || held == _current) return;

            _previous = _current;
            _current = held;
        }

        /// <summary>
        /// True on the frame the modifier is let go after a tap: a press shorter than TapTime with
        /// no other key, mouse button or wheel notch in between, and the game window in focus
        /// throughout. Anything else makes it a hold, which belongs to other uses of the key.
        /// </summary>
        private bool ReadTap(float now)
        {
            if (_world.ModifierDown)
            {
                _tapStart = now;
                _tapSpoiled = false;
                return false;
            }

            if (_world.ModifierHeld)
            {
                // The wheel counts whatever it was used for, zoom included.
                if (_world.OtherKeyDown || !_world.Focused || _world.Wheel != 0f) _tapSpoiled = true;
                return false;
            }

            if (!_world.ModifierUp) return false;

            var tapped = !_tapSpoiled && now - _tapStart <= TapTime;
            _tapSpoiled = true;
            return tapped;
        }

        /// <summary>
        /// Goes back to what the hands held before, as a tap on the modifier does; the self-test
        /// calls it here. While a pick is still on its way, what is in hand now is the one to go
        /// back to, which withdraws the pick. With empty hands, as after the Hide key, it is the
        /// last thing held, which brings the stowed items back.
        /// </summary>
        internal void Flip(float now)
        {
            var empty = _world.Right == null && _world.Left == null;
            var target = Pending != null || empty ? _current : _previous;
            var slot = SlotOf(target);
            if (slot < 0) return;

            PickSlot(slot, now, false);
        }

        private void ReadWheel(float now)
        {
            if (!_world.OwnsWheel)
            {
                _wheel.Clear();
                return;
            }

            var step = _wheel.Read(_world.Wheel, now, _world.Invert);
            if (step != 0) Step(step);
        }

        /// <summary>One notch along the bar, 1 to the right; the self-test calls it here as the wheel does.</summary>
        internal void Step(int step)
        {
            if (!Choosing)
            {
                // A pick still on its way counts as already in hand: scrolling continues from it,
                // and it keeps being followed until the new pick is committed.
                var start = Pending != null ? PendingSlot : SlotOfHeld();
                if (NextStop(start, step) < 0) return;

                Choosing = true;
                Cursor = start;
                RememberHands();
            }

            // Scrolling makes the press a hold, not a tap.
            _tapSpoiled = true;

            var next = NextStop(Cursor, step);
            if (next < 0 || next == Cursor) return;

            Cursor = next;
            _world.Tick();
        }

        private void CommitPick(float now)
        {
            Choosing = false;
            var slot = Cursor;
            Cursor = -1;

            if (slot >= 0) PickSlot(slot, now, true);
        }

        /// <summary>
        /// Makes the item in a hotbar slot the pick, from the wheel or from a flip back. When the
        /// wheel lands on the slot already in hand, the pick takes that item out of the hand, as
        /// its number key would; a flip back never does.
        /// </summary>
        private void PickSlot(int slot, float now, bool mayUnequip)
        {
            var item = _world.ItemAt(slot);
            if (item == null) return;

            // Landing on the earlier pick keeps it as it is. A flip back onto an unequip still on
            // its way withdraws it instead, since the item is still in hand.
            if (item == Pending && (mayUnequip || !_unequip)) return;

            // The latest pick wins, as a second number key would: an earlier one still waiting or
            // in the game's equip bar is withdrawn, including when the wheel went back to the item
            // already in hand.
            var earlier = Pending;
            if (earlier != null)
            {
                if (_world.Queued(earlier)) _world.Withdraw(earlier);
                ClearPending();
            }

            if (_world.Queued(item)) return;

            // Going back to the item in hand from an earlier pick only withdraws that pick.
            var unequip = _world.Equipped(item);
            if (unequip && (!mayUnequip || earlier != null)) return;

            Pending = item;
            PendingSlot = slot;
            _unequip = unequip;
            _pendingUntil = now + _world.WaitWhileBusy;
            _asked = false;
            _queueSeen = false;
            _lastBusy = null;
            RememberHands();
        }

        /// <summary>
        /// Follows a pick until the game has carried it out. The game can do so straight away,
        /// queue it behind its equip bar, refuse it while busy, or drop a queued equip or unequip
        /// when the player runs, jumps, dodges or attacks. A pick refused or dropped that way is
        /// asked for again as soon as the game allows, within WaitWhileBusy seconds. The equip bar
        /// itself does not count against that time.
        /// </summary>
        private void TryEquipPending(float now)
        {
            var item = Pending;
            if (_world.ItemAt(PendingSlot) != item)
            {
                ClearPending();
                return;
            }

            if (_world.Queued(item))
            {
                _queueSeen = true;
                _pendingUntil = now + _world.WaitWhileBusy;
                return;
            }

            var busy = _world.Busy(item);
            if (busy != null)
            {
                if (now >= _pendingUntil)
                {
                    Lose(now);
                    ClearPending();
                    return;
                }

                if (_lastBusy == null) WaitingSince = now;
                _lastBusy = busy;
                return;
            }

            _lastBusy = null;

            // Asked before, never queued and still not done: the game refused it for a reason of
            // its own, such as a broken item or a world level, and asking again would repeat its
            // message every frame.
            if (_asked && !_queueSeen)
            {
                Lose(now);
                ClearPending();
                return;
            }

            if (!_asked && !_unequip) _world.Watch(item, now);
            _asked = true;
            _queueSeen = false;

            // Scrolling back onto what the Hide key put away undoes the stow, both hands, the same
            // as pressing Hide again. Equipping just this item would leave a stowed shield behind.
            if (_world.IsStowed(item)) _world.Unstow();
            else _world.Equip(item);

            if (Done(item))
            {
                ClearPending();
            }
            else if (_world.Queued(item))
            {
                _queueSeen = true;
            }
        }

        /// <summary>The next slot to stop on (see <see cref="HotbarWalk.NextStop"/>).</summary>
        private int NextStop(int from, int step) =>
            HotbarWalk.NextStop(_world.Width, from, step, _world.Wrap, slot => _world.IsStop(_world.ItemAt(slot)));

        private int SlotOf(TItem item)
        {
            if (item == null) return -1;
            var width = _world.Width;
            for (var slot = 0; slot < width; slot++)
            {
                if (_world.ItemAt(slot) == item) return slot;
            }

            return -1;
        }

        /// <summary>
        /// Where the first notch starts from: the hotbar slot of what the right hand holds, else
        /// the left. With both hands empty, the slot of what the Hide key put away, so scrolling
        /// continues from the stowed weapon as if it were still in hand. With nothing stowed
        /// either, -1, and the first notch starts from the end of the bar.
        /// </summary>
        private int SlotOfHeld()
        {
            if (_world.Right != null || _world.Left != null)
            {
                var held = SlotOf(_world.Right);
                return held >= 0 ? held : SlotOf(_world.Left);
            }

            var stowed = SlotOf(_world.StowedRight);
            return stowed >= 0 ? stowed : SlotOf(_world.StowedLeft);
        }

        private void RememberHands()
        {
            _heldRight = _world.Right;
            _heldLeft = _world.Left;
        }

        private bool HandsChanged() => _world.Right != _heldRight || _world.Left != _heldLeft;

        private void Lose(float now)
        {
            LostSlot = PendingSlot;
            LostAt = now;
            _world.Lost();
        }

        /// <summary>True once the pick's item is in hand, or out of it for an unequip.</summary>
        private bool Done(TItem item) => _world.Equipped(item) != _unequip;

        private void ClearPending()
        {
            Pending = null;
            PendingSlot = -1;
            _unequip = false;
            _lastBusy = null;
        }
    }
}
