using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ScrollToArms.SelfTest
{
    /// <summary>
    /// The self-test: what ScrollToArms found in the game when it started, a walk along the
    /// player's own hotbar the way the wheel walks it, and real picks on it, driven through the
    /// picker the way a notch and a tap drive it, each checked in the player's hands and on the bar,
    /// a notch past the end of the bar with Wrap off among them.
    /// What the hands held, and what the Hide key put away, is put back at the end. Results are
    /// [selftest] lines in LogOutput.log, ending with a DONE line, which also shows on screen.
    /// </summary>
    internal sealed class Tests
    {
        // Long enough for any item's equip bar; the game's own are a fraction of a second.
        private const float ArrivalTime = 3f;

        private static readonly FieldInfo Elements = AccessTools.Field(typeof(HotkeyBar), "m_elements");
        private static readonly System.Type ElementType = AccessTools.Inner(typeof(HotkeyBar), "ElementData");
        private static readonly FieldInfo Selection = ElementType == null ? null : AccessTools.Field(ElementType, "m_selection");
        private static readonly FieldInfo QueuedMarker = ElementType == null ? null : AccessTools.Field(ElementType, "m_queued");

        private readonly ManualLogSource _log;
        private readonly List<string> _failed = new List<string>();
        private int _passed;
        private int _skipped;
        private Player _player;

        public Tests(ManualLogSource log)
        {
            _log = log;
        }

        public IEnumerator Run()
        {
            _player = Player.m_localPlayer;
            Write("ScrollToArms self-test. It picks from your hotbar and puts your hands back at the end.");

            Write("START found in the game");
            Check(WheelReads.CameraReady, "the camera zoom hands the wheel over");
            Check(WheelReads.PlacementReady, "the piece rotation hands the wheel over in build mode");
            Check(Picker.EquipFound, "the game's equip call was found");
            Check(Stow.Available, "what the Hide key put away can be read and brought back");
            Check(HotbarView.Available, "the hotbar's markers for the pick were found");
            if (Plugin.PlaySounds)
            {
                Check(Sound.Found(Sound.Cue.Tick), "the tick's sound was found in the game");
                Check(Sound.Found(Sound.Cue.Lost), "the lost pick's sound was found in the game");
            }
            else
            {
                Skip("PlaySounds is off, so the sounds were not looked for");
            }

            Write("START the walk along your hotbar");
            var stops = Walk();

            Write("START picks");
            if (stops.Count < 2)
            {
                Skip("fewer than two weapons or tools on your hotbar; put two there to check real picks");
            }
            else if (Picker.EquipFound)
            {
                yield return Picks(stops);
            }

            Summary();
        }

        /// <summary>
        /// Walks the player's hotbar both ways through the picker's own walk, which reads the slots
        /// from the inventory, and checks it against what the slots hold. Gives back the stops.
        /// </summary>
        private List<int> Walk()
        {
            var right = Round(1);
            if (right.Count == 0)
            {
                Skip("nothing on your hotbar for the wheel to stop on; put a weapon or a tool there to check the walk");
                return right;
            }

            Note("the wheel stops on slot " + string.Join(", ", right.Select(slot => slot + 1)));

            var other = right.Where(slot => !TakenInHand(Picker.ItemAt(_player, slot))).Select(slot => slot + 1).ToList();
            Check(other.Count == 0, "every slot the wheel stops on holds a weapon, a bow, a shield, a tool or a torch",
                "slot " + string.Join(", ", other));

            var left = Round(-1);
            left.Reverse();
            Check(left.SequenceEqual(right), "scrolling left stops on the same slots as scrolling right",
                "left " + string.Join(", ", left.Select(slot => slot + 1)));

            Check(Picker.NextStop(_player, -1, 1) == right[0], "with nothing in hand the first notch right stops on the leftmost of them");

            var last = right[right.Count - 1];
            var past = Picker.NextStop(_player, last, 1);
            if (Plugin.Wrap) Check(past == right[0], "past the last of them the wheel continues at the first, as Wrap is on");
            else Check(past == last, "past the last of them the wheel stays, as Wrap is off");

            return right;
        }

        /// <summary>
        /// Real picks between the first two stops: a notch and letting go, a tap back, a pick
        /// landing on the held slot, and the Hide key's items picked back. The hands are put back
        /// however a part ends.
        /// </summary>
        private IEnumerator Picks(List<int> stops)
        {
            var saved = new Hands(_player);

            if (!Picker.Usable(_player))
            {
                Note("waiting for the console or menu to close");
                var until = Time.time + 30f;
                while (!Picker.Usable(_player) && Time.time < until) yield return null;
            }

            if (!Picker.Usable(_player))
            {
                Skip("the hotbar could not be used for 30 seconds (console, menu or inventory open); close it to let the picks run");
                yield break;
            }

            var first = Picker.ItemAt(_player, stops[0]);
            var second = Picker.ItemAt(_player, stops[1]);

            // Start from the first stop in hand, the way a number key would leave it.
            Clear();
            _player.EquipItem(first, false);
            yield return null;
            Check(first.m_equipped, "the test starts with the first weapon in hand", first.m_shared.m_name);

            // A notch to the right shows the frame on the next stop, and letting go takes it in hand.
            Picker.Flow.Step(1);
            yield return new WaitForEndOfFrame();
            Check(Picker.Choosing && Picker.Cursor == stops[1], "a notch moves the frame to the next weapon",
                "cursor " + (Picker.Cursor + 1));
            Check(MarkerShown(Selection, stops[1]), "the bar shows the frame on that slot");

            yield return Arrival(second, true);
            Check(second.m_equipped, "letting go takes the framed weapon in hand", second.m_shared.m_name);
            Check(Picker.Pending == null, "the pick is done once the weapon is in hand");

            // A tap goes back to what was held before.
            Picker.Flow.Flip(Time.time);
            yield return Arrival(first, true);
            Check(first.m_equipped, "a tap on the modifier goes back to the weapon held before", first.m_shared.m_name);

            // A pick landing on the held slot puts the item away, as its number key would.
            Picker.Flow.Step(1);
            Picker.Flow.Step(-1);
            yield return null;
            yield return Arrival(first, false);
            Check(!first.m_equipped, "a pick landing on the held slot puts that weapon away", first.m_shared.m_name);

            // What the Hide key put away is picked back with both hands.
            _player.EquipItem(first, false);
            yield return null;
            _player.HideHandItems(false, false);
            yield return null;
            if (!Stow.Available || (Stow.Right(_player) != first && Stow.Left(_player) != first))
            {
                Skip("the Hide key's memory could not be read, so picking put-away items was not tried");
            }
            else
            {
                Picker.Flow.Step(1);
                Picker.Flow.Step(-1);
                yield return Arrival(first, true);
                Check(first.m_equipped, "picking the put-away weapon brings it back as the Hide key would", first.m_shared.m_name);
            }

            yield return PastTheEnd(stops);

            saved.Restore(_player);
            yield return null;
            Check(saved.Matches(_player), "your hands hold what they held before the test");
        }

        /// <summary>
        /// Waits until the item is in hand, or out of it, or the arrival time has passed, and checks
        /// that the bar marks its slot as queued on every frame the game has it in its equip queue.
        /// An item brought back with the Hide key's items arrives at once and is never queued.
        /// </summary>
        private IEnumerator Arrival(ItemDrop.ItemData item, bool inHand)
        {
            var queued = 0;
            var marked = 0;
            var until = Time.time + ArrivalTime;
            while (item.m_equipped != inHand && Time.time < until)
            {
                yield return new WaitForEndOfFrame();
                if (_player.IsEquipActionQueued(item))
                {
                    queued++;
                    if (MarkerShown(QueuedMarker, SlotOnBar(item))) marked++;
                }

                yield return null;
            }

            if (queued > 0)
                Check(marked == queued, "the bar shows the game's queued marker while the game equips the item",
                    item.m_shared.m_name + ": marked on " + marked + " of " + queued + " frames");
        }

        /// <summary>
        /// With Wrap off for the part, which is set in memory only and put back after, a notch past
        /// the last weapon on the bar starts no pick, and the weapon stays in hand after the pause.
        /// </summary>
        private IEnumerator PastTheEnd(List<int> stops)
        {
            var config = BepInEx.Bootstrap.Chainloader.PluginInfos[Plugin.Guid].Instance.Config;
            if (!config.TryGetEntry("Selection", "Wrap", out ConfigEntry<bool> wrap))
            {
                Skip("the Wrap setting was not found, so a notch past the end of the bar was not tried");
                yield break;
            }

            var last = Picker.ItemAt(_player, stops[stops.Count - 1]);
            Clear();
            _player.EquipItem(last, false);
            yield return null;

            var was = wrap.Value;
            var saves = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                wrap.Value = false;
                Picker.Flow.Step(1);
                Check(!Picker.Choosing, "with Wrap off a notch past the last weapon starts no pick");

                var until = Time.time + Plugin.PauseTime + 0.5f;
                while (Time.time < until) yield return null;
                Check(last.m_equipped, "the last weapon stays in hand after a notch past the end", last.m_shared.m_name);
            }
            finally
            {
                wrap.Value = was;
                config.SaveOnConfigSet = saves;
            }
        }

        /// <summary>The hotbar slot holding the item, or -1.</summary>
        private int SlotOnBar(ItemDrop.ItemData item)
        {
            for (var slot = 0; slot < Picker.Slots; slot++)
            {
                if (Picker.ItemAt(_player, slot) == item) return slot;
            }

            return -1;
        }

        private void Clear()
        {
            Picker.Flow.EndPick();
            if (_player.RightItem != null) _player.UnequipItem(_player.RightItem, false);
            if (_player.LeftItem != null) _player.UnequipItem(_player.LeftItem, false);
        }

        /// <summary>Whether the vanilla hotbar shows one of its per-slot markers on a slot.</summary>
        private static bool MarkerShown(FieldInfo marker, int slot)
        {
            var hud = Hud.instance;
            if (hud == null || Elements == null || marker == null) return false;

            var bar = hud.GetComponentsInChildren<HotkeyBar>(true).FirstOrDefault(b => b.name == "HotKeyBar");
            if (bar == null || !(Elements.GetValue(bar) is IList elements) || slot < 0 || slot >= elements.Count) return false;

            var go = marker.GetValue(elements[slot]) as GameObject;
            return go != null && go.activeSelf;
        }

        /// <summary>The slots the wheel stops on from the end of the bar, in one direction, until it comes round or stops moving.</summary>
        private List<int> Round(int step)
        {
            var seen = new List<int>();
            var slot = Picker.NextStop(_player, -1, step);
            while (slot >= 0 && !seen.Contains(slot) && seen.Count < Picker.Slots)
            {
                seen.Add(slot);
                var next = Picker.NextStop(_player, slot, step);
                if (next == slot) break;
                slot = next;
            }

            return seen;
        }

        /// <summary>Whether an item is something the hands take, by the game's own item type.</summary>
        private static bool TakenInHand(ItemDrop.ItemData item)
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
                    return true;
                default:
                    return false;
            }
        }

        private void Check(bool held, string what, string found = null)
        {
            if (held)
            {
                _passed++;
                Write("  PASS " + what);
                return;
            }

            _failed.Add(what + (found == null ? string.Empty : " (" + found + ")"));
            Write("  FAIL " + what + (found == null ? string.Empty : ": " + found));
        }

        private void Skip(string why)
        {
            _skipped++;
            Write("  SKIP " + why);
        }

        private void Note(string text) => Write("  note " + text);

        private void Summary()
        {
            var done = "DONE " + _passed + " passed, " + _failed.Count + " failed, " + _skipped + " skipped";
            Write(done);
            foreach (var failure in _failed) Write("  failed: " + failure);
            _player.Message(MessageHud.MessageType.Center, "ScrollToArms self-test: " + done);
        }

        private void Write(string line) => _log.LogInfo("[selftest] " + line);

        /// <summary>What the hands held and what the Hide key had put away, to put back at the end.</summary>
        private sealed class Hands
        {
            private readonly ItemDrop.ItemData _right;
            private readonly ItemDrop.ItemData _left;
            private readonly ItemDrop.ItemData _stowedRight;
            private readonly ItemDrop.ItemData _stowedLeft;

            public Hands(Player player)
            {
                _right = player.RightItem;
                _left = player.LeftItem;
                _stowedRight = Stow.Right(player);
                _stowedLeft = Stow.Left(player);
            }

            public void Restore(Player player)
            {
                Picker.Flow.EndPick();
                if (player.RightItem != null) player.UnequipItem(player.RightItem, false);
                if (player.LeftItem != null) player.UnequipItem(player.LeftItem, false);

                var stowed = _right == null && _left == null && (_stowedRight != null || _stowedLeft != null);
                var right = stowed ? _stowedRight : _right;
                var left = stowed ? _stowedLeft : _left;
                if (right != null) player.EquipItem(right, false);
                if (left != null) player.EquipItem(left, false);
                if (stowed) player.HideHandItems(false, false);
            }

            public bool Matches(Player player) =>
                player.RightItem == _right && player.LeftItem == _left
                && (_right != null || _left != null || (Stow.Right(player) == _stowedRight && Stow.Left(player) == _stowedLeft));
        }
    }
}
