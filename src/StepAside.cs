namespace ScrollToArms
{
    /// <summary>
    /// Tools whose own controls use the modifier with the wheel. While one of them is in hand, the
    /// modifier and the wheel are left to the tool. Which tools do that cannot be read from the
    /// outside, so the list is a setting.
    /// </summary>
    internal static class StepAside
    {
        private static readonly ItemNameList Tools = new ItemNameList(() => Plugin.StepAsideTools);

        /// <summary>True when either hand holds a tool on the list.</summary>
        public static bool Holding(Player player) =>
            Listed(player.RightItem) || Listed(player.LeftItem);

        public static bool Listed(ItemDrop.ItemData item) => Tools.Contains(item);
    }
}
