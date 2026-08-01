using System.Collections;
using AiGameStudio.ArcadeControls;
using Meditation.Game;
using Meditation.Mechanics;
using Meditation.Stand;
using Meditation.Tuning;
using Meditation.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Meditation.Tests
{
    /// <summary>
    /// The game itself, played through by the suite: titles → card → level → card → … → finale, on the
    /// real <c>Game.unity</c>, with both hands driven from a <c>FakeBackend</c>.
    ///
    /// Everything numeric here is a named test-only constant or a value read back out of
    /// <see cref="TuningConfig"/> — never a balance number spelled into an assertion, so re-tuning
    /// after the playtest cannot turn a design decision into a red suite.
    /// </summary>
    public class GameFlowTests
    {
        /// <summary>Collecting fast enough that a whole run fits in a test, not a coffee break.</summary>
        private const float TestOnlyFastCollectSeconds = 0.35f;

        /// <summary>Degrees of crank per frame while a test "turns the handle".</summary>
        private const float CrankPerFrame = 20f;

        /// <summary>Longest a single wait in this suite may block before it is a failure.</summary>
        private const float PatienceSeconds = 60f;

        [SetUp]
        public void SetUp()
        {
            StandTestHarness.IsolateTuningFile();
            TuningConfig.ResetToDefaults();
            TuningConfig.PanelVisible = false;
        }

        [TearDown]
        public void TearDown()
        {
            StandTestHarness.ReleaseTuningFile();
            GameTestHarness.ForgetLauncher();
            TuningPanel.ScreenshotMode = false;
            TuningConfig.ResetToDefaults();
            TuningConfig.ActiveLevelIndex = 0;
        }

        // ---- boot (done contract §1) ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheGameBootsOnTheTitle_WithNothingInTheConsole()
        {
            yield return GameTestHarness.LoadGame();
            GameFlow flow = GameTestHarness.Flow();

            Assert.AreEqual(GamePhase.Title, flow.Phase, "Игра обязана стартовать с титула.");
            Assert.IsInstanceOf<TitleScreen>(flow.Screen);

            DesignStage stage = StandTestHarness.Stage();
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "TitleText"), "Заголовок");
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "StartHint"), "Подсказка старта");
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "CrankCard"), "Карточка «КРУТИ»");
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "ShakeCard"), "Карточка «ТРЯСИ»");

            yield return GameTestHarness.Frames(30);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TitleTexts_AreTheOnesInTheRegistry()
        {
            yield return GameTestHarness.LoadGame();
            DesignStage stage = StandTestHarness.Stage();

            Assert.AreEqual(GameTexts.Title,
                StandTestHarness.Find(stage, "TitleText").GetComponent<UnityEngine.UI.Text>().text);
            Assert.AreEqual(GameTexts.TitleStartHint,
                StandTestHarness.Find(stage, "StartHint").GetComponent<UnityEngine.UI.Text>().text);
        }

        // ---- the two turns that start the run (done contract §2) -------------------------------------

        [UnityTest]
        public IEnumerator TwoTurnsOfTheDynamo_StartTheRun_AndOneDoesNot()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            yield return GameTestHarness.CrankDegrees(fake, TitleScreen.StartDegrees * 0.5f);
            Assert.AreEqual(GamePhase.Title, flow.Phase,
                "Одного оборота не хватает — старт обязан быть по двум.");

            yield return GameTestHarness.CrankDegrees(fake, TitleScreen.StartDegrees * 0.6f);
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.LevelCard, "карточка уровня 1");

            Assert.AreEqual(0, flow.LevelIndex, "Старт обязан вести на первый уровень.");
            LogAssert.NoUnexpectedReceived();
        }

        // ---- every level really loads with its art (done contract §3) --------------------------------

        [UnityTest]
        public IEnumerator EveryLevel_LoadsItsOwnArt_AtThePreviewsPositions(
            [Values(0, 1, 2, 3, 4)] int levelIndex)
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, levelIndex);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelDefinition level = LevelCatalog.At(levelIndex);
            DesignStage stage = StandTestHarness.Stage();

            Assert.AreEqual(level.Number, screen.Level.Number);
            Assert.IsNotNull(screen.View.Background.sprite, level.Title + ": фон не загрузился.");

            for (int i = 0; i < level.DetailCount; i++)
            {
                ArtDetail detail = level.Details[i];
                string name = "Detail_" + detail.Name;

                RectTransform rt = StandTestHarness.Find(stage, name);
                Assert.IsNotNull(rt.GetComponent<UnityEngine.UI.Image>().sprite,
                    name + ": спрайт детали не загрузился.");

                // The rect that is actually DRAWN, not activeSelf — «зелёный тест, сломанная игра».
                Rect drawn = stage.DesignRectOf(rt);
                Assert.AreEqual(detail.Home.x, drawn.center.x, 20f, name + ": X не по превью (±20).");
                Assert.AreEqual(detail.Home.y, drawn.center.y, 20f, name + ": Y не по превью (±20).");
                Assert.Greater(drawn.width, 1f, name + ": деталь нарисована нулевой ширины.");
                Assert.Greater(drawn.height, 1f, name + ": деталь нарисована нулевой высоты.");
            }

            // One HUD slot per detail of THIS level (SCREENS «HUD: слоты деталей»).
            Assert.AreEqual(level.DetailCount, screen.View.Slots.Count, "Слотов не по числу деталей.");
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "Slot1"), "Первый слот");
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "Vessel"), "Сосуд");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The baked bag's fill indicator: it reports on the bag, so it must not BE on the bag. The
        /// first version was a translucent card over 85 % of it — the design gate read it as a grey
        /// plate stuck to the passenger. Now it is a slim bar on the floor under the bag, and this test
        /// pins both halves: it fills with the haul, and it never touches the thing it reports on.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelThree_ShowsItsFillBarUnderTheBakedBag_WithoutCoveringIt()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 2);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            DesignStage stage = StandTestHarness.Stage();

            Assert.IsTrue(screen.Level.VesselIsBaked, "У третьего уровня сосуд запечён в фон.");
            Assert.IsNotNull(screen.View.VesselFillLevel, "Нет индикатора наполнения.");
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "VesselFillTrack"),
                "Индикатор наполнения");

            Rect bar = stage.DesignRectOf(StandTestHarness.Find(stage, "VesselFillTrack"));
            Rect bag = stage.DesignRectOf(StandTestHarness.Find(stage, "Vessel"));

            Assert.AreEqual(bag.center.x, bar.center.x, 20f, "Индикатор не под сумкой по X.");
            Assert.IsFalse(StandTestHarness.Overlaps(bar, bag),
                "Индикатор снова лёг НА сумку — он о ней докладывает, а не закрывает её.");
            Assert.Greater(bar.yMin, bag.yMax - 1f, "Индикатор обязан стоять ниже сумки.");
            Assert.Less(bar.yMax, DesignStage.DesignHeight, "Индикатор ушёл за нижний край кадра.");

            float empty = screen.View.VesselFillLevel.rectTransform.rect.width;
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            float filled = screen.View.VesselFillLevel.rectTransform.rect.width;

            Assert.Greater(filled, empty, "Индикатор не подрос после первой детали.");
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the level-1 tutorial (done contract §4) -------------------------------------------------

        [UnityTest]
        public IEnumerator Tutorial_HoldsTheWavesAndTheClock_UntilTheFirstThoughtIsBeatenOff()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            DesignStage stage = StandTestHarness.Stage();

            // Beat 1: «Крути ручку!». No thoughts at all, the clock is held, the dandelion is already
            // noticed so the only thing being asked for is the handle.
            Assert.AreEqual(TutorialBeat.Crank, screen.Beat);
            Assert.AreEqual(0, screen.Runtime.NoticedIndex, "Первая деталь должна быть уже замечена.");
            Assert.IsFalse(screen.TimerRunning, "Таймер в обучении обязан стоять [toggle].");
            Assert.AreEqual(GameTexts.TutorialCrank, screen.View.Hint.Label.text);
            StandTestHarness.AssertVisible(screen.View.Hint.Rect, "Карточка обучения");

            float clockAtStart = screen.Rules.TimeLeft;
            yield return GameTestHarness.Idle(fake, 30);
            Assert.AreEqual(clockAtStart, screen.Rules.TimeLeft, 1e-3f, "Таймер шёл во время обучения.");
            Assert.AreEqual(0, screen.Runtime.Field.Thoughts.Count,
                "До первой детали мысли спавниться не должны.");

            // Beat 2: the dandelion lands, one thought appears ON the next detail, still nothing else.
            // The notice variant is left alone — beat 1 already noticed the dandelion for the player,
            // which is the whole point of that beat.
            yield return GameTestHarness.CollectOneDetail(fake, screen, false);
            Assert.AreEqual(TutorialBeat.Shake, screen.Beat, "После первой детали должен идти бит «тряси».");
            Assert.AreEqual(1, screen.Runtime.Field.Thoughts.Count, "Должна быть ровно одна мысль.");
            Assert.AreEqual(GameTexts.TutorialShake, screen.View.Hint.Label.text);
            Assert.IsFalse(screen.TimerRunning, "Таймер обязан стоять и на втором бите.");

            Vector2 blob = screen.Runtime.Field.Thoughts[0].Position;
            Vector2 nextDetail = screen.Level.Details[1].Home;
            Assert.AreEqual(nextDetail.x, blob.x, 1f, "Мысль не над следующей деталью.");
            Assert.AreEqual(nextDetail.y, blob.y, 1f, "Мысль не над следующей деталью.");

            // Beat 3: shaken off — the clock starts and the last card comes up.
            yield return GameTestHarness.ShakeUntil(fake,
                () => screen.Runtime.Field.Thoughts.Count == 0, "мысль отбита");
            yield return GameTestHarness.Frames(3);

            Assert.AreEqual(TutorialBeat.Gaze, screen.Beat);
            Assert.IsTrue(screen.TimerRunning, "После отбитой мысли таймер обязан пойти.");
            Assert.AreEqual(GameTexts.TutorialGaze, screen.View.Hint.Label.text);

            yield return GameTestHarness.Idle(fake, 30);
            Assert.Less(screen.Rules.TimeLeft, clockAtStart, "Таймер так и не пошёл.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// SCREENS «Обучение», beat 2: «сбор заблокирован её появлением поверх следующей детали». Not
        /// «slowed», not «the next detail is skipped» — nothing is collected until the bear is shaken
        /// off. The block used to be a hope: the fixed order picked the covered detail anyway and the
        /// gaze looked straight through the blob, so the crank alone finished the level while the card
        /// still asked for the joystick.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTutorialsSecondBeat_BlocksCollecting_UntilTheThoughtIsShakenOff()
        {
            TuningConfig.CollectSeconds = TestOnlyFastCollectSeconds;
            TuningConfig.Notice = NoticeMode.FixedOrder;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            yield return GameTestHarness.CollectOneDetail(fake, screen, false);
            Assert.AreEqual(TutorialBeat.Shake, screen.Beat, "После первой детали должен идти бит «тряси».");
            Assert.IsTrue(screen.CollectionBlockedByTutorial, "Мысль над деталью обязана блокировать сбор.");

            int collected = screen.Runtime.CollectedCount;

            // Turn the handle for many times longer than a whole detail takes: nothing moves.
            yield return GameTestHarness.CrankDegrees(fake, 360f * 6f);
            yield return GameTestHarness.Frames(4);

            Assert.AreEqual(collected, screen.Runtime.CollectedCount,
                "До отбитой мысли кручение не имеет права давать прогресс.");
            Assert.AreEqual(0f, screen.Runtime.Collector.Progress01, 1e-3f,
                "Сбор набирал прогресс сквозь мысль.");
            Assert.AreEqual(-1, screen.Runtime.NoticedIndex, "Деталь под мыслью замечать нельзя.");
            Assert.AreEqual(1, screen.Runtime.Field.Thoughts.Count, "Мысль обучения обязана быть на месте.");

            // Shaken off — and the same hand, doing the same thing, now works.
            yield return GameTestHarness.ShakeUntil(fake,
                () => screen.Runtime.Field.Thoughts.Count == 0, "мысль отбита");
            yield return GameTestHarness.Frames(3);
            Assert.IsFalse(screen.CollectionBlockedByTutorial, "Мысль отбита — блок обязан сняться.");

            yield return GameTestHarness.CollectOneDetail(fake, screen);
            Assert.Greater(screen.Runtime.CollectedCount, collected,
                "После отбитой мысли сбор обязан пойти.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheTutorialTimerToggle_LetsTheClockRunFromTheStart()
        {
            TuningConfig.TutorialTimerPaused = false;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            Assert.IsTrue(screen.TimerRunning, "С выключенным тогглером таймер идёт с самого начала.");

            float before = screen.Rules.TimeLeft;
            yield return GameTestHarness.Idle(fake, 30);
            Assert.Less(screen.Rules.TimeLeft, before, "Таймер обязан убывать.");
        }

        [UnityTest]
        public IEnumerator EveryLevelAfterTheFirst_DoesNotTeach([Values(1, 2, 3, 4)] int levelIndex)
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, levelIndex);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            Assert.AreEqual(TutorialBeat.Done, screen.Beat, "Обучение — только на первом уровне.");
            Assert.IsTrue(screen.TimerRunning, "Без обучения таймер идёт сразу.");
            Assert.IsFalse(screen.View.Hint.IsShown,
                "Уровень " + (levelIndex + 1) + ": карточек обучения быть не должно.");
        }

        // ---- level 5: the decoys and the foreground strip ---------------------------------------------

        /// <summary>
        /// The office plate has two yellow sticky notes painted into it (SCREENS «Уровень 5»: «это НЕ
        /// деталь»), and exactly one sticky note is a collectable. So the level must offer five targets
        /// and no more — a sixth would be a detail the player chases and can never collect, and victory
        /// («все N деталей») would be unreachable through no fault of theirs.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelFive_OffersFiveTargets_AndTheBakedStickersAreNotAmongThem()
        {
            TuningConfig.CollectSeconds = TestOnlyFastCollectSeconds;
            TuningConfig.Notice = NoticeMode.FixedOrder;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 4);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelDefinition level = screen.Level;

            Assert.AreEqual(level.DetailCount, screen.View.DetailImages.Count,
                "Нарисованных деталей не столько, сколько их в каталоге.");
            Assert.AreEqual(level.DetailCount, screen.Runtime.Collected.Length,
                "Цикл сбора видит не то число целей, что уровень объявил.");

            int stickers = 0;
            foreach (ArtDetail detail in level.Details)
                if (detail.Sprite.EndsWith("sticky-note")) stickers++;
            Assert.AreEqual(1, stickers, "Стикер-деталь ровно один — оба фоновых жёлтых стикера декор.");

            // …and those five really are the whole win condition.
            yield return GameTestHarness.PlayUntil(fake,
                () => screen.Runtime.CollectedCount >= level.DetailCount, "офис собран");
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Win, "победа на уровне 5");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// SCREENS «Уровень 5»: «тапки и кружка лежат в полосе переднего плана — слой мыслей должен
        /// закрывать и её (кроме HUD)». The strip is not a separate plate in a real level, so what has
        /// to be true is that the thought layer spans the WHOLE frame and sits above the details while
        /// staying below the HUD.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelFive_ThoughtsCoverTheForegroundStripToo_ButNeverTheHud()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 4);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelView view = screen.View;
            DesignStage stage = StandTestHarness.Stage();

            bool inStrip = false;
            foreach (ArtDetail detail in screen.Level.Details)
                if (detail.Home.y > LevelCatalog.SceneHeight) inStrip = true;
            Assert.IsTrue(inStrip, "На пятом уровне есть детали в полосе переднего плана — иначе тест ни о чём.");

            Rect thoughts = stage.DesignRectOf(view.ThoughtsLayer);
            Assert.LessOrEqual(thoughts.yMin, 1f, "Слой мыслей начинается ниже верха кадра.");
            Assert.GreaterOrEqual(thoughts.yMax, DesignStage.DesignHeight - 1f,
                "Слой мыслей не доходит до низа кадра — полоса переднего плана осталась не закрытой.");

            Assert.Greater(view.ThoughtsLayer.GetSiblingIndex(), view.DetailsLayer.GetSiblingIndex(),
                "Мысли должны быть ПОВЕРХ деталей.");
            Assert.Greater(view.HudLayer.GetSiblingIndex(), view.ThoughtsLayer.GetSiblingIndex(),
                "HUD мысли не закрывают (SCREENS «Зоны»).");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- the chaos peak has to EXIST on screen (SCREENS «Мысли») ---------------------------------

        /// <summary>
        /// The peak was a number with no picture: mock 16's veil (#3a3050 @0.12) over a night street
        /// moves the road by four values out of 255, and the design gate measured the peak frame as no
        /// darker than ordinary play. SCREENS asks for the edges in the same line — «лёгкое затемнение
        /// краёв экрана» — and that is what is checked here: at the peak the edges are really drawn,
        /// over the whole frame, under the HUD; below the threshold they are gone.
        /// </summary>
        [UnityTest]
        public IEnumerator TheChaosPeak_DarkensTheEdgesOfTheFrame_AndOnlyAtItsOwnThreshold()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelView view = screen.View;
            DesignStage stage = StandTestHarness.Stage();

            // After the level is in: entering one applies its own band of values over the live ones.
            TuningConfig.PeakOverlapPercent = 30f;

            yield return GameTestHarness.Idle(fake, 2);
            Assert.AreEqual(0f, view.PeakEdges.color.a, 1e-3f, "Пустой экран — никакого пика.");
            Assert.AreEqual(0f, view.PeakVignette.color.a, 1e-3f, "Пустой экран — никакой вуали.");

            GameTestHarness.CrowdTheScreen(screen, 15);
            yield return GameTestHarness.Idle(fake, 3);

            Assert.GreaterOrEqual(screen.Runtime.Field.OverlapPercent, TuningConfig.PeakOverlapPercent,
                "Не добрались до порога пика.");
            Assert.AreEqual(LevelStage.Play, screen.Stage, "Пик — состояние игры, а не исхода.");
            Assert.Greater(view.PeakEdges.color.a, 0.3f, "Края кадра в пике не темнеют.");
            Assert.Greater(view.PeakVignette.color.a, 0f, "Вуаль пика не появилась.");

            Rect edges = stage.DesignRectOf(view.PeakEdges.rectTransform);
            Assert.LessOrEqual(edges.xMin, 1f, "Затемнение не доходит до левого края.");
            Assert.LessOrEqual(edges.yMin, 1f, "Затемнение не доходит до верха.");
            Assert.GreaterOrEqual(edges.xMax, DesignStage.DesignWidth - 1f, "…до правого края.");
            Assert.GreaterOrEqual(edges.yMax, DesignStage.DesignHeight - 1f, "…до низа.");

            Assert.Greater(view.HudLayer.GetSiblingIndex(), view.PeakLayer.GetSiblingIndex(),
                "HUD пик не закрывает (SCREENS «Зоны»).");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- the tutorial introduces one hand at a time ----------------------------------------------

        /// <summary>
        /// The gaze circle belongs to the third beat. In beats 1–2 the walkthrough asks for the crank
        /// and then for the shake, and a blue circle drifting through those frames both teaches the
        /// wrong hand and spoils the two frames the design gate looks at.
        /// </summary>
        [UnityTest]
        public IEnumerator TheGazeCircle_AppearsOnlyWithItsOwnTeachingBeat()
        {
            TuningConfig.Notice = NoticeMode.GazeJoystick;   // the shipped variant B
            TuningConfig.CollectSeconds = TestOnlyFastCollectSeconds;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelView view = screen.View;

            Assert.AreEqual(TutorialBeat.Crank, screen.Beat);
            yield return GameTestHarness.Idle(fake, 2);
            Assert.IsFalse(view.Gaze.gameObject.activeSelf, "Бит «крути»: круга-взгляда быть не должно.");

            yield return GameTestHarness.CollectOneDetail(fake, screen, false);
            yield return GameTestHarness.Idle(fake, 2);
            Assert.AreEqual(TutorialBeat.Shake, screen.Beat);
            Assert.IsFalse(view.Gaze.gameObject.activeSelf, "Бит «тряси»: круга-взгляда быть не должно.");

            yield return GameTestHarness.ShakeUntil(fake,
                () => screen.Runtime.Field.Thoughts.Count == 0, "мысль отбита");
            yield return GameTestHarness.Idle(fake, 3);

            Assert.AreEqual(TutorialBeat.Gaze, screen.Beat);
            StandTestHarness.AssertVisible(view.Gaze.rectTransform, "Круг-взгляд на бите «оглядись»");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- the cover toggle and the whole z-order (MECHANICS §4) ------------------------------------

        /// <summary>
        /// The toggle «мысли закрывают сосуд» has to be reversible, and reversible means the WHOLE
        /// order comes back — not just the two layers that swapped. Switching it on and back off used
        /// to leave the vessel above the peak veil, so after one round trip the chaos-peak veil stopped
        /// falling on the vessel and on the detail being dragged.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCoverToggle_IsReversible_AndKeepsTheWholeLayerOrder()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            LevelView view = ((LevelScreen)GameTestHarness.Flow().Screen).View;

            for (int round = 1; round <= 3; round++)
            {
                TuningConfig.ThoughtsCoverVessel = false;
                yield return GameTestHarness.Idle(fake, 2);
                AssertLayerOrder(view, false, "выкл, круг " + round);

                TuningConfig.ThoughtsCoverVessel = true;
                yield return GameTestHarness.Idle(fake, 2);
                AssertLayerOrder(view, true, "вкл, круг " + round);
            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The base order of ARCHITECTURE §Z-порядок: scene → details → thread → thoughts → vessel →
        /// peak veil → HUD → messages, with the vessel and the thoughts swapping on the toggle. Checked
        /// whole and in sequence — a pairwise check is what let the vessel drift past the veil.
        /// </summary>
        private static void AssertLayerOrder(LevelView view, bool cover, string what)
        {
            RectTransform[] expected = cover
                ? new[]
                {
                    view.SceneLayer, view.DetailsLayer, view.ThreadLayer, view.VesselLayer,
                    view.ThoughtsLayer, view.PeakLayer, view.HudLayer, view.MessageLayer
                }
                : new[]
                {
                    view.SceneLayer, view.DetailsLayer, view.ThreadLayer, view.ThoughtsLayer,
                    view.VesselLayer, view.PeakLayer, view.HudLayer, view.MessageLayer
                };

            for (int i = 1; i < expected.Length; i++)
                Assert.AreEqual(expected[i - 1].GetSiblingIndex() + 1, expected[i].GetSiblingIndex(),
                    what + ": «" + expected[i].name + "» стоит не сразу за «" + expected[i - 1].name + "».");
        }

        // ---- the intro state (SCREENS S3) ------------------------------------------------------------

        [UnityTest]
        public IEnumerator ALevelOpensOnItsTwoSecondSurvey_WithNoThoughtsAndAHeldClock()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();

            yield return GameTestHarness.CrankDegrees(fake, TitleScreen.StartDegrees + 40f);
            yield return GameTestHarness.Until(() => GameTestHarness.Flow().Phase == GamePhase.Level,
                "уровень открылся");

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            Assert.AreEqual(LevelStage.Intro, screen.Stage, "Уровень обязан открываться обзором.");
            Assert.AreEqual(0, screen.Runtime.Field.Thoughts.Count, "В обзоре мыслей нет.");
            Assert.IsFalse(screen.TimerRunning, "В обзоре таймер стоит.");

            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "обзор закончился");
            LogAssert.NoUnexpectedReceived();
        }

        // ---- victory (done contract §3) --------------------------------------------------------------

        [UnityTest]
        public IEnumerator ALevelIsWonByCollectingEveryOneOfItsDetails()
        {
            TuningConfig.CollectSeconds = TestOnlyFastCollectSeconds;
            TuningConfig.Notice = NoticeMode.FixedOrder;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);   // no tutorial in the way

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            int needed = screen.Level.DetailCount;

            yield return GameTestHarness.PlayUntil(fake,
                () => screen.Runtime.CollectedCount >= needed, "все детали собраны");

            Assert.AreEqual(needed, screen.Runtime.CollectedCount);
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Win, "победа");

            yield return GameTestHarness.Until(() => screen.VictoryPresented, "сосуд ×2 с добычей");
            Assert.AreEqual(0, screen.Runtime.Field.Thoughts.Count, "На победе мысли обязаны раствориться.");

            DesignStage stage = StandTestHarness.Stage();
            Rect vessel = stage.DesignRectOf(StandTestHarness.Find(stage, "Vessel"));
            Assert.AreEqual(960f, vessel.center.x, 30f, "Сосуд не выехал в центр.");
            Assert.AreEqual(540f, vessel.center.y, 30f, "Сосуд не выехал в центр.");
            // «Масштаб ×2» is the ceiling, and mock 18's 480×300 box is the rule: at a flat ×2 the
            // bucket covered the caption and the slot row underneath it.
            Rect box = LevelCatalog.VictoryTableauRectOf(screen.Level);
            Assert.AreEqual(box.width, vessel.width, 30f, "Сосуд не нормирован к коробке макета.");
            Assert.LessOrEqual(vessel.height, LevelCatalog.VictoryBoxHeight + 30f,
                "Сосуд победы выше коробки макета — подпись и слоты под ним.");

            Assert.AreEqual(GameTexts.Collected(screen.Level.Title), screen.View.BigMessage.text);
            StandTestHarness.AssertVisible(screen.View.BigMessage.rectTransform, "Строка «Собрано»");

            // S4 is «текст + ряд ЗАПОЛНЕННЫХ слотов» (mock 18) — the line alone was half the screen.
            AssertVictoryRowIsFull(screen, stage);

            // …and the haul is inside the vessel, not spread across and past it.
            AssertHaulIsInsideTheVessel(screen, stage);

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>Every slot of the victory row is drawn, and every one of them is filled.</summary>
        private static void AssertVictoryRowIsFull(LevelScreen screen, DesignStage stage)
        {
            StandTestHarness.AssertVisible(screen.View.VictorySlots, "Ряд слотов победы");

            for (int i = 0; i < screen.Level.DetailCount; i++)
            {
                RectTransform slot = StandTestHarness.Find(stage, "VictorySlot" + (i + 1));
                StandTestHarness.AssertVisible(slot, "Слот победы " + (i + 1));

                RectTransform art = StandTestHarness.Find(stage, "VictorySlotArt" + (i + 1));
                var image = art.GetComponent<UnityEngine.UI.Image>();
                Assert.IsNotNull(image.sprite, "Слот " + (i + 1) + " победы без силуэта детали.");
                Assert.AreEqual(1f, image.color.a, 1e-3f,
                    "Слот " + (i + 1) + " на победе не заполнен — уровень же собран целиком.");
            }
        }

        /// <summary>
        /// «Деталь появляется ВНУТРИ сосуда» (SCREENS «Сбор»), measured on the drawn rectangles: the
        /// haul used to run wider than the briefcase and paste a detail bigger than it was in the
        /// scene over the lock.
        /// </summary>
        private static void AssertHaulIsInsideTheVessel(LevelScreen screen, DesignStage stage)
        {
            Rect vessel = stage.DesignRectOf(StandTestHarness.Find(stage, "Vessel"));

            for (int i = 0; i < screen.Level.DetailCount; i++)
            {
                string name = "InVessel_" + screen.Level.Details[i].Name;
                Rect item = stage.DesignRectOf(StandTestHarness.Find(stage, name));

                Assert.GreaterOrEqual(item.xMin, vessel.xMin - 1f, name + ": вылез за сосуд слева.");
                Assert.LessOrEqual(item.xMax, vessel.xMax + 1f, name + ": вылез за сосуд справа.");
                Assert.GreaterOrEqual(item.yMin, vessel.yMin - 1f, name + ": вылез за сосуд сверху.");
                Assert.LessOrEqual(item.yMax, vessel.yMax + 1f, name + ": вылез за сосуд снизу.");
            }
        }

        // ---- both defeats and the retry (done contract §5) -------------------------------------------

        [UnityTest]
        public IEnumerator TheClockRunningOut_LosesTheLevel()
        {
            TuningConfig.AutoRetry = false;
            TuningConfig.SetLevelSeconds(1, 60f);   // the shortest the spec's own slider allows

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            // Burn the clock through the rules rather than by waiting a real minute: what is under test
            // is what happens AT zero, not how long a minute is.
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");
            GameTestHarness.DrainTheClock(screen);
            yield return GameTestHarness.Idle(fake, 4);

            Assert.AreEqual(LevelStage.Lose, screen.Stage, "Истёкший таймер обязан быть поражением.");
            Assert.AreEqual(LevelRules.LoseByTimer, screen.Rules.LoseReason);
            Assert.AreEqual(GameTexts.DefeatBig, screen.View.BigMessage.text);
            StandTestHarness.AssertVisible(screen.View.BigMessage.rectTransform, "Строка поражения");

            // SCREENS S5: экран целиком закрыт мыслями — иначе оборотам динамо нечего стирать.
            Assert.GreaterOrEqual(screen.Runtime.Field.OverlapPercent, TuningConfig.LossOverlapPercent,
                "Экран поражения обязан быть закрыт мыслями.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// «Экран целиком закрыт мыслями» (SCREENS S5) counts the PICTURE, not just the coverage
        /// number: with the toggle off the vessel layer draws above the thoughts, and the lost frame
        /// came out with the briefcase — and the detail on its thread — still bright in the middle of
        /// the wallpaper. On the defeat screen there is nothing left to aim at, so the thoughts go on
        /// top whatever the toggle says, and the toggle's own order comes back with the restart.
        /// </summary>
        [UnityTest]
        public IEnumerator TheDefeatScreen_ClosesOverTheVesselToo_WhateverTheToggleSays()
        {
            TuningConfig.AutoRetry = false;
            TuningConfig.ThoughtsCoverVessel = false;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelView view = screen.View;
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");
            AssertLayerOrder(view, false, "в игре, тумблер выключен");

            GameTestHarness.BuryTheScreen(screen);
            yield return GameTestHarness.Idle(fake, 4);
            Assert.AreEqual(LevelStage.Lose, screen.Stage, "Ожидалось поражение.");

            AssertLayerOrder(view, true, "поражение");

            // …and the vessel keeps its own colour under the wallpaper: the half-fade of the toggle is
            // for a vessel the player is still aiming at.
            Assert.AreEqual(1f, view.VesselAlpha, 0.001f, "Сосуд под обоями не должен быть полупрозрачным.");

            // Wipe the screen clear — the level restarts, and the run goes on with the normal order.
            GameFlow flow = GameTestHarness.Flow();
            yield return GameTestHarness.CrankUntil(fake,
                () => flow.Phase == GamePhase.LevelCard, "экран расчищен, уровень перезапущен");
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.Level, "уровень заново", 20f);
            yield return GameTestHarness.Idle(fake, 2);

            AssertLayerOrder(((LevelScreen)flow.Screen).View, false, "после рестарта");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ThoughtsTakingTheScreen_LoseTheLevel()
        {
            TuningConfig.AutoRetry = false;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");

            GameTestHarness.BuryTheScreen(screen);
            yield return GameTestHarness.Idle(fake, 4);

            Assert.AreEqual(LevelStage.Lose, screen.Stage, "Перекрытие обязано быть поражением.");
            Assert.AreEqual(LevelRules.LoseByThoughts, screen.Rules.LoseReason);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TurningTheCrank_WipesTheDefeatScreen_AndRestartsTheSameLevel()
        {
            TuningConfig.AutoRetry = false;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

            GameFlow flow = GameTestHarness.Flow();
            var screen = (LevelScreen)flow.Screen;
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");

            GameTestHarness.BuryTheScreen(screen);
            yield return GameTestHarness.Idle(fake, 4);
            Assert.AreEqual(LevelStage.Lose, screen.Stage);

            int buried = screen.DefeatThoughtsLeft;
            Assert.Greater(buried, 0, "Нечего стирать.");

            // One turn has to visibly clear part of the screen — «~10 % мыслей за оборот».
            yield return GameTestHarness.CrankDegrees(fake, 400f);
            Assert.Less(screen.DefeatThoughtsLeft, buried, "Оборот динамо ничего не стёр.");

            yield return GameTestHarness.CrankUntil(fake,
                () => flow.Phase == GamePhase.LevelCard, "экран расчищен, уровень перезапущен");

            Assert.AreEqual(1, flow.LevelIndex, "Рестарт обязан вести на ТОТ ЖЕ уровень.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheAutoRetryToggle_RestartsWithoutTheCrank()
        {
            TuningConfig.AutoRetry = true;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

            GameFlow flow = GameTestHarness.Flow();
            var screen = (LevelScreen)flow.Screen;
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");

            GameTestHarness.BuryTheScreen(screen);
            yield return GameTestHarness.Idle(fake, 4);
            Assert.AreEqual(LevelStage.Lose, screen.Stage);

            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.LevelCard,
                "авто-ретрай сработал", PatienceSeconds);
            Assert.AreEqual(1, flow.LevelIndex);
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the whole flow, end to end (done contract §1) -------------------------------------------

        [UnityTest]
        public IEnumerator TheWholeRun_GoesTitleToFinale_ThroughAllFiveLevels()
        {
            TuningConfig.CollectSeconds = TestOnlyFastCollectSeconds;
            TuningConfig.Notice = NoticeMode.FixedOrder;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            yield return GameTestHarness.CrankDegrees(fake, TitleScreen.StartDegrees + 40f);

            for (int levelIndex = 0; levelIndex < LevelCatalog.Count; levelIndex++)
            {
                yield return GameTestHarness.Until(
                    () => flow.Phase == GamePhase.Level && flow.LevelIndex == levelIndex,
                    "уровень " + (levelIndex + 1));

                var screen = (LevelScreen)flow.Screen;
                Assert.AreEqual(LevelCatalog.At(levelIndex).Number, screen.Level.Number);

                int needed = screen.Level.DetailCount;
                yield return GameTestHarness.PlayUntil(fake,
                    () => screen.Runtime.CollectedCount >= needed,
                    "уровень " + (levelIndex + 1) + " собран");

                // Every level's tuning band is the one that is live while it is on screen.
                Assert.AreEqual(levelIndex, TuningConfig.ActiveLevelIndex);
                Assert.AreEqual(TuningConfig.LevelSecondsOf(levelIndex), TuningConfig.LevelSeconds, 1e-3f);
            }

            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.Finale, "финал",
                PatienceSeconds);

            DesignStage stage = StandTestHarness.Stage();
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "FinaleTitle"), "Строка финала");
            Assert.AreEqual(GameTexts.FinaleBig,
                StandTestHarness.Find(stage, "FinaleTitle").GetComponent<UnityEngine.UI.Text>().text);
            Assert.AreEqual(LevelCatalog.VesselWords(),
                StandTestHarness.Find(stage, "FinaleVessels").GetComponent<UnityEngine.UI.Text>().text);

            for (int i = 0; i < LevelCatalog.Count; i++)
                StandTestHarness.AssertVisible(
                    StandTestHarness.Find(stage, "FinaleVessel" + LevelCatalog.At(i).Number),
                    "Сосуд уровня " + (i + 1));

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The finale's row of five, judged the way the design gate judges it: every haul inside its
        /// vessel (a flower used to sit on the rim of the mug), and the one vessel that is a cut-out of
        /// its plate — level 3's bag — travelling in a rounded window rather than as a raw rectangle
        /// with a corner of the passenger's coat in it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheFinale_KeepsEveryHaulInsideItsVessel_AndCutsTheBakedBagToShape()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            GameTestHarness.JumpTo(flow, GamePhase.Finale, LevelCatalog.Count - 1);
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.Finale, "финал");
            yield return GameTestHarness.SettleScreen(fake);

            DesignStage stage = StandTestHarness.Stage();

            for (int levelIndex = 0; levelIndex < LevelCatalog.Count; levelIndex++)
            {
                LevelDefinition level = LevelCatalog.At(levelIndex);
                Rect vessel = stage.DesignRectOf(
                    StandTestHarness.Find(stage, "FinaleVessel" + level.Number));

                for (int i = 0; i < level.DetailCount; i++)
                {
                    string name = "FinaleDetail" + level.Number + "_" + i;
                    Rect item = stage.DesignRectOf(StandTestHarness.Find(stage, name));

                    Assert.GreaterOrEqual(item.xMin, vessel.xMin - 1f, name + ": вылез за сосуд слева.");
                    Assert.LessOrEqual(item.xMax, vessel.xMax + 1f, name + ": вылез за сосуд справа.");
                    Assert.GreaterOrEqual(item.yMin, vessel.yMin - 1f, name + ": вылез за сосуд сверху.");
                    Assert.LessOrEqual(item.yMax, vessel.yMax + 1f, name + ": вылез за сосуд снизу.");
                }

                if (!level.VesselIsBaked) continue;

                RectTransform window = StandTestHarness.Find(stage,
                    "FinaleVesselWindow" + level.Number);
                Assert.IsNotNull(window.GetComponent<UnityEngine.UI.Mask>(),
                    "Вырезанный из фона сосуд обязан ехать в окне-маске, а не прямоугольником.");
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheFinale_LeavesOnAnyInput()
        {
            GameTestHarness.PretendLauncherIsListening();

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            GameTestHarness.JumpTo(flow, GamePhase.Finale, LevelCatalog.Count - 1);
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.Finale, "финал");

            // Wait out the grace window in the screen's OWN clock, not in frames: the very input that
            // finished level 3 must not be able to skip the finale, so the screen ignores input for a
            // moment, and a frame count is not a promise about how much time has passed.
            fake.Next = new BackendSnapshot();
            yield return GameTestHarness.Until(() => flow.Screen.Age > 1.2f, "окно «не засчитывать ввод»");

            fake.Next = new BackendSnapshot { CrankDeltaDegrees = CrankPerFrame };
            yield return GameTestHarness.AssertHandedToLauncher(flow, 1, "финал по вводу");

            // …and exactly once, however many frames the input lasts.
            yield return GameTestHarness.Frames(10);
            Assert.AreEqual(1, GameTestHarness.LauncherExits, "Выход запрошен больше одного раза.");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- the cabinet contract (done contract §9) --------------------------------------------------

        /// <summary>
        /// The exit as the cabinet will see it (ARCADE_INTEGRATION_CONTRACT §5): the launcher is
        /// listening, so «в меню» hands the screen back and the game finishes — it does NOT reload
        /// <c>Game.unity</c>, which is what it used to do and what would have restarted the game on the
        /// cabinet instead of leaving it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheMenuButton_HandsTheScreenToTheLauncher_FromEveryScreenOfTheFlow()
        {
            foreach ((GamePhase phase, int level, string what) in ExitStops())
            {
                GameTestHarness.PretendLauncherIsListening();

                yield return GameTestHarness.LoadGame();
                FakeBackend fake = StandTestHarness.TakeOverInput();
                GameFlow flow = GameTestHarness.Flow();

                if (phase != GamePhase.Title)
                {
                    GameTestHarness.JumpTo(flow, phase, level);
                    yield return GameTestHarness.Until(() => flow.Phase == phase, what);
                }

                fake.Next = new BackendSnapshot { MenuHeld = true };
                yield return GameTestHarness.AssertHandedToLauncher(flow, 1, what);

                fake.Next = new BackendSnapshot();
                yield return GameTestHarness.Frames(2);
                GameTestHarness.ForgetLauncher();
            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The fallback, and only the fallback: nobody is listening (the editor, or a standalone build
        /// of the game by itself), so there is nothing to hand the screen to and the game goes back to
        /// its own title — cleanly, with no state of the previous run behind it.
        /// </summary>
        [UnityTest]
        public IEnumerator WithNoLauncherListening_TheExitFallsBackToTheTitle()
        {
            GameTestHarness.ForgetLauncher();

            foreach ((GamePhase phase, int level, string what) in ExitStops())
            {
                yield return GameTestHarness.LoadGame();
                FakeBackend fake = StandTestHarness.TakeOverInput();
                GameFlow flow = GameTestHarness.Flow();

                if (phase != GamePhase.Title)
                {
                    GameTestHarness.JumpTo(flow, phase, level);
                    yield return GameTestHarness.Until(() => flow.Phase == phase, what);
                }

                fake.Next = new BackendSnapshot { MenuHeld = true };
                yield return GameTestHarness.AssertCleanRestart(what + " (без лаунчера)");

                fake.Next = new BackendSnapshot();
                yield return GameTestHarness.Frames(2);
            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>Every point of the flow «в меню» has to work from (done contract §9).</summary>
        private static (GamePhase Phase, int Level, string What)[] ExitStops() =>
            new (GamePhase, int, string)[]
            {
                (GamePhase.Title, 0, "титул"),
                (GamePhase.LevelCard, 0, "карточка уровня"),
                (GamePhase.Level, 0, "уровень 1"),
                (GamePhase.Level, 2, "уровень 3"),
                (GamePhase.Level, 4, "уровень 5"),
                (GamePhase.Finale, LevelCatalog.Count - 1, "финал")
            };

        [UnityTest]
        public IEnumerator TheGameSceneIsTheEntryScene()
        {
            // ARCADE_INTEGRATION_CONTRACT §6: entry-сцена — в Build Settings, и она первая.
            yield return GameTestHarness.LoadGame();
            Assert.AreEqual(PreviewScenes.Game, SceneManager.GetSceneByBuildIndex(0).name,
                "Сцена игры обязана быть нулевой в Build Settings — её грузит лаунчер.");
        }

        // ---- the tuning panel across a run (done contract §7) -----------------------------------------

        [UnityTest]
        public IEnumerator ThePanelIsOnEveryLevel_AndItsValuesSurviveTheWholeRun()
        {
            TuningConfig.PanelVisible = true;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            Assert.IsNotNull(flow.Panel, "Панель обязана существовать на титуле.");

            yield return GameTestHarness.EnterLevel(fake, 0);
            Assert.IsNotNull(flow.Panel, "Панель обязана пережить вход в уровень.");

            // A value moved on level 3's section while level 1 is being played must still be there
            // when level 3 comes up — that is what «автосохраняется» has to mean across a run.
            TuningConfig.SetWaveInterval(2, 6.75f);
            yield return GameTestHarness.EnterLevel(fake, 2);

            Assert.AreEqual(6.75f, TuningConfig.WaveIntervalOf(2), 1e-3f,
                "Значение третьего уровня не дожило до третьего уровня.");
            Assert.AreEqual(6.75f, TuningConfig.WaveIntervalSeconds, 1e-3f,
                "Войдя в уровень, игра обязана применить именно его полосу значений.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The cabinet's first impression: the game opens as a GAME.
        ///
        /// The tuning panel is the founder's instrument, and since her playtest it ships COLLAPSED
        /// (<c>TuningConfig.Defaults.PanelVisible</c>, финал 2026-08-01). Every other case in this
        /// suite sees a collapsed panel only because <c>SetUp</c> puts it there by hand, so the shipped
        /// default needs the one case that overrides nothing — a fresh cabinet start, no saved file.
        /// </summary>
        [UnityTest]
        public IEnumerator TheShippedGame_OpensWithThePanelCollapsed()
        {
            TuningConfig.ResetToDefaults();   // and nothing after it: this IS the shipped state

            yield return GameTestHarness.LoadGame();
            GameFlow flow = GameTestHarness.Flow();

            Assert.IsNotNull(flow.Panel, "Панель обязана существовать — свёрнутая, но живая.");
            Assert.IsFalse(TuningConfig.PanelVisible,
                "Игра обязана открываться со свёрнутой панелью тюнинга.");
            Assert.IsFalse(FindInPanel(flow.Panel, "Body").gameObject.activeInHierarchy,
                "Панель тюнинга не должна занимать кадр на старте игры.");

            // …and the way back to it is still in the frame, or the founder cannot tune at all.
            StandTestHarness.AssertVisible(FindInPanel(flow.Panel, "Collapse"), "Кнопка «параметры»");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// A collapsed panel gave the frame the whole window, but its «параметры» button stayed in the
        /// corner — stand chrome inside every frame the design gate judges the composition by. In the
        /// screenshot mode the button goes too; in the live game it stays, because without it there is
        /// no way back to the panel while playing.
        /// </summary>
        [UnityTest]
        public IEnumerator TheParametersButton_StaysInTheLiveGame_AndLeavesTheScreenshot()
        {
            TuningConfig.PanelVisible = false;
            TuningPanel.ScreenshotMode = false;

            yield return GameTestHarness.LoadGame();
            GameFlow flow = GameTestHarness.Flow();

            RectTransform button = FindInPanel(flow.Panel, "Collapse");
            StandTestHarness.AssertVisible(button, "Кнопка «параметры» в живой игре");

            TuningPanel.ScreenshotMode = true;
            yield return GameTestHarness.Frames(2);
            Assert.IsFalse(button.gameObject.activeInHierarchy,
                "В скриншот-режиме кнопки «параметры» в кадре быть не должно.");

            TuningPanel.ScreenshotMode = false;
            yield return GameTestHarness.Frames(2);
            StandTestHarness.AssertVisible(button, "Кнопка «параметры» после скриншот-режима");

            LogAssert.NoUnexpectedReceived();
        }

        private static RectTransform FindInPanel(TuningPanel panel, string name)
        {
            Assert.IsNotNull(panel, "Панели нет.");
            foreach (RectTransform rt in panel.GetComponentsInChildren<RectTransform>(true))
                if (rt.name == name) return rt;

            Assert.Fail("В панели нет виджета «" + name + "».");
            return null;
        }
    }
}
