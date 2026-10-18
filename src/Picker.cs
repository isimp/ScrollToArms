using System;
using HarmonyLib;
using UnityEngine;

namespace ScrollToArms
{
    /// <summary>
    /// The pick in the game: the local player's hands and hotbar, the keys and the wheel, read for
    /// <see cref="PickFlow{TItem}"/> once a frame, which decides what happens, and the game's own
    /// calls it makes to carry that out. What the picker does is described there; what counts as
    /// a stop and what holds a pick back in <see cref="PickRules"/>.
    ///
    /// Items are equipped through Player.ToggleEquipped, never Player.UseHotbarItem. The hotbar
    /// path uses the item on whatever the player is looking at, eats food and unequips the item
    /// already in hand, none of which a wheel sweeping past a slot should do. ToggleEquipped keeps
    /// the game's own rules: an item with an equip time is queued with its progress bar, and the
    /// hand rules decide what the new item replaces.
    /// </summary>
    internal static class Picker
    {
        public const int Slots = 8;

        private static readonly Func<Player, ItemDrop.ItemData, bool> ToggleEquipped = ResolveToggleEquipped();

        private static readonly ItemNameList Skipped = new ItemNameList(() => Plugin.SkipItems);

        private static readonly PlayerWorld World = new PlayerWorld();

        /// <summary>The pick itself, driven by <see cref="Update"/>; the self-test drives it as the wheel does.</summary>
        internal static readonly PickFlow<ItemDrop.ItemData> Flow = new PickFlow<ItemDrop.ItemData>(World);

        private static bool _warnedNotPatched;

        /// <summary>False when the game's equip call was not found, which leaves the picker off.</summary>
        internal static bool EquipFound => ToggleEquipped != null;

        /// <summary>True from the first notch until the pick is committed or dropped.</summary>
        public static bool Choosing => Flow.Choosing;

        /// <summary>The hotbar slot the wheel points at, 0 to 7, or -1 while not choosing.</summary>
        public static int Cursor => Flow.Cursor;

        /// <summary>A committed pick the game has not carried out yet (see <see cref="PickFlow{TItem}.Pending"/>).</summary>
        public static ItemDrop.ItemData Pending => Flow.Pending;

        /// <summary>The hotbar slot of <see cref="Pending"/>.</summary>
        public static int PendingSlot => Flow.PendingSlot;

        /// <summary>True while a pick is held back because the game would refuse or lose it.</summary>
        public static bool Waiting => Flow.Waiting;

        /// <summary>When the current wait began, so a pulse can start with it.</summary>
        public static float WaitingSince => Flow.WaitingSince;

        /// <summary>The hotbar slot of the last pick that was lost, and when (see <see cref="PickFlow{TItem}.LostSlot"/>).</summary>
        public static int LostSlot => Flow.LostSlot;

        public static float LostAt => Flow.LostAt;

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

            return PickRules.OwnsWheel(Input.GetKey(Plugin.Modifier), player.InPlaceMode(), WheelReads.PlacementReady, Plugin.PlainScroll);
        }

        public static void Update(float dt)
        {
            var player = Player.m_localPlayer;
            if (player == null || !Plugin.Enabled || ToggleEquipped == null)
            {
                Flow.EndPick();
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

            World.Player = player;
            Flow.Update(now);
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

        /// <summary>Whether the wheel stops on an item (see <see cref="PickRules.IsStop"/>).</summary>
        private static bool IsStop(ItemDrop.ItemData item) =>
            item != null && PickRules.IsStop(item.IsEquipable(), item.m_shared.m_itemType, item.m_shared.m_buildPieces != null,
                Plugin.SkipBuildTools, Skipped.Contains(item), item.m_shared.m_useDurability, item.m_durability);

        /// <summary>Why the game would refuse or lose a pick of the item right now, or null (see <see cref="PickRules.Busy"/>).</summary>
        private static string BusyReason(Player player, ItemDrop.ItemData item) =>
            PickRules.Busy(player.InAttack(), player.InDodge(), player.IsSwimming(), player.IsOnGround(),
                item.m_shared.m_equipDuration > 0f, player.IsRunning(), Stow.IsStowed(player, item));

        private static int Width(Player player) => Mathf.Min(Slots, player.GetInventory().GetWidth());

        public static ItemDrop.ItemData ItemAt(Player player, int slot)
        {
            if (slot < 0 || slot >= Width(player)) return null;
            return player.GetInventory().GetItemAt(slot, 0);
        }

        /// <summary>The next slot on the player's hotbar to stop on (see <see cref="HotbarWalk.NextStop"/>).</summary>
        internal static int NextStop(Player player, int from, int step) =>
            HotbarWalk.NextStop(Width(player), from, step, Plugin.Wrap, slot => IsStop(ItemAt(player, slot)));

        /// <summary>The local player's game, read and acted on for the pick.</summary>
        private sealed class PlayerWorld : IPickWorld<ItemDrop.ItemData>
        {
            public Player Player;

            public PlainScroll PlainScroll => Plugin.PlainScroll;

            public Commit Commit => Plugin.EffectiveCommit;

            public float PauseTime => Plugin.PauseTime;

            public bool Wrap => Plugin.Wrap;

            public bool Invert => Plugin.Invert;

            public bool FlipBack => Plugin.FlipBack;

            public float WaitWhileBusy => Plugin.WaitWhileBusy;

            public bool CanPick => Picker.CanPick(Player);

            public bool OwnsWheel => HotbarOwnsWheel();

            public float Wheel => ZInput.GetMouseScrollWheel();

            public bool ModifierDown => Input.GetKeyDown(Plugin.Modifier);

            public bool ModifierHeld => Input.GetKey(Plugin.Modifier);

            public bool ModifierUp => Input.GetKeyUp(Plugin.Modifier);

            public bool OtherKeyDown => Input.anyKeyDown;

            public bool Focused => Application.isFocused;

            public ItemDrop.ItemData Right => Player.RightItem;

            public ItemDrop.ItemData Left => Player.LeftItem;

            public ItemDrop.ItemData StowedRight => Stow.Right(Player);

            public ItemDrop.ItemData StowedLeft => Stow.Left(Player);

            public int Width => Picker.Width(Player);

            public ItemDrop.ItemData ItemAt(int slot) => Picker.ItemAt(Player, slot);

            public bool IsStop(ItemDrop.ItemData item) => Picker.IsStop(item);

            public bool Equipped(ItemDrop.ItemData item) => item.m_equipped;

            public bool Queued(ItemDrop.ItemData item) => Player.IsEquipActionQueued(item);

            public bool IsStowed(ItemDrop.ItemData item) => Stow.IsStowed(Player, item);

            public string Busy(ItemDrop.ItemData item) => BusyReason(Player, item);

            public void Equip(ItemDrop.ItemData item) => ToggleEquipped(Player, item);

            public void Unstow() => Stow.Unstow(Player);

            public void Withdraw(ItemDrop.ItemData item) => Player.RemoveEquipAction(item);

            public void Tick() => Sound.Play(Sound.Cue.Tick);

            public void Lost() => Sound.Play(Sound.Cue.Lost);

            public void Watch(ItemDrop.ItemData item, float now) => BuildToolHint.Watch(item, now);
        }
    }
}
