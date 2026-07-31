using Meditation.Tuning;
using UnityEngine;

namespace Meditation.Mechanics
{
    /// <summary>
    /// MECHANICS §1 — the slow line. Continuous cranking drags the noticed detail into the vessel;
    /// stall longer than the grace period and the progress is lost (reset or melt, [toggle]).
    /// Pure logic, no Unity objects: the view reads <see cref="Progress01"/> and the flags.
    /// </summary>
    public sealed class CrankCollector
    {
        /// <summary>0..1 along the path from the detail's home to the vessel.</summary>
        public float Progress01 { get; private set; }

        /// <summary>Was the crank above the threshold on the last tick?</summary>
        public bool IsSpinning { get; private set; }

        /// <summary>Seconds spent below the crank threshold (resets on every spinning tick).</summary>
        public float StalledSeconds { get; private set; }

        /// <summary>True while the stall is still inside the grace window (ring goes red, nothing lost yet).</summary>
        public bool InGrace => !IsSpinning && Progress01 > 0f && StalledSeconds * 1000f <= TuningConfig.GraceMs;

        /// <summary>True on the tick where the grace window expired and progress started being lost.</summary>
        public bool SlippedThisTick { get; private set; }

        /// <summary>True on the tick the detail reached the vessel.</summary>
        public bool CompletedThisTick { get; private set; }

        public bool IsComplete => Progress01 >= 1f;

        public void Reset()
        {
            Progress01 = 0f;
            IsSpinning = false;
            StalledSeconds = 0f;
            SlippedThisTick = false;
            CompletedThisTick = false;
        }

        /// <param name="crankSpeedDegPerSec">Smoothed crank speed (see <see cref="CrankSpeedMeter"/>).</param>
        /// <param name="deltaTime">Frame time, seconds.</param>
        public void Tick(float crankSpeedDegPerSec, float deltaTime)
        {
            SlippedThisTick = false;
            CompletedThisTick = false;
            if (deltaTime <= 0f) return;
            if (IsComplete) return;

            IsSpinning = crankSpeedDegPerSec >= TuningConfig.CrankThresholdDegPerSec;

            if (IsSpinning)
            {
                StalledSeconds = 0f;
                float perSecond = 1f / Mathf.Max(0.01f, TuningConfig.CollectSeconds);
                if (TuningConfig.SpeedMode == CrankSpeedMode.SpeedScaled)
                {
                    float reference = Mathf.Max(1f, TuningConfig.CrankReferenceDegPerSec);
                    perSecond *= Mathf.Clamp(crankSpeedDegPerSec / reference, 0.2f, 2f);
                }

                Progress01 = Mathf.Clamp01(Progress01 + perSecond * deltaTime);
                if (Progress01 >= 1f) CompletedThisTick = true;
                return;
            }

            StalledSeconds += deltaTime;
            if (StalledSeconds * 1000f <= TuningConfig.GraceMs) return;   // still forgiven
            if (Progress01 <= 0f) return;

            if (TuningConfig.StopMode == CrankStopMode.ResetToZero)
            {
                Progress01 = 0f;
                SlippedThisTick = true;
            }
            else
            {
                Progress01 = Mathf.Max(0f, Progress01 - TuningConfig.DecayPerSecond * deltaTime);
                SlippedThisTick = true;
            }
        }
    }
}
