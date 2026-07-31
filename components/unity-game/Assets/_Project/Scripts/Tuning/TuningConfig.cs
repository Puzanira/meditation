using UnityEngine;

namespace Meditation.Tuning
{
    /// <summary>What happens to collection progress when the crank stops (MECHANICS §1).</summary>
    public enum CrankStopMode
    {
        /// <summary>A. progress drops to zero, the detail flies home.</summary>
        ResetToZero = 0,
        /// <summary>B. progress melts away — come back to cranking fast enough and part of it is saved.</summary>
        Decay = 1
    }

    /// <summary>Does crank SPEED matter, or only continuity (MECHANICS §1)?</summary>
    public enum CrankSpeedMode
    {
        /// <summary>A. no — only continuity matters (more meditative).</summary>
        ContinuityOnly = 0,
        /// <summary>B. yes, up to a ceiling (more arcade-y).</summary>
        SpeedScaled = 1
    }

    /// <summary>Which thought(s) a shake hit lands on (MECHANICS §3).</summary>
    public enum ShakeTargeting
    {
        /// <summary>A. every thought on screen takes the hit.</summary>
        AllOnScreen = 0,
        /// <summary>B. one — the one nearest to the screen centre.</summary>
        NearestToCenter = 1,
        /// <summary>C. the one the stick is pointing at.</summary>
        StickDirection = 2
    }

    /// <summary>How the player picks the detail to collect (MECHANICS §2 / SCREENS "Выбор детали").</summary>
    public enum NoticeMode
    {
        /// <summary>A. fixed authored order, current target highlighted.</summary>
        FixedOrder = 0,
        /// <summary>B. gaze circle driven by a smooth joystick tilt (base variant).</summary>
        GazeJoystick = 1,
        /// <summary>C. auto-pick the uncovered detail nearest to the vessel.</summary>
        AutoNearest = 2
    }

    /// <summary>Thought toughness class (MECHANICS §3): weak / medium / strong.</summary>
    public enum ThoughtStrength
    {
        Weak = 0,
        Medium = 1,
        Strong = 2
    }

    /// <summary>
    /// Every [tune]/[toggle] parameter from MECHANICS.md, in one place, as mutable static state.
    ///
    /// Static on purpose: the preview stand must keep the founder's values when she jumps between
    /// scenettes (done contract §7). This lives only in the tuning stand — shipping game code must
    /// not depend on it, and the final numbers get written back into MECHANICS.md after the playtest.
    ///
    /// Shipped starting values live in <see cref="Defaults"/>. Tests compare against those constants,
    /// never against a literal, so re-tuning a number after the playtest cannot turn a balance
    /// decision into a red test.
    ///
    /// Static state alone survives a scene change but not a closed editor, so <see cref="TuningStore"/>
    /// mirrors every field of this class into <c>UserSettings/tuning.json</c> on each panel interaction
    /// and reads it back before the first scenette. Adding a field here is enough to persist it — the
    /// store finds fields by reflection.
    /// </summary>
    public static class TuningConfig
    {
        /// <summary>Shipped starting values. After a playtest they change here — and only here.</summary>
        public static class Defaults
        {
            public const float CrankThresholdDegPerSec = 120f;
            public const float GraceMs = 300f;
            public const float CollectSeconds = 6f;
            public const CrankStopMode StopMode = CrankStopMode.ResetToZero;
            public const float DecayPerSecond = 0.4f;
            public const CrankSpeedMode SpeedMode = CrankSpeedMode.ContinuityOnly;
            public const float CrankReferenceDegPerSec = 360f;

            public const NoticeMode Notice = NoticeMode.GazeJoystick;
            public const float GazeSpeedPxPerSec = 900f;
            public const float GazeDwellSeconds = 0.4f;

            public const float ShakeAmplitude = 0.5f;
            public const float ShakeGestureSpeed = 3f;

            // Pip counts of the walkthrough's own frames: гора посуды 4, клубок ? 3.
            public const int DurabilityWeak = 3;
            public const int DurabilityMedium = 4;
            public const int DurabilityStrong = 7;

            public const bool HitDecayEnabled = true;
            public const float HitDecayMs = 800f;
            public const ShakeTargeting Targeting = ShakeTargeting.NearestToCenter;

            public const float WaveIntervalSeconds = 6f;
            public const int WaveWeak = 1;
            public const int WaveMedium = 1;
            public const int WaveStrong = 0;
            public const bool PressureRamp = false;
            public const float PressureRampPercent = 10f;
            public const bool ThoughtDrift = true;
            public const float DriftPxPerSec = 40f;
            public const bool ThoughtsCoverVessel = false;
            public const float LossOverlapPercent = 92f;

            public const float LevelSeconds = 120f;
            public const bool BreatherEnabled = true;
            public const float BreatherSeconds = 2f;
            public const bool AutoRetry = true;
        }

        // ---- §1 Сбор (динамо) ----------------------------------------------------------------
        /// <summary>Minimum crank speed that still counts as "cranking", deg/s. [tune]</summary>
        public static float CrankThresholdDegPerSec = Defaults.CrankThresholdDegPerSec;
        /// <summary>How long a stall is forgiven before progress is lost, ms. [tune 0–800]</summary>
        public static float GraceMs = Defaults.GraceMs;
        /// <summary>Time to collect one detail at normal cranking, s. [tune 3–15]</summary>
        public static float CollectSeconds = Defaults.CollectSeconds;
        /// <summary>A: reset to zero / B: melt. [toggle]</summary>
        public static CrankStopMode StopMode = Defaults.StopMode;
        /// <summary>Melting rate in mode B, progress fraction per second.</summary>
        public static float DecayPerSecond = Defaults.DecayPerSecond;
        /// <summary>A: continuity only / B: speed matters up to a ceiling. [toggle]</summary>
        public static CrankSpeedMode SpeedMode = Defaults.SpeedMode;
        /// <summary>Crank speed that equals "normal" collection speed in mode B, deg/s.</summary>
        public static float CrankReferenceDegPerSec = Defaults.CrankReferenceDegPerSec;

        // ---- §2 Выбор детали -----------------------------------------------------------------
        /// <summary>A/B/C detail picking. [toggle]</summary>
        public static NoticeMode Notice = Defaults.Notice;
        /// <summary>Gaze circle speed, px/s. [tune]</summary>
        public static float GazeSpeedPxPerSec = Defaults.GazeSpeedPxPerSec;
        /// <summary>How long the gaze must rest on a detail to notice it, s. [tune]</summary>
        public static float GazeDwellSeconds = Defaults.GazeDwellSeconds;

        // ---- §3 Отгон мыслей (джойстик) ------------------------------------------------------
        /// <summary>Stick deflection that counts as a hit, 0..1. [tune]</summary>
        public static float ShakeAmplitude = Defaults.ShakeAmplitude;
        /// <summary>Gesture speed that separates a shake from a smooth gaze tilt, units/s. [tune]</summary>
        public static float ShakeGestureSpeed = Defaults.ShakeGestureSpeed;
        /// <summary>Hits needed to pop a weak thought. [tune 2–12]</summary>
        public static int DurabilityWeak = Defaults.DurabilityWeak;
        /// <summary>Hits needed to pop a medium thought. [tune 2–12]</summary>
        public static int DurabilityMedium = Defaults.DurabilityMedium;
        /// <summary>Hits needed to pop a strong thought. [tune 2–12]</summary>
        public static int DurabilityStrong = Defaults.DurabilityStrong;
        /// <summary>Does a thought's hit counter reset after a pause? [toggle]</summary>
        public static bool HitDecayEnabled = Defaults.HitDecayEnabled;
        /// <summary>Pause between hits that resets the counter, ms. [tune 400–1500]</summary>
        public static float HitDecayMs = Defaults.HitDecayMs;
        /// <summary>A/B/C hit targeting. [toggle]</summary>
        public static ShakeTargeting Targeting = Defaults.Targeting;

        // ---- §4 Мысли: волны и перекрытие ----------------------------------------------------
        /// <summary>Seconds between waves. [tune per level]</summary>
        public static float WaveIntervalSeconds = Defaults.WaveIntervalSeconds;
        /// <summary>Wave composition: weak thoughts per wave. [tune per level]</summary>
        public static int WaveWeak = Defaults.WaveWeak;
        /// <summary>Wave composition: medium thoughts per wave. [tune per level]</summary>
        public static int WaveMedium = Defaults.WaveMedium;
        /// <summary>Wave composition: strong thoughts per wave. [tune per level]</summary>
        public static int WaveStrong = Defaults.WaveStrong;
        /// <summary>Does the pressure grow inside a level — do waves come closer together? [toggle]</summary>
        public static bool PressureRamp = Defaults.PressureRamp;
        /// <summary>How much each wave shortens the next interval, %. [tune]</summary>
        public static float PressureRampPercent = Defaults.PressureRampPercent;
        /// <summary>Do thoughts drift towards the centre? [toggle]</summary>
        public static bool ThoughtDrift = Defaults.ThoughtDrift;
        /// <summary>Drift speed, px/s. [tune 20–60]</summary>
        public static float DriftPxPerSec = Defaults.DriftPxPerSec;
        /// <summary>May thoughts cover the vessel and the detail being dragged? [toggle]</summary>
        public static bool ThoughtsCoverVessel = Defaults.ThoughtsCoverVessel;
        /// <summary>Screen coverage that means defeat, %. [tune 85–100]</summary>
        public static float LossOverlapPercent = Defaults.LossOverlapPercent;

        // ---- §5 Таймер, победа, поражение ----------------------------------------------------
        /// <summary>Level duration, s. [tune 60–180]</summary>
        public static float LevelSeconds = Defaults.LevelSeconds;
        /// <summary>Breather (no spawns) after each collected detail? [toggle]</summary>
        public static bool BreatherEnabled = Defaults.BreatherEnabled;
        /// <summary>Breather length, s. [tune 0–5]</summary>
        public static float BreatherSeconds = Defaults.BreatherSeconds;
        /// <summary>Auto-restart the scenette 4 s after a defeat instead of waiting for the crank. [toggle]</summary>
        public static bool AutoRetry = Defaults.AutoRetry;

        // ---- Стенд ---------------------------------------------------------------------------
        /// <summary>Is the tuning panel expanded? Remembered between scenettes like every other value.</summary>
        public static bool PanelVisible = true;

        /// <summary>Total thoughts in one wave (composition of the three types).</summary>
        public static int ThoughtsPerWave => Mathf.Max(1, WaveWeak + WaveMedium + WaveStrong);

        /// <summary>Back to the shipped defaults (the panel's "сброс" button).</summary>
        public static void ResetToDefaults()
        {
            CrankThresholdDegPerSec = Defaults.CrankThresholdDegPerSec;
            GraceMs = Defaults.GraceMs;
            CollectSeconds = Defaults.CollectSeconds;
            StopMode = Defaults.StopMode;
            DecayPerSecond = Defaults.DecayPerSecond;
            SpeedMode = Defaults.SpeedMode;
            CrankReferenceDegPerSec = Defaults.CrankReferenceDegPerSec;

            Notice = Defaults.Notice;
            GazeSpeedPxPerSec = Defaults.GazeSpeedPxPerSec;
            GazeDwellSeconds = Defaults.GazeDwellSeconds;

            ShakeAmplitude = Defaults.ShakeAmplitude;
            ShakeGestureSpeed = Defaults.ShakeGestureSpeed;
            DurabilityWeak = Defaults.DurabilityWeak;
            DurabilityMedium = Defaults.DurabilityMedium;
            DurabilityStrong = Defaults.DurabilityStrong;
            HitDecayEnabled = Defaults.HitDecayEnabled;
            HitDecayMs = Defaults.HitDecayMs;
            Targeting = Defaults.Targeting;

            WaveIntervalSeconds = Defaults.WaveIntervalSeconds;
            WaveWeak = Defaults.WaveWeak;
            WaveMedium = Defaults.WaveMedium;
            WaveStrong = Defaults.WaveStrong;
            PressureRamp = Defaults.PressureRamp;
            PressureRampPercent = Defaults.PressureRampPercent;
            ThoughtDrift = Defaults.ThoughtDrift;
            DriftPxPerSec = Defaults.DriftPxPerSec;
            ThoughtsCoverVessel = Defaults.ThoughtsCoverVessel;
            LossOverlapPercent = Defaults.LossOverlapPercent;

            LevelSeconds = Defaults.LevelSeconds;
            BreatherEnabled = Defaults.BreatherEnabled;
            BreatherSeconds = Defaults.BreatherSeconds;
            AutoRetry = Defaults.AutoRetry;

            PanelVisible = true;
        }

        /// <summary>Hits needed to pop a thought of the given strength.</summary>
        public static int DurabilityOf(ThoughtStrength strength)
        {
            switch (strength)
            {
                case ThoughtStrength.Weak: return DurabilityWeak;
                case ThoughtStrength.Medium: return DurabilityMedium;
                default: return DurabilityStrong;
            }
        }
    }
}
