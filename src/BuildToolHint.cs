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
        private static readonly HintWatch<ItemDrop.ItemData> Arrival = new HintWatch<ItemDrop.ItemData>();

        /// <summary>Called for every item the wheel equips or queues.</summary>
        public static void Watch(ItemDrop.ItemData item, float now) => Arrival.Watch(item, KindOf(item), now);

        private static HintKind KindOf(ItemDrop.ItemData item) =>
            item?.m_shared == null
                ? HintKind.None
                : WheelWords.HintKindOf(StepAside.Listed(item), item.m_shared.m_buildPieces != null, WheelReads.PlacementReady, Plugin.PlainScroll);

        public static void Update(Player player, float now)
        {
            var watched = Arrival.Watched;
            if (Arrival.Arrived(now, watched != null && watched.m_equipped, out var item, out var kind)) Show(player, item, kind);
        }

        private static void Show(Player player, ItemDrop.ItemData item, HintKind kind)
        {
            if (!Plugin.ShowCenterHints) return;

            var name = ItemNameList.ShownName(item.m_shared.m_name);
            player.Message(MessageHud.MessageType.Center, WheelWords.Hint(kind, name, KeyText.Modifier, KeyText.Hide, Plugin.FlipBack));
        }
    }
}
