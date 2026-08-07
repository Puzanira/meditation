using System.Collections;
using AiGameStudio.ArcadeControls;
using Meditation.Game;
using Meditation.Mechanics;
using Meditation.Tuning;
using Meditation.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Meditation.Tests
{
    /// <summary>
    /// The design gate's evidence for the game, done contract §8. The set, `Game01…Game32`:
    ///
    /// <list type="bullet">
    /// <item>01 титул · 02 карточка уровня 1 · 30–32 карточки уровней 2–4 · 16 карточка уровня 5;</item>
    /// <item>03 обзор · 17 бит «НАВОДИ» · 04 бит «КРУТИ РУЧКУ» + «ТАЩИ» · 05 бит отгона ·
    ///       27 мысль лопается на последнем пипсе (середина разрыва) ·
    ///       29 кадр сразу после отгона (таймер пошёл);</item>
    /// <item>06 игра · 18 срыв детали · 07 пик хаоса (L1) · 24 пик хаоса (L3) —
    ///       оба набраны ЖИВЫМИ волнами, не разложены сеткой;</item>
    /// <item>09 · 10 (L3, виден индикатор сосуда) · 14 · 15 — уровни 2–5 в игре;
    ///       23 мысли на полосе переднего плана (L2);</item>
    /// <item>08 бит награды (L1) · 19–22 победы уровней 2–5 · 25 готовый экран победы;</item>
    /// <item>11 поражение · 12 ретрай под оборотами · 13 финал;</item>
    /// <item>26 луч на L5 · 28 луч на L2 — ранний уровень, полосу видно в первые минуты.</item>
    /// </list>
    ///
    /// Every frame is PLAYED into existence rather than posed: a still opening screen proves nothing
    /// about a game that is about two hands, and the states this gate has to judge — «мысль над
    /// деталью», «пик хаоса», «сосуд ×2 с добычей» — only exist while the loop is running. A missing
    /// or blank frame fails the suite. That rule is why the peak frames were re-staged in the fix round
    /// of 2026-08-07: they were laid out on a 5×3 lattice, which is a composition the game has no way
    /// of producing (<see cref="GameTestHarness.CrowdTheScreenByPlaying"/>).
    /// </summary>
    [Category("Visual")]
    public class GameScreenshotTests
    {
        /// <summary>The cabinet's letterbox is black — the game's plates are photographs, not swatches.</summary>
        private static readonly Color Letterbox = Color.black;

        /// <summary>
        /// Frame 27 is caught in the middle of a 200 ms burst, which a batch run draws about three
        /// frames of. The clock is slowed for the length of that one shot so the loop can stop on a
        /// scale instead of hoping a frame lands in the window.
        /// </summary>
        private const float BurstShotTimeScale = 0.1f;

        /// <summary>…and the scale it stops at: the middle of «масштаб 1.1 → 0».</summary>
        private const float BurstShotScale = 0.6f;

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
            Time.timeScale = 1f;   // frame 27 slows it down; nothing after it may inherit that
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

            // 17 · обучение, бит 1: кнопка «НАВОДИ» у первой детали, круг-взгляд в кадре.
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "обзор закончился");
            yield return GameTestHarness.SettleScreen(fake);
            Assert.AreEqual(TutorialBeat.Aim, screen.Beat, "Кадр «наводи» снят не на своём бите.");
            Assert.AreEqual(ArtLibrary.Get(ArtScreens.ButtonAim), screen.View.Hint.Button.sprite);
            StandTestHarness.AssertVisible(screen.View.Gaze.rectTransform, "Круг-взгляд");
            Shoot("Game17_L1_tutorial_aim");

            // 04 · обучение, бит 2: «КРУТИ РУЧКУ» у индикатора динамо и «ТАЩИ» у едущей детали.
            yield return GameTestHarness.NoticeSomething(fake, screen);
            yield return GameTestHarness.CrankUntil(fake,
                () => screen.Runtime.Collector.Progress01 > 0.35f, "деталь в пути");
            fake.Next = new BackendSnapshot { CrankDeltaDegrees = GameTestHarness.CrankPerFrame };
            yield return GameTestHarness.Frames(2);
            Assert.AreEqual(TutorialBeat.Crank, screen.Beat);
            Assert.AreEqual(ArtLibrary.Get(ArtScreens.ButtonCrank), screen.View.Hint.Button.sprite);
            Assert.IsTrue(screen.View.SecondHint.IsShown, "На кадре нет второй кнопки «ТАЩИ».");
            Shoot("Game04_L1_tutorial_crank");

            // 05 · обучение, бит 3: мысль-кот сидит поверх следующей детали, стрелка на джойстик —
            // кнопки «ТРЯСИ» в дропе нет, и кадр должен показывать именно это.
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.Idle(fake, 4);
            Assert.AreEqual(TutorialBeat.Shake, screen.Beat);
            Assert.AreEqual(1, screen.Runtime.Field.Thoughts.Count, "На кадре должна быть ровно одна мысль.");
            Assert.IsFalse(screen.View.Hint.Button.gameObject.activeSelf,
                "На бите отгона кнопки быть не должно — её нет в дропе.");
            Shoot("Game05_L1_tutorial_shake");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- 27 последний пипс, 29 кадр после отгона -----------------------------------------------------

        /// <summary>
        /// Two frames the gate asked for and had no evidence of, and they are the two halves of one beat.
        ///
        /// 27 · the last pip being SPENT — «мысль лопается (масштаб 1.1 → 0, 200 мс)», caught halfway
        /// down. The first version of this frame was the state one hit EARLIER, because the burst did
        /// not exist yet and a frame of it would have been a frame of nothing; now that it does, the
        /// shot is of the thing SCREENS describes. 29 · the same beat one moment later: «мысль отбита →
        /// таймер запускается, дальше обычный play» (SCREENS §Обучение п.4) — the arrow is gone, the sun
        /// is bright, and nothing of the tutorial is left on the screen.
        /// </summary>
        [UnityTest]
        public IEnumerator TheShakeBeat_LastPipAndTheFrameAfterIt()
        {
            // How many pips there are is level 1's own band (ApplyLevel copies it in on entry, so it is
            // not this test's to choose) — what the shot needs is only that the counter does not
            // quietly reset itself while it is being set up.
            TuningConfig.HitDecayEnabled = false;
            TuningConfig.Targeting = ShakeTargeting.AllOnScreen;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.NoticeSomething(fake, screen);
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.Idle(fake, 4);

            Assert.AreEqual(TutorialBeat.Shake, screen.Beat, "Бит отгона не начался.");
            Assert.AreEqual(1, screen.Runtime.Field.Thoughts.Count, "На кадре должна быть ровно одна мысль.");
            Thought thought = screen.Runtime.Field.Thoughts[0];

            // First to the last pip.
            //
            // The hits are landed through the field's own ApplyHits — the very call the shaking hand
            // makes — rather than by shaking until the counter looks right: a hand cannot be stopped
            // on a given pip. Letting go of the stick is itself a sharp reversal, so «трясти, пока не
            // останется один» overshot to zero and the frame had no thought on it at all.
            screen.Runtime.Field.ApplyHits(thought.Durability - 1, Vector2.zero);
            yield return GameTestHarness.Idle(fake, 2);

            Assert.AreEqual(1, thought.HitsRemaining, "Не встали на последний пипс.");
            Assert.AreEqual(1, screen.Runtime.Field.Thoughts.Count, "Мысль уже лопнула — судить нечего.");

            // 27 · и последний удар, снятый на середине разрыва.
            //
            // 200 ms is three frames of a batch run, which is not a window a screenshot can be aimed
            // at — so the clock is slowed for the length of the shot and the loop waits for the SCALE
            // rather than for a number of frames. The capture itself renders the current state without
            // advancing anything (StandTestHarness.Capture), so what is asserted here is what is in
            // the file.
            Time.timeScale = BurstShotTimeScale;
            try
            {
                screen.Runtime.Field.ApplyHits(1, Vector2.zero);
                LevelView view = screen.View;

                float deadline = Time.realtimeSinceStartup + 30f;
                while ((view.Pops.Count == 0 || view.Pops[0].Scale > BurstShotScale) &&
                       Time.realtimeSinceStartup < deadline)
                {
                    fake.Next = new BackendSnapshot();
                    yield return null;
                }

                Assert.AreEqual(1, view.Pops.Count, "Разрыв кончился раньше, чем кадр был снят.");
                Assert.That(view.PopViews[view.Pops[0].Slot].Scale,
                    Is.InRange(0.45f, BurstShotScale + 0.02f),
                    "Кадр 27 снят не на середине разрыва.");
                StandTestHarness.AssertVisible(view.PopViews[view.Pops[0].Slot].Rect,
                    "Лопающаяся мысль на кадре 27");

                Shoot("Game27_L1_thought_last_pip");
            }
            finally
            {
                Time.timeScale = 1f;
            }

            // 29 · и кадр сразу после отгона: подсказки нет, таймер пошёл.
            yield return GameTestHarness.ShakeUntil(fake, () => screen.Beat == TutorialBeat.Done,
                "мысль отбита, обучение кончилось");
            yield return GameTestHarness.Idle(fake, 3);

            Assert.IsFalse(screen.View.Hint.IsShown, "После отгона подсказки на кадре быть не должно.");
            Assert.IsFalse(screen.View.SecondHint.IsShown, "Вторая кнопка тоже обязана уйти.");
            Assert.IsTrue(screen.TimerRunning, "После отгона таймер обязан идти — иначе кадр не про это.");
            Shoot("Game29_L1_after_the_shake_beat");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- 28 луч на РАННЕМ уровне --------------------------------------------------------------------

        /// <summary>
        /// The light band on an EARLY level: the gate asked to see the sweep where a player meets it in
        /// the first minutes, not only on the last level of the run
        /// (<see cref="TheCity_LightSweepAcrossItsDetails"/>). Shipped strength, like frame 26 — the
        /// question at the gate is whether 0.45 is enough on a plate darkened ×0.49.
        ///
        /// Level 2 and not level 1: level 1 is the teaching level, and its frame in the first minute has
        /// two hint plates and their arrows across it — a frame about the band would be a frame about
        /// the tutorial.
        /// </summary>
        [UnityTest]
        public IEnumerator TheOffice_LightSweepAcrossItsDetails()
        {
            // Staged exactly like frame 26, so the two are comparable: a chosen detail (and therefore
            // no gaze circle parked in the middle of the shot), the shipped strength, a short period.
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.SweepPeriodSeconds = 4f;
            TuningConfig.SweepRarerOnLateLevels = false;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            // Caught ON an object and at full strength, not merely «somewhere in the frame»: the band
            // is multiplied by each sprite's own alpha, so a pass over bare plate is a pass with
            // nothing to show, and its edges fade in and out.
            yield return GameTestHarness.Until(
                () => screen.Sweep.Active && BandIsOverAnUnnoticedDetail(screen) &&
                      screen.Sweep.Strength >= TuningConfig.SweepStrength * 0.9f,
                "луч дошёл до детали в полную силу", 30f);

            Shoot("Game28_L2_sweep");
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// Is the band on a detail the sweep is actually allowed to light? The shipped toggle «луч
        /// только по незамеченным» skips the one already on its thread, and a frame staged over THAT
        /// one shows nothing lit at all.
        /// </summary>
        private static bool BandIsOverAnUnnoticedDetail(LevelScreen screen)
        {
            float centre = screen.Sweep.CentreX;
            for (int i = 0; i < screen.Level.DetailCount; i++)
            {
                if (TuningConfig.SweepOnlyUnnoticed && i == screen.Runtime.NoticedIndex) continue;
                Rect box = LevelCatalog.RectOf(screen.Level.Details[i]);
                if (centre >= box.xMin && centre <= box.xMax) return true;
            }
            return false;
        }

        // ---- луч: измеренная прибавка яркости, а не «материал получил число» ----------------------------

        /// <summary>
        /// The gate's own question, answered in pixels: does the SHIPPED strength put light on the art
        /// that a human can see?
        ///
        /// <c>TheLightSweep_ReachesEveryDetailsOwnMaterial</c> proves the number arrives at every
        /// detail's material, which is a different claim and was never the one in doubt — a band that
        /// reaches the material and lands one brightness unit on the sprite passes it and ships an
        /// invisible feature. The design gate of 2026-08-07 read frames 26 and 28 as «луча нет» off a
        /// per-detail p95 that had been taken on details the band was NOT over, and there was no
        /// machine claim to contradict it with. This is that claim.
        ///
        /// Measured off <see cref="StandTestHarness.Capture"/> — the very path that writes the gate's
        /// frames — twice in ONE frame: band on, band off, nothing else moved between them. Level 2
        /// because frame 28 is level 2, and the band is driven onto each detail in turn rather than
        /// waited for, so the measurement is the same every run.
        /// </summary>
        [UnityTest]
        public IEnumerator TheLightSweep_VisiblyBrightensTheDetailsItIsOver()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelDefinition level = screen.Level;

            // The plate as it is with no band anywhere — the baseline every lift below is measured off.
            screen.View.ApplySweep(false, 0f, 0f, 0f, -1);
            Texture2D dark = StandTestHarness.Capture(Letterbox);

            float loudest = float.MinValue;
            string loudestName = "—";
            float worstBleed = 0f;

            try
            {
                for (int i = 0; i < level.DetailCount; i++)
                {
                    Rect box = LevelCatalog.RectOf(level.Details[i]);

                    // The band centred on this detail, at the strength the game ships.
                    screen.View.ApplySweep(true, box.center.x, TuningConfig.SweepWidthPx,
                        TuningConfig.SweepStrength, -1);
                    Texture2D lit = StandTestHarness.Capture(Letterbox);

                    try
                    {
                        float lift = P95Of(lit, box) - P95Of(dark, box);
                        if (lift > loudest)
                        {
                            loudest = lift;
                            loudestName = level.Details[i].Name;
                        }

                        // …and it must stop at the sprite's edge: a strip of BARE plate inside the
                        // band's own column may not have moved at all, or the light is landing on the
                        // picture instead of on the objects. Skipped where the strip would catch
                        // another detail — that one is lit on purpose.
                        Rect plate = new Rect(box.xMin, Mathf.Max(0f, box.yMin - box.height - 40f),
                            box.width, box.height);
                        if (!TouchesADetail(level, plate))
                            worstBleed = Mathf.Max(worstBleed,
                                Mathf.Abs(P95Of(lit, plate) - P95Of(dark, plate)));
                    }
                    finally
                    {
                        Object.DestroyImmediate(lit);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(dark);
            }

            Assert.Greater(loudest, MinSweepLift,
                "Луч штатной силы (" + TuningConfig.SweepStrength.ToString("0.00") +
                ") не даёт заметной прибавки ни на одной детали уровня 2: лучшая — «" + loudestName +
                "», +" + loudest.ToString("0.0") + " ед. p95 при пороге " + MinSweepLift + ".");

            Assert.Less(worstBleed, MaxSweepBleed,
                "Луч светит по плите, а не по спрайтам: пустой участок фона изменился на " +
                worstBleed.ToString("0.0") + " ед.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The lift the founder's own «заметно» was measured against: the drop's frames put +46 on the
        /// office sticky note and +46 on the city's curtains, so a floor of 20 fails long before a
        /// human would stop seeing it — and it fails immediately if the band ever stops reaching the
        /// sprites at all, which is the failure this test exists for.
        /// </summary>
        private const float MinSweepLift = 20f;

        /// <summary>…and how much the bare plate beside a detail is allowed to move: nothing real.</summary>
        private const float MaxSweepBleed = 3f;

        private static bool TouchesADetail(LevelDefinition level, Rect strip)
        {
            for (int i = 0; i < level.DetailCount; i++)
                if (strip.Overlaps(LevelCatalog.RectOf(level.Details[i]))) return true;
            return false;
        }

        /// <summary>95th percentile of luminance, 0…255, over a design-space rectangle of a frame.</summary>
        private static float P95Of(Texture2D frame, Rect design)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(design.xMin), 0, frame.width);
            int y = Mathf.Clamp(Mathf.RoundToInt(design.yMin), 0, frame.height);
            var box = new RectInt(x, y,
                Mathf.Clamp(Mathf.RoundToInt(design.width), 0, frame.width - x),
                Mathf.Clamp(Mathf.RoundToInt(design.height), 0, frame.height - y));

            Color[] pixels = StandTestHarness.PixelsOf(frame, box);
            if (pixels.Length == 0) return 0f;

            var luminance = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                luminance[i] = (pixels[i].r + pixels[i].g + pixels[i].b) * (255f / 3f);

            System.Array.Sort(luminance);
            return luminance[Mathf.Clamp(Mathf.FloorToInt(luminance.Length * 0.95f), 0, luminance.Length - 1)];
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
            yield return GameTestHarness.NoticeSomething(fake, screen);

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
            yield return GameTestHarness.ShakeUntil(fake, () => screen.Beat == TutorialBeat.Done,
                "обучение пройдено");

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
            // сеттинг проступает между мыслями. Кадр набирается ЖИВЫМИ волнами: прежняя постановка
            // раскладывала мысли сеткой 5×3, а такого кадра игра не выдаёт (дизайн-гейт 2026-08-07).
            TuningConfig.PeakOverlapPercent = 32f;
            TuningConfig.ThoughtsCoverVessel = true;
            yield return GameTestHarness.CrowdTheScreenByPlaying(fake, screen,
                TuningConfig.PeakOverlapPercent);

            Assert.Less(screen.Runtime.Field.OverlapPercent, TuningConfig.LossOverlapPercent,
                "Это уже поражение, а не пик — на кадре должна быть ещё играбельная сцена.");
            Assert.AreEqual(LevelStage.Play, screen.Stage, "Пик — состояние игры, а не исхода.");
            Shoot("Game07_L1_peak");

            // 08 · победа: мысли растворились, портфель ×2 в центре со всей добычей.
            TuningConfig.ThoughtsCoverVessel = false;
            TuningConfig.CollectSeconds = GameTestHarness.FastCollectSeconds;
            // Stop feeding the field while the last details go in: the screen is already two thirds
            // covered from the peak frame, and more waves on top of it would end in a defeat. The LIVE
            // value, not the level's band — the level is open, and its band was copied in on entry.
            TuningConfig.WaveIntervalSeconds = 20f;
            TuningConfig.WaveWeak = 1;
            TuningConfig.WaveMedium = 0;
            TuningConfig.WaveStrong = 0;
            int needed = screen.Level.DetailCount;
            yield return GameTestHarness.PlayUntil(fake,
                () => screen.Runtime.CollectedCount >= needed, "уровень 1 собран", 90f);

            yield return GameTestHarness.Until(() => screen.VictoryPresented, "сосуд с добычей");
            yield return GameTestHarness.Idle(fake, 2);
            Assert.AreEqual(0, screen.Runtime.Field.Thoughts.Count, "На победе мысли обязаны раствориться.");
            Shoot("Game08_L1_victory");

            // 25 · и готовый экран победы, который приходит поверх бита награды.
            yield return GameTestHarness.Until(() => screen.CompleteScreenShown, "экран «Отлично!»");
            yield return GameTestHarness.Frames(2);
            Shoot("Game25_L1_level_complete");

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
            Assert.IsFalse(screen.CompleteScreenShown,
                "Кадр снят уже под готовым экраном — на нём не видно добычи.");

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
        /// SCREENS «Уровень 2»: the slippers and the mug lie in the foreground strip and the thought
        /// layer covers it too. The gate asked to SEE that, not only to have it asserted.
        /// </summary>
        [UnityTest]
        public IEnumerator TheOffice_ThoughtsOnTheForegroundStrip()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            // …and with the toggle ON, because the two things the gate asks to SEE covered are the
            // slippers and the MUG, and the mug is this level's vessel: with the toggle off the vessel
            // layer draws above the thoughts and a blob dropped on the mug simply disappears behind it.
            TuningConfig.ThoughtsCoverVessel = true;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

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
            Shoot("Game23_L2_thoughts_on_strip");

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

            yield return GameTestHarness.CrowdTheScreenByPlaying(fake, screen,
                TuningConfig.PeakOverlapPercent);

            Assert.Less(screen.Runtime.Field.OverlapPercent, TuningConfig.LossOverlapPercent,
                "Это уже поражение, а не пик.");
            Assert.AreEqual(LevelStage.Play, screen.Stage, "Пик — состояние игры, а не исхода.");
            Assert.Greater(screen.View.PeakEdges.color.a, 0.3f, "Края кадра в пике не темнеют.");

            Shoot("Game24_L3_peak");
            LogAssert.NoUnexpectedReceived();
        }

        // ---- 16 the card of the last level ----------------------------------------------------------------

        /// <summary>
        /// The card of the last level as its own frame: the drop draws one per level, and the gate has
        /// to see that the flow really picks the right render rather than always the first.
        /// </summary>
        [UnityTest]
        public IEnumerator LastLevel_Card()
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

        // ---- 30–32 карточки уровней 2, 3 и 4 --------------------------------------------------------------

        /// <summary>
        /// …and the three cards in between. The gate had the first and the last and was asked to take
        /// the middle on faith — five renders is five renders, and this costs a jump and a settle each.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryMiddleLevel_Card([Values(1, 2, 3)] int levelIndex)
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            GameTestHarness.JumpTo(flow, GamePhase.LevelCard, levelIndex);
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.LevelCard,
                "карточка уровня " + (levelIndex + 1));
            yield return GameTestHarness.SettleScreen(fake);

            Shoot("Game" + (30 + levelIndex - 1) + "_L" + (levelIndex + 1) + "_level_card");
            LogAssert.NoUnexpectedReceived();
        }

        // ---- 26 луч-подсветка деталей (заказ founder 2026-08-07) -------------------------------------

        /// <summary>
        /// The light band, caught mid-frame on the fullest scene of the run.
        ///
        /// Shot at the SHIPPED strength rather than at the ceiling: the whole question the founder has
        /// to answer at the gate is whether 0.45 is enough to make a detail «вспыхнуть на секунду» on a
        /// plate darkened by ×0.49, and a frame staged at 1.0 would answer a question nobody asked.
        /// The period is shortened so the shot does not sit through eight seconds of waiting — that is
        /// the clock, not the look.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCity_LightSweepAcrossItsDetails()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.SweepPeriodSeconds = 4f;
            TuningConfig.SweepRarerOnLateLevels = false;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 4);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            // Half-way across, where the band is at full strength and covers the middle of the frame.
            yield return GameTestHarness.Until(
                () => screen.Sweep.Active && screen.Sweep.CentreX > 700f && screen.Sweep.CentreX < 1200f,
                "луч в середине кадра", 30f);

            Shoot("Game26_L5_sweep");
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

            // 11 · поражение: готовый экран дропа во весь кадр.
            Assert.AreEqual(1f, screen.DefeatCoverLeft01, 1e-3f, "Экран поражения закрывает кадр не целиком.");
            Shoot("Game11_defeat");

            // 12 · ретрай: несколько оборотов стёрли часть экрана — уровень проступает обратно.
            yield return GameTestHarness.CrankDegrees(fake, 360f * 4f);
            yield return GameTestHarness.Frames(2);

            Assert.Less(screen.DefeatCoverLeft01, 1f, "Обороты ничего не стёрли.");
            Assert.Greater(screen.DefeatCoverLeft01, 0f, "Стёрли всё — на кадре не видно, что идёт ретрай.");
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
