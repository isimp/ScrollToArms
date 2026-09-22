namespace ScrollToArms
{
    /// <summary>
    /// Tells the player how to get out of build mode when the wheel put a build tool in their
    /// hand. In build mode the wheel rotates the piece being placed and hotbar scrolling is off,
    /// so the wheel alone cannot switch away again. The game's Hide key can: it puts the hand
    /// items away even in build mode, which ends it.
    ///
    /// Shown only for a tool the wheel equipped, never for one taken out another way.
    /// </summary>
    internal static class BuildToolHint
    {
        private static ItemDrop.ItemData _watched;
        private static float _watchUntil;

        // Covers the equip time of a queued item; a tool not in hand by then was not equipped.
        private const float WatchTime = 5f;

        /// <summary>Called for every item the wheel equips or queues.</summary>
        public static void Watch(ItemDrop.ItemData item, float now)
        {
            if (item?.m_shared?.m_buildPieces == null)
            {
                _watched = null;
                return;
            }

            _watched = item;
            _watchUntil = now + WatchTime;
        }

        public static void Update(Player player, float now)
        {
            var item = _watched;
            if (item == null) return;

            if (now > _watchUntil)
            {
                _watched = null;
                return;
            }

            if (!item.m_equipped || !player.InPlaceMode()) return;

            _watched = null;
            Show(player, item);
        }

        private static void Show(Player player, ItemDrop.ItemData item)
        {
            var localization = Localization.instance;
            var name = localization != null ? localization.Localize(item.m_shared.m_name) : item.m_shared.m_name;

            var key = ZInput.instance != null ? ZInput.instance.GetBoundKeyString("Hide", emptyStringOnMissing: true) : "";
            if (!string.IsNullOrEmpty(key) && localization != null) key = localization.Localize(key);

            var text = string.IsNullOrEmpty(key)
                ? $"{name} is out, so the wheel builds. Put it away to scroll the hotbar again."
                : $"{name} is out, so the wheel builds. [<color=yellow><b>{key}</b></color>] puts it away.";

            player.Message(MessageHud.MessageType.Center, text);
        }
    }
}
