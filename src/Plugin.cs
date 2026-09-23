using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ScrollToArms
{
    /// <summary>What the mouse wheel does when the modifier is not held.</summary>
    public enum PlainScroll
    {
        Zoom,
        Hotbar
    }

    /// <summary>When a slot picked with the wheel is put in your hand.</summary>
    public enum Commit
    {
        OnRelease,
        AfterPause
    }

    /// <summary>
    /// Scroll the hotbar with the mouse wheel. A Harmony transpiler takes the wheel away from the
    /// camera zoom and the piece rotation while the hotbar owns it; the rest is the picker, driven
    /// from Update, and the selection frame, drawn in LateUpdate.
    /// </summary>
    [BepInPlugin(Guid, "ScrollToArms", Version)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "isimp.ScrollToArms";
        public const string Version = "0.1.0";

        public static ManualLogSource Log;

        // Set when the camera patch cannot be requested at all. Without it the wheel would zoom
        // and scroll the hotbar at the same time, so the mod stays out of the way instead. A patch
        // that is requested but finds nothing is handled by WheelReads.CameraReady.
        private static bool _broken;

        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<KeyCode> _modifier;
        private static ConfigEntry<PlainScroll> _plainScroll;
        private static ConfigEntry<Commit> _commit;
        private static ConfigEntry<float> _pauseTime;
        private static ConfigEntry<bool> _wrap;
        private static ConfigEntry<bool> _invert;
        private static ConfigEntry<bool> _skipBuildTools;
        private static ConfigEntry<string> _stepAsideTools;
        private static ConfigEntry<float> _waitWhileBusy;

        public static bool Enabled => !_broken && (_enabled == null || _enabled.Value);
        public static KeyCode Modifier => _modifier?.Value ?? KeyCode.LeftAlt;
        public static PlainScroll PlainScroll => _plainScroll?.Value ?? PlainScroll.Zoom;
        public static Commit CommitSetting => _commit?.Value ?? Commit.OnRelease;
        public static float PauseTime => _pauseTime?.Value ?? 0.4f;
        public static bool Wrap => _wrap == null || _wrap.Value;
        public static bool Invert => _invert != null && _invert.Value;
        public static bool SkipBuildTools => _skipBuildTools != null && _skipBuildTools.Value;
        public static string StepAsideTools => _stepAsideTools?.Value ?? DefaultStepAsideTools;
        public static float WaitWhileBusy => _waitWhileBusy?.Value ?? 2f;

        private const string DefaultStepAsideTools = "BlueprintRune, PlanHammer";

        /// <summary>
        /// The commit that applies right now. Releasing the modifier can only end a pick that was
        /// made with it held, so when plain scroll drives the hotbar the pause is used instead.
        /// </summary>
        public static Commit EffectiveCommit =>
            PlainScroll == PlainScroll.Hotbar ? Commit.AfterPause : CommitSetting;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            _enabled = Config.Bind("General", "Enabled", true,
                "Turn hotbar scrolling off without removing the mod.");

            // A raw KeyCode read through legacy Input, not a KeyboardShortcut: a BepInEx shortcut
            // refuses to fire while any other keyboard key is held, and this one is held while
            // moving with WASD.
            _modifier = Config.Bind("General", "Modifier", KeyCode.LeftAlt,
                "Hold this while scrolling to use the other half of the wheel: the hotbar when PlainScroll is Zoom, the camera zoom when PlainScroll is Hotbar. In build mode the plain wheel rotates the piece being placed, so there this always picks from the hotbar.");

            _plainScroll = Config.Bind("General", "PlainScroll", PlainScroll.Zoom,
                "What the mouse wheel does without the modifier. Zoom keeps the game's camera zoom on the wheel and scrolls the hotbar with the modifier held. Hotbar swaps the two. With a build tool out, the plain wheel rotates the piece either way and the modifier with the wheel picks from the hotbar; with Hotbar a message says so when the wheel takes a build tool out.");

            _commit = Config.Bind("Selection", "Commit", Commit.OnRelease,
                "When the picked slot is equipped. OnRelease equips it when you let go of the modifier. AfterPause equips it once the wheel has been still for PauseTime. When PlainScroll is Hotbar there is no modifier to release, so AfterPause is always used.");

            _pauseTime = Config.Bind("Selection", "PauseTime", 0.4f,
                new ConfigDescription(
                    "Seconds the wheel has to be still before the picked slot is equipped, when AfterPause applies.",
                    new AcceptableValueRange<float>(0.05f, 1.5f)));

            _wrap = Config.Bind("Selection", "Wrap", true,
                "Scrolling past the last slot continues at the first, and the other way round.");

            _invert = Config.Bind("Selection", "InvertDirection", false,
                "By default scrolling down moves right along the hotbar and scrolling up moves left. Turn this on to swap them.");

            _skipBuildTools = Config.Bind("Selection", "SkipBuildTools", false,
                "Pass over the hammer, hoe, cultivator and other tools that open build mode. With them in hand the plain wheel rotates the piece being placed, and the modifier with the wheel still picks from the hotbar.");

            _stepAsideTools = Config.Bind("Selection", "StepAsideTools", DefaultStepAsideTools,
                "Tools whose own controls use the modifier with the wheel. While one of them is in hand, the modifier and the wheel are left to the tool, and the Hide key or a number key switches away. Item prefab names, separated by commas. The defaults are the Blueprint Rune and Plan Hammer from PlanBuild.");

            _waitWhileBusy = Config.Bind("Rules", "WaitWhileBusy", 2f,
                new ConfigDescription(
                    "The game refuses to change what you hold while you attack, dodge or swim, and cancels an item's equip time while you run, jump or attack. A pick refused or cancelled that way is kept for this many seconds, with its slot pulsing on the hotbar, and equipped as soon as the game allows it. If the time runs out first, the slot flashes red and the pick is dropped. The game's own equip bar does not count against this time. 0 drops the pick at once, the way the number keys do.",
                    new AcceptableValueRange<float>(0f, 3f)));

            _harmony = new Harmony(Guid);
            _broken = !WheelReads.Apply(_harmony);
            if (_broken)
            {
                Log.LogError("ScrollToArms: hotbar scrolling is off for this session. The camera zoom is untouched.");
                return;
            }

            Log.LogInfo($"ScrollToArms {Version}: plain scroll {PlainScroll}, modifier {Modifier}.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private void Update()
        {
            Picker.Update(Time.deltaTime);
        }

        // After every Update, so the frame drawn here is the last word for this frame even when
        // the hotbar refreshed its own icons earlier in it.
        private void LateUpdate()
        {
            HotbarView.Draw();
        }
    }
}
