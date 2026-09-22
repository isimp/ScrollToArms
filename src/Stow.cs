using System;
using HarmonyLib;

namespace ScrollToArms
{
    /// <summary>
    /// What the game's Hide key put away. HideHandItems unequips both hands and remembers them in
    /// two private fields, and ShowHandItems equips both again. Equipping any other hand item
    /// clears that memory, so it only ever describes the stow the player can still undo.
    /// </summary>
    internal static class Stow
    {
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> HiddenRight = Field("m_hiddenRightItem");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> HiddenLeft = Field("m_hiddenLeftItem");
        private static readonly Action<Humanoid, bool, bool> ShowHandItems = ResolveShow();

        /// <summary>False when a game update moved any of it; the hotbar then treats stowed hands as empty.</summary>
        public static bool Available => HiddenRight != null && HiddenLeft != null && ShowHandItems != null;

        private static AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> Field(string name)
        {
            try
            {
                return AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>(name);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"ScrollToArms: Humanoid.{name} could not be read ({ex.Message}). Stowed items are treated as empty hands.");
                return null;
            }
        }

        private static Action<Humanoid, bool, bool> ResolveShow()
        {
            try
            {
                var method = AccessTools.Method(typeof(Humanoid), "ShowHandItems");
                if (method != null) return AccessTools.MethodDelegate<Action<Humanoid, bool, bool>>(method);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"ScrollToArms: Humanoid.ShowHandItems could not be resolved ({ex.Message}). Stowed items are treated as empty hands.");
                return null;
            }

            Plugin.Log?.LogWarning("ScrollToArms: Humanoid.ShowHandItems was not found. Stowed items are treated as empty hands.");
            return null;
        }

        public static ItemDrop.ItemData Right(Player player) => Available ? HiddenRight(player) : null;

        public static ItemDrop.ItemData Left(Player player) => Available ? HiddenLeft(player) : null;

        /// <summary>
        /// True when both hands are empty and <paramref name="item"/> is what the Hide key would
        /// bring back, which is the only state in which the game's own Hide key re-equips.
        /// </summary>
        public static bool IsStowed(Player player, ItemDrop.ItemData item)
        {
            if (!Available || item == null) return false;
            if (player.RightItem != null || player.LeftItem != null) return false;
            return item == HiddenRight(player) || item == HiddenLeft(player);
        }

        /// <summary>Brings back both stowed items, as the game's Hide key does with empty hands.</summary>
        public static void Unstow(Player player)
        {
            ShowHandItems(player, false, true);
        }
    }
}
