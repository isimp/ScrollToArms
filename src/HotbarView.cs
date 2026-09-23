using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ScrollToArms
{
    /// <summary>
    /// Shows the pick on the vanilla hotbar with the markers it already has. Each slot carries a
    /// "selected" frame, the game's gamepad cursor, which it only shows while a gamepad is in use,
    /// and a "queued" marker for an item waiting to be equipped. The frame marks the slot the
    /// wheel points at; the queued marker marks a pick waiting for the game to allow it. The frame
    /// pulses while a pick waits, and flashes red and fades on the slot of a pick that was lost.
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

            // A lost pick flashes red on its slot and fades out, unless the frame is back on it.
            var flashAge = Time.time - Picker.LostAt;
            var flash = Picker.LostSlot >= 0 && flashAge < FlashTime && Picker.LostSlot != frame ? Picker.LostSlot : -1;

            if (frame < 0 && waiting < 0 && flash < 0)
            {
                if (_drawn) Restore(bar, player);
                _drawn = false;
                return;
            }

            if (!(ElementsField.GetValue(bar) is IList elements)) return;

            if (flash < 0) StopFlash();

            for (var i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                if (element == null) continue;

                var selection = SelectionField.GetValue(element) as GameObject;
                SetActive(selection, i == frame || i == flash);

                // A pick held back while the game would refuse it pulses, so a wait reads as a wait
                // rather than as a pick that went nowhere.
                if (i == frame) Pulse(selection, !Picker.Choosing && Picker.Waiting);
                if (i == flash) Flash(selection, flashAge / FlashTime);

                var item = Picker.ItemAt(player, i);
                var queued = i == waiting || (item != null && player.IsEquipActionQueued(item));
                SetActive(QueuedField.GetValue(element) as GameObject, queued);
            }

            _drawn = true;
        }

        // How long a lost pick's red frame takes to fade out.
        private const float FlashTime = 0.5f;

        private static readonly Color FlashColor = new Color(1f, 0.25f, 0.2f, 1f);

        private static GameObject _flashed;
        private static Graphic[] _flashedGraphics;
        private static Color[] _flashedColors;

        private static void Flash(GameObject selection, float progress)
        {
            if (selection == null) return;

            if (_flashed != selection)
            {
                StopFlash();
                _flashed = selection;
                _flashedGraphics = selection.GetComponentsInChildren<Graphic>(true);
                _flashedColors = new Color[_flashedGraphics.Length];
                for (var i = 0; i < _flashedGraphics.Length; i++)
                {
                    _flashedColors[i] = _flashedGraphics[i].color;
                    var red = FlashColor;
                    red.a = _flashedColors[i].a;
                    _flashedGraphics[i].color = red;
                }
            }

            var group = selection.GetComponent<CanvasGroup>() ?? selection.AddComponent<CanvasGroup>();

            // The same frame may have been pulsing for the wait that just ran out; the fade owns
            // its opacity from here on.
            if (_pulsed == group) _pulsed = null;

            group.alpha = 1f - Mathf.Clamp01(progress);
        }

        private static void StopFlash()
        {
            if (_flashed == null) return;

            for (var i = 0; i < _flashedGraphics.Length; i++)
            {
                if (_flashedGraphics[i] != null) _flashedGraphics[i].color = _flashedColors[i];
            }

            var group = _flashed.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;

            _flashed = null;
            _flashedGraphics = null;
            _flashedColors = null;
        }

        // Twice per second, between PulseLow and full opacity, starting dim the moment the wait
        // begins, so a wait of half a second still shows one clear dip.
        private const float PulseLow = 0.15f;
        private const float PulseRate = 2f * 2f * Mathf.PI;

        private static CanvasGroup _pulsed;

        private static void Pulse(GameObject selection, bool on)
        {
            if (selection == null) return;

            if (!on)
            {
                StopPulse();
                return;
            }

            var group = selection.GetComponent<CanvasGroup>() ?? selection.AddComponent<CanvasGroup>();
            if (_pulsed != group) StopPulse();
            _pulsed = group;

            var wave = 0.5f - 0.5f * Mathf.Cos((Time.time - Picker.WaitingSince) * PulseRate);
            group.alpha = Mathf.Lerp(PulseLow, 1f, wave);
        }

        private static void StopPulse()
        {
            if (_pulsed != null) _pulsed.alpha = 1f;
            _pulsed = null;
        }

        /// <summary>The markers as HotkeyBar.UpdateIcons would set them.</summary>
        private static void Restore(HotkeyBar bar, Player player)
        {
            StopPulse();
            StopFlash();
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
