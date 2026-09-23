using System;
using System.Collections.Generic;

namespace ScrollToArms
{
    /// <summary>
    /// Tools whose own controls use the modifier with the wheel. While one of them is in hand, the
    /// modifier and the wheel are left to the tool. Which tools do that cannot be read from the
    /// outside, so the list is a setting, matched against item prefab names.
    /// </summary>
    internal static class StepAside
    {
        private static HashSet<string> _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string _parsed;

        /// <summary>True when either hand holds a tool on the list.</summary>
        public static bool Holding(Player player) =>
            Listed(player.RightItem) || Listed(player.LeftItem);

        public static bool Listed(ItemDrop.ItemData item)
        {
            var prefab = item?.m_dropPrefab;
            if (prefab == null) return false;

            Refresh();
            return _names.Count > 0 && _names.Contains(prefab.name);
        }

        // Parsed again only when the setting's text changes.
        private static void Refresh()
        {
            var text = Plugin.StepAsideTools ?? "";
            if (text == _parsed) return;
            _parsed = text;

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in text.Split(','))
            {
                var name = part.Trim();
                if (name.Length > 0) names.Add(name);
            }

            _names = names;
        }
    }
}
