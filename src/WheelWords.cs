using System.Collections.Generic;
using System.Text;

namespace ScrollToArms
{
    /// <summary>What the line above the hotbar says the wheel does; see <see cref="WheelWords.State"/>.</summary>
    internal enum WheelState
    {
        None,
        ModifierPicks,
        ModifierZooms,
        BuildRotates,
        BuildStuck,
        ToolKeeps,
    }

    /// <summary>Which message a tool the wheel took out gets; see <see cref="WheelWords.HintKindOf"/>.</summary>
    internal enum HintKind
    {
        None,
        KeepsTheWheel,
        WheelRotates,
    }

    /// <summary>
    /// The words the mod shows, with no game in them: the line saying what the wheel does, the
    /// message when the wheel takes out a tool that changes it, and key names as the player reads
    /// them. The game side reads the facts and the key bindings and shows the result; see
    /// <see cref="WheelMode"/> and <see cref="BuildToolHint"/>.
    /// </summary>
    internal static class WheelWords
    {
        /// <summary>
        /// What the wheel does right now, when it is worth saying: while the modifier is held,
        /// while a build tool takes the plain wheel away from the hotbar in PlainScroll Hotbar,
        /// while the piece rotation could not be redirected, and while a tool on the StepAsideTools
        /// list keeps the wheel. In ordinary play, nothing.
        /// </summary>
        public static WheelState State(bool toolKeeps, bool modifierHeld, bool placing, bool placementReady, PlainScroll plain)
        {
            if (toolKeeps) return WheelState.ToolKeeps;

            if (placing)
            {
                if (!placementReady) return WheelState.BuildStuck;
                if (modifierHeld) return WheelState.ModifierPicks;
                return plain == PlainScroll.Hotbar ? WheelState.BuildRotates : WheelState.None;
            }

            if (!modifierHeld) return WheelState.None;
            return plain == PlainScroll.Zoom ? WheelState.ModifierPicks : WheelState.ModifierZooms;
        }

        /// <summary>
        /// The line for a state, or null for none. <paramref name="modifier"/> and
        /// <paramref name="hide"/> are key names; an empty Hide key leaves its part out, and so
        /// does FlipBack off for the tap.
        /// </summary>
        public static string Line(WheelState state, string modifier, string hide, bool flipBack, string tool)
        {
            var key = $"[{modifier}]";
            var putAway = string.IsNullOrEmpty(hide) ? "" : $"  |  [{hide}] put away";
            var back = flipBack ? $"  |  tap {key} back" : "";

            switch (state)
            {
                case WheelState.ModifierPicks:
                    return "Wheel: hotbar";
                case WheelState.ModifierZooms:
                    return "Wheel: zoom";
                case WheelState.BuildRotates:
                    return $"Wheel: rotate  |  {key} + wheel: hotbar";
                case WheelState.BuildStuck:
                    return $"Wheel: rotate{putAway}{back}";
                case WheelState.ToolKeeps:
                    return $"{tool} keeps the wheel{putAway}{back}";
                default:
                    return null;
            }
        }

        /// <summary>
        /// The message a tool the wheel took out gets. A tool on StepAsideTools keeps the modifier
        /// and the wheel for itself, and so does any build tool when the piece rotation could not
        /// be redirected. Any other build tool gives the plain wheel to the rotation, which with
        /// PlainScroll on Zoom is the game's own wheel, so nothing is said, and with PlainScroll on
        /// Hotbar stops the plain wheel picking, which is said.
        /// </summary>
        public static HintKind HintKindOf(bool listed, bool buildTool, bool placementReady, PlainScroll plain)
        {
            if (listed) return HintKind.KeepsTheWheel;
            if (!buildTool) return HintKind.None;
            if (!placementReady) return HintKind.KeepsTheWheel;
            return plain == PlainScroll.Hotbar ? HintKind.WheelRotates : HintKind.None;
        }

        /// <summary>The centre message for a tool named <paramref name="name"/>, with key names marked as the game marks them.</summary>
        public static string Hint(HintKind kind, string name, string modifier, string hide, bool flipBack)
        {
            var key = Marked(modifier);
            if (kind == HintKind.WheelRotates) return $"{name} is out: the wheel rotates the piece. {key} with the wheel switches.";

            var text = string.IsNullOrEmpty(hide)
                ? $"{name} keeps the wheel. Put it away to scroll the hotbar again."
                : $"{name} keeps the wheel. {Marked(hide)} puts it away.";

            return flipBack ? text + $" A tap on {key} goes back." : text;
        }

        /// <summary>
        /// The log's line for what the wheel patch found in the camera zoom or the piece rotation.
        /// Reads found are information; none in the camera is an error, as hotbar scrolling stays off,
        /// none in the rotation a warning, as only build mode loses it.
        /// </summary>
        public static string PatchReport(bool camera, int replaced, out ReportLevel level)
        {
            if (replaced > 0)
            {
                level = ReportLevel.Info;
                var what = camera ? "camera zoom" : "piece rotation";
                return $"ScrollToArms: {what} hands over the wheel at {replaced} {(replaced == 1 ? "read" : "reads")}.";
            }

            if (camera)
            {
                level = ReportLevel.Error;
                return "ScrollToArms: GameCamera.UpdateCamera no longer reads the mouse wheel where expected. Hotbar scrolling stays off; the camera zoom is untouched.";
            }

            level = ReportLevel.Warning;
            return "ScrollToArms: Player.UpdatePlacement no longer reads the mouse wheel where expected. Hotbar scrolling stays off in build mode.";
        }

        /// <summary>A key code's name with spaces between its words: "LeftAlt" reads as "Left Alt".</summary>
        public static string Spaced(string raw)
        {
            var builder = new StringBuilder(raw.Length + 4);
            for (var i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1])) builder.Append(' ');
                builder.Append(raw[i]);
            }

            return builder.ToString();
        }

        /// <summary>The game's own look for a key in a centre message.</summary>
        public static string Marked(string label) => $"[<color=yellow><b>{label}</b></color>]";
    }

    /// <summary>How loud a line in the log is.</summary>
    internal enum ReportLevel
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// Which patch results have been told already. The transpiler runs again whenever another mod
    /// patches the same method, and a result is told once, and again only when it changes.
    /// </summary>
    internal sealed class PatchReports<TKey>
    {
        private readonly Dictionary<TKey, int> _told = new Dictionary<TKey, int>();

        /// <summary>Whether this count for this method is new, and so to be told; it counts as told from here on.</summary>
        public bool IsNew(TKey method, int replaced)
        {
            if (_told.TryGetValue(method, out var last) && last == replaced) return false;
            _told[method] = replaced;
            return true;
        }
    }

    /// <summary>
    /// Waits for a tool the wheel took out to arrive in hand, so its message is shown once, when
    /// it does. An item with an equip time arrives a moment later; one not in hand within
    /// <see cref="WatchTime"/> seconds was not equipped.
    /// </summary>
    internal sealed class HintWatch<TItem> where TItem : class
    {
        // Covers the equip time of a queued item.
        private const float WatchTime = 5f;

        private HintKind _kind;
        private float _until;

        /// <summary>The tool being waited for, or null.</summary>
        public TItem Watched { get; private set; }

        /// <summary>Called for every item the wheel equips or queues; one that changes nothing is not watched.</summary>
        public void Watch(TItem item, HintKind kind, float now)
        {
            _kind = kind;
            Watched = kind == HintKind.None ? null : item;
            _until = now + WatchTime;
        }

        /// <summary>True once, when the watched tool is in hand (<paramref name="equipped"/>) in time, with what to announce.</summary>
        public bool Arrived(float now, bool equipped, out TItem item, out HintKind kind)
        {
            item = null;
            kind = HintKind.None;
            if (Watched == null) return false;

            if (now > _until)
            {
                Watched = null;
                return false;
            }

            if (!equipped) return false;

            item = Watched;
            kind = _kind;
            Watched = null;
            return true;
        }
    }
}
