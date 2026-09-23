using System;
using HarmonyLib;
using UnityEngine;

namespace ScrollToArms
{
    /// <summary>
    /// The pick: which hotbar slot the wheel points at, and whether an item is waiting for the
    /// game to allow it into the player's hand.
    ///
    /// Items are equipped through Player.ToggleEquipped, never Player.UseHotbarItem. The hotbar
    /// path uses the item on whatever the player is looking at, eats food and unequips the item
    /// already in hand, none of which a wheel sweeping past a slot should do. ToggleEquipped keeps
    /// the game's own rules: an item with an equip time is queued with its progress bar, and the
    /// hand rules decide what the new item replaces.
    /// </summary>
    public static class Picker
    {
        public const int Slots = 8;

        /// <summary>True from the first notch until the pick is committed or dropped.</summary>
        public static bool Choosing { get; private set; }

        /// <summary>The hotbar slot the wheel points at, 0 to 7, or -1 while not choosing.</summary>
        public static int Cursor { get; private set; } = -1;

        /// <summary>
        /// A committed pick the game has not carried out yet: an item on its way into the hand, or
        /// out of it when the pick landed on the slot already held.
        /// </summary>
        public static ItemDrop.ItemData Pending { get; private set; }

        /// <summary>The hotbar slot of <see cref="Pending"/>.</summary>
        public static int PendingSlot { get; private set; } = -1;

        /// <summary>
        /// True while a pick is held back because the game would refuse or lose it, as opposed to
        /// being in the game's own equip bar.
        /// </summary>
        public static bool Waiting => Pending != null && _lastBusy != null;

        /// <summary>When the current wait began, so a pulse can start with it.</summary>
        public static float WaitingSince { get; private set; }

        /// <summary>
        /// The hotbar slot of the last pick that was lost, because its wait ran out or the game
        /// would not take it, and when. Picks the player cancelled another way are not counted.
        /// </summary>
        public static int LostSlot { get; private set; } = -1;

        public static float LostAt { get; private set; }

        private static float _pendingUntil;
        private static bool _unequip;
        private static bool _asked;
        private static bool _queueSeen;
        private static string _lastBusy;
        private static float _lastNotch;
        private static float _wheel;
        private static ItemDrop.ItemData _heldRight;
        private static ItemDrop.ItemData _heldLeft;
        private static bool _warnedNotPatched;

        // What the hands held most recently and before that, for flipping back. Empty hands, as
        // after the Hide key, do not count as a change.
        private static ItemDrop.ItemData _current;
        private static ItemDrop.ItemData _previous;

        private static float _tapStart;
        private static bool _tapSpoiled = true;

        // Longer than this is a hold, not a tap.
        private const float TapTime = 0.3f;

        private static readonly ItemNameList Skipped = new ItemNameList(() => Plugin.SkipItems);

        // The same step vanilla uses to rotate a building piece one notch at a time
        // (Player.m_scrollAmountThreshold), so one notch moves the cursor by one slot.
        private const float NotchThreshold = 0.1f;

        // A partial notch older than this is discarded rather than completed by the next one.
        private const float WheelMemory = 0.3f;

        private static readonly Func<Player, ItemDrop.ItemData, bool> ToggleEquipped = ResolveToggleEquipped();

        private static Func<Player, ItemDrop.ItemData, bool> ResolveToggleEquipped()
        {
            try
            {
                var method = AccessTools.Method(typeof(Player), "ToggleEquipped");
                if (method != null) return AccessTools.MethodDelegate<Func<Player, ItemDrop.ItemData, bool>>(method);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"ScrollToArms: Player.ToggleEquipped could not be resolved ({ex.Message}).");
                return null;
            }

            Plugin.Log?.LogWarning("ScrollToArms: Player.ToggleEquipped was not found.");
            return null;
        }

        /// <summary>
        /// True when the wheel belongs to the hotbar this frame, which is also when the camera must
        /// not zoom and the piece being placed must not rotate. Outside the states the hotbar can be
        /// used in, the wheel is always the game's.
        /// </summary>
        public static bool HotbarOwnsWheel()
        {
            if (!Plugin.Enabled || ToggleEquipped == null || !WheelReads.CameraReady) return false;

            var player = Player.m_localPlayer;
            if (player == null || !CanPick(player)) return false;
            if (StepAside.Holding(player)) return false;

            var held = Input.GetKey(Plugin.Modifier);

            // In build mode the plain wheel rotates the piece, whatever PlainScroll says, so the
            // hotbar is always the modifier's there.
            if (player.InPlaceMode()) return held && WheelReads.PlacementReady;

            return Plugin.PlainScroll == PlainScroll.Zoom ? held : !held;
        }

        public static void Update(float dt)
        {
            var player = Player.m_localPlayer;
            if (player == null || !Plugin.Enabled || ToggleEquipped == null)
            {
                Reset();
                return;
            }

            if (!WheelReads.CameraRan && !_warnedNotPatched)
            {
                _warnedNotPatched = true;
                Plugin.Log.LogWarning("ScrollToArms: the camera patch has not been applied yet, so hotbar scrolling waits for it.");
            }

            var now = Time.time;

            // Outside the CanPick gate: the message depends only on the tool arriving in hand.
            BuildToolHint.Update(player, now);

            TrackHands(player);
            var tapped = ReadTap(now);

            if (!CanPick(player))
            {
                _tapSpoiled = true;
                Reset();
                return;
            }

            // A pick that arrived changes the hands itself, so it is settled before that check, and
            // the hands it produced become the ones a pick still being chosen started from.
            if (Pending != null && Done(Pending))
            {
                ClearPending();
                RememberHands(player);
            }

            // A number key, the inventory or anything else that changes the hands ends the pick:
            // the player has chosen another way.
            if ((Choosing || Pending != null) && HandsChanged(player))
            {
                Reset();
                return;
            }

            ReadWheel(player, now);

            if (Choosing)
            {
                var released = Plugin.PlainScroll == PlainScroll.Zoom && !Input.GetKey(Plugin.Modifier);
                var paused = Plugin.EffectiveCommit == Commit.AfterPause && now - _lastNotch >= Plugin.PauseTime;
                if (released || paused) CommitPick(player, now);
            }
            else if (tapped && Plugin.FlipBack)
            {
                Flip(player, now);
            }

            if (Pending != null) TryEquipPending(player, now);
        }

        /// <summary>
        /// Follows what the hands hold. A change to another item moves the last one into
        /// <see cref="_previous"/>; empty hands leave both as they are.
        /// </summary>
        private static void TrackHands(Player player)
        {
            var held = player.RightItem ?? player.LeftItem;
            if (held == null || held == _current) return;

            _previous = _current;
            _current = held;
        }

        /// <summary>
        /// True on the frame the modifier is let go after a tap: a press shorter than TapTime with
        /// no other key, mouse button or wheel notch in between, and the game window in focus
        /// throughout. Anything else makes it a hold, which belongs to other uses of the key.
        /// </summary>
        private static bool ReadTap(float now)
        {
            var key = Plugin.Modifier;

            if (Input.GetKeyDown(key))
            {
                _tapStart = now;
                _tapSpoiled = false;
                return false;
            }

            if (Input.GetKey(key))
            {
                // The wheel counts whatever it was used for, zoom included.
                if (Input.anyKeyDown || !Application.isFocused || ZInput.GetMouseScrollWheel() != 0f) _tapSpoiled = true;
                return false;
            }

            if (!Input.GetKeyUp(key)) return false;

            var tapped = !_tapSpoiled && now - _tapStart <= TapTime;
            _tapSpoiled = true;
            return tapped;
        }

        /// <summary>
        /// Goes back to what the hands held before. While a pick is still on its way, what is in
        /// hand now is the one to go back to, which withdraws the pick. With empty hands, as after
        /// the Hide key, it is the last thing held, which brings the stowed items back.
        /// </summary>
        private static void Flip(Player player, float now)
        {
            var empty = player.RightItem == null && player.LeftItem == null;
            var target = Pending != null || empty ? _current : _previous;
            var slot = SlotOf(player, target);
            if (slot < 0) return;

            PickSlot(player, slot, now, false);
        }

        private static void ReadWheel(Player player, float now)
        {
            if (!HotbarOwnsWheel())
            {
                _wheel = 0f;
                return;
            }

            var delta = ZInput.GetMouseScrollWheel();
            if (delta == 0f)
            {
                if (now - _lastNotch > WheelMemory) _wheel = 0f;
                return;
            }

            _wheel += delta;
            if (Mathf.Abs(_wheel) < NotchThreshold) return;

            // Wheel up reads positive. Up moves left along the bar unless inverted.
            var step = _wheel > 0f ? -1 : 1;
            if (Plugin.Invert) step = -step;
            _wheel = 0f;
            _lastNotch = now;

            Step(player, step);
        }

        private static void Step(Player player, int step)
        {
            if (!Choosing)
            {
                // A pick still on its way counts as already in hand: scrolling continues from it,
                // and it keeps being followed until the new pick is committed.
                var start = Pending != null ? PendingSlot : SlotOfHeld(player);
                if (NextStop(player, start, step) < 0) return;

                Choosing = true;
                Cursor = start;
                RememberHands(player);
            }

            // Scrolling makes the press a hold, not a tap.
            _tapSpoiled = true;

            var next = NextStop(player, Cursor, step);
            if (next < 0 || next == Cursor) return;

            Cursor = next;
            Sound.Play(Sound.Cue.Tick);
        }

        private static void CommitPick(Player player, float now)
        {
            Choosing = false;
            var slot = Cursor;
            Cursor = -1;

            if (slot >= 0) PickSlot(player, slot, now, true);
        }

        /// <summary>
        /// Makes the item in a hotbar slot the pick, from the wheel or from a flip back. When the
        /// wheel lands on the slot already in hand, the pick takes that item out of the hand, as
        /// its number key would; a flip back never does.
        /// </summary>
        private static void PickSlot(Player player, int slot, float now, bool mayUnequip)
        {
            var item = ItemAt(player, slot);
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
                if (player.IsEquipActionQueued(earlier)) player.RemoveEquipAction(earlier);
                ClearPending();
            }

            if (player.IsEquipActionQueued(item)) return;

            // Going back to the item in hand from an earlier pick only withdraws that pick.
            var unequip = item.m_equipped;
            if (unequip && (!mayUnequip || earlier != null)) return;

            Pending = item;
            PendingSlot = slot;
            _unequip = unequip;
            _pendingUntil = now + Plugin.WaitWhileBusy;
            _asked = false;
            _queueSeen = false;
            _lastBusy = null;
            RememberHands(player);
        }

        /// <summary>
        /// Follows a pick until the game has carried it out. The game can do so straight away,
        /// queue it behind its equip bar, refuse it while busy, or drop a queued equip or unequip
        /// when the player runs, jumps, dodges or attacks. A pick refused or dropped that way is
        /// asked for again as soon as the game allows, within WaitWhileBusy seconds. The equip bar
        /// itself does not count against that time.
        /// </summary>
        private static void TryEquipPending(Player player, float now)
        {
            var item = Pending;
            if (ItemAt(player, PendingSlot) != item)
            {
                ClearPending();
                return;
            }

            if (player.IsEquipActionQueued(item))
            {
                _queueSeen = true;
                _pendingUntil = now + Plugin.WaitWhileBusy;
                return;
            }

            var busy = BusyReason(player, item);
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

            if (!_asked && !_unequip) BuildToolHint.Watch(item, now);
            _asked = true;
            _queueSeen = false;

            // Scrolling back onto what the Hide key put away undoes the stow, both hands, the same
            // as pressing Hide again. Equipping just this item would leave a stowed shield behind.
            if (Stow.IsStowed(player, item)) Stow.Unstow(player);
            else ToggleEquipped(player, item);

            if (Done(item))
            {
                ClearPending();
            }
            else if (player.IsEquipActionQueued(item))
            {
                _queueSeen = true;
            }
        }

        /// <summary>
        /// Why the game would refuse or lose the pick right now, or null. Attacking, dodging and
        /// swimming refuse any equip. Running clears the equip queue every frame
        /// (Player.CheckRun), so an item with an equip time cannot arrive while running, while one
        /// without still swaps straight away.
        /// </summary>
        private static string BusyReason(Player player, ItemDrop.ItemData item)
        {
            if (player.InAttack()) return "attacking";
            if (player.InDodge()) return "dodging";
            if (player.IsSwimming() && !player.IsOnGround()) return "swimming";
            if (item.m_shared.m_equipDuration > 0f && player.IsRunning() && !Stow.IsStowed(player, item)) return "running";
            return null;
        }

        /// <summary>True where the hotbar can be used, for the wheel indicator.</summary>
        public static bool Usable(Player player) => CanPick(player);

        /// <summary>
        /// Where the hotbar can be used: the same gate the vanilla hotbar applies to its own
        /// gamepad cursor. The piece menu is part of it, so build mode counts only while placing.
        /// </summary>
        private static bool CanPick(Player player)
        {
            if (player.IsDead() || player.InCutscene() || player.IsTeleporting()) return false;
            if (InventoryGui.IsVisible()) return false;
            if (StoreGui.IsVisible()) return false;
            if (Menu.IsVisible()) return false;
            if (global::Console.IsVisible()) return false;
            if (TextInput.IsVisible()) return false;
            if (Minimap.instance != null && Minimap.IsOpen()) return false;
            if (Hud.IsPieceSelectionVisible()) return false;
            if (Hud.InRadial()) return false;
            if (Chat.instance != null && Chat.instance.HasFocus()) return false;
            if (PlayerCustomizaton.IsBarberGuiVisible()) return false;
            if (GameCamera.InFreeFly()) return false;
            return true;
        }

        /// <summary>
        /// A slot the cursor stops on: something held in the hands that can be equipped now.
        /// Food, armour and materials are passed over, and so is a broken item, which the game
        /// would refuse. A tool that opens build mode is passed over when SkipBuildTools is on, and
        /// so is anything on the SkipItems list.
        /// </summary>
        private static bool IsStop(ItemDrop.ItemData item)
        {
            if (item == null || !item.IsEquipable()) return false;

            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.Tool:
                    break;
                default:
                    return false;
            }

            if (Plugin.SkipBuildTools && item.m_shared.m_buildPieces != null) return false;
            if (Skipped.Contains(item)) return false;

            return !(item.m_shared.m_useDurability && item.m_durability <= 0f);
        }

        private static int Width(Player player) => Mathf.Min(Slots, player.GetInventory().GetWidth());

        public static ItemDrop.ItemData ItemAt(Player player, int slot)
        {
            if (slot < 0 || slot >= Width(player)) return null;
            return player.GetInventory().GetItemAt(slot, 0);
        }

        /// <summary>
        /// The next slot to stop on from <paramref name="from"/> in the direction of
        /// <paramref name="step"/>, or -1 when there is none. From -1 the search starts at the edge
        /// the step moves away from.
        /// </summary>
        private static int NextStop(Player player, int from, int step)
        {
            var width = Width(player);
            if (width <= 0) return -1;

            var slot = from;
            if (slot < 0) slot = step > 0 ? -1 : width;

            for (var i = 0; i < width; i++)
            {
                slot += step;
                if (slot < 0 || slot >= width)
                {
                    if (!Plugin.Wrap) return from >= 0 ? from : -1;
                    slot = (slot + width) % width;
                }

                if (IsStop(ItemAt(player, slot))) return slot;
            }

            return from >= 0 && IsStop(ItemAt(player, from)) ? from : -1;
        }

        private static int SlotOf(Player player, ItemDrop.ItemData item)
        {
            if (item == null) return -1;
            var width = Width(player);
            for (var slot = 0; slot < width; slot++)
            {
                if (ItemAt(player, slot) == item) return slot;
            }

            return -1;
        }

        /// <summary>
        /// Where the first notch starts from: the hotbar slot of what the right hand holds, else
        /// the left. With both hands empty, the slot of what the Hide key put away, so scrolling
        /// continues from the stowed weapon as if it were still in hand. With nothing stowed
        /// either, -1, and the first notch starts from the end of the bar.
        /// </summary>
        private static int SlotOfHeld(Player player)
        {
            if (player.RightItem != null || player.LeftItem != null)
            {
                var held = SlotOf(player, player.RightItem);
                return held >= 0 ? held : SlotOf(player, player.LeftItem);
            }

            var stowed = SlotOf(player, Stow.Right(player));
            return stowed >= 0 ? stowed : SlotOf(player, Stow.Left(player));
        }

        private static void RememberHands(Player player)
        {
            _heldRight = player.RightItem;
            _heldLeft = player.LeftItem;
        }

        private static bool HandsChanged(Player player) =>
            player.RightItem != _heldRight || player.LeftItem != _heldLeft;

        private static void Lose(float now)
        {
            LostSlot = PendingSlot;
            LostAt = now;
            Sound.Play(Sound.Cue.Lost);
        }

        /// <summary>True once the pick's item is in hand, or out of it for an unequip.</summary>
        private static bool Done(ItemDrop.ItemData item) => item.m_equipped != _unequip;

        private static void ClearPending()
        {
            Pending = null;
            PendingSlot = -1;
            _unequip = false;
            _lastBusy = null;
        }

        private static void Reset()
        {
            Choosing = false;
            Cursor = -1;
            ClearPending();
            _wheel = 0f;
        }
    }
}
