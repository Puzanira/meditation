using System.Collections.Generic;

namespace Meditation.Tuning
{
    /// <summary>
    /// The [tune]/[toggle] rows each scenette puts on screen. MECHANICS.md §7 fixes which parameters
    /// belong to which scenette; ranges here are the spec's ranges, not invented ones.
    /// </summary>
    public static class TuningCatalog
    {
        public static IList<TuningParam> CrankCollect()
        {
            return new List<TuningParam>
            {
                FloatParam.Make("порог кручения", 10f, 1200f,
                    () => TuningConfig.CrankThresholdDegPerSec, v => TuningConfig.CrankThresholdDegPerSec = v,
                    "°/с", "0"),
                FloatParam.Make("grace-период", 0f, 800f,
                    () => TuningConfig.GraceMs, v => TuningConfig.GraceMs = v, "мс", "0"),
                FloatParam.Make("время сбора детали", 3f, 15f,
                    () => TuningConfig.CollectSeconds, v => TuningConfig.CollectSeconds = v, "с", "0.0"),
                ChoiceParam.Make("при остановке", new[] { "A: в ноль", "B: тает" },
                    () => (int)TuningConfig.StopMode, v => TuningConfig.StopMode = (CrankStopMode)v),
                FloatParam.Make("скорость таяния (B)", 0.1f, 2f,
                    () => TuningConfig.DecayPerSecond, v => TuningConfig.DecayPerSecond = v, "доли/с", "0.00"),
                ChoiceParam.Make("скорость кручения влияет", new[] { "A: нет", "B: да" },
                    () => (int)TuningConfig.SpeedMode, v => TuningConfig.SpeedMode = (CrankSpeedMode)v),
                FloatParam.Make("эталон скорости (B)", 60f, 1200f,
                    () => TuningConfig.CrankReferenceDegPerSec, v => TuningConfig.CrankReferenceDegPerSec = v,
                    "°/с", "0")
            };
        }

        public static IList<TuningParam> ShakeAway()
        {
            return new List<TuningParam>
            {
                FloatParam.Make("порог удара: амплитуда", 0.1f, 1f,
                    () => TuningConfig.ShakeAmplitude, v => TuningConfig.ShakeAmplitude = v, "", "0.00"),
                FloatParam.Make("порог удара: резкость", 0.5f, 20f,
                    () => TuningConfig.ShakeGestureSpeed, v => TuningConfig.ShakeGestureSpeed = v, "ед/с", "0.0"),
                FloatParam.Make("прочность: слабые", 2f, 12f,
                    () => TuningConfig.DurabilityWeak, v => TuningConfig.DurabilityWeak = (int)v, "уд.", "0", true),
                FloatParam.Make("прочность: средние", 2f, 12f,
                    () => TuningConfig.DurabilityMedium, v => TuningConfig.DurabilityMedium = (int)v, "уд.", "0", true),
                FloatParam.Make("прочность: крепкие", 2f, 12f,
                    () => TuningConfig.DurabilityStrong, v => TuningConfig.DurabilityStrong = (int)v, "уд.", "0", true),
                BoolParam.Make("затухание счётчика ударов",
                    () => TuningConfig.HitDecayEnabled, v => TuningConfig.HitDecayEnabled = v),
                FloatParam.Make("пауза затухания", 400f, 1500f,
                    () => TuningConfig.HitDecayMs, v => TuningConfig.HitDecayMs = v, "мс", "0"),
                ChoiceParam.Make("таргетинг ударов", new[] { "A: все", "B: ближняя", "C: по стику" },
                    () => (int)TuningConfig.Targeting, v => TuningConfig.Targeting = (ShakeTargeting)v),
                FloatParam.Make("интервал волн", 1f, 20f,
                    () => TuningConfig.WaveIntervalSeconds, v => TuningConfig.WaveIntervalSeconds = v, "с", "0.0"),
                WaveWeak(), WaveMedium(), WaveStrong()
            };
        }

        // --- MECHANICS §4 "состав волны: сколько и каких мыслей" -------------------------------

        private static FloatParam WaveWeak() => FloatParam.Make("в волне: слабых", 0f, 5f,
            () => TuningConfig.WaveWeak, v => TuningConfig.WaveWeak = (int)v, "", "0", true);

        private static FloatParam WaveMedium() => FloatParam.Make("в волне: средних", 0f, 5f,
            () => TuningConfig.WaveMedium, v => TuningConfig.WaveMedium = (int)v, "", "0", true);

        private static FloatParam WaveStrong() => FloatParam.Make("в волне: крепких", 0f, 5f,
            () => TuningConfig.WaveStrong, v => TuningConfig.WaveStrong = (int)v, "", "0", true);

        private static BoolParam PressureRamp() => BoolParam.Make("рост давления в уровне",
            () => TuningConfig.PressureRamp, v => TuningConfig.PressureRamp = v);

        private static FloatParam PressureRampPercent() => FloatParam.Make("сокращение интервала за волну",
            0f, 50f, () => TuningConfig.PressureRampPercent, v => TuningConfig.PressureRampPercent = v, "%", "0");

        private static BoolParam ThoughtsCoverVessel() => BoolParam.Make("мысли закрывают сосуд и деталь",
            () => TuningConfig.ThoughtsCoverVessel, v => TuningConfig.ThoughtsCoverVessel = v);

        private static FloatParam GazeSpeed() => FloatParam.Make("скорость взгляда", 200f, 1600f,
            () => TuningConfig.GazeSpeedPxPerSec, v => TuningConfig.GazeSpeedPxPerSec = v, "px/с", "0");

        private static FloatParam GazeDwell() => FloatParam.Make("удержание взгляда", 0.1f, 1.5f,
            () => TuningConfig.GazeDwellSeconds, v => TuningConfig.GazeDwellSeconds = v, "с", "0.00");

        public static IList<TuningParam> TwoHands()
        {
            return new List<TuningParam>
            {
                ChoiceParam.Make("выбор детали", new[] { "A: порядок", "B: взгляд", "C: авто" },
                    () => (int)TuningConfig.Notice, v => TuningConfig.Notice = (NoticeMode)v),
                GazeSpeed(), GazeDwell(),
                FloatParam.Make("интервал волн", 1f, 20f,
                    () => TuningConfig.WaveIntervalSeconds, v => TuningConfig.WaveIntervalSeconds = v, "с", "0.0"),
                WaveWeak(), WaveMedium(), WaveStrong(),
                PressureRamp(), PressureRampPercent(),
                BoolParam.Make("дрейф мыслей к центру",
                    () => TuningConfig.ThoughtDrift, v => TuningConfig.ThoughtDrift = v),
                FloatParam.Make("скорость дрейфа", 20f, 60f,
                    () => TuningConfig.DriftPxPerSec, v => TuningConfig.DriftPxPerSec = v, "px/с", "0"),
                ThoughtsCoverVessel(),
                FloatParam.Make("порог поражения (перекрытие)", 85f, 100f,
                    () => TuningConfig.LossOverlapPercent, v => TuningConfig.LossOverlapPercent = v, "%", "0"),
                FloatParam.Make("порог кручения", 10f, 1200f,
                    () => TuningConfig.CrankThresholdDegPerSec, v => TuningConfig.CrankThresholdDegPerSec = v,
                    "°/с", "0"),
                FloatParam.Make("время сбора детали", 3f, 15f,
                    () => TuningConfig.CollectSeconds, v => TuningConfig.CollectSeconds = v, "с", "0.0")
            };
        }

        public static IList<TuningParam> FullLevel()
        {
            return new List<TuningParam>
            {
                FloatParam.Make("длительность уровня", 60f, 180f,
                    () => TuningConfig.LevelSeconds, v => TuningConfig.LevelSeconds = v, "с", "0"),
                BoolParam.Make("передышка после детали",
                    () => TuningConfig.BreatherEnabled, v => TuningConfig.BreatherEnabled = v),
                FloatParam.Make("длина передышки", 0f, 5f,
                    () => TuningConfig.BreatherSeconds, v => TuningConfig.BreatherSeconds = v, "с", "0.0"),
                BoolParam.Make("авто-ретрай после поражения",
                    () => TuningConfig.AutoRetry, v => TuningConfig.AutoRetry = v),
                FloatParam.Make("интервал волн", 1f, 20f,
                    () => TuningConfig.WaveIntervalSeconds, v => TuningConfig.WaveIntervalSeconds = v, "с", "0.0"),
                WaveWeak(), WaveMedium(), WaveStrong(),
                PressureRamp(), PressureRampPercent(),
                BoolParam.Make("дрейф мыслей к центру",
                    () => TuningConfig.ThoughtDrift, v => TuningConfig.ThoughtDrift = v),
                FloatParam.Make("скорость дрейфа", 20f, 60f,
                    () => TuningConfig.DriftPxPerSec, v => TuningConfig.DriftPxPerSec = v, "px/с", "0"),
                ThoughtsCoverVessel(),
                FloatParam.Make("порог поражения (перекрытие)", 85f, 100f,
                    () => TuningConfig.LossOverlapPercent, v => TuningConfig.LossOverlapPercent = v, "%", "0"),
                ChoiceParam.Make("выбор детали", new[] { "A: порядок", "B: взгляд", "C: авто" },
                    () => (int)TuningConfig.Notice, v => TuningConfig.Notice = (NoticeMode)v),
                GazeSpeed(), GazeDwell()
            };
        }
    }
}
