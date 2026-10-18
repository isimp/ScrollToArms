using System;

namespace ScrollToArms
{
    /// <summary>
    /// How the hotbar marks a pick, with no game in it: which slot carries the frame, the pulse of a
    /// pick held back, the red flash of a lost one and how faint the name above the bar is. The
    /// game side sets the bar's own markers from these; see <see cref="HotbarView"/>.
    /// </summary>
    internal static class PickLook
    {
        // How long a lost pick's red frame takes to fade out.
        private const float FlashTime = 0.5f;

        // Twice per second, between PulseLow and full opacity, starting dim the moment the wait
        // begins, so a wait of half a second still shows one clear dip.
        private const float PulseLow = 0.15f;
        private const float PulseRate = 2f * 2f * (float)Math.PI;

        // The name above the bar is this faint, so it informs without drawing the eye.
        private const float NameFaintness = 0.55f;

        /// <summary>The slot the frame is on: the cursor while choosing, then the pick on its way; -1 for none.</summary>
        public static int Frame(bool choosing, int cursor, int pendingSlot) => choosing ? cursor : pendingSlot;

        /// <summary>The slot a lost pick flashes on, <paramref name="age"/> seconds after it was lost, unless the frame is back on it; -1 for none.</summary>
        public static int Flash(int lostSlot, float age, int frame) =>
            lostSlot >= 0 && age < FlashTime && lostSlot != frame ? lostSlot : -1;

        /// <summary>How much of a lost pick's flash is left, from 1 to 0, <paramref name="age"/> seconds after it was lost.</summary>
        public static float Fade(float age) => 1f - Clamp01(age / FlashTime);

        /// <summary>A pick held back while the game would refuse it pulses, so a wait reads as a wait rather than as a pick that went nowhere.</summary>
        public static bool Pulses(bool choosing, bool waiting) => !choosing && waiting;

        /// <summary>The frame's opacity <paramref name="sinceWait"/> seconds into a wait.</summary>
        public static float PulseAlpha(float sinceWait)
        {
            var wave = 0.5f - 0.5f * (float)Math.Cos(sinceWait * PulseRate);
            return PulseLow + (1f - PulseLow) * Clamp01(wave);
        }

        /// <summary>Whether the bar shows anything of the mod's; with nothing to show it is left as the game sets it.</summary>
        public static bool Shows(int frame, int waiting, int flash) => frame >= 0 || waiting >= 0 || flash >= 0;

        /// <summary>Whether a slot shows the game's queued marker: the pick on its way, or any item the game itself queued.</summary>
        public static bool Queued(int slot, int waitingSlot, bool itemQueued) => slot == waitingSlot || itemQueued;

        /// <summary>The slot whose item the name above the bar gives: the frame's, else the flash's; -1 for none.</summary>
        public static int NamedSlot(int frame, int flash) => frame >= 0 ? frame : flash;

        /// <summary>Whether the name is a lost pick's, which shows in red and fades with its flash.</summary>
        public static bool LostName(bool hasItem, int frame) => hasItem && frame < 0;

        /// <summary>The name's opacity: faint always, and fading with the flash for a lost pick.</summary>
        public static float NameAlpha(bool lostName, float fade) => (lostName ? fade : 1f) * NameFaintness;

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    /// <summary>Keeps ticks at least 40 milliseconds apart, so fast scrolling does not stack them into a buzz.</summary>
    internal sealed class TickGate
    {
        private const float MinTickGap = 0.04f;

        private float _last = -1f;

        /// <summary>Whether a tick may play now; one that may is counted as played.</summary>
        public bool Allow(float now)
        {
            if (now - _last < MinTickGap) return false;
            _last = now;
            return true;
        }
    }
}
