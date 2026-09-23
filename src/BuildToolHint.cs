namespace ScrollToArms
{
    /// <summary>
    /// Tells the player what the wheel does after it put a tool in their hand that changes it.
    ///
    /// A tool on the StepAsideTools list keeps the modifier and the wheel for itself, and so does
    /// any build tool when the piece rotation could not be patched. The wheel cannot switch away
    /// from those, but the game's Hide key can: it puts the hand items away even in build mode.
    /// So can a tap on the modifier when FlipBack is on.
    ///
    /// Any other build tool gives the plain wheel to the piece rotation, with the modifier and the
    /// wheel still picking from the hotbar. With PlainScroll on Zoom that matches the game's own
    /// wheel, so nothing is said. With PlainScroll on Hotbar the plain wheel stops picking, which
    /// is said once each time the wheel equips such a tool.
    ///
    /// Shown only for a tool the wheel equipped, never for one taken out another way.
    /// </summary>
    internal static class BuildToolHint
    {
        private enum Kind
        {
            None,
            KeepsTheWheel,
            WheelRotates
        }

        private static ItemDrop.ItemData _watched;
        private static Kind _kind;
        private static float _watchUntil;

        // Covers the equip time of a queued item; a tool not in hand by then was not equipped.
        private const float WatchTime = 5f;

        /// <summary>Called for every item the wheel equips or queues.</summary>
        public static void Watch(ItemDrop.ItemData item, float now)
        {
            _kind = KindOf(item);
            _watched = _kind == Kind.None ? null : item;
            _watchUntil = now + WatchTime;
        }

        private static Kind KindOf(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null) return Kind.None;
            if (StepAside.Listed(item)) return Kind.KeepsTheWheel;
            if (item.m_shared.m_buildPieces == null) return Kind.None;
            if (!WheelReads.PlacementReady) return Kind.KeepsTheWheel;
            return Plugin.PlainScroll == PlainScroll.Hotbar ? Kind.WheelRotates : Kind.None;
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

            if (!item.m_equipped) return;

            _watched = null;
            Show(player, item, _kind);
        }

        private static void Show(Player player, ItemDrop.ItemData item, Kind kind)
        {
            if (!Plugin.ShowCenterHints) return;

            var name = ItemNameList.ShownName(item.m_shared.m_name);
            var modifier = KeyText.Marked(KeyText.Modifier);

            string text;
            if (kind == Kind.WheelRotates)
            {
                text = $"{name} is out: the wheel rotates the piece. {modifier} with the wheel switches.";
            }
            else
            {
                var hide = KeyText.Hide;
                text = string.IsNullOrEmpty(hide)
                    ? $"{name} keeps the wheel. Put it away to scroll the hotbar again."
                    : $"{name} keeps the wheel. {KeyText.Marked(hide)} puts it away.";

                if (Plugin.FlipBack) text += $" A tap on {modifier} goes back.";
            }

            player.Message(MessageHud.MessageType.Center, text);
        }
    }
}
