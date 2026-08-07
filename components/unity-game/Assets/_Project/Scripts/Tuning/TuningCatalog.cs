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

        // ---- the game itself (levels 1–5 with art) ------------------------------------------------

        /// <summary>
        /// The whole game's panel: one [tune per level] section per level, then the values that are
        /// the same wherever you are (feel of the two hands, thresholds, the tutorial).
        ///
        /// All five sections are on the panel at once rather than behind a tab for the level being
        /// played: the founder tunes level 5's pressure while level 1 is still under her hands, and a
        /// panel that reshuffles itself at every level card is a panel she has to re-find her place in
        /// five times a run. The list scrolls (TuningPanel owns a viewport), so length is cheap and
        /// «параметры отдельно для каждого уровня» stays literally true — «У2 · …» is level 2's number
        /// and nothing else's.
        /// </summary>
        public static IList<TuningParam> Game()
        {
            var rows = new List<TuningParam>();
            for (int level = 0; level < TuningConfig.LevelBands; level++) rows.AddRange(GameLevel(level));
            rows.AddRange(GameShared());
            return rows;
        }

        /// <summary>Section «У{n} · …» — MECHANICS §6, the [tune per level] progression knobs.</summary>
        public static IList<TuningParam> GameLevel(int index)
        {
            int i = index;
            string p = "У" + (i + 1) + " · ";

            return new List<TuningParam>
            {
                FloatParam.Make(p + "длительность уровня", 60f, 180f,
                    () => TuningConfig.LevelSecondsOf(i), v => TuningConfig.SetLevelSeconds(i, v), "с", "0"),
                FloatParam.Make(p + "интервал волн", 1f, 20f,
                    () => TuningConfig.WaveIntervalOf(i), v => TuningConfig.SetWaveInterval(i, v), "с", "0.0"),
                FloatParam.Make(p + "в волне: слабых", 0f, 5f,
                    () => TuningConfig.WaveWeakOf(i), v => TuningConfig.SetWaveWeak(i, (int)v), "", "0", true),
                FloatParam.Make(p + "в волне: средних", 0f, 5f,
                    () => TuningConfig.WaveMediumOf(i), v => TuningConfig.SetWaveMedium(i, (int)v), "", "0", true),
                FloatParam.Make(p + "в волне: крепких", 0f, 5f,
                    () => TuningConfig.WaveStrongOf(i), v => TuningConfig.SetWaveStrong(i, (int)v), "", "0", true),
                FloatParam.Make(p + "прочность: слабые", 2f, 12f,
                    () => TuningConfig.DurabilityWeakOf(i), v => TuningConfig.SetDurabilityWeak(i, (int)v), "уд.", "0", true),
                FloatParam.Make(p + "прочность: средние", 2f, 12f,
                    () => TuningConfig.DurabilityMediumOf(i), v => TuningConfig.SetDurabilityMedium(i, (int)v), "уд.", "0", true),
                FloatParam.Make(p + "прочность: крепкие", 2f, 12f,
                    () => TuningConfig.DurabilityStrongOf(i), v => TuningConfig.SetDurabilityStrong(i, (int)v), "уд.", "0", true),
                FloatParam.Make(p + "скорость дрейфа", 20f, 60f,
                    () => TuningConfig.DriftOf(i), v => TuningConfig.SetDrift(i, v), "px/с", "0"),
                FloatParam.Make(p + "сокращение интервала за волну", 0f, 50f,
                    () => TuningConfig.PressureRampPercentOf(i), v => TuningConfig.SetPressureRampPercent(i, v), "%", "0")
            };
        }

        /// <summary>Values that are the game's, not a level's: feel, thresholds, the tutorial [toggle].</summary>
        public static IList<TuningParam> GameShared()
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
                ChoiceParam.Make("выбор детали", new[] { "A: порядок", "B: взгляд", "C: авто" },
                    () => (int)TuningConfig.Notice, v => TuningConfig.Notice = (NoticeMode)v),
                GazeSpeed(), GazeDwell(),
                FloatParam.Make("порог удара: амплитуда", 0.1f, 1f,
                    () => TuningConfig.ShakeAmplitude, v => TuningConfig.ShakeAmplitude = v, "", "0.00"),
                FloatParam.Make("порог удара: резкость", 0.5f, 20f,
                    () => TuningConfig.ShakeGestureSpeed, v => TuningConfig.ShakeGestureSpeed = v, "ед/с", "0.0"),
                BoolParam.Make("затухание счётчика ударов",
                    () => TuningConfig.HitDecayEnabled, v => TuningConfig.HitDecayEnabled = v),
                FloatParam.Make("пауза затухания", 400f, 1500f,
                    () => TuningConfig.HitDecayMs, v => TuningConfig.HitDecayMs = v, "мс", "0"),
                ChoiceParam.Make("таргетинг ударов", new[] { "A: все", "B: ближняя", "C: по стику" },
                    () => (int)TuningConfig.Targeting, v => TuningConfig.Targeting = (ShakeTargeting)v),
                BoolParam.Make("дрейф мыслей к центру",
                    () => TuningConfig.ThoughtDrift, v => TuningConfig.ThoughtDrift = v),
                ThoughtsCoverVessel(),
                FloatParam.Make("порог пика хаоса", 40f, 100f,
                    () => TuningConfig.PeakOverlapPercent, v => TuningConfig.PeakOverlapPercent = v, "%", "0"),
                FloatParam.Make("порог поражения (перекрытие)", 85f, 100f,
                    () => TuningConfig.LossOverlapPercent, v => TuningConfig.LossOverlapPercent = v, "%", "0"),
                BoolParam.Make("передышка после детали",
                    () => TuningConfig.BreatherEnabled, v => TuningConfig.BreatherEnabled = v),
                FloatParam.Make("длина передышки", 0f, 5f,
                    () => TuningConfig.BreatherSeconds, v => TuningConfig.BreatherSeconds = v, "с", "0.0"),
                BoolParam.Make("авто-ретрай после поражения",
                    () => TuningConfig.AutoRetry, v => TuningConfig.AutoRetry = v),
                BoolParam.Make("таймер в обучении стоит",
                    () => TuningConfig.TutorialTimerPaused, v => TuningConfig.TutorialTimerPaused = v),

                // ---- §8 Звук: три слоя, три группы ручек --------------------------------------
                FloatParam.Make("звук: громкость фона", 0f, 1f,
                    () => TuningConfig.AudioBackgroundVolume,
                    v => TuningConfig.AudioBackgroundVolume = v, "", "0.00"),
                FloatParam.Make("звук: кроссфейд в медитацию", 0.1f, 1.5f,
                    () => TuningConfig.AudioMeditationFadeInSeconds,
                    v => TuningConfig.AudioMeditationFadeInSeconds = v, "с", "0.00"),
                FloatParam.Make("звук: кроссфейд из медитации", 0.1f, 1.5f,
                    () => TuningConfig.AudioMeditationFadeOutSeconds,
                    v => TuningConfig.AudioMeditationFadeOutSeconds = v, "с", "0.00"),
                FloatParam.Make("звук: хвост медитации", 0f, 4f,
                    () => TuningConfig.AudioMeditationTailSeconds,
                    v => TuningConfig.AudioMeditationTailSeconds = v, "с", "0.0"),
                BoolParam.Make("звук: медитация заменяет фон",
                    () => TuningConfig.AudioMeditationReplacesBackground,
                    v => TuningConfig.AudioMeditationReplacesBackground = v),
                FloatParam.Make("звук: потолок слоя мыслей", 0f, 1f,
                    () => TuningConfig.AudioThoughtsMaxVolume,
                    v => TuningConfig.AudioThoughtsMaxVolume = v, "", "0.00"),
                FloatParam.Make("звук: мыслей до максимума", 4f, 15f,
                    () => TuningConfig.AudioThoughtsAtCount,
                    v => TuningConfig.AudioThoughtsAtCount = v, "", "0", true),
                FloatParam.Make("звук: сглаживание громкости", 0.1f, 2f,
                    () => TuningConfig.AudioThoughtsSmoothingSeconds,
                    v => TuningConfig.AudioThoughtsSmoothingSeconds = v, "с", "0.00"),
                BoolParam.Make("звук: мысли по перекрытию, а не по числу",
                    () => TuningConfig.AudioThoughtsByOverlap,
                    v => TuningConfig.AudioThoughtsByOverlap = v),
                BoolParam.Make("звук: тишина вне уровня",
                    () => TuningConfig.AudioSilentOffLevel, v => TuningConfig.AudioSilentOffLevel = v),

                // ---- Луч-подсветка деталей (SCREENS «Детали в сцене») -------------------------
                FloatParam.Make("луч: период", 4f, 20f,
                    () => TuningConfig.SweepPeriodSeconds,
                    v => TuningConfig.SweepPeriodSeconds = v, "с", "0.0"),
                FloatParam.Make("луч: длительность прохода", 0.6f, 2.5f,
                    () => TuningConfig.SweepDurationSeconds,
                    v => TuningConfig.SweepDurationSeconds = v, "с", "0.0"),
                FloatParam.Make("луч: ширина полосы", 150f, 600f,
                    () => TuningConfig.SweepWidthPx, v => TuningConfig.SweepWidthPx = v, "px", "0"),
                FloatParam.Make("луч: сила подсветки", 0.1f, 1f,
                    () => TuningConfig.SweepStrength, v => TuningConfig.SweepStrength = v, "", "0.00"),
                BoolParam.Make("луч: только по незамеченным",
                    () => TuningConfig.SweepOnlyUnnoticed, v => TuningConfig.SweepOnlyUnnoticed = v),
                BoolParam.Make("луч: реже на поздних уровнях",
                    () => TuningConfig.SweepRarerOnLateLevels,
                    v => TuningConfig.SweepRarerOnLateLevels = v)
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
