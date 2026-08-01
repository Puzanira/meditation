using System.Collections;
using AiGameStudio.ArcadeControls;
using Meditation.Game;
using Meditation.Mechanics;
using Meditation.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Meditation.Tests
{
    /// <summary>
    /// The design gate's evidence for the game, done contract §8: the obligatory set — title, level
    /// card, level 1 (intro, both teaching beats, play, peak, victory), levels 2–5 in play (level 3
    /// with the vessel overlay visible), defeat with its retry, the card of the last level, and the
    /// finale with all five vessels.
    ///
    /// Every frame is PLAYED into existence rather than posed: a still opening screen proves nothing
    /// about a game that is about two hands, and the states this gate has to judge — «мысль над
    /// деталью», «пик хаоса», «сосуд ×2 с добычей» — only exist while the loop is running. A missing
    /// or blank frame fails the suite.
    /// </summary>
    [Category("Visual")]
    public class GameScreenshotTests
    {
        /// <summary>The cabinet's letterbox is black — the game's plates are photographs, not swatches.</summary>
        private static readonly Color Letterbox = Color.black;

        [SetUp]
        public void SetUp()
        {
            StandTestHarness.IsolateTuningFile();
            TuningConfig.ResetToDefaults();
            TuningConfig.PanelVisible = false;   // shoot the pure composition
            // …and «pure» includes the panel's own «параметры» button: hiding the body left stand
            // chrome sitting in the corner of every frame the design gate judges.
            TuningPanel.ScreenshotMode = true;
        }

        [TearDown]
        public void TearDown()
        {
            StandTestHarness.ReleaseTuningFile();
            TuningPanel.ScreenshotMode = false;
            TuningConfig.ResetToDefaults();
            TuningConfig.ActiveLevelIndex = 0;
        }

        private static void Shoot(string name) => StandTestHarness.Shoot(name, Letterbox);

        // ---- 01 title, 02 level card -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator TitleAndLevelCard()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            // Let the drifting thoughts move off their spawn row before the shot.
            yield return GameTestHarness.Idle(fake, 90);
            yield return GameTestHarness.SettleScreen(fake);
            Shoot("Game01_title");

            GameTestHarness.JumpTo(flow, GamePhase.LevelCard, 0);
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.LevelCard, "карточка уровня");
            yield return GameTestHarness.SettleScreen(fake);
            Shoot("Game02_level_card");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- 03 intro, 04–05 the two teaching beats ----------------------------------------------------

        [UnityTest]
        public IEnumerator LevelOne_IntroAndBothTeachingBeats()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            GameTestHarness.JumpTo(flow, GamePhase.Level, 0);
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.Level, "уровень 1");

            var screen = (LevelScreen)flow.Screen;

            // 03 · обзор: сцена без мыслей, все детали пульсируют разом.
            yield return GameTestHarness.SettleScreen(fake);
            Assert.AreEqual(LevelStage.Intro, screen.Stage, "Кадр обзора снят не в обзоре.");
            Shoot("Game03_L1_intro");

            // 04 · обучение, бит 1: «Крути ручку!», нить к одуванчику уже протянута.
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "обзор закончился");
            yield return GameTestHarness.SettleScreen(fake);
            Assert.AreEqual(TutorialBeat.Crank, screen.Beat);
            Assert.AreEqual(GameTexts.TutorialCrank, screen.View.Hint.Label.text);
            Shoot("Game04_L1_tutorial_crank");

            // 05 · обучение, бит 2: «Тряси джойстик!» — мишка сидит поверх следующей детали.
            yield return GameTestHarness.CollectOneDetail(fake, screen, false);
            yield return GameTestHarness.Idle(fake, 4);
            Assert.AreEqual(TutorialBeat.Shake, screen.Beat);
            Assert.AreEqual(1, screen.Runtime.Field.Thoughts.Count, "На кадре должна быть ровно одна мысль.");
            Assert.AreEqual(GameTexts.TutorialShake, screen.View.Hint.Label.text);
            Shoot("Game05_L1_tutorial_shake");

            // 17 · обучение, бит 3: «Оглядись — наклони стик», круг-взгляд появляется впервые.
            yield return GameTestHarness.ShakeUntil(fake,
                () => screen.Runtime.Field.Thoughts.Count == 0, "мысль отбита");
            yield return GameTestHarness.Idle(fake, 3);
            Assert.AreEqual(TutorialBeat.Gaze, screen.Beat, "Кадр «оглядись» снят не на своём бите.");
            Assert.AreEqual(GameTexts.TutorialGaze, screen.View.Hint.Label.text);
            StandTestHarness.AssertVisible(screen.View.Gaze.rectTransform, "Круг-взгляд");
            Shoot("Game17_L1_tutorial_gaze");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- 18 срыв детали: красное кольцо и полёт обратно ---------------------------------------------

        /// <summary>
        /// The slip is a state the gate has to be able to judge and had no frame of: the ring closes
        /// RED and the detail flies back to its place over 0.6 s (SCREENS «Сбор», mock 14).
        /// </summary>
        [UnityTest]
        public IEnumerator LevelOne_SlipOfADetail()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.TutorialTimerPaused = false;
            TuningConfig.CollectSeconds = 8f;      // slow enough to be caught mid-thread
            TuningConfig.GraceMs = 0f;             // …and the slip lands the moment the hand stops

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            // Take a detail a good way along its thread, then let go of the handle.
            yield return GameTestHarness.CrankUntil(fake,
                () => screen.Runtime.Collector.Progress01 > 0.5f, "деталь на полпути");
            yield return GameTestHarness.Until(() => screen.Runtime.Slipping, "деталь сорвалась");

            Assert.Greater(screen.Runtime.DisplayProgress, 0.05f,
                "Кадр срыва снят, когда деталь уже дома — на нём нечего судить.");
            Shoot("Game18_L1_slip");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- 06 play, 07 peak, 08 victory ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator LevelOne_PlayPeakAndVictory()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.TutorialTimerPaused = false;

            // Level 1's shipped band is deliberately sparse — one weak thought every seven seconds —
            // so the frame is staged with the panel's own knobs rather than by waiting two minutes.
            TuningConfig.SetWaveInterval(0, 1.5f);
            TuningConfig.SetWaveWeak(0, 1);
            TuningConfig.SetWaveMedium(0, 1);

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            GameTestHarness.JumpTo(flow, GamePhase.Level, 0);
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.Level, "уровень 1");
            var screen = (LevelScreen)flow.Screen;
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");

            // Past the teaching beats — this frame is the ordinary loop, not the tutorial.
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.ShakeUntil(fake, () => screen.Beat == TutorialBeat.Done ||
                                                                screen.Runtime.Field.Thoughts.Count == 0,
                "первая мысль отбита");

            // 06 · игра: деталь едет по нити, мысли на экране, таймер идёт.
            // Only the crank here: shaking every frame would pop the waves as fast as they arrive, and
            // this frame is supposed to show what the player is up against.
            TuningConfig.CollectSeconds = 8f;   // slow enough to be caught mid-thread
            yield return GameTestHarness.CrankUntil(fake,
                () => screen.Runtime.Collector.Progress01 > 0.35f &&
                      screen.Runtime.Field.Thoughts.Count >= 2,
                "деталь в пути и мысли на экране");
            fake.Next = new BackendSnapshot { CrankDeltaDegrees = GameTestHarness.CrankPerFrame };
            yield return GameTestHarness.Frames(2);
            Shoot("Game06_L1_play");

            // 07 · пик хаоса: перекрытие выше СВОЕГО порога, но игра ещё идёт — края темнеют,
            // сеттинг проступает между мыслями.
            TuningConfig.PeakOverlapPercent = 32f;
            TuningConfig.ThoughtsCoverVessel = true;
            GameTestHarness.CrowdTheScreen(screen, 15);
            yield return GameTestHarness.Frames(3);

            Assert.GreaterOrEqual(screen.Runtime.Field.OverlapPercent, TuningConfig.PeakOverlapPercent,
                "Пик не достигнут.");
            Assert.Less(screen.Runtime.Field.OverlapPercent, TuningConfig.LossOverlapPercent,
                "Это уже поражение, а не пик — на кадре должна быть ещё играбельная сцена.");
            Assert.AreEqual(LevelStage.Play, screen.Stage, "Пик — состояние игры, а не исхода.");
            Shoot("Game07_L1_peak");

            // 08 · победа: мысли растворились, портфель ×2 в центре со всей добычей.
            TuningConfig.ThoughtsCoverVessel = false;
            TuningConfig.CollectSeconds = GameTestHarness.FastCollectSeconds;
            // Stop feeding the field while the last details go in: the screen is already two thirds
            // covered from the peak frame, and more waves on top of it would end in a defeat.
            TuningConfig.SetWaveInterval(0, 20f);
            int needed = screen.Level.DetailCount;
            yield return GameTestHarness.PlayUntil(fake,
                () => screen.Runtime.CollectedCount >= needed, "уровень 1 собран", 90f);

            yield return GameTestHarness.Until(() => screen.VictoryPresented, "сосуд ×2 с добычей");
            yield return GameTestHarness.Idle(fake, 2);
            Assert.AreEqual(0, screen.Runtime.Field.Thoughts.Count, "На победе мысли обязаны раствориться.");
            Shoot("Game08_L1_victory");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- 09 level 2, 10 level 3 with its overlay, 14 level 4, 15 level 5 -----------------------------

        [UnityTest]
        public IEnumerator EveryLaterLevel_InPlay([Values(1, 2, 3, 4)] int levelIndex)
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.CollectSeconds = 8f;
            TuningConfig.SetWaveInterval(levelIndex, 1.5f);

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, levelIndex);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            // Level 3's frame has to prove the fill overlay: SCREENS asks for an indicator over the
            // baked bag, and an empty one proves nothing — so a detail goes in before the shot.
            if (screen.Level.VesselIsBaked)
            {
                yield return GameTestHarness.CollectOneDetail(fake, screen);
                yield return GameTestHarness.CollectOneDetail(fake, screen);
                Assert.Greater(screen.View.VesselFillLevel.rectTransform.rect.width, 1f,
                    "Индикатор наполнения пуст — кадр ничего не доказывает.");
            }

            TuningConfig.CollectSeconds = 8f;
            yield return GameTestHarness.PlayUntil(fake,
                () => screen.Runtime.Collector.Progress01 > 0.3f &&
                      screen.Runtime.Field.Thoughts.Count >= 2,
                "деталь в пути и мысли на экране");

            fake.Next = new BackendSnapshot { CrankDeltaDegrees = GameTestHarness.CrankPerFrame };
            yield return GameTestHarness.Frames(2);

            Shoot(FrameNameOf(levelIndex));
            LogAssert.NoUnexpectedReceived();
        }

        private static string FrameNameOf(int levelIndex)
        {
            switch (levelIndex)
            {
                case 1: return "Game09_L2_play";
                case 2: return "Game10_L3_play_vessel_overlay";
                case 3: return "Game14_L4_play";
                default: return "Game15_L5_play";
            }
        }

        // ---- 19–22 победы уровней 2–5 ------------------------------------------------------------------

        /// <summary>
        /// Every level's victory, not only level 1's: the tableau is where the haul is laid out inside
        /// the vessel and where the row of filled slots stands, and each vessel packs differently —
        /// eight details into a backpack, seven into a bag cut out of its own plate, five into a bucket
        /// and five into a mug.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryLaterLevel_Victory([Values(1, 2, 3, 4)] int levelIndex)
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.CollectSeconds = GameTestHarness.FastCollectSeconds;
            // Nothing should be spawning while the level is being swept: this frame is about the haul.
            TuningConfig.SetWaveInterval(levelIndex, 30f);

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, levelIndex);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            int needed = screen.Level.DetailCount;

            yield return GameTestHarness.PlayUntil(fake,
                () => screen.Runtime.CollectedCount >= needed,
                "уровень " + (levelIndex + 1) + " собран", 90f);

            yield return GameTestHarness.Until(() => screen.VictoryPresented, "сосуд ×2 с добычей");
            yield return GameTestHarness.Idle(fake, 2);

            Assert.AreEqual(0, screen.Runtime.Field.Thoughts.Count, "На победе мысли обязаны раствориться.");
            StandTestHarness.AssertVisible(screen.View.VictorySlots, "Ряд слотов победы");

            Shoot(VictoryFrameNameOf(levelIndex));
            LogAssert.NoUnexpectedReceived();
        }

        private static string VictoryFrameNameOf(int levelIndex)
        {
            switch (levelIndex)
            {
                case 1: return "Game19_L2_victory";
                case 2: return "Game20_L3_victory";
                case 3: return "Game21_L4_victory";
                default: return "Game22_L5_victory";
            }
        }

        // ---- 23 мысли на полосе переднего плана (L5), 24 пик хаоса не на первом уровне ------------------

        /// <summary>
        /// SCREENS «Уровень 5»: the slippers and the mug lie in the foreground strip and the thought
        /// layer covers it too. The gate asked to SEE that, not only to have it asserted.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelFive_ThoughtsOnTheForegroundStrip()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            // …and with the toggle ON, because the two things the gate asks to SEE covered are the
            // slippers and the MUG, and the mug is this level's vessel: with the toggle off the vessel
            // layer draws above the thoughts and a blob dropped on the mug simply disappears behind it.
            TuningConfig.ThoughtsCoverVessel = true;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 4);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            // Put blobs ON the strip through the real field — on the slippers, on the mug, and on the
            // open floor between them, so the frame shows the layer covering the strip as such.
            ThoughtField field = screen.Runtime.Field;
            ArtDetail slippers = screen.Level.Details[screen.Level.DetailCount - 1];
            field.SpawnAt(ThoughtStrength.Medium, screen.Level.ThoughtSprites[0], slippers.Home);
            field.SpawnAt(ThoughtStrength.Strong, screen.Level.ThoughtSprites[1], screen.Level.VesselCentre);
            field.SpawnAt(ThoughtStrength.Medium, screen.Level.ThoughtSprites[2], new Vector2(1560f, 960f));
            yield return GameTestHarness.Idle(fake, 3);

            Assert.GreaterOrEqual(field.Thoughts.Count, 3, "Мыслей на полосе не оказалось.");
            Assert.IsTrue(field.IsCovered(slippers.Home), "Тапки на кадре не накрыты.");
            Assert.IsTrue(field.IsCovered(screen.Level.VesselCentre), "Кружка на кадре не накрыта.");
            Shoot("Game23_L5_thoughts_on_strip");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>The peak on a plate that is not level 1's — the state has to exist everywhere.</summary>
        [UnityTest]
        public IEnumerator LevelThree_ChaosPeak()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 2);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            TuningConfig.PeakOverlapPercent = 32f;
            TuningConfig.ThoughtsCoverVessel = true;

            GameTestHarness.CrowdTheScreen(screen, 15);
            yield return GameTestHarness.Idle(fake, 3);

            Assert.GreaterOrEqual(screen.Runtime.Field.OverlapPercent, TuningConfig.PeakOverlapPercent,
                "Пик не достигнут.");
            Assert.Less(screen.Runtime.Field.OverlapPercent, TuningConfig.LossOverlapPercent,
                "Это уже поражение, а не пик.");
            Assert.AreEqual(LevelStage.Play, screen.Stage, "Пик — состояние игры, а не исхода.");
            Assert.Greater(screen.View.PeakEdges.color.a, 0.3f, "Края кадра в пике не темнеют.");

            Shoot("Game24_L3_peak");
            LogAssert.NoUnexpectedReceived();
        }

        // ---- 16 the card of the last level ----------------------------------------------------------------

        /// <summary>
        /// The card of level 5 as its own frame: the silhouette on it is the mug — the vessel the run
        /// ends with — and the card is the only screen where a level's promise is shown before it starts.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelFive_Card()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            GameTestHarness.JumpTo(flow, GamePhase.LevelCard, LevelCatalog.Count - 1);
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.LevelCard, "карточка уровня 5");
            yield return GameTestHarness.SettleScreen(fake);
            Shoot("Game16_L5_level_card");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- 11 defeat, 12 the retry under way ------------------------------------------------------------

        [UnityTest]
        public IEnumerator DefeatAndItsRetry()
        {
            TuningConfig.AutoRetry = false;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");

            GameTestHarness.BuryTheScreen(screen);
            yield return GameTestHarness.Idle(fake, 4);
            Assert.AreEqual(LevelStage.Lose, screen.Stage);

            // 11 · поражение: экран закрыт мыслями, цвета гаснут, обе строки на подложке.
            Assert.AreEqual(GameTexts.DefeatBig, screen.View.BigMessage.text);
            Assert.AreEqual(GameTexts.DefeatSmall, screen.View.SmallMessage.text);
            Shoot("Game11_defeat");

            // 12 · ретрай: несколько оборотов стёрли часть мыслей — сеттинг проступает обратно.
            int buried = screen.DefeatThoughtsLeft;
            yield return GameTestHarness.CrankDegrees(fake, 360f * 4f);
            yield return GameTestHarness.Frames(2);

            Assert.Less(screen.DefeatThoughtsLeft, buried, "Обороты ничего не стёрли.");
            Assert.Greater(screen.DefeatThoughtsLeft, 0, "Стёрли всё — на кадре не видно, что идёт ретрай.");
            Shoot("Game12_defeat_retry");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- 13 the finale --------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Finale()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            GameTestHarness.JumpTo(flow, GamePhase.Finale, LevelCatalog.Count - 1);
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.Finale, "финал");
            yield return GameTestHarness.SettleScreen(fake);
            Shoot("Game13_finale");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
