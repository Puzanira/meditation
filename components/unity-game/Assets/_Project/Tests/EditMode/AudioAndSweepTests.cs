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

            // …and it grows along the CURVE, not along a straight line, since the founder's playtest
            // of 2026-09-22 («кривая мыслей менее агрессивная»): at half the dial the layer is at the
            // curve's own value, which at the shipped exponent of 2 is a quarter of the ceiling and
            // not a half. The expectation is written through AudioMix.ThoughtsCurve rather than as a
            // number, so moving the knob on the panel is a tuning decision and not a red test.
            int half = Mathf.RoundToInt(TuningConfig.AudioThoughtsAtCount * 0.5f);
            Run(mix, new AudioScene(true, 0, false, false, half, 0f), settle);
            float atHalf = mix.Thoughts;
            float wanted = AudioMix.ThoughtsCurve(half / TuningConfig.AudioThoughtsAtCount) *
                           TuningConfig.AudioThoughtsMaxVolume;
            Assert.AreEqual(wanted, atHalf, 0.02f, "Слой мыслей растёт не по своей кривой.");

            Assert.Greater(atHalf, 0f, "На середине шкалы слой мыслей обязан быть слышен.");
            Assert.Less(atHalf, TuningConfig.AudioThoughtsMaxVolume * 0.5f,
                "Кривая слоя мыслей не смягчена — на середине шкалы она всё ещё на половине потолка " +
                "(заказ founder 2026-09-22).");

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

        // ---- §8: смена дорожки уровня — кроссфейд, который СЛЫШНО --------------------------------

        /// <summary>
        /// «Фоновый должен немного затухать, а новый должен нарастать» (founder, 2026-09-22, п.10) —
        /// and the only place that sentence can come true is a frame where BOTH tracks are audible.
        ///
        /// The bug Codex found: the next level's index reaches the mix on its CARD, the card is
        /// silence under the shipped «тишина вне уровня», and the blend spent its 1.2 s there. By the
        /// first frame of the level it was finished, the outgoing track was already released, and
        /// what the founder heard was one track fading in from nothing — the very thing the crossfade
        /// was ordered to replace. The PlayMode test that checks the CLIPS cannot see this: the clips
        /// are right either way, it is the volumes over time that are wrong.
        ///
        /// So: level 1 · card of level 2 (silence, longer than the crossfade) · level 2.
        /// </summary>
        [Test]
        public void TheTrackSwap_IsHeardOnTheLevel_NotSpentInTheSilenceOfTheCard()
        {
            var mix = new AudioMix();
            var levelOne = new AudioScene(true, 0, false, false, 0, 0f);
            AudioScene card = AudioScene.Quiet(1);          // карточка уровня 2 — уже просит трек У2
            var levelTwo = new AudioScene(true, 1, false, false, 0, 0f);

            float ceiling = TuningConfig.AudioBackgroundVolume;
            float crossfade = TuningConfig.AudioBackgroundCrossfadeSeconds;
            Assert.Greater(TuningConfig.LevelCardSeconds, crossfade,
                "Карточка короче кроссфейда — тест не поймал бы «блент доехал в тишине».");

            Run(mix, levelOne, 2f);
            Assert.AreEqual(0, mix.BackgroundLevelIndex, "На уровне 1 играет не его дорожка.");
            Assert.AreEqual(ceiling, mix.BackgroundIn, 1e-3f, "Дорожка уровня 1 не вышла на потолок.");

            // The card: the whole bus goes down, and the swap must WAIT there.
            Run(mix, card, TuningConfig.LevelCardSeconds);
            Assert.AreEqual(0f, mix.Background, 1e-3f, "На карточке фон обязан молчать (§8).");
            Assert.AreEqual(0, mix.BackgroundLevelIndex,
                "Карточка увела фон на дорожку следующего уровня, пока никто не слышит.");
            Assert.IsFalse(mix.SwappingTracks, "Смена дорожек началась и кончилась в тишине карточки.");

            // Level 2: the swap starts HERE, on the first frame the ear is in the room.
            mix.Tick(Frame, levelTwo);
            Assert.IsTrue(mix.SwappingTracks, "На входе в уровень 2 смены дорожек не началось.");
            Assert.AreEqual(1, mix.BackgroundLevelIndex, "Пришедшая дорожка — не уровня 2.");
            Assert.AreEqual(0, mix.OutgoingLevelIndex, "Уходит дорожка не уровня 1.");
            Assert.Less(mix.TrackBlend, 0.1f,
                "Блент дорожек доехал до входа в уровень — кроссфейда никто не услышит.");

            // …and now the two tracks really exchange places, frame by frame: the old one falls, the
            // new one rises, and for most of the crossfade both are sounding.
            float previousOut = 0f;
            float previousIn = -1f;
            bool busWasAtItsCeiling = false;
            int framesBothAudible = 0;
            int frames = Mathf.CeilToInt(crossfade / Frame);
            for (int i = 0; i < frames; i++)
            {
                mix.Tick(Frame, levelTwo);

                if (mix.BackgroundOut > 0.01f && mix.BackgroundIn > 0.01f) framesBothAudible++;

                Assert.GreaterOrEqual(mix.BackgroundIn, previousIn - 1e-4f,
                    "Новая дорожка не нарастает — кадр " + i + ".");

                // The bus itself is still fading in over the first fraction of a second, and while it
                // is, the outgoing track rides up with it. It is required to FALL from the frame the
                // bus stands at its ceiling — from there on nothing but the blend moves it.
                if (busWasAtItsCeiling)
                    Assert.Less(mix.BackgroundOut, previousOut + 1e-4f,
                        "Старая дорожка не затухает — кадр " + i + ".");

                busWasAtItsCeiling = mix.Background >= ceiling - 1e-4f;
                previousOut = mix.BackgroundOut;
                previousIn = mix.BackgroundIn;
            }

            Assert.Greater(framesBothAudible, frames / 2,
                "Две дорожки звучат вместе меньше половины кроссфейда — это не «одна затухает, " +
                "другая нарастает», а подмена.");

            // Half-way through: the old track is still a real part of the sound.
            var half = new AudioMix();
            Run(half, levelOne, 2f);
            Run(half, card, TuningConfig.LevelCardSeconds);
            Run(half, levelTwo, crossfade * 0.5f);
            Assert.That(half.TrackBlend, Is.InRange(0.3f, 0.7f),
                "Кроссфейд дорожек идёт не за отведённое время.");
            Assert.Greater(half.BackgroundOut, ceiling * 0.2f,
                "На середине кроссфейда старая дорожка уже неслышна.");
            Assert.Greater(half.BackgroundIn, 0f, "На середине кроссфейда новой дорожки ещё нет.");

            // …and it ends: one track, at the ceiling, with the second source released.
            Run(mix, levelTwo, crossfade);
            Assert.AreEqual(1f, mix.TrackBlend, 1e-3f, "Кроссфейд дорожек так и не закончился.");
            Assert.IsFalse(mix.SwappingTracks, "Ушедшая дорожка осталась висеть на втором источнике.");
            Assert.AreEqual(0f, mix.BackgroundOut, 1e-6f);
            Assert.AreEqual(ceiling, mix.BackgroundIn, 1e-3f);
        }

        /// <summary>
        /// The other side of the same rule: with «тишина вне уровня» OFF the background carries
        /// through the card, so the card is where the change of music is heard — and that is where
        /// the crossfade must run. The rule is «the fade runs where the ear is», not «only on levels».
        /// </summary>
        [Test]
        public void WithTheBackgroundCarryingThroughTheCards_TheSwapHappensOnTheCard()
        {
            TuningConfig.AudioSilentOffLevel = false;

            var mix = new AudioMix();
            Run(mix, new AudioScene(true, 0, false, false, 0, 0f), 2f);

            mix.Tick(Frame, AudioScene.Quiet(1));
            Assert.IsTrue(mix.SwappingTracks,
                "Фон слышен на карточке, а дорожки не начали меняться — кроссфейд ждёт впустую.");
            Assert.Greater(mix.BackgroundOut, 0.1f, "Старая дорожка обязана быть слышна на карточке.");
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
            // The toggle SHIPS OFF since the founder's playtest of 2026-09-22 («блики по всем объектам
            // на всех уровнях»), so the shipped state is the second half of this test and the first
            // half has to switch it on by hand. Both halves are still worth having: the toggle is on
            // the panel, and the founder may well want the thinning-out back once she can find the
            // objects at all.
            var sweep = new DetailSweep { LevelIndex = 0 };

            Assert.IsFalse(TuningConfig.SweepRarerOnLateLevels,
                "Тогглер «реже на поздних уровнях» обязан приезжать ВЫКЛЮЧЕННЫМ (founder 2026-09-22).");
            float first = sweep.PeriodOfThisLevel;
            sweep.LevelIndex = 4;
            Assert.AreEqual(first, sweep.PeriodOfThisLevel, 1e-3f,
                "С выключенным тогглером период обязан быть одинаковым на всех уровнях.");

            TuningConfig.SweepRarerOnLateLevels = true;
            sweep.LevelIndex = 0;
            first = sweep.PeriodOfThisLevel;
            sweep.LevelIndex = 4;
            Assert.AreEqual(first * Mathf.Pow(DetailSweep.LateLevelFactor, 4), sweep.PeriodOfThisLevel,
                1e-2f, "Тогглер «реже на поздних уровнях» не растягивает период по уровням.");
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
