using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ScrollToArms
{
    /// <summary>
    /// Takes the wheel away from the game while the hotbar owns it.
    ///
    /// Two places in the game act on ZInput.GetMouseScrollWheel while the hotbar can be used:
    /// the camera zoom in GameCamera.UpdateCamera, and the rotation of the piece being placed in
    /// Player.UpdatePlacement. Their reads, and only those, are redirected through
    /// <see cref="Read"/>. The minimap and other mods keep reading the real value.
    ///
    /// Each transpiler is judged when it runs, not when Patch returns. A patcher can defer Harmony
    /// patches until later in startup, so at the time of the Patch call a transpiler may not have
    /// run yet. Until it has run and found the reads, the hotbar never takes the wheel from that
    /// place.
    /// </summary>
    internal static class WheelReads
    {
        private static readonly MethodInfo Original =
            AccessTools.Method(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel));

        private static readonly MethodInfo Replacement =
            AccessTools.Method(typeof(WheelReads), nameof(Read));

        private static readonly MethodInfo Camera = AccessTools.Method(typeof(GameCamera), "UpdateCamera");
        private static readonly MethodInfo Placement = AccessTools.Method(typeof(Player), "UpdatePlacement");

        // Reads found by the latest run of the transpiler, per patched method. Absent until it ran.
        private static readonly Dictionary<MethodBase, int> Found = new Dictionary<MethodBase, int>();
        private static readonly Dictionary<MethodBase, int> Reported = new Dictionary<MethodBase, int>();

        /// <summary>True once the camera zoom's wheel reads go through <see cref="Read"/>.</summary>
        public static bool CameraReady => ReadyFor(Camera);

        /// <summary>True once the piece rotation's wheel read goes through <see cref="Read"/>.</summary>
        public static bool PlacementReady => ReadyFor(Placement);

        /// <summary>True once the camera transpiler has run, whatever it found.</summary>
        public static bool CameraRan => Camera != null && Found.ContainsKey(Camera);

        private static bool ReadyFor(MethodBase method) =>
            method != null && Found.TryGetValue(method, out var count) && count > 0;

        /// <summary>False when the camera cannot be patched, which leaves the mod nothing to do.</summary>
        public static bool Apply(Harmony harmony)
        {
            if (Original == null || Camera == null)
            {
                Plugin.Log.LogError("ScrollToArms: GameCamera.UpdateCamera or ZInput.GetMouseScrollWheel was not found.");
                return false;
            }

            if (!Patch(harmony, Camera, "GameCamera.UpdateCamera")) return false;

            // Without it the hotbar simply stays out of build mode.
            if (Placement == null) Plugin.Log.LogWarning("ScrollToArms: Player.UpdatePlacement was not found. Hotbar scrolling stays off in build mode.");
            else Patch(harmony, Placement, "Player.UpdatePlacement");

            return true;
        }

        private static bool Patch(Harmony harmony, MethodBase target, string name)
        {
            try
            {
                harmony.Patch(target, transpiler: new HarmonyMethod(typeof(WheelReads), nameof(Transpiler)));
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"ScrollToArms: could not patch {name} - {ex.Message}");
                return false;
            }
        }

        // Runs every time a patched method is rebuilt, which happens again whenever another mod
        // patches it. Each run replaces the previous result for that method.
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var replaced = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(Original))
                {
                    instruction.operand = Replacement;
                    replaced++;
                }

                yield return instruction;
            }

            Found[original] = replaced;
            Report(original, replaced);
        }

        private static void Report(MethodBase method, int replaced)
        {
            if (Reported.TryGetValue(method, out var last) && last == replaced) return;
            Reported[method] = replaced;

            var what = method == Camera ? "camera zoom" : "piece rotation";
            if (replaced > 0)
                Plugin.Log.LogInfo($"ScrollToArms: {what} hands over the wheel at {replaced} {(replaced == 1 ? "read" : "reads")}.");
            else if (method == Camera)
                Plugin.Log.LogError("ScrollToArms: GameCamera.UpdateCamera no longer reads the mouse wheel where expected. Hotbar scrolling stays off; the camera zoom is untouched.");
            else
                Plugin.Log.LogWarning("ScrollToArms: Player.UpdatePlacement no longer reads the mouse wheel where expected. Hotbar scrolling stays off in build mode.");
        }

        /// <summary>The wheel as the camera and the piece rotation see it: nothing while the hotbar owns it.</summary>
        public static float Read()
        {
            var value = ZInput.GetMouseScrollWheel();
            return Picker.HotbarOwnsWheel() ? 0f : value;
        }
    }
}
