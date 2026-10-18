using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;

namespace ScrollToArms.SelfTest
{
    /// <summary>
    /// The self-test: what ScrollToArms found in the game when it started, and a walk along the
    /// player's own hotbar the way the wheel walks it. It only reads: no item moves and nothing is
    /// equipped. Results are [selftest] lines in LogOutput.log, ending with a DONE line, which the
    /// console shows as well.
    /// </summary>
    internal sealed class Tests
    {
        private readonly ManualLogSource _log;
        private readonly List<string> _failed = new List<string>();
        private int _passed;
        private int _skipped;

        public Tests(ManualLogSource log)
        {
            _log = log;
        }

        /// <summary>Runs every check and gives back the DONE line.</summary>
        public string Run()
        {
            Write("ScrollToArms self-test. It reads the game and your hotbar, and changes nothing.");

            Write("START found in the game");
            Check(WheelReads.CameraReady, "the camera zoom hands the wheel over");
            Check(WheelReads.PlacementReady, "the piece rotation hands the wheel over in build mode");
            Check(Picker.EquipFound, "the game's equip call was found");
            Check(Stow.Available, "what the Hide key put away can be read and brought back");
            Check(HotbarView.Available, "the hotbar's markers for the pick were found");

            Write("START the walk along your hotbar");
            Walk(Player.m_localPlayer);

            return Summary();
        }

        /// <summary>
        /// Walks the player's hotbar both ways through the picker's own walk, which reads the slots
        /// from the inventory, and checks it against what the slots hold.
        /// </summary>
        private void Walk(Player player)
        {
            var right = Round(player, 1);
            if (right.Count == 0)
            {
                Skip("nothing on your hotbar for the wheel to stop on; put a weapon or a tool there to check the walk");
                return;
            }

            Note("the wheel stops on slot " + string.Join(", ", right.Select(slot => slot + 1)));

            var other = right.Where(slot => !TakenInHand(Picker.ItemAt(player, slot))).Select(slot => slot + 1).ToList();
            Check(other.Count == 0, "every slot the wheel stops on holds a weapon, a bow, a shield, a tool or a torch",
                "slot " + string.Join(", ", other));

            var left = Round(player, -1);
            left.Reverse();
            Check(left.SequenceEqual(right), "scrolling left stops on the same slots as scrolling right",
                "left " + string.Join(", ", left.Select(slot => slot + 1)));

            Check(Picker.NextStop(player, -1, 1) == right[0], "with nothing in hand the first notch right stops on the leftmost of them");

            var last = right[right.Count - 1];
            var past = Picker.NextStop(player, last, 1);
            if (Plugin.Wrap) Check(past == right[0], "past the last of them the wheel continues at the first, as Wrap is on");
            else Check(past == last, "past the last of them the wheel stays, as Wrap is off");
        }

        /// <summary>The slots the wheel stops on from the end of the bar, in one direction, until it comes round or stops moving.</summary>
        private static List<int> Round(Player player, int step)
        {
            var seen = new List<int>();
            var slot = Picker.NextStop(player, -1, step);
            while (slot >= 0 && !seen.Contains(slot) && seen.Count < Picker.Slots)
            {
                seen.Add(slot);
                var next = Picker.NextStop(player, slot, step);
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

        private string Summary()
        {
            var done = "DONE " + _passed + " passed, " + _failed.Count + " failed, " + _skipped + " skipped";
            Write(done);
            foreach (var failure in _failed) Write("  failed: " + failure);
            return "ScrollToArms self-test: " + done + (_failed.Count == 0 ? string.Empty : "; see LogOutput.log");
        }

        private void Write(string line) => _log.LogInfo("[selftest] " + line);
    }
}
