using System;
using System.Collections.Generic;

namespace ScrollToArms
{
    /// <summary>
    /// A comma-separated list of items from a setting. An entry matches an item by the name the
    /// game shows for it (in the current language), by its name token such as $item_hammer, or by
    /// its prefab name. Case does not matter.
    /// </summary>
    internal sealed class ItemNameList
    {
        private readonly Func<string> _source;
        private HashSet<string> _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _parsed;

        // Localizing is a lookup per call, and the same few hotbar items are asked about every
        // frame, so each token is localized once.
        private static readonly Dictionary<string, string> Shown = new Dictionary<string, string>();

        public ItemNameList(Func<string> source)
        {
            _source = source;
        }

        public bool Contains(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null) return false;

            // The prefab's name is asked of Unity only when there is a list to look it up in.
            Refresh();
            if (_names.Count == 0) return false;

            var prefab = item.m_dropPrefab;
            return Contains(prefab != null ? prefab.name : null, item.m_shared.m_name, ShownName);
        }

        /// <summary>
        /// Whether the list names an item with this prefab name and name token, whose shown name
        /// <paramref name="shown"/> gives; either name may be missing. The shown name is asked for
        /// last, as it is the one that costs a lookup.
        /// </summary>
        internal bool Contains(string prefab, string token, Func<string, string> shown)
        {
            Refresh();
            if (_names.Count == 0) return false;

            if (prefab != null && _names.Contains(prefab)) return true;

            if (string.IsNullOrEmpty(token)) return false;
            if (_names.Contains(token)) return true;

            return _names.Contains(shown(token));
        }

        /// <summary>The name the game shows for an item, in the current language.</summary>
        public static string ShownName(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            if (Shown.TryGetValue(token, out var shown)) return shown;

            var localization = Localization.instance;
            if (localization == null) return token;

            shown = localization.Localize(token);
            Shown[token] = shown;
            return shown;
        }

        // Parsed again only when the setting's text changes.
        private void Refresh()
        {
            var text = _source() ?? "";
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
