namespace ScrollToArms
{
    /// <summary>Key names as the player reads them, for messages and the wheel indicator.</summary>
    internal static class KeyText
    {
        /// <summary>The modifier, such as "Left Alt" for LeftAlt.</summary>
        public static string Modifier => WheelWords.Spaced(Plugin.Modifier.ToString());

        /// <summary>The key bound to the game's Hide action, or an empty string when it has none.</summary>
        public static string Hide
        {
            get
            {
                var key = ZInput.instance != null ? ZInput.instance.GetBoundKeyString("Hide", emptyStringOnMissing: true) : "";
                var localization = Localization.instance;
                return !string.IsNullOrEmpty(key) && localization != null ? localization.Localize(key) : key ?? "";
            }
        }
    }
}
