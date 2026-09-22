using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ScrollToArms
{
    /// <summary>
    /// Shows the pick on the vanilla hotbar with the markers it already has. Each slot carries a
    /// "selected" frame, the game's gamepad cursor, which it only shows while a gamepad is in use,
    /// and a "queued" marker for an item waiting to be equipped. The frame marks the slot the
    /// wheel points at; the queued marker marks a pick waiting for the game to allow it.
    ///
    /// Written every LateUpdate while there is something to show, because the bar is not
    /// guaranteed to refresh its icons every frame. Once there is nothing to show, the markers are
    /// put back to what the game itself would show.
    /// </summary>
    internal static class HotbarView
    {
        // The vanilla bar's object name. Mods that add their own bars clone the same component
        // under other names.
        private const string VanillaBarName = "HotKeyBar";

        private static readonly FieldInfo ElementsField = AccessTools.Field(typeof(HotkeyBar), "m_elements");
        private static readonly FieldInfo SelectedField = AccessTools.Field(typeof(HotkeyBar), "m_selected");
        private static readonly Type ElementType = AccessTools.Inner(typeof(HotkeyBar), "ElementData");
        private static readonly FieldInfo SelectionField = ElementType == null ? null : AccessTools.Field(ElementType, "m_selection");
        private static readonly FieldInfo QueuedField = ElementType == null ? null : AccessTools.Field(ElementType, "m_queued");

        private static readonly bool Available = CheckAvailable();

        private static Hud _hud;
        private static HotkeyBar _bar;
        private static bool _drawn;

        private static bool CheckAvailable()
        {
            if (ElementsField != null && SelectedField != null && SelectionField != null && QueuedField != null) return true;

            Plugin.Log?.LogWarning("ScrollToArms: the hotbar's markers were not found. Scrolling still works, but the pick is not shown on the bar.");
            return false;
        }

        public static void Draw()
        {
            if (!Available) return;

            var player = Player.m_localPlayer;
            var bar = FindBar();
            if (player == null || bar == null)
            {
                _drawn = false;
                return;
            }

            var frame = Picker.Choosing ? Picker.Cursor : Picker.PendingSlot;
            var waiting = Picker.PendingSlot;

            if (frame < 0 && waiting < 0)
            {
                if (_drawn) Restore(bar, player);
                _drawn = false;
                return;
            }

            if (!(ElementsField.GetValue(bar) is IList elements)) return;

            for (var i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                if (element == null) continue;

                SetActive(SelectionField.GetValue(element) as GameObject, i == frame);

                var item = Picker.ItemAt(player, i);
                var queued = i == waiting || (item != null && player.IsEquipActionQueued(item));
                SetActive(QueuedField.GetValue(element) as GameObject, queued);
            }

            _drawn = true;
        }

        /// <summary>The markers as HotkeyBar.UpdateIcons would set them.</summary>
        private static void Restore(HotkeyBar bar, Player player)
        {
            if (!(ElementsField.GetValue(bar) is IList elements)) return;

            var gamepad = ZInput.IsGamepadActive();
            var selected = (int)SelectedField.GetValue(bar);

            for (var i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                if (element == null) continue;

                SetActive(SelectionField.GetValue(element) as GameObject, gamepad && i == selected);

                var item = Picker.ItemAt(player, i);
                SetActive(QueuedField.GetValue(element) as GameObject, item != null && player.IsEquipActionQueued(item));
            }
        }

        private static HotkeyBar FindBar()
        {
            var hud = Hud.instance;
            if (hud == null)
            {
                _hud = null;
                _bar = null;
                return null;
            }

            // One search per Hud. A bar that is missing stays missing until the next Hud.
            if (hud == _hud) return _bar;

            _hud = hud;
            _bar = null;
            foreach (var bar in hud.GetComponentsInChildren<HotkeyBar>(true))
            {
                if (bar.name != VanillaBarName) continue;
                _bar = bar;
                break;
            }

            return _bar;
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
