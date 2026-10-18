using System;

namespace ScrollToArms
{
    /// <summary>
    /// The walk along the hotbar, with no game in it: which slot the cursor stops on next. The
    /// picker says which slots hold something to stop on; see <see cref="Picker"/>.
    /// </summary>
    internal static class HotbarWalk
    {
        /// <summary>
        /// The next slot to stop on from <paramref name="from"/> in the direction of
        /// <paramref name="step"/> along a bar <paramref name="width"/> slots wide, or -1 when
        /// there is none. From -1 the search starts at the edge the step moves away from. Past an
        /// end it continues at the other with <paramref name="wrap"/>, and otherwise stays where it
        /// was.
        /// </summary>
        public static int NextStop(int width, int from, int step, bool wrap, Func<int, bool> isStop)
        {
            if (width <= 0) return -1;

            var slot = from;
            if (slot < 0) slot = step > 0 ? -1 : width;

            for (var i = 0; i < width; i++)
            {
                slot += step;
                if (slot < 0 || slot >= width)
                {
                    if (!wrap) return from >= 0 ? from : -1;
                    slot = (slot + width) % width;
                }

                if (isStop(slot)) return slot;
            }

            return from >= 0 && isStop(from) ? from : -1;
        }
    }

    /// <summary>
    /// Turns the wheel's readings into steps along the hotbar, one slot a notch.
    /// </summary>
    internal sealed class Notches
    {
        // The same step vanilla uses to rotate a building piece one notch at a time
        // (Player.m_scrollAmountThreshold), so one notch moves the cursor by one slot.
        private const float Threshold = 0.1f;

        // A partial notch is dropped on a still frame once the last whole notch is older than this.
        private const float Memory = 0.3f;

        private float _wheel;

        /// <summary>When the wheel last moved the cursor.</summary>
        public float LastNotch { get; private set; }

        /// <summary>Drops a notch that has only partly come in.</summary>
        public void Clear() => _wheel = 0f;

        /// <summary>
        /// The step this frame's reading makes: 1 to the right, -1 to the left, 0 for none. Wheel
        /// up reads positive and moves left along the bar unless <paramref name="invert"/>.
        /// </summary>
        public int Read(float delta, float now, bool invert)
        {
            if (delta == 0f)
            {
                if (now - LastNotch > Memory) _wheel = 0f;
                return 0;
            }

            _wheel += delta;
            if (Math.Abs(_wheel) < Threshold) return 0;

            var step = _wheel > 0f ? -1 : 1;
            if (invert) step = -step;
            _wheel = 0f;
            LastNotch = now;
            return step;
        }
    }
}
