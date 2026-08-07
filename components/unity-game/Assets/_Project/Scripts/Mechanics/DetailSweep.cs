using Meditation.Tuning;
using UnityEngine;

namespace Meditation.Mechanics
{
    /// <summary>
    /// The light that walks the scene now and then and lights up the details — SCREENS «Детали в
    /// сцене», заказ founder 2026-08-07.
    ///
    /// This class is the CLOCK and the position of the band, and nothing else: it knows when a pass is
    /// due, how long it lasts and where the band's centre is at this instant, in design px. What the
    /// band does when it gets there is the view's business (<c>LevelView</c> puts it through the
    /// details' own alpha, so the light falls on the objects and never on the plate).
    ///
    /// Splitting it that way is what makes the timing testable at all: «раз в восемь секунд, полторы
    /// секунды, реже на поздних уровнях» is arithmetic, and arithmetic does not need a rendered frame
    /// to be checked. Everything it reads comes off <see cref="TuningConfig"/>, so the founder's panel
    /// changes it in the same frame she moves the slider.
    /// </summary>
    public sealed class DetailSweep
    {
        /// <summary>
        /// How much longer the gap gets with each level when «реже на поздних уровнях» is on: the
        /// spec's own «период ×1.5 к каждому уровню». Level 1 keeps the period as set.
        /// </summary>
        public const float LateLevelFactor = 1.5f;

        /// <summary>
        /// The band starts and ends fully off the frame, so a detail at x 0 and a detail at x 1920 both
        /// get a whole pass rather than half of one.
        /// </summary>
        private const float FrameWidth = 1920f;

        private float _seconds;

        /// <summary>0-based level the sweep is running on — only «реже на поздних уровнях» reads it.</summary>
        public int LevelIndex { get; set; }

        /// <summary>True while a pass is crossing the frame right now.</summary>
        public bool Active { get; private set; }

        /// <summary>Centre of the band in design px; meaningless while <see cref="Active"/> is false.</summary>
        public float CentreX { get; private set; }

        /// <summary>
        /// The strength this pass has at this instant: the tuned strength, faded in and out over the
        /// pass so the light arrives and leaves instead of blinking. Zero when no pass is running.
        /// </summary>
        public float Strength { get; private set; }

        /// <summary>Seconds between the START of one pass and the start of the next, on this level.</summary>
        public float PeriodOfThisLevel
        {
            get
            {
                float period = Mathf.Max(0.1f, TuningConfig.SweepPeriodSeconds);
                if (!TuningConfig.SweepRarerOnLateLevels) return period;
                return period * Mathf.Pow(LateLevelFactor, Mathf.Max(0, LevelIndex));
            }
        }

        public void Reset()
        {
            _seconds = 0f;
            Active = false;
            Strength = 0f;
            CentreX = -TuningConfig.SweepWidthPx;
        }

        /// <summary>
        /// Advance the clock. <paramref name="suppressed"/> is the chaos peak: SCREENS says the light
        /// goes out there, because a band of light and the darkened edges of the peak are two pictures
        /// arguing about the same frame. Suppressed means the pass in flight ends, not that the clock
        /// stops — the light comes back on its own schedule once the screen is dug out.
        /// </summary>
        public void Tick(float deltaTime, bool suppressed)
        {
            float period = PeriodOfThisLevel;
            float duration = Mathf.Clamp(TuningConfig.SweepDurationSeconds, 0.05f, period);

            _seconds += deltaTime;
            while (_seconds >= period) _seconds -= period;

            if (suppressed || _seconds > duration)
            {
                Active = false;
                Strength = 0f;
                return;
            }

            Active = true;

            float travel = _seconds / duration;               // 0 → 1 across the pass
            float half = Mathf.Max(1f, TuningConfig.SweepWidthPx) * 0.5f;
            CentreX = Mathf.Lerp(-half, FrameWidth + half, travel);

            // sin over the pass: in at the left edge, full in the middle, out at the right one.
            Strength = Mathf.Max(0f, TuningConfig.SweepStrength) *
                       Mathf.Sin(Mathf.Clamp01(travel) * Mathf.PI);
        }
    }
}
