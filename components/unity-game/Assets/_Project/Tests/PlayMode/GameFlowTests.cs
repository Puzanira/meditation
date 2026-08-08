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
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "TitleBackground"), "Титул");
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "StartButton"), "Кнопка НАЧАТЬ");

            // The drop's own render, not a fallback plate: a missing sprite would leave a flat sky
            // that looks like a title and proves nothing.
            var title = (TitleScreen)flow.Screen;
            Assert.IsNotNull(title.Background.sprite, "Титул рисуется не картинкой дропа.");
            Assert.IsNotNull(title.StartButton.sprite, "Кнопка НАЧАТЬ — не картинка дропа.");

            // …and it stands where превью.png puts it: centred, in the lower third.
            Rect button = stage.DesignRectOf(StandTestHarness.Find(stage, "StartButton"));
            Assert.AreEqual(TitleScreen.ButtonCentre.x, button.center.x, 20f, "Кнопка не по центру.");
            Assert.AreEqual(TitleScreen.ButtonCentre.y, button.center.y, 20f, "Кнопка не на своём месте.");

            yield return GameTestHarness.Frames(30);
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The one thing the title still says by itself: how much of the two turns is in. It carries no
        /// words — the button is the designer's call to action and the bar is the machine answering.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTitlesProgressBar_FollowsTheTurns()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();

            var title = (TitleScreen)GameTestHarness.Flow().Screen;
            Assert.AreEqual(0f, title.Progress01, 1e-3f, "Бар обязан начинаться пустым.");

            yield return GameTestHarness.CrankDegrees(fake, TitleScreen.StartDegrees * 0.5f);
            Assert.That(title.Progress01, Is.InRange(0.3f, 0.7f),
                "Бар не следит за оборотами: " + title.Progress01.ToString("0.00"));
            StandTestHarness.AssertVisible(title.Progress.rectTransform, "Бар прогресса старта");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// «Старые формулировки выведены из игры» (SCREENS S1–S6, walkthrough «Реестр текстов»).
        ///
        /// The drop replaced the title, the five cards and all three outcome screens with renders that
        /// carry their own typography, and the rule for it is «поверх ничего не писать». Deleting the
        /// lines from the screens is only half of that: a withdrawn line comes back the next time
        /// somebody needs a caption, in a font the designer did not choose, over a picture that already
        /// says it. So the whole flow is walked and every rendered string is checked against the list.
        ///
        /// The level's own timer is the one text the game still draws, and it is a HUD readout with a
        /// plate under it in SCREENS «Зоны» — not a line of copy.
        /// </summary>
        [UnityTest]
        public IEnumerator NoWithdrawnLine_IsRenderedOnAnyScreenOfTheFlow()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            AssertNothingWithdrawnOnScreen("титул");

            GameTestHarness.JumpTo(flow, GamePhase.LevelCard, 0);
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.LevelCard, "карточка");
            yield return GameTestHarness.SettleScreen(fake);
            AssertNothingWithdrawnOnScreen("карточка уровня");

            yield return GameTestHarness.EnterLevel(fake, 1);
            AssertNothingWithdrawnOnScreen("уровень");

            var screen = (LevelScreen)flow.Screen;
            for (int i = 0; i < screen.Level.DetailCount; i++) screen.View.CollectDetail(i);
            screen.View.StageVictory();
            yield return GameTestHarness.Frames(2);
            AssertNothingWithdrawnOnScreen("победа");

            GameTestHarness.JumpTo(flow, GamePhase.Finale, LevelCatalog.Count - 1);
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.Finale, "финал");
            yield return GameTestHarness.SettleScreen(fake);
            AssertNothingWithdrawnOnScreen("финал");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The registry is a registry in BOTH directions since 2026-08-08 — <see cref="GameTexts.Live"/>
        /// is what the game may say, <see cref="GameTexts.Withdrawn"/> is what it may not — and the two
        /// lists must not overlap.
        ///
        /// That is not a tautology waiting to happen. The lines the founder asked for say almost exactly
        /// what two withdrawn ones said («Крути ручку, чтобы начать», «Тряси джойстик!»), because they
        /// are answers to the same two questions; the difference is that those were the greybox's own
        /// captions and the drop baked their meaning into pictures. A live line that drifts back into
        /// being a withdrawn one is the mistake this catches, and it is a plausible one.
        /// </summary>
        [Test]
        public void TheLiveLines_AreNotTheWithdrawnOnesComingBack()
        {
            Assert.IsNotEmpty(GameTexts.Live, "Реестр живых строк пуст, а игра что-то рисует.");

            foreach (string live in GameTexts.Live)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(live), "В реестре живых строк пустая строка.");
                foreach (string withdrawn in GameTexts.Withdrawn)
                    Assert.AreNotEqual(withdrawn, live,
                        "Строка «" + live + "» одновременно живая и выведенная из игры.");
            }
        }

        private static void AssertNothingWithdrawnOnScreen(string where)
        {
            DesignStage stage = StandTestHarness.Stage();
            foreach (UnityEngine.UI.Text label in
                     stage.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                string text = label.text;
                if (string.IsNullOrWhiteSpace(text)) continue;

                foreach (string withdrawn in GameTexts.Withdrawn)
                    Assert.AreNotEqual(withdrawn, text.Trim(),
                        where + ": строка «" + withdrawn + "» выведена из игры дропом 2026-08-07, " +
                        "но всё ещё рисуется поверх готового экрана.");

                Assert.IsFalse(text.StartsWith(GameTexts.WithdrawnCollectedPrefix),
                    where + ": «Собрано: …» выведено из игры — добычу показывает бит награды.");
                Assert.IsFalse(text.StartsWith(GameTexts.WithdrawnLevelPrefix),
                    where + ": «Уровень N» запечён в карточку — поверх него ничего не пишем.");
            }
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

        /// <summary>
        /// …and the title SAYS so. The founder sat down in front of the finished render and could not
        /// start the game (2026-08-08): a drawn НАЧАТЬ over a bar that has not moved yet reads as a
        /// button, and there is nothing on the cabinet to press it with.
        ///
        /// The claim is not «есть какой-то текст» but that the text matches the CODE — the run starts on
        /// <see cref="TitleScreen.StartDegrees"/> of the dynamo, so the line names the handle and the
        /// count. If somebody ever moves the start onto a button, this test is where the caption stops
        /// being true out loud instead of quietly.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTitle_SaysHowToStart_AndSaysWhatTheCodeDoes()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();
            yield return GameTestHarness.SettleScreen(fake);

            var title = (TitleScreen)flow.Screen;

            Assert.AreEqual(GameTexts.TitleStart, title.StartLabel.text,
                "Подпись титула пишется мимо реестра GameTexts.");
            Assert.AreEqual(GameTexts.TitleStartOnDesk, title.DeskLabel.text,
                "ПК-скобка пишется мимо реестра GameTexts.");

            StandTestHarness.AssertVisible(title.StartLabel.rectTransform, "Подпись «как начать»");
            StandTestHarness.AssertVisible(title.DeskLabel.rectTransform, "ПК-скобка титула");

            Assert.Greater(title.StartLabel.fontSize, title.DeskLabel.fontSize,
                "Скобка для ПК набрана не мельче основной строки — на автомате она вводит в заблуждение.");

            // It names the dynamo, because the dynamo is what the code actually waits for.
            Assert.AreEqual(720f, TitleScreen.StartDegrees, 1e-3f,
                "Старт больше не два оборота — подпись титула стала неправдой.");
            StringAssert.Contains("ручку", title.StartLabel.text,
                "Подпись не называет ручку, а старт — это она.");

            // …and it is not one of the lines the drop withdrew.
            foreach (string withdrawn in GameTexts.Withdrawn)
                Assert.AreNotEqual(withdrawn, title.StartLabel.text,
                    "Подпись титула — это выведенная дропом строка «" + withdrawn + "».");

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

            // The HUD's row of detail slots left the game on 2026-08-08 (founder: «убрать ряд
            // совсем»), so «сколько собрано» is the bar under the vessel and the haul inside it — and
            // the row must not quietly come back with the next art pass.
            Assert.IsNull(StandTestHarness.FindOrNull(stage, "Slot1"),
                "Ряд слотов вернулся в игру — его вывели решением founder 2026-08-08.");
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "Vessel"), "Сосуд");
            StandTestHarness.AssertVisible(screen.View.VesselFillTrack.rectTransform, "Полоса наполнения");

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

        /// <summary>
        /// …and since 2026-08-07 that bar is on EVERY level (founder), not only on the metro's baked
        /// bag. Same widget, same numbers — so this is the level-3 case above run across all five,
        /// asked of the picture rather than of the catalogue.
        ///
        /// The library is the level that made the clamp necessary: its backpack's own lower edge is
        /// nine pixels past the frame, so «под сосудом» taken literally would draw the bar off screen
        /// on exactly the level with the most details to keep track of.
        /// </summary>
        [UnityTest]
        public IEnumerator TheVesselBar_IsDrawnAndFills_OnEveryLevel([Values(0, 1, 2, 3, 4)] int levelIndex)
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.CollectSeconds = TestOnlyFastCollectSeconds;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, levelIndex);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            DesignStage stage = StandTestHarness.Stage();
            string where = "уровень " + screen.Level.Number + " («" + screen.Level.Title + "»)";

            Assert.IsNotNull(screen.View.VesselFillLevel, where + ": нет полосы наполнения.");
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "VesselFillTrack"),
                where + " · полоса наполнения");

            Rect bar = stage.DesignRectOf(StandTestHarness.Find(stage, "VesselFillTrack"));
            Assert.AreEqual(screen.Level.VesselCentre.x, bar.center.x, 20f,
                where + ": полоса не под сосудом по X.");
            Assert.LessOrEqual(bar.yMax, DesignStage.DesignHeight,
                where + ": полоса ушла за нижний край кадра.");
            Assert.Greater(bar.yMin, screen.Level.VesselCentre.y,
                where + ": полоса обязана быть ПОД серединой сосуда.");

            float empty = screen.View.VesselFillLevel.rectTransform.rect.width;
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            Assert.Greater(screen.View.VesselFillLevel.rectTransform.rect.width, empty,
                where + ": полоса не подросла после первой детали.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The aim's radius is a [tune] now, and it is one number: the circle the player SEES and the
        /// circle the rules HIT-TEST against.
        ///
        /// Two numbers is the failure this forbids. It would have been the natural shape — a constant
        /// in <see cref="GazeSelector"/> and a slider in the view — and it would have made «крупнее»
        /// a lie the founder could not see: a bigger drawing over an unchanged hit test aims worse
        /// than the small one did, because what it circles is no longer what it selects.
        /// </summary>
        [UnityTest]
        public IEnumerator TheAimsRadius_MovesTheDrawnCircleAndTheHitTestTogether()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            DesignStage stage = StandTestHarness.Stage();
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");

            Assert.AreEqual(TuningConfig.GazeRadiusPx, GazeSelector.Radius, 1e-3f,
                "Правила меряют не тот радиус, что стоит на панели.");
            Assert.Greater(TuningConfig.GazeRadiusPx, GazeSelector.ScreensRadius,
                "Круг взгляда обязан быть КРУПНЕЕ прежних 90 px (решение founder 2026-08-07).");

            screen.View.SetGaze(new Vector2(960f, 400f), 0f, true);
            yield return GameTestHarness.Frames(2);
            float small = stage.DesignRectOf(screen.View.Gaze.rectTransform).width;

            TuningConfig.GazeRadiusPx = 200f;
            screen.View.SetGaze(new Vector2(960f, 400f), 0f, true);
            yield return GameTestHarness.Frames(2);
            float big = stage.DesignRectOf(screen.View.Gaze.rectTransform).width;

            Assert.Greater(big, small + 40f, "Слайдер радиуса не двигает нарисованный круг.");
            Assert.AreEqual(200f, GazeSelector.Radius, 1e-3f, "…и не двигает попадание.");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- три контроллера: динамо тянет, стик целится, датчики отгоняют ---------------------------

        /// <summary>
        /// Done contract §4 of the sensors increment: the joystick lands no hits in ANY mode of
        /// choosing a detail — and the отгон answers the sensors in all three.
        ///
        /// Both halves in one test on purpose. «Стик больше не бьёт» proved alone would also pass if
        /// the отгон were broken outright, and that is exactly the failure a rewrite of the detector
        /// invites; so the same screen, the same thoughts and the same patience are given first to the
        /// stick and then to a hand over the sensor, and the two answers have to differ.
        /// </summary>
        [UnityTest]
        public IEnumerator TheJoystick_LandsNoHits_InAnyNoticeMode_ButTheSensorsDo(
            [Values(NoticeMode.FixedOrder, NoticeMode.GazeJoystick, NoticeMode.AutoNearest)] NoticeMode mode)
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();

            // Level 2: no tutorial beats in the way, so the claim is about the loop itself. The knobs
            // are set AFTER the level opens — entering one copies its own band over the live values.
            yield return GameTestHarness.EnterLevel(fake, 1);
            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            TuningConfig.Targeting = HitTargeting.AllOnScreen;
            TuningConfig.HitDecayEnabled = false;
            TuningConfig.Notice = mode;
            yield return GameTestHarness.Until(() => screen.Runtime.Field.Thoughts.Count > 0,
                "первая волна пришла");

            Thought target = screen.Runtime.Field.Thoughts[0];
            int pipsBefore = target.HitsRemaining;
            int liveBefore = screen.Runtime.Field.Thoughts.Count;

            // Two seconds of the stick being slammed from stop to stop — the old отгон, verbatim.
            float end = Time.realtimeSinceStartup + 2f;
            bool right = true;
            while (Time.realtimeSinceStartup < end)
            {
                fake.Next = new BackendSnapshot { Joystick = new Vector2(right ? 1f : -1f, 0f) };
                right = !right;
                yield return null;
            }

            Assert.AreEqual(pipsBefore, target.HitsRemaining,
                mode + ": джойстик выбил пипсы — тряска обязана уйти со стика полностью.");
            Assert.GreaterOrEqual(screen.Runtime.Field.Thoughts.Count, liveBefore,
                mode + ": мыслей стало меньше, пока играл только стик.");

            // …and the sensor, on the very same thought, does what the stick no longer can.
            yield return GameTestHarness.SwipeUntil(fake,
                () => target.HitsRemaining < pipsBefore,
                mode + ": взмах над датчиком обязан отбивать мысль");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- the level-1 tutorial (done contract §4) -------------------------------------------------

        [UnityTest]
        public IEnumerator Tutorial_TeachesAim_ThenCrank_ThenSwipe_HoldingTheWaves()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            // Beat 1 «НАВОДИ»: nothing is noticed FOR the player any more — the gaze is what the beat
            // asks for, and until it lands the level is a still picture with a button beside a detail.
            Assert.AreEqual(TutorialBeat.Aim, screen.Beat, "Обучение обязано начинаться с «НАВОДИ».");
            Assert.AreEqual(-1, screen.Runtime.NoticedIndex,
                "Первый бит просит НАВЕСТИСЬ — замечать деталь за игрока нельзя.");
            AssertHintIs(screen, ArtScreens.ButtonAim, "НАВОДИ");

            yield return GameTestHarness.Idle(fake, 30);
            Assert.AreEqual(0, screen.Runtime.Field.Thoughts.Count,
                "До отбитой мысли волны спавниться не должны.");

            // Beat 2 «КРУТИ РУЧКУ» + «ТАЩИ»: two drawn buttons at once — the hand, and what it is
            // doing it to (SCREENS §Обучение п.2).
            yield return GameTestHarness.NoticeSomething(fake, screen);
            Assert.AreEqual(TutorialBeat.Crank, screen.Beat, "После наводки должен идти бит «крути».");
            AssertHintIs(screen, ArtScreens.ButtonCrank, "КРУТИ РУЧКУ");
            Assert.IsTrue(screen.View.SecondHint.IsShown, "Рядом с едущей деталью нет кнопки «ТАЩИ».");
            Assert.AreEqual(ArtLibrary.Get(ArtScreens.ButtonDrag), screen.View.SecondHint.Button.sprite,
                "Вторая кнопка бита — не «ТАЩИ».");

            // Beat 3: the first detail lands, one thought appears ON the next one, and the beat has no
            // button at all — the drop ships no «ТРЯСИ», so it is an arrow at the sensors.
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            Assert.AreEqual(TutorialBeat.Swipe, screen.Beat, "После первой детали должен идти бит отгона.");
            Assert.AreEqual(1, screen.Runtime.Field.Thoughts.Count, "Должна быть ровно одна мысль.");
            Assert.IsFalse(screen.View.Hint.Button.gameObject.activeSelf,
                "Кнопки «ТРЯСИ» в дропе нет — на этом бите текста быть не должно.");
            Assert.IsTrue(screen.View.Hint.IsShown, "Бит отгона обязан показывать хотя бы стрелку.");

            int covered = screen.SwipeBeatDetailIndex;
            Assert.GreaterOrEqual(covered, 0, "Мысль обучения села не на деталь.");
            Assert.IsTrue(screen.Runtime.Field.IsCovered(screen.Level.Details[covered].Home),
                "Мысль обучения не накрывает деталь, сбор которой должна блокировать.");

            // Beaten off — the waves start and the tutorial is over (SCREENS §Обучение п.4; the clock
            // that used to start here went out with the timer, 2026-08-07).
            yield return GameTestHarness.SwipeUntil(fake,
                () => screen.Runtime.Field.Thoughts.Count == 0, "мысль отбита");
            yield return GameTestHarness.Frames(3);

            Assert.AreEqual(TutorialBeat.Done, screen.Beat);
            Assert.IsFalse(screen.View.Hint.IsShown, "Обучение кончилось — подсказок быть не должно.");

            yield return GameTestHarness.Until(() => screen.Runtime.Field.Thoughts.Count > 0,
                "волны пошли после обучения");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- «последний пипс — мысль лопается» (SCREENS §Мысли (визуал состояния)) --------------------

        /// <summary>
        /// The hit that spends the last pip does two things at once, and they must NOT happen at the
        /// same speed: the thought leaves the game instantly, and leaves the screen over 200 ms.
        ///
        /// The instant half is the one that can be got wrong quietly. If the burst were animated by
        /// keeping the thought in <see cref="ThoughtField"/> for a fifth of a second, then for that
        /// fifth of a second a dead thought would still cover the detail under it, still soak up the
        /// next hit, and still be counted into «мыслей на экране» that the chaos layer's volume is
        /// mixed from — a fifth of a second of the player fighting a picture. So the field is asked
        /// here, in the same frame as the hit and before anything is drawn.
        /// </summary>
        [UnityTest]
        public IEnumerator TheLastPip_BurstsTheThought_AndTheGameForgetsItInTheSameFrame()
        {
            // The counter must not decay under the test while the thought is walked to its last pip,
            // and the hits have to land on the one thought that is there.
            TuningConfig.HitDecayEnabled = false;
            TuningConfig.Targeting = HitTargeting.AllOnScreen;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.NoticeSomething(fake, screen);
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.Idle(fake, 4);

            ThoughtField field = screen.Runtime.Field;
            Assert.AreEqual(TutorialBeat.Swipe, screen.Beat, "Бит отгона не начался.");
            Assert.AreEqual(1, field.Thoughts.Count, "Для этой проверки нужна ровно одна мысль.");

            Thought thought = field.Thoughts[0];
            Vector2 where = thought.Position;

            // …up to the last pip, through the field's own call — the one the shaking hand makes.
            field.ApplyHits(thought.Durability - 1, Vector2.zero);
            yield return GameTestHarness.Idle(fake, 2);

            Assert.AreEqual(1, thought.HitsRemaining, "Не встали на последний пипс.");
            Assert.Greater(field.OverlapPercent, 0f, "Живая мысль обязана перекрывать экран.");
            Assert.IsTrue(field.IsCovered(where), "Живая мысль обязана закрывать место под собой.");

            // The killing hit. Nothing has been drawn since — every assertion below is about the state
            // the game is in the moment the pip goes out.
            field.ApplyHits(1, Vector2.zero);

            Assert.AreEqual(0, field.Thoughts.Count, "Модель обязана забыть мысль в тот же кадр.");
            Assert.AreEqual(0f, field.OverlapPercent, 1e-4f,
                "Лопнувшая мысль не смеет числиться в перекрытии экрана.");
            Assert.IsFalse(field.IsCovered(where),
                "Место под лопнувшей мыслью обязано освободиться сразу, а не через 200 мс.");
            Assert.AreEqual(0, screen.Audio.ThoughtCount,
                "Громкость слоя хаоса считается по живым мыслям — лопнувшая в счёт не идёт.");

            // …and the next hit has nothing to land on: the burst is a picture, not a target.
            field.ApplyHits(1, Vector2.zero);
            Assert.AreEqual(0, field.Thoughts.Count, "Отгон обязан проходить сквозь разрыв.");

            // The picture, meanwhile, is still on the screen and going out.
            yield return null;

            LevelView view = screen.View;
            Assert.AreEqual(1, view.Pops.Count,
                "Мысль обязана лопаться (SCREENS: масштаб 1.1 → 0, 200 мс), а не исчезать мгновенно.");

            ThoughtPop pop = view.Pops[0];
            ArtThoughtView bursting = view.PopViews[pop.Slot];
            StandTestHarness.AssertVisible(bursting.Rect, "Лопающаяся мысль");
            Assert.LessOrEqual(bursting.Scale, ThoughtPop.StartScale + 1e-3f,
                "Разрыв начинается с 1.1, не больше.");

            float startedAt = Time.realtimeSinceStartup;
            float previousScale = bursting.Scale;
            float previousElapsed = pop.Elapsed;
            while (view.Pops.Count > 0)
            {
                Assert.AreEqual(ThoughtPop.ScaleAt(pop.Elapsed), bursting.Scale, 1e-3f,
                    "Мысль нарисована не на том масштабе, который у разрыва на часах.");
                yield return null;
                if (view.Pops.Count == 0) break;

                // Time moved on, so the thought has to be smaller than it was — «масштаб 1.1 → 0».
                if (pop.Elapsed > previousElapsed)
                    Assert.Less(bursting.Scale, previousScale, "Масштаб разрыва обязан убывать.");

                previousScale = bursting.Scale;
                previousElapsed = pop.Elapsed;
                Assert.Less(Time.realtimeSinceStartup - startedAt, 2f,
                    "Разрыв не кончается — мысль осталась висеть на экране.");
            }

            // 200 ms are up: nothing of the thought is left in the frame.
            Assert.GreaterOrEqual(pop.Elapsed, ThoughtPop.DurationSeconds,
                "Разрыв оборвали раньше 200 мс.");
            Assert.IsFalse(bursting.Rect.gameObject.activeInHierarchy,
                "После разрыва мысли в кадре быть не должно.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The burst itself, on a clock the test owns: «масштаб 1.1 → 0, 200 мс».
        ///
        /// <see cref="TheLastPip_BurstsTheThought_AndTheGameForgetsItInTheSameFrame"/> plays it at
        /// whatever frame rate the batch run happens to draw at, which proves the wiring but cannot
        /// prove the numbers — a run fast enough could step over the whole animation between two
        /// assertions. Here the view is handed its own field and its own delta times, so every quarter
        /// of the 200 ms is checked against the value SCREENS names.
        /// </summary>
        [UnityTest]
        public IEnumerator TheBurst_GoesFromOnePointOneToNothing_OverTwoHundredMilliseconds()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelDefinition level = screen.Level;

            // A view of the same level, driven by hand: the live one is being ticked by the game every
            // frame and cannot be stepped in quarters of a fifth of a second.
            var view = new LevelView(screen.Root, level);
            var field = new ThoughtField(4242)
            {
                Labels = level.ThoughtSprites,
                ArtFitter = ArtLibrary.FitThought
            };

            try
            {
                Thought thought = field.SpawnAt(ThoughtStrength.Weak, level.ThoughtSprites[0],
                    new Vector2(ThoughtField.ScreenWidth * 0.5f, ThoughtField.ScreenHeight * 0.5f));

                view.SyncThoughts(field.Thoughts, 0f);
                Assert.AreEqual(0, view.Pops.Count, "Живая мысль не лопается.");

                field.ApplyHits(thought.Durability, Vector2.zero);
                Assert.AreEqual(0, field.Thoughts.Count, "Мысль обязана уйти из модели сразу.");

                const float Step = ThoughtPop.DurationSeconds * 0.25f;   // 50 мс

                view.SyncThoughts(field.Thoughts, 0f);
                Assert.AreEqual(1, view.Pops.Count, "Разрыв не начался.");

                ThoughtPop pop = view.Pops[0];
                ArtThoughtView bursting = view.PopViews[pop.Slot];
                StandTestHarness.AssertVisible(bursting.Rect, "Лопающаяся мысль на старте разрыва");
                Assert.AreEqual(ThoughtPop.StartScale, bursting.Scale, 1e-4f, "Разрыв стартует с 1.1.");

                // Three quarters of the way down, and the row of pips rides the ink it belongs to.
                foreach (float expected in new[] { 0.825f, 0.55f, 0.275f })
                {
                    view.SyncThoughts(field.Thoughts, Step);
                    Assert.AreEqual(1, view.Pops.Count, "Разрыв оборвался раньше 200 мс.");
                    Assert.AreEqual(expected, bursting.Scale, 1e-3f,
                        "Масштаб разрыва идёт не по прямой 1.1 → 0 за 200 мс.");
                    StandTestHarness.AssertVisible(bursting.Rect, "Лопающаяся мысль");
                }

                // …and the last quarter takes it off the screen.
                view.SyncThoughts(field.Thoughts, Step);
                Assert.AreEqual(0, view.Pops.Count, "На 200 мс разрыв обязан кончиться.");
                Assert.IsFalse(bursting.Rect.gameObject.activeInHierarchy,
                    "После 200 мс мысли в кадре быть не должно.");

                // A thought taken off the field any other way is not a burst: the victory dissolve
                // clears the field, and the handle wipes the defeat screen by RemoveOldest.
                field.SpawnAt(ThoughtStrength.Weak, level.ThoughtSprites[0], new Vector2(500f, 400f));
                view.SyncThoughts(field.Thoughts, 0f);
                field.RemoveOldest();
                view.SyncThoughts(field.Thoughts, 0f);
                Assert.AreEqual(0, view.Pops.Count,
                    "Снятая без ударов мысль не лопается — иначе ретрай осыпался бы разрывами.");
            }
            finally
            {
                view.Dispose();
            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The teaching plates cover nothing — checked on the DRAWN rectangles, not on the catalogue's
        /// arithmetic (which <c>LevelCatalogTests</c> holds separately).
        ///
        /// Two different claims, and both were needed: the arithmetic says the placement rule is sound,
        /// this says the level actually placed the button where the rule put it. The frame the design
        /// gate judged had «КРУТИ РУЧКУ» lying across the whole shark fin and the left edge of the
        /// bucket, and every arithmetic test in the suite was green at the time.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTeachingButtons_CoverNoDetail_NoVessel_AndNoHudWidget()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelDefinition level = screen.Level;

            Assert.AreEqual(TutorialBeat.Aim, screen.Beat);
            AssertHintPlateIsClear(screen.View.Hint.Rect, level, "НАВОДИ");

            yield return GameTestHarness.NoticeSomething(fake, screen);
            Assert.AreEqual(TutorialBeat.Crank, screen.Beat);
            AssertHintPlateIsClear(screen.View.Hint.Rect, level, "КРУТИ РУЧКУ");

            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertHintPlateIsClear(RectTransform plateRect,
            LevelDefinition level, string what)
        {
            StandTestHarness.AssertVisible(plateRect, "Плашка «" + what + "»");
            Rect plate = StandTestHarness.Stage().DesignRectOf(plateRect);

            foreach (ArtDetail detail in level.Details)
                Assert.IsFalse(StandTestHarness.Overlaps(plate, LevelCatalog.RectOf(detail)),
                    "Плашка «" + what + "» накрыла деталь «" + detail.Name + "».");

            Assert.IsFalse(StandTestHarness.Overlaps(plate, LevelCatalog.VesselRectOf(level)),
                "Плашка «" + what + "» накрыла сосуд.");
            Assert.IsFalse(StandTestHarness.Overlaps(plate, LevelCatalog.CrankRectOf(level)),
                "Плашка «" + what + "» накрыла индикатор динамо.");
            Assert.IsFalse(StandTestHarness.Overlaps(plate, LevelCatalog.VesselBarRectOf(level)),
                "Плашка «" + what + "» накрыла полосу наполнения сосуда.");
        }

        /// <summary>
        /// «ТАЩИ» rides the detail it names, and never lies on the progress ring or on the thread.
        ///
        /// The gate's frame had it standing where the detail STARTED: the detail drove out from under
        /// its own label, and the plate sat across the ring — the only thing on screen that reports what
        /// the handle is doing. SCREENS §Обучение п.2 says «рядом с едущей деталью».
        /// </summary>
        [UnityTest]
        public IEnumerator TheDragButton_FollowsTheMovingDetail_OffTheRingAndOffTheThread()
        {
            TuningConfig.CollectSeconds = 10f;   // slow enough to watch it travel

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.NoticeSomething(fake, screen);
            Assert.AreEqual(TutorialBeat.Crank, screen.Beat);

            int index = screen.Runtime.NoticedIndex;
            float ring = screen.View.RingRadiusOf(index);
            DesignStage stage = StandTestHarness.Stage();
            Vector2 startedAt = stage.DesignRectOf(screen.View.SecondHint.Rect).center;
            int checks = 0;

            foreach (float mark in new[] { 0.25f, 0.5f, 0.75f })
            {
                yield return GameTestHarness.CrankUntil(fake,
                    () => screen.Runtime.DisplayProgress >= mark, "деталь прошла " + mark + " пути");
                yield return GameTestHarness.Frames(1);

                Vector2 detail = Vector2.Lerp(screen.Level.Details[index].Home, screen.Level.VesselCentre,
                    screen.Runtime.DisplayProgress) + LevelCatalog.AnchorOffsetOf(screen.Level.Details[index]);
                Rect plate = stage.DesignRectOf(screen.View.SecondHint.Rect);

                Assert.Greater(Vector2.Distance(plate.center, detail), ring,
                    "«ТАЩИ» залезла в кольцо прогресса на " + mark + " пути.");
                Assert.IsFalse(
                    HintPlacement.SegmentHits(detail, screen.Level.VesselCentre, plate),
                    "«ТАЩИ» легла на нить на " + mark + " пути.");
                Assert.Less(Vector2.Distance(plate.center, detail), MaxDragHintReach,
                    "«ТАЩИ» отстала от детали на " + mark + " пути.");
                checks++;
            }

            Assert.AreEqual(3, checks);
            Assert.Greater(Vector2.Distance(
                    stage.DesignRectOf(screen.View.SecondHint.Rect).center, startedAt), 60f,
                "Плашка «ТАЩИ» осталась на месте, пока деталь ехала.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>How far «ТАЩИ» may trail the detail it names, design px.</summary>
        private const float MaxDragHintReach = 420f;

        /// <summary>
        /// The отгон beat's arrow: turquoise, clear of everything the thought PAINTS, running down the
        /// frame and ending on the sensor panel — at the SAME point every time it is drawn.
        ///
        /// Every clause of that is a finding. The stroke was a brick-red 1047 px line from the middle of
        /// the drawn thought, crossing the art out (gate, 2026-08-07). The 240 px stroke that replaced it
        /// answered the crossing-out and bought two new problems (gate, 2026-08-08): it measured its
        /// clearance off the blob's RECTANGLE while the pips hang below that rectangle, so on level 1 it
        /// grew straight out of them with no air at all; and it took both its length and its X off the
        /// thought, so the beat's only hint ended in empty sky at a different place every run. The tip is
        /// now fixed on the panel the gesture names, and it is the TAIL that the thought and the swing
        /// move.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSwipeArrow_ClearsTheWholeThought_AndAlwaysLandsOnTheSensorPanel()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.NoticeSomething(fake, screen);
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.Idle(fake, 4);

            Assert.AreEqual(TutorialBeat.Swipe, screen.Beat, "Бит отгона не начался.");
            Assert.AreEqual(1, screen.Runtime.Field.Thoughts.Count);
            Thought thought = screen.Runtime.Field.Thoughts[0];

            HintArrow arrow = screen.View.Hint.Arrow;
            Assert.IsTrue(arrow.IsShown, "Стрелка отгона не нарисована.");
            StandTestHarness.AssertVisible(arrow.FirstSegment, "Стрелка отгона");

            // …the tail is clear of the DRAWING, pips and their discs included.
            float painted = ArtThoughtView.DrawnBottomY(thought.Position, thought.Size);
            Assert.GreaterOrEqual(arrow.From.y, painted + MinSwipeArrowClearance,
                "Хвост стрелки прижат к нарисованному низу мысли (" + arrow.From.y.ToString("0") +
                " против " + painted.ToString("0") + "): пипсы с подложками рисуются НИЖЕ прямоугольника.");

            // …the tip is on the panel, at the bottom edge of the frame.
            Assert.AreEqual(LevelScreen.SensorsCue.x, arrow.To.x, 0.5f,
                "Остриё уехало с середины нижней кромки — жест перестал называть одно и то же место.");
            Assert.GreaterOrEqual(arrow.To.y, DesignStage.DesignHeight - MaxSwipeArrowTipGap,
                "Остриё обрывается в небе: панель с датчиками физически внизу кадра.");
            Assert.Greater(arrow.To.y, arrow.From.y, "Стрелка идёт вверх, а пульт с датчиками снизу.");

            // …and it is the same point a moment later, when the thought has drifted and the swing has
            // moved on: a name has to be the same word twice.
            Vector2 tip = arrow.To;
            Vector2 tail = arrow.From;
            fake.Next = new BackendSnapshot();
            yield return GameTestHarness.Until(() => Vector2.Distance(tail, arrow.From) > 2f,
                "хвост стрелки качнулся");

            Assert.AreEqual(tip.x, arrow.To.x, 0.5f, "Остриё поехало за мыслью по X.");
            Assert.AreEqual(tip.y, arrow.To.y, 0.5f, "Остриё поехало за мыслью по Y.");

            Color ink = HintCard.ToneColour(HintTone.Swipe);
            Assert.Greater(ink.b, ink.r + 0.2f, "Цвет отгона не бирюзовый — это снова кирпич грейбокса.");
            Assert.Greater(ink.g, ink.r + 0.2f, "Цвет отгона не бирюзово-зелёный.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// …and the words that go with that arrow: «Маши над датчиком!» on a teaching card, beside the
        /// thought, off the art and off its own stroke.
        ///
        /// The beat had no words at all until 2026-08-08 — the drop ships no «ТРЯСИ» — and the founder,
        /// playing it, did not know what the arrow was asking her to do. An arrow that ends on a piece
        /// of furniture names a PLACE; the sentence names the movement, and the movement is the whole
        /// mechanic. The card is checked exactly the way the drawn buttons are (it covers no detail, no
        /// vessel, no HUD widget), plus one thing they never had to prove: it does not lie on the stroke
        /// it belongs to.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSwipeBeat_SaysWhatToDo_BesideItsOwnArrow()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelDefinition level = screen.Level;

            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.Idle(fake, 4);
            Assert.AreEqual(TutorialBeat.Swipe, screen.Beat, "Бит отгона не начался.");

            HintCard card = screen.View.SwipeCard;
            Assert.IsTrue(card.IsShown, "На бите отгона нет подписи — игрок снова видит одну стрелку.");
            Assert.AreEqual(GameTexts.SwipeHint, card.Label.text,
                "Подпись бита отгона пишется мимо реестра GameTexts.");
            StandTestHarness.AssertVisible(card.Rect, "Карточка «" + GameTexts.SwipeHint + "»");

            Rect plate = StandTestHarness.Stage().DesignRectOf(card.Rect);
            foreach (ArtDetail detail in level.Details)
                Assert.IsFalse(StandTestHarness.Overlaps(plate, LevelCatalog.RectOf(detail)),
                    "Подпись отгона накрыла деталь «" + detail.Name + "».");
            Assert.IsFalse(StandTestHarness.Overlaps(plate, LevelCatalog.VesselRectOf(level)),
                "Подпись отгона накрыла сосуд.");
            Assert.IsFalse(StandTestHarness.Overlaps(plate, LevelCatalog.VesselBarRectOf(level)),
                "Подпись отгона накрыла полосу наполнения сосуда.");
            Assert.IsFalse(StandTestHarness.Overlaps(plate, LevelCatalog.CrankRectOf(level)),
                "Подпись отгона накрыла индикатор динамо.");

            HintArrow arrow = screen.View.Hint.Arrow;
            Assert.IsFalse(HintPlacement.SegmentHits(arrow.From, arrow.To, plate),
                "Подпись отгона легла на собственную стрелку.");

            // …and it goes away with the beat, like every other hint of the tutorial.
            yield return GameTestHarness.SwipeUntil(fake,
                () => screen.Runtime.Field.Thoughts.Count == 0, "мысль отбита");
            yield return GameTestHarness.Frames(3);
            Assert.AreEqual(TutorialBeat.Done, screen.Beat, "Обучение не закончилось после отбитой мысли.");
            Assert.IsFalse(card.IsShown, "Подпись отгона осталась на экране после обучения.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>Air the tail owes the thought's own drawing, design px — less is «растёт из пипсов».</summary>
        private const float MinSwipeArrowClearance = 8f;

        /// <summary>How far short of the bottom edge the tip may stop, design px.</summary>
        private const float MaxSwipeArrowTipGap = 60f;

        /// <summary>The hint has no text any more, so «which hint» is a question about WHICH PICTURE.</summary>
        private static void AssertHintIs(LevelScreen screen, string buttonKey, string what)
        {
            Assert.IsTrue(screen.View.Hint.IsShown, "Подсказки «" + what + "» нет на экране.");
            StandTestHarness.AssertVisible(screen.View.Hint.Rect, "Кнопка-подсказка «" + what + "»");
            Assert.AreEqual(ArtLibrary.Get(buttonKey), screen.View.Hint.Button.sprite,
                "На экране не та кнопка обучения — ожидалась «" + what + "».");
        }

        /// <summary>
        /// SCREENS «Обучение», beat 3: «сбор заблокирован её появлением поверх следующей детали». Not
        /// «slowed», not «the next detail is skipped» — nothing is collected until the cat is beaten
        /// off. The block used to be a hope: the fixed order picked the covered detail anyway and the
        /// gaze looked straight through the blob, so the crank alone finished the level while the beat
        /// was still asking for the joystick.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSwipeBeat_BlocksCollecting_UntilTheThoughtIsBeatenOff()
        {
            TuningConfig.CollectSeconds = TestOnlyFastCollectSeconds;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            yield return GameTestHarness.CollectOneDetail(fake, screen);
            Assert.AreEqual(TutorialBeat.Swipe, screen.Beat, "После первой детали должен идти бит отгона.");
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

            // Beaten off — and the same hand, doing the same thing, now works.
            yield return GameTestHarness.SwipeUntil(fake,
                () => screen.Runtime.Field.Thoughts.Count == 0, "мысль отбита");
            yield return GameTestHarness.Frames(3);
            Assert.IsFalse(screen.CollectionBlockedByTutorial, "Мысль отбита — блок обязан сняться.");

            yield return GameTestHarness.CollectOneDetail(fake, screen);
            Assert.Greater(screen.Runtime.CollectedCount, collected,
                "После отбитой мысли сбор обязан пойти.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// «Обучение не повторяется после поражения» (founder, 2026-08-07): the FIRST run through
        /// level 1 teaches; the attempt after a defeat goes straight into play — no beats, no обзор.
        ///
        /// Played through the real flow rather than by poking a flag, because the flag is the whole
        /// point: a LevelScreen is rebuilt for every attempt and can remember nothing, so what is under
        /// test is that the memory lives one level up and survives the card in between.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTutorial_DoesNotComeBack_OnTheAttemptAfterADefeat()
        {
            TuningConfig.AutoRetry = true;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var first = (LevelScreen)GameTestHarness.Flow().Screen;
            Assert.IsTrue(first.Teaches, "Первый заход на первый уровень обязан учить.");
            Assert.AreEqual(TutorialBeat.Aim, first.Beat);
            Assert.IsFalse(first.SkipsTheIntro, "Первый заход обязан открываться обзором.");

            // Lose it, and let the flow take the level's own card and come back.
            GameTestHarness.BuryTheScreen(first);
            yield return GameTestHarness.Until(() => first.Stage == LevelStage.Lose, "поражение");
            yield return GameTestHarness.Until(
                () => GameTestHarness.Flow().Phase == GamePhase.Level &&
                      GameTestHarness.Flow().Screen != first, "первый уровень заново");

            var again = (LevelScreen)GameTestHarness.Flow().Screen;
            Assert.AreEqual(0, GameTestHarness.Flow().LevelIndex, "Рестарт обязан быть того же уровня.");
            Assert.IsFalse(again.Teaches, "Обучение повторилось после поражения.");
            Assert.AreEqual(TutorialBeat.Done, again.Beat, "Биты обучения обязаны быть пропущены.");
            Assert.IsTrue(again.SkipsTheIntro);
            Assert.AreEqual(LevelStage.Play, again.Stage, "Рестарт обязан начинаться СРАЗУ в игре.");
            Assert.IsFalse(again.View.Hint.IsShown, "На рестарте подсказок быть не должно.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// …and a NEW run gets it back. The cabinet hands one process to one stranger after another,
        /// so a flag that outlived the run would show the second player a game that assumes they
        /// watched the first player's tutorial.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTutorial_ComesBackForANewRun()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            Assert.IsTrue(((LevelScreen)GameTestHarness.Flow().Screen).Teaches);

            GameFlow flow = GameTestHarness.Flow();
            flow.ExitToLauncher();                          // back to the title (editor fallback)
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.Title, "титул");

            yield return GameTestHarness.EnterLevel(fake, 0);
            Assert.IsTrue(((LevelScreen)GameTestHarness.Flow().Screen).Teaches,
                "Новый прогон обязан снова учить — иначе второй игрок у автомата начинает вслепую.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EveryLevelAfterTheFirst_DoesNotTeach([Values(1, 2, 3, 4)] int levelIndex)
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, levelIndex);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            Assert.AreEqual(TutorialBeat.Done, screen.Beat, "Обучение — только на первом уровне.");
            Assert.IsFalse(screen.SkipsTheIntro, "Обзор пропускается только на рестарте первого уровня.");
            Assert.IsFalse(screen.View.Hint.IsShown,
                "Уровень " + (levelIndex + 1) + ": карточек обучения быть не должно.");
        }

        // ---- the office: its decoys and its foreground strip ------------------------------------------

        /// <summary>
        /// The office plate has two yellow sticky notes painted into it (SCREENS «Уровень 2»: «это НЕ
        /// деталь»), and exactly one sticky note is a collectable. So the level must offer five targets
        /// and no more — a sixth would be a detail the player chases and can never collect, and victory
        /// («все N деталей») would be unreachable through no fault of theirs.
        /// </summary>
        [UnityTest]
        public IEnumerator TheOffice_OffersFiveTargets_AndTheBakedStickersAreNotAmongThem()
        {
            TuningConfig.CollectSeconds = TestOnlyFastCollectSeconds;
            TuningConfig.Notice = NoticeMode.FixedOrder;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

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
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Win, "победа в офисе");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// SCREENS «Уровень 2»: «тапки и кружка лежат в полосе переднего плана — слой мыслей должен
        /// закрывать и её (кроме HUD)». The strip is not a separate plate in a real level, so what has
        /// to be true is that the thought layer spans the WHOLE frame and sits above the details while
        /// staying below the HUD.
        /// </summary>
        [UnityTest]
        public IEnumerator TheOffice_ThoughtsCoverTheForegroundStripToo_ButNeverTheHud()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelView view = screen.View;
            DesignStage stage = StandTestHarness.Stage();

            bool inStrip = false;
            foreach (ArtDetail detail in screen.Level.Details)
                if (detail.Home.y > LevelCatalog.SceneHeight) inStrip = true;
            Assert.IsTrue(inStrip, "В офисе есть детали в полосе переднего плана — иначе тест ни о чём.");

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
        /// The gaze is the FIRST thing the tutorial teaches now, so the circle has to be in play from
        /// the opening beat — the old order switched it off for beats 1–2 and introduced it last, after
        /// the player had already been shown the two hands that need it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheGazeCircle_IsInPlayFromTheAimBeat()
        {
            TuningConfig.Notice = NoticeMode.GazeJoystick;   // the shipped variant B

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.Idle(fake, 2);

            Assert.AreEqual(TutorialBeat.Aim, screen.Beat);
            StandTestHarness.AssertVisible(screen.View.Gaze.rectTransform, "Круг-взгляд на бите «НАВОДИ»");

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
        public IEnumerator ALevelOpensOnItsTwoSecondSurvey_WithNoThoughts()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();

            yield return GameTestHarness.CrankDegrees(fake, TitleScreen.StartDegrees + 40f);
            yield return GameTestHarness.Until(() => GameTestHarness.Flow().Phase == GamePhase.Level,
                "уровень открылся");

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            Assert.AreEqual(LevelStage.Intro, screen.Stage, "Уровень обязан открываться обзором.");
            Assert.AreEqual(0, screen.Runtime.Field.Thoughts.Count, "В обзоре мыслей нет.");

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

            // The haul is inside the vessel, not spread across and past it — this beat is the only
            // look the player ever gets at what they collected.
            AssertHaulIsInsideTheVessel(screen, stage);

            // …and nothing of ours is written over it: «Собрано: …» left the game with the drop.
            Assert.IsFalse(screen.CompleteScreenShown, "Готовый экран победы пришёл раньше бита награды.");

            // …and then the drawn «Отлично!» screen comes over it and the flow moves on.
            yield return GameTestHarness.Until(() => screen.CompleteScreenShown,
                "готовый экран победы");
            StandTestHarness.AssertVisible(screen.View.OutcomeScreen.rectTransform, "Экран «Отлично!»");
            Assert.AreEqual(ArtLibrary.Get(ArtScreens.LevelComplete),
                screen.View.OutcomeScreen.sprite, "На победе показан не тот готовый экран.");

            LogAssert.NoUnexpectedReceived();
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

        /// <summary>
        /// There is exactly ONE way to lose now (founder, 2026-08-07): the thoughts close over the
        /// screen. The old «время вышло» is gone, and this is the case that used to prove it.
        ///
        /// Both halves are worth keeping: the HUD carries no clock at all (no sun, no caption — a level
        /// that still drew them would be advertising a rule it does not have), and the drawn defeat
        /// screen behaves exactly as it did, because S5 never depended on WHICH way the level was lost.
        /// </summary>
        [UnityTest]
        public IEnumerator TheOnlyDefeat_IsTheScreenFillingUp_AndTheHudCarriesNoClock()
        {
            TuningConfig.AutoRetry = false;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            DesignStage stage = StandTestHarness.Stage();
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");

            Assert.IsNull(StandTestHarness.FindOrNull(stage, "Sun"), "В HUD осталось солнце-таймер.");
            Assert.IsNull(StandTestHarness.FindOrNull(stage, "SunDial"), "В HUD остался циферблат.");
            Assert.IsNull(StandTestHarness.FindOrNull(stage, "TimerLabel"), "В HUD осталась подпись таймера.");
            Assert.IsNull(StandTestHarness.FindOrNull(stage, "TimerPlate"), "В HUD осталась плашка таймера.");

            GameTestHarness.BuryTheScreen(screen);
            yield return GameTestHarness.Idle(fake, 4);

            Assert.AreEqual(LevelStage.Lose, screen.Stage, "Заполненный экран обязан быть поражением.");
            Assert.AreEqual(LevelRules.LoseByThoughts, screen.Rules.LoseReason);

            StandTestHarness.AssertVisible(screen.View.OutcomeScreen.rectTransform, "Экран поражения");
            Assert.AreEqual(ArtLibrary.Get(ArtScreens.GameOver), screen.View.OutcomeScreen.sprite,
                "На поражении показан не тот готовый экран.");
            Assert.AreEqual(1f, screen.DefeatCoverLeft01, 1e-3f,
                "Экран поражения обязан закрывать кадр целиком, пока его не начали стирать.");

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

            // …and the outcome layer is above even the HUD: a finished screen covers the frame whole.
            Assert.Greater(view.OutcomeLayer.GetSiblingIndex(), view.HudLayer.GetSiblingIndex(),
                "Готовый экран обязан лежать выше HUD.");

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

            float covered = screen.DefeatCoverLeft01;
            Assert.AreEqual(1f, covered, 1e-3f, "Нечего стирать.");

            // One turn has to visibly clear part of the screen — «каждый оборот стирает ~10 %».
            yield return GameTestHarness.CrankDegrees(fake, 360f);
            Assert.AreEqual(covered - LevelScreen.WipeSharePerTurn, screen.DefeatCoverLeft01, 0.03f,
                "Оборот динамо стёр не ту долю экрана поражения.");

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
                Assert.AreEqual(TuningConfig.WaveIntervalOf(levelIndex),
                    TuningConfig.WaveIntervalSeconds, 1e-3f);
            }

            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.Finale, "финал",
                PatienceSeconds);

            DesignStage stage = StandTestHarness.Stage();
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "FinaleScreenArt"), "Экран финала");
            Assert.AreEqual(ArtLibrary.Get(ArtScreens.Finale),
                ((FinaleScreen)flow.Screen).Panorama.sprite, "Финал рисуется не картинкой дропа.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The finale is one drawn frame now: «сосуды в ряд больше не рисуем (в рендере их нет)»
        /// (SCREENS S6). What used to be tested here — every haul inside its vessel, the baked bag
        /// travelling in a rounded window — moved with the haul itself, which is shown in exactly one
        /// place: the reward beat of each level's victory.
        /// </summary>
        [UnityTest]
        public IEnumerator TheFinale_IsTheDrawnPanorama_WithNoRowOfVessels()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            GameTestHarness.JumpTo(flow, GamePhase.Finale, LevelCatalog.Count - 1);
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.Finale, "финал");
            yield return GameTestHarness.SettleScreen(fake);

            var finale = (FinaleScreen)flow.Screen;
            StandTestHarness.AssertVisible(finale.Panorama.rectTransform, "Панорама финала");
            Assert.IsNotNull(finale.Panorama.sprite, "Финал рисуется не картинкой дропа.");

            DesignStage stage = StandTestHarness.Stage();
            Rect frame = stage.DesignRectOf(finale.Panorama.rectTransform);
            Assert.AreEqual(DesignStage.DesignWidth, frame.width, 2f, "Панорама не во весь кадр.");
            Assert.AreEqual(DesignStage.DesignHeight, frame.height, 2f, "Панорама не во весь кадр.");

            foreach (RectTransform rt in stage.GetComponentsInChildren<RectTransform>(true))
                Assert.IsFalse(rt.name.StartsWith("FinaleVessel"),
                    "На финале снова стоят сосуды — в рендере дропа их нет.");

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

        // ---- звук и луч, как их видит живая сцена (MECHANICS §8, SCREENS «Детали в сцене») ------------

        /// <summary>
        /// The wiring the arithmetic in <c>AudioAndSweepTests</c> cannot check: that the three sources
        /// exist, that each of them has a real clip behind it, and that the level's own track is the
        /// one loaded when that level starts.
        ///
        /// Volumes are not asserted here — batch mode has no audio device, and every rule about them is
        /// already pinned frame by frame in EditMode.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryLevel_LoadsItsOwnSoundtrack([Values(0, 1, 2, 3, 4)] int levelIndex)
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            Assert.IsNotNull(flow.Audio, "У игры нет звуковых слоёв.");
            Assert.IsNotNull(flow.Audio.MeditationSource.clip, "Нет трека «медитация».");
            Assert.IsNotNull(flow.Audio.ThoughtsSource.clip, "Нет трека «мысли».");

            yield return GameTestHarness.EnterLevel(fake, levelIndex);
            yield return GameTestHarness.Idle(fake, 3);

            Assert.AreEqual(ArtLibrary.Clip(GameAudio.BackgroundKeyOf(levelIndex)),
                flow.Audio.BackgroundSource.clip,
                "Уровень " + (levelIndex + 1) + " играет чужой фоновый трек.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// «Кнопка „в меню“ — мгновенная тишина вместе с выходом» (MECHANICS §8). Not a fade, and not a
        /// volume of zero on a source that keeps decoding: the launcher takes the screen back at once,
        /// and music playing over somebody else's menu is the loudest possible way to leave a room.
        /// </summary>
        [UnityTest]
        public IEnumerator TheMenuButton_SilencesEveryLayerAtOnce()
        {
            GameTestHarness.PretendLauncherIsListening();

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();
            GameAudio audio = flow.Audio;

            yield return GameTestHarness.EnterLevel(fake, 1);
            GameTestHarness.CrowdTheScreen((LevelScreen)flow.Screen, 10);
            yield return GameTestHarness.Idle(fake, 60);
            Assert.Greater(audio.Mix.Background + audio.Mix.Thoughts, 0.05f,
                "Нечего заглушать — на уровне звук так и не поднялся.");

            fake.Next = new BackendSnapshot { MenuHeld = true };
            yield return GameTestHarness.Frames(3);

            Assert.AreEqual(0f, audio.BackgroundSource.volume, 1e-4f, "Фон не замолчал по «в меню».");
            Assert.AreEqual(0f, audio.MeditationSource.volume, 1e-4f, "Медитация не замолчала.");
            Assert.AreEqual(0f, audio.ThoughtsSource.volume, 1e-4f, "Слой мыслей не замолчал.");
            Assert.IsFalse(audio.BackgroundSource.isPlaying, "Фон продолжает играть за спиной лаунчера.");
            Assert.IsFalse(audio.ThoughtsSource.isPlaying, "Слой мыслей продолжает играть.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The light band really reaches the details' own material, and really stops at the chaos peak.
        ///
        /// The band's clock is arithmetic (EditMode); what only a live scene can say is that each detail
        /// got its own material instance of <c>Meditation/DetailSweep</c> — the shader masks the light
        /// by the sprite's alpha, which is the whole «светит по спрайтам деталей, а не по фону».
        /// </summary>
        [UnityTest]
        public IEnumerator TheLightSweep_ReachesEveryDetailsOwnMaterial_AndGoesOutAtThePeak()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            // Every detail carries its own instance, or the band would be in one place on all of them.
            var seen = new System.Collections.Generic.HashSet<Material>();
            foreach (UnityEngine.UI.Image detail in screen.View.DetailImages)
            {
                Assert.IsNotNull(detail.material, "У детали нет материала луча.");
                Assert.AreEqual("Meditation/DetailSweep", detail.material.shader.name,
                    "Деталь рисуется не шейдером луча — светить будет фон, а не спрайт.");
                Assert.IsTrue(seen.Add(detail.material),
                    "Две детали делят один материал — полоса встанет на них в одно и то же место.");
            }

            // A pass, forced through the panel so the test does not wait out a period.
            TuningConfig.SweepPeriodSeconds = 4f;
            TuningConfig.SweepDurationSeconds = 2.5f;
            TuningConfig.SweepRarerOnLateLevels = false;
            yield return GameTestHarness.Until(() => screen.Sweep.Active, "луч пошёл", 20f);
            yield return null;

            Assert.Greater(MaxSweepStrength(screen), 0f, "Идущий луч ничего не подсветил.");

            // …and at the peak it goes out, whatever its own clock says.
            TuningConfig.PeakOverlapPercent = 20f;
            GameTestHarness.CrowdTheScreen(screen, 15);
            yield return GameTestHarness.Idle(fake, 4);

            Assert.GreaterOrEqual(screen.Runtime.Field.OverlapPercent, TuningConfig.PeakOverlapPercent,
                "Не добрались до порога пика.");
            Assert.AreEqual(0f, MaxSweepStrength(screen), 1e-4f,
                "На пике хаоса луч обязан гаснуть — он спорит с затемнением краёв.");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- кольцо прогресса: не рисуется, пока показывать нечего --------------------------------------

        /// <summary>
        /// The progress ring stays off the frame until the crank has produced something for it to say.
        ///
        /// Frames 27 and 29 shipped a 2×14 px red tick in the sky over the набережная (design gate,
        /// 2026-08-07): the gull had been noticed, the teaching cat had landed on it and the dynamo had
        /// wound down, so a ring was drawn at fill 0 in the alarm colour. Fill 0 is not «прогресс 0 %» —
        /// it is a stroke of red on a painted plate, reporting a loss on a detail that had collected
        /// nothing. The rule the founder set: no ring while the fill is zero.
        ///
        /// Checked on the tutorial's crank beat because that is the state that produced it — a detail
        /// noticed, the handle untouched — and then the other half: the ring HAS to appear the moment
        /// the handle earns it, or this becomes a fix that deletes the crank's only feedback.
        /// </summary>
        [UnityTest]
        public IEnumerator TheProgressRing_StaysOffTheFrame_UntilThereIsProgressToShow()
        {
            TuningConfig.CollectSeconds = 10f;   // slow enough that «немного покрутили» is a real state

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.NoticeSomething(fake, screen);
            yield return GameTestHarness.Frames(1);

            int index = screen.Runtime.NoticedIndex;
            Assert.GreaterOrEqual(index, 0, "Тест бессмыслен, пока ничего не замечено.");
            Assert.AreEqual(0f, screen.Runtime.DisplayProgress, 1e-3f,
                "Ручку никто не крутил — заливка обязана быть нулевой.");

            DesignStage stage = StandTestHarness.Stage();
            RectTransform ring = StandTestHarness.Find(stage, "Ring_" + screen.Level.Details[index].Name);

            Assert.IsFalse(ring.gameObject.activeInHierarchy,
                "Кольцо прогресса нарисовано при нулевой заливке — это красная риска на плите, " +
                "а не показание.");

            yield return GameTestHarness.CrankUntil(fake,
                () => screen.Runtime.DisplayProgress > 0.1f, "ручка набрала заметную заливку");
            yield return GameTestHarness.Frames(1);

            StandTestHarness.AssertVisible(ring, "кольцо прогресса под ручкой");

            LogAssert.NoUnexpectedReceived();
        }

        private static float MaxSweepStrength(LevelScreen screen)
        {
            float loudest = 0f;
            foreach (UnityEngine.UI.Image detail in screen.View.DetailImages)
                if (detail.material != null)
                    loudest = Mathf.Max(loudest, detail.material.GetFloat("_SweepStrength"));
            return loudest;
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
