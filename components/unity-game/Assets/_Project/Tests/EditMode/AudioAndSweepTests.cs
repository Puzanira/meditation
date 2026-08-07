using Meditation.Mechanics;
using Meditation.Tuning;
using Meditation.View;
using NUnit.Framework;
using UnityEngine;

namespace Meditation.Tests
{
    /// <summary>
    /// The two things the drop of 2026-08-07 ordered that are pure arithmetic: the three-layer sound
    /// mix (MECHANICS §8) and the clock of the light band (SCREENS «Детали в сцене»).
    ///
    /// Both are tested here rather than in PlayMode on purpose. Batch mode has no audio device, so a
    /// test that asserts «the meditation track is playing» would be asserting against silence either
    /// way; and «луч проходит раз в восемь секунд, полторы секунды, реже на поздних уровнях» is a
    /// statement about time, which a rendered frame can only sample. What PlayMode still owns is the
    /// wiring — that the clips are there, that «в меню» really stops them, that the band reaches the
    /// details' own material.
    ///
    /// Every number below is read out of <see cref="TuningConfig"/> or its <c>Defaults</c>, never
    /// spelled into an assertion: the founder tunes all of these at the gate.
    /// </summary>
    public class AudioAndSweepTests
    {
        /// <summary>One frame at 60 Hz — the step everything here is advanced by.</summary>
        private const float Frame = 1f / 60f;

        [SetUp]
        public void SetUp() => TuningConfig.ResetToDefaults();

        [TearDown]
        public void TearDown() => TuningConfig.ResetToDefaults();

        // ---- §8, слой 1: фон уровня ------------------------------------------------------------

        [Test]
        public void TheLevelBackground_RisesToItsCeiling_AndCarriesTheLevelsOwnTrack()
        {
            var mix = new AudioMix();
            var level = new AudioScene(true, 2, false, false, 0, 0f);

            Run(mix, level, 2f);

            Assert.AreEqual(TuningConfig.AudioBackgroundVolume, mix.Background, 1e-3f,
                "Фон уровня не дошёл до своего потолка.");
            Assert.AreEqual(2, mix.BackgroundLevelIndex, "Микс просит трек не того уровня.");
        }

        [Test]
        public void OffTheLevel_ItIsSilent_ExceptOnTheTitle()
        {
            var mix = new AudioMix();

            // A card, a victory screen, the finale: «старт — тишина» (§8, тогглер включён).
            Run(mix, AudioScene.Quiet(0), 3f);
            Assert.AreEqual(0f, mix.Background, 1e-3f, "На экране вне уровня фон обязан молчать.");

            // The title is the exception the spec names by hand.
            var title = new AudioScene(false, 0, true, false, 0, 0f);
            Run(mix, title, 3f);
            Assert.Greater(mix.Background, 0.1f, "На титуле фон обязан играть.");
        }

        [Test]
        public void TheOffLevelToggle_LetsTheBackgroundCarryThroughTheCards()
        {
            TuningConfig.AudioSilentOffLevel = false;

            var mix = new AudioMix();
            Run(mix, AudioScene.Quiet(1), 3f);

            Assert.AreEqual(TuningConfig.AudioBackgroundVolume, mix.Background, 1e-3f,
                "С выключенным тогглером фон обязан играть и вне уровня.");
        }

        // ---- §8, слой 2: «медитация» -------------------------------------------------------------

        [Test]
        public void Meditation_ReplacesTheBackgroundWhileTheCollectionGoesWell()
        {
            var mix = new AudioMix();
            var collecting = new AudioScene(true, 0, false, true, 0, 0f);

            Run(mix, collecting, TuningConfig.AudioMeditationFadeInSeconds + 0.2f);

            Assert.AreEqual(TuningConfig.AudioBackgroundVolume, mix.Meditation, 1e-3f,
                "«Медитация» не вышла на громкость фона, который заменяет.");
            Assert.AreEqual(0f, mix.Background, 1e-3f,
                "Тогглер «заменяет фон» включён — фон обязан уйти в ноль.");
        }

        [Test]
        public void TheCrossfadeTakesTheTunedTime_InBothDirections()
        {
            var mix = new AudioMix();
            var collecting = new AudioScene(true, 0, false, true, 0, 0f);
            var idle = new AudioScene(true, 0, false, false, 0, 0f);

            // Half-way through the fade-in it must be roughly half-way up — a crossfade that arrives
            // in one frame is not a crossfade.
            Run(mix, collecting, TuningConfig.AudioMeditationFadeInSeconds * 0.5f);
            Assert.That(mix.MeditationBlend, Is.InRange(0.35f, 0.65f),
                "Кроссфейд в медитацию идёт не за отведённое время.");

            Run(mix, collecting, TuningConfig.AudioMeditationFadeInSeconds);
            Assert.AreEqual(1f, mix.MeditationBlend, 1e-3f);

            // A slip: the layer leaves at its own (longer) fade-out.
            mix.NoteCollectionBroken();
            Run(mix, idle, TuningConfig.AudioMeditationFadeOutSeconds * 0.5f);
            Assert.That(mix.MeditationBlend, Is.InRange(0.35f, 0.65f),
                "Кроссфейд обратно в фон идёт не за отведённое время.");

            Run(mix, idle, TuningConfig.AudioMeditationFadeOutSeconds);
            Assert.AreEqual(0f, mix.MeditationBlend, 1e-3f, "«Медитация» так и не ушла после срыва.");
            Assert.Greater(mix.Background, 0.1f, "Фон не вернулся после срыва.");
        }

        [Test]
        public void ALandedDetail_LetsMeditationPlayOnForItsTail_AndANewCollectionKeepsIt()
        {
            var mix = new AudioMix();
            var collecting = new AudioScene(true, 0, false, true, 0, 0f);
            var idle = new AudioScene(true, 0, false, false, 0, 0f);

            Run(mix, collecting, TuningConfig.AudioMeditationFadeInSeconds + 0.1f);
            mix.NoteDetailLanded();

            // Well inside the tail: still meditating even though nothing is being collected.
            Run(mix, idle, TuningConfig.AudioMeditationTailSeconds * 0.5f);
            Assert.AreEqual(1f, mix.MeditationBlend, 1e-3f,
                "Медитация обязана доигрывать хвост после попадания детали в сосуд.");

            // A new collection started inside the tail: the layer simply carries on — «если за это
            // время начался новый сбор — не выключается, слой продолжается». Measured as the LOWEST
            // point it reaches, because «не выключается» is a statement about the seam, not about
            // where it happens to be when the loop stops.
            float lowest = 1f;
            for (int i = 0; i < 12; i++)
            {
                mix.Tick(Frame, collecting);
                lowest = Mathf.Min(lowest, mix.MeditationBlend);
            }

            Assert.AreEqual(1f, lowest, 1e-3f,
                "Новый сбор внутри хвоста обязан продолжить слой без провала.");

            // …and once the collection really stops and the tail runs out, it goes back to the fon.
            mix.NoteDetailLanded();
            Run(mix, idle,
                TuningConfig.AudioMeditationTailSeconds + TuningConfig.AudioMeditationFadeOutSeconds + 0.2f);
            Assert.AreEqual(0f, mix.MeditationBlend, 1e-3f, "Хвост кончился, а медитация осталась.");
        }

        [Test]
        public void TheOverlayToggle_DucksTheBackgroundInsteadOfKillingIt()
        {
            TuningConfig.AudioMeditationReplacesBackground = false;

            var mix = new AudioMix();
            Run(mix, new AudioScene(true, 0, false, true, 0, 0f),
                TuningConfig.AudioMeditationFadeInSeconds + 0.2f);

            float expected = TuningConfig.AudioBackgroundVolume * AudioMix.DuckedBackground;
            Assert.AreEqual(expected, mix.Background, 1e-3f,
                "Вариант «поверх приглушённого фона» обязан оставить фон слышимым.");
            Assert.Greater(mix.Background, 0f);
            Assert.Less(mix.Background, TuningConfig.AudioBackgroundVolume);
        }

        // ---- §8, слой 3: «мысли» -----------------------------------------------------------------

        [Test]
        public void TheThoughtLayer_GrowsWithTheNumberOfThoughts_UpToItsCeiling()
        {
            var mix = new AudioMix();
            float settle = TuningConfig.AudioThoughtsSmoothingSeconds + 0.2f;

            Run(mix, new AudioScene(true, 0, false, false, 0, 0f), settle);
            Assert.AreEqual(0f, mix.Thoughts, 1e-3f, "Ноль мыслей — ноль громкости (§8).");

            int half = Mathf.RoundToInt(TuningConfig.AudioThoughtsAtCount * 0.5f);
            Run(mix, new AudioScene(true, 0, false, false, half, 0f), settle);
            float atHalf = mix.Thoughts;
            Assert.That(atHalf, Is.InRange(TuningConfig.AudioThoughtsMaxVolume * 0.35f,
                    TuningConfig.AudioThoughtsMaxVolume * 0.65f),
                "Слой мыслей растёт не линейно с их числом.");

            int many = Mathf.CeilToInt(TuningConfig.AudioThoughtsAtCount) + 5;
            Run(mix, new AudioScene(true, 0, false, false, many, 0f), settle);
            Assert.AreEqual(TuningConfig.AudioThoughtsMaxVolume, mix.Thoughts, 1e-3f,
                "Слой мыслей не дошёл до потолка / перерос его.");
        }

        [Test]
        public void TheThoughtLayer_IsSmoothed_SoAPoppedThoughtDoesNotJolt()
        {
            var mix = new AudioMix();
            int many = Mathf.CeilToInt(TuningConfig.AudioThoughtsAtCount) + 5;

            Run(mix, new AudioScene(true, 0, false, false, many, 0f),
                TuningConfig.AudioThoughtsSmoothingSeconds + 0.2f);
            float loud = mix.Thoughts;

            // One frame after the whole field pops: the volume must not have fallen off a cliff.
            mix.Tick(Frame, new AudioScene(true, 0, false, false, 0, 0f));
            Assert.Greater(mix.Thoughts, loud * 0.8f,
                "Громкость слоя мыслей дёрнулась за один кадр — сглаживание не работает.");
        }

        [Test]
        public void TheOverlapToggle_DrivesTheLayerByCoverageInstead()
        {
            TuningConfig.AudioThoughtsByOverlap = true;

            var mix = new AudioMix();
            float settle = TuningConfig.AudioThoughtsSmoothingSeconds + 0.2f;

            // Many thoughts but no coverage: with the toggle on, the count no longer decides.
            Run(mix, new AudioScene(true, 0, false, false, 20, 0f), settle);
            Assert.AreEqual(0f, mix.Thoughts, 1e-3f, "С тогглером громкость обязана идти по перекрытию.");

            Run(mix, new AudioScene(true, 0, false, false, 0, TuningConfig.LossOverlapPercent), settle);
            Assert.AreEqual(TuningConfig.AudioThoughtsMaxVolume, mix.Thoughts, 1e-3f,
                "На пороге поражения слой мыслей обязан быть на потолке.");
        }

        // ---- §8: «в меню» — мгновенная тишина ----------------------------------------------------

        [Test]
        public void SilenceNow_TakesEveryLayerDownAtOnce_AndForgetsTheTail()
        {
            var mix = new AudioMix();
            Run(mix, new AudioScene(true, 0, false, true, 10, 0f), 3f);
            Assert.Greater(mix.Meditation + mix.Thoughts, 0.1f, "Нечего заглушать — тест ни о чём.");

            mix.SilenceNow();

            Assert.AreEqual(0f, mix.Background, 1e-6f);
            Assert.AreEqual(0f, mix.Meditation, 1e-6f);
            Assert.AreEqual(0f, mix.Thoughts, 1e-6f);
            Assert.AreEqual(0f, mix.MeditationBlend, 1e-6f, "Хвост медитации пережил «в меню».");
        }

        // ---- луч-подсветка: часы прохода ---------------------------------------------------------

        [Test]
        public void TheSweep_RunsForItsDuration_ThenWaitsOutItsPeriod()
        {
            var sweep = new DetailSweep();
            sweep.Reset();

            float duration = TuningConfig.SweepDurationSeconds;
            float period = sweep.PeriodOfThisLevel;

            // Just inside the pass.
            sweep.Tick(duration * 0.5f, false);
            Assert.IsTrue(sweep.Active, "Луч обязан идти в первые секунды периода.");
            Assert.Greater(sweep.Strength, 0f, "Идущий луч обязан что-то светить.");

            // …and just past it.
            sweep.Tick(duration * 0.6f, false);
            Assert.IsFalse(sweep.Active, "Луч обязан погаснуть после своей длительности.");
            Assert.AreEqual(0f, sweep.Strength, 1e-4f);

            // Round the period and it starts again.
            sweep.Tick(period - duration * 1.1f + 0.05f, false);
            Assert.IsTrue(sweep.Active, "Луч не вернулся через период.");
        }

        [Test]
        public void TheSweep_CrossesTheFrameLeftToRight()
        {
            var sweep = new DetailSweep();
            sweep.Reset();

            sweep.Tick(0.01f, false);
            float early = sweep.CentreX;

            sweep.Tick(TuningConfig.SweepDurationSeconds * 0.97f, false);
            float late = sweep.CentreX;

            Assert.Less(early, 0f, "Луч обязан входить в кадр из-за левого края.");
            Assert.Greater(late, DesignStage.DesignWidth,
                "Луч обязан уходить за правый край, иначе крайние детали он не задевает.");
            Assert.Greater(late, early, "Луч идёт не слева направо.");
        }

        [Test]
        public void TheSweep_GoesOutAtTheChaosPeak()
        {
            var sweep = new DetailSweep();
            sweep.Reset();

            sweep.Tick(TuningConfig.SweepDurationSeconds * 0.5f, true);
            Assert.IsFalse(sweep.Active, "На пике хаоса луч обязан гаснуть (SCREENS).");
            Assert.AreEqual(0f, sweep.Strength, 1e-4f);
        }

        [Test]
        public void TheSweep_IsRarerOnLaterLevels_WhenTheToggleSaysSo()
        {
            var sweep = new DetailSweep { LevelIndex = 0 };
            float first = sweep.PeriodOfThisLevel;

            sweep.LevelIndex = 4;
            float last = sweep.PeriodOfThisLevel;

            Assert.AreEqual(first * Mathf.Pow(DetailSweep.LateLevelFactor, 4), last, 1e-2f,
                "Тогглер «реже на поздних уровнях» не растягивает период по уровням.");

            TuningConfig.SweepRarerOnLateLevels = false;
            Assert.AreEqual(first, sweep.PeriodOfThisLevel, 1e-3f,
                "С выключенным тогглером период обязан быть одинаковым на всех уровнях.");
        }

        // ---- plumbing ------------------------------------------------------------------------------

        /// <summary>Advance the mix by whole frames for <paramref name="seconds"/> of the same scene.</summary>
        private static void Run(AudioMix mix, in AudioScene scene, float seconds)
        {
            int frames = Mathf.Max(1, Mathf.CeilToInt(seconds / Frame));
            for (int i = 0; i < frames; i++) mix.Tick(Frame, scene);
        }
    }
}
