using UnityEngine;

namespace ScrollToArms
{
    /// <summary>
    /// What the wheel does right now, shown where the item name goes whenever it is worth saying:
    /// while the modifier is held, while a build tool takes the plain wheel away from the hotbar
    /// in PlainScroll Hotbar, and while a tool on the StepAsideTools list keeps the wheel. In
    /// ordinary play nothing is shown. The item name takes the spot back as soon as a pick starts.
    /// </summary>
    internal static class WheelMode
    {
        // The text is rebuilt only when what it says changes, not every frame.
        private static WheelState _state;
        private static ItemDrop.ItemData _tool;
        private static KeyCode _modifier;
        private static bool _flipBack;
        private static string _text;

        /// <summary>The indicator's text, or null when there is nothing to say (see <see cref="WheelWords.State"/>).</summary>
        public static string Text(Player player)
        {
            if (!Plugin.ShowWheelMode || !Plugin.Enabled || !WheelReads.CameraReady || !Picker.Usable(player)) return null;

            var tool = StepAside.Listed(player.RightItem) ? player.RightItem : StepAside.Listed(player.LeftItem) ? player.LeftItem : null;
            var state = WheelWords.State(tool != null, Input.GetKey(Plugin.Modifier), player.InPlaceMode(), WheelReads.PlacementReady, Plugin.PlainScroll);
            if (state == WheelState.None) return null;

            if (state != _state || tool != _tool || Plugin.Modifier != _modifier || Plugin.FlipBack != _flipBack || _text == null)
            {
                _state = state;
                _tool = tool;
                _modifier = Plugin.Modifier;
                _flipBack = Plugin.FlipBack;
                _text = WheelWords.Line(state, KeyText.Modifier, KeyText.Hide, Plugin.FlipBack,
                    tool != null ? ItemNameList.ShownName(tool.m_shared.m_name) : null);
            }

            return _text;
        }
    }
}
