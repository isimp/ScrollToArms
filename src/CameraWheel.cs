using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ScrollToArms
{
    /// <summary>
    /// Takes the wheel away from the camera zoom while the hotbar owns it.
    ///
    /// GameCamera.UpdateCamera reads ZInput.GetMouseScrollWheel itself. The minimap, the placement
    /// ghost's rotation and other mods read the same value, so the value is left alone and only
    /// the camera's reads are redirected through <see cref="Read"/>.
    /// </summary>
    internal static class CameraWheel
    {
        private static readonly MethodInfo Original =
            AccessTools.Method(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel));

        private static readonly MethodInfo Replacement =
            AccessTools.Method(typeof(CameraWheel), nameof(Read));

        private static int _replaced;

        public static bool Apply(Harmony harmony)
        {
            try
            {
                var target = AccessTools.Method(typeof(GameCamera), "UpdateCamera");
                if (target == null || Original == null)
                {
                    Plugin.Log.LogError("ScrollToArms: GameCamera.UpdateCamera or ZInput.GetMouseScrollWheel was not found.");
                    return false;
                }

                _replaced = 0;
                harmony.Patch(target, transpiler: new HarmonyMethod(typeof(CameraWheel), nameof(Transpiler)));
                if (_replaced == 0)
                {
                    Plugin.Log.LogError("ScrollToArms: GameCamera.UpdateCamera no longer reads the mouse wheel where expected.");
                    harmony.Unpatch(target, HarmonyPatchType.Transpiler, harmony.Id);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"ScrollToArms: could not patch GameCamera.UpdateCamera - {ex.Message}");
                return false;
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(Original))
                {
                    instruction.operand = Replacement;
                    _replaced++;
                }

                yield return instruction;
            }
        }

        /// <summary>The wheel as the camera sees it: nothing while the hotbar owns it.</summary>
        public static float Read()
        {
            var value = ZInput.GetMouseScrollWheel();
            return Picker.HotbarOwnsWheel() ? 0f : value;
        }
    }
}
