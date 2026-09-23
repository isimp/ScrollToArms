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
        private enum State
        {
            None,
            ModifierPicks,
            ModifierZooms,
            BuildRotates,
            BuildStuck,
            ToolKeeps
        }

        // The text is rebuilt only when what it says changes, not every frame.
        private static State _state;
        private static ItemDrop.ItemData _tool;
        private static KeyCode _modifier;
        private static bool _flipBack;
        private static string _text;

        /// <summary>The indicator's text, or null when there is nothing to say.</summary>
        public static string Text(Player player)
        {
            if (!Plugin.ShowWheelMode || !Plugin.Enabled || !WheelReads.CameraReady || !Picker.Usable(player)) return null;

            var tool = StepAside.Listed(player.RightItem) ? player.RightItem : StepAside.Listed(player.LeftItem) ? player.LeftItem : null;
            var state = StateOf(player, tool);
            if (state == State.None) return null;

            if (state != _state || tool != _tool || Plugin.Modifier != _modifier || Plugin.FlipBack != _flipBack || _text == null)
            {
                _state = state;
                _tool = tool;
                _modifier = Plugin.Modifier;
                _flipBack = Plugin.FlipBack;
                _text = Build(state, tool);
            }

            return _text;
        }

        private static State StateOf(Player player, ItemDrop.ItemData tool)
        {
            if (tool != null) return State.ToolKeeps;

            var held = Input.GetKey(Plugin.Modifier);
            if (player.InPlaceMode())
            {
                if (!WheelReads.PlacementReady) return State.BuildStuck;
                if (held) return State.ModifierPicks;
                return Plugin.PlainScroll == PlainScroll.Hotbar ? State.BuildRotates : State.None;
            }

            if (!held) return State.None;
            return Plugin.PlainScroll == PlainScroll.Zoom ? State.ModifierPicks : State.ModifierZooms;
        }

        private static string Build(State state, ItemDrop.ItemData tool)
        {
            var modifier = $"[{KeyText.Modifier}]";
            var hide = KeyText.Hide;
            var putAway = string.IsNullOrEmpty(hide) ? "" : $"  |  [{hide}] put away";
            var back = Plugin.FlipBack ? $"  |  tap {modifier} back" : "";

            switch (state)
            {
                case State.ModifierPicks:
                    return "Wheel: hotbar";
                case State.ModifierZooms:
                    return "Wheel: zoom";
                case State.BuildRotates:
                    return $"Wheel: rotate  |  {modifier} + wheel: hotbar";
                case State.BuildStuck:
                    return $"Wheel: rotate{putAway}{back}";
                case State.ToolKeeps:
                    return $"{ItemNameList.ShownName(tool.m_shared.m_name)} keeps the wheel{putAway}{back}";
                default:
                    return null;
            }
        }
    }
}
