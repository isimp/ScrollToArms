using System.Text;
using UnityEngine;

namespace ScrollToArms
{
    /// <summary>Key names as the player reads them, for messages and the wheel indicator.</summary>
    internal static class KeyText
    {
        /// <summary>The modifier, such as "Left Alt" for LeftAlt.</summary>
        public static string Modifier => Spaced(Plugin.Modifier.ToString());

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

        /// <summary>The game's own look for a key in a centre message.</summary>
        public static string Marked(string label) => $"[<color=yellow><b>{label}</b></color>]";

        // "LeftAlt" reads as "Left Alt".
        private static string Spaced(string raw)
        {
            var builder = new StringBuilder(raw.Length + 4);
            for (var i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1])) builder.Append(' ');
                builder.Append(raw[i]);
            }

            return builder.ToString();
        }
    }
}
