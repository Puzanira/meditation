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

            // The drop's own render, not a fallback plate: a missing sprite would leave a flat sky
            // that looks like a title and proves nothing.
            var title = (TitleScreen)flow.Screen;
            Assert.IsNotNull(title.Background.sprite, "Титул рисуется не картинкой дропа.");

            // …and the drawn НАЧАТЬ button is GONE (founder, playtest 2026-09-22). A button on a
            // cabinet that has nothing to press it with is an instruction to do the wrong thing, and
            // she stood in front of this screen looking for the mouse.
            Assert.IsNull(StandTestHarness.FindOrNull(stage, "StartButton"),
                "Кнопка НАЧАТЬ снова на титуле — её убрали решением founder 2026-09-22.");

            // What is left in its place is an ANCHOR: the render was composed with a hole at these
            // coordinates and the designer's handle animation is what goes into it. It must stay empty
            // — an anchor that grew an Image again is the button back under another name.
            Assert.IsNotNull(title.StartAnchor, "Якорь под анимацию Кати не построен.");
            Assert.AreEqual(TitleScreen.ButtonCentre.x,
                stage.DesignRectOf(title.StartAnchor).center.x, 2f, "Якорь не по центру.");
            Assert.AreEqual(TitleScreen.ButtonCentre.y,
                stage.DesignRectOf(title.StartAnchor).center.y, 2f, "Якорь не на месте кнопки.");
            Assert.IsNull(title.StartAnchor.GetComponent<UnityEngine.UI.Graphic>(),
                "Якорь под анимацию снова что-то рисует — это вернувшаяся кнопка.");
            Assert.AreEqual(0, title.StartAnchor.childCount,
                "В якоре под анимацию что-то построено — он обязан приезжать пустым.");

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
        /// what withdrawn ones said («Крути ручку, чтобы начать», «Тряси джойстик!»), because they are
        /// answers to the same questions; the difference is a word — the panel calls that control a
        /// крутилка, and the ручка wording went back into Withdrawn on 2026-09-22 when the founder
        /// said so. A live line that drifts back into being a withdrawn one is the mistake this
        /// catches, and «ручка» proves how plausible it is: it has now been live twice.
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

            // …and it names it by the PANEL's word. «Крутилка», not «ручка» (founder, 2026-09-22;
            // system/CONTROLS_BRIEF.md). The negative half is the whole point: the old wording is a
            // withdrawn line now, and a caption that drifts back to it is naming a control the cabinet
            // does not have.
            StringAssert.Contains("крутилку", title.StartLabel.text,
                "Подпись не называет крутилку, а старт — это она.");
            StringAssert.DoesNotContain("ручку", title.StartLabel.text,
                "Титул снова зовёт крутилку «ручкой» — на пульте такого органа нет.");

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

            // Beat 1 «наводи»: nothing is noticed FOR the player any more — the gaze is what the beat
            // asks for, and until it lands the level is a still picture with one sentence on it.
            Assert.AreEqual(TutorialBeat.Aim, screen.Beat, "Обучение обязано начинаться с наводки.");
            Assert.AreEqual(-1, screen.Runtime.NoticedIndex,
                "Первый бит просит НАВЕСТИСЬ — замечать деталь за игрока нельзя.");
            AssertBeatPlateIs(screen, GameTexts.BeatAim, "наводи");

            yield return GameTestHarness.Idle(fake, 30);
            Assert.AreEqual(0, screen.Runtime.Field.Thoughts.Count,
                "До отбитой мысли волны спавниться не должны.");

            // Beat 2 «крути крутилку и тащи»: one sentence for both hands. The two drawn buttons that
            // stood here — «КРУТИ РУЧКУ» at the thread and «ТАЩИ» walking beside the travelling detail
            // — left the teaching screens on 2026-09-22 (founder); the negative check that they stay
            // gone is TheTutorial_DrawsNothingButItsPlates.
            yield return GameTestHarness.NoticeSomething(fake, screen);
            Assert.AreEqual(TutorialBeat.Crank, screen.Beat, "После наводки должен идти бит «крути».");
            AssertBeatPlateIs(screen, GameTexts.BeatCollect, "крути крутилку и тащи");

            // …and the same check against the DETAIL, which is the return of the design skeptic on
            // 2026-09-22 (blocker Б1): the plate was placed clear of a 160×160 box at the detail's
            // STARTING position, and the detail then drove up its thread and under it — 16 px of gap
            // on frame 04 with the nose of the aeroplane sticking out. A plate that never moves has to
            // be placed against the whole travel, not against a point of it
            // (<c>LevelScreen.AddDetailTrack</c>).
            // …and it runs the WHOLE travel, not a fixed 45 frames of it. 45 frames of a batch run is
            // three quarters of a second, i.e. the first eighth of the thread — the detail had not yet
            // reached the plate when the loop gave up, which is how frame 04 went to the gate with the
            // aeroplane buried under the sentence.
            int frames = 0;
            float deadline = Time.realtimeSinceStartup + GameTestHarness.DefaultPatienceSeconds;
            while (screen.Beat == TutorialBeat.Crank && Time.realtimeSinceStartup < deadline)
            {
                fake.Next = new BackendSnapshot { CrankDeltaDegrees = GameTestHarness.CrankPerFrame };
                yield return null;
                frames++;
                if (screen.Beat != TutorialBeat.Crank) break;

                Rect plate = screen.View.BeatPlate.CardRect;
                int riding = screen.Runtime.NoticedIndex;
                Assert.GreaterOrEqual(riding, 0, "Едущая деталь потерялась посреди бита.");
                Rect sprite = StandTestHarness.Stage().DesignRectOf(
                    screen.View.DetailImages[riding].rectTransform);
                Assert.IsFalse(StandTestHarness.Overlaps(plate, sprite),
                    "Подпись бита сбора легла на едущую деталь «" +
                    screen.Level.Details[riding].Name + "» на прогрессе " +
                    screen.Runtime.Collector.Progress01.ToString("0.00") +
                    ": плашка " + plate + ", деталь " + sprite +
                    " (блокер Б1, возврат дизайн-скептика 2026-09-22).");
            }
            fake.Next = new BackendSnapshot();

            Assert.Greater(frames, 45,
                "Бит сбора кончился за " + frames + " кадров — путь детали толком не проверен.");

            // Beat 3: the first detail lands, one thought appears ON the next one, and the beat says
            // what the blob IS and where the hand goes. The detail that ends the beat is the one the
            // loop above has just cranked all the way in.
            Assert.AreEqual(TutorialBeat.Swipe, screen.Beat, "После первой детали должен идти бит отгона.");
            Assert.AreEqual(1, screen.Runtime.Field.Thoughts.Count, "Должна быть ровно одна мысль.");
            AssertBeatPlateIs(screen, GameTexts.SwipeHint, "отгон");

            int covered = screen.SwipeBeatDetailIndex;
            Assert.GreaterOrEqual(covered, 0, "Мысль обучения села не на деталь.");
            Assert.IsTrue(screen.Runtime.Field.IsCovered(screen.Level.Details[covered].Home),
                "Мысль обучения не накрывает деталь, сбор которой должна блокировать.");

            // Beaten off — the waves start, and the FOURTH beat says what they are for (founder,
            // 2026-09-22). It blocks nothing: the plate is up and the level is already running
            // underneath it.
            yield return GameTestHarness.SwipeUntil(fake,
                () => screen.Runtime.Field.Thoughts.Count == 0, "мысль отбита");
            yield return GameTestHarness.Frames(3);

            Assert.AreEqual(TutorialBeat.Whole, screen.Beat, "Четвёртый бит обучения не начался.");
            AssertBeatPlateIs(screen, GameTexts.BeatWhole, "весь уровень");

            yield return GameTestHarness.Until(() => screen.Runtime.Field.Thoughts.Count > 0,
                "волны пошли после обучения");

            // …and then it leaves on its own clock and the lesson is over for good.
            yield return GameTestHarness.Until(() => screen.Beat == TutorialBeat.Done,
                "обучение закончилось", LevelScreen.WholeBeatSeconds + 6f);
            Assert.IsFalse(screen.View.BeatPlate.IsShown,
                "Обучение кончилось — плашка обязана уйти с экрана.");

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
        /// this says the level actually placed the hint where the rule put it. The frame the design
        /// gate judged had «КРУТИ РУЧКУ» lying across the whole shark fin and the left edge of the
        /// bucket, and every arithmetic test in the suite was green at the time.
        ///
        /// It used to be checked on the drawn BUTTON of each beat. The buttons are off the teaching
        /// screens (founder, 2026-09-22) and the plate is the only thing left, so the guard moves to
        /// the plate — which is what blocker Б1 was really about either way: «деталь видна».
        /// </summary>
        [UnityTest]
        public IEnumerator TheTeachingPlates_CoverNoDetail_NoVessel_AndNoHudWidget()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelDefinition level = screen.Level;

            Assert.AreEqual(TutorialBeat.Aim, screen.Beat);
            AssertHintPlateIsClear(screen.View.BeatPlate.Rect, level, GameTexts.BeatAim);

            yield return GameTestHarness.NoticeSomething(fake, screen);
            Assert.AreEqual(TutorialBeat.Crank, screen.Beat);
            AssertHintPlateIsClear(screen.View.BeatPlate.Rect, level, GameTexts.BeatCollect);

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// …and a teaching beat draws NOTHING ELSE: no caps button, no turquoise arrow, no pictogram.
        ///
        /// The negative half of the founder's order of 2026-09-22 — «убрать стрелки все с экранов
        /// обучений… остальные элементы подсказок убрать» — and the half that can rot silently, because
        /// a hint that comes back comes back as «just one arrow, to make it clearer». So the claim is
        /// made against the SCENE and not against the classes that were deleted: every beat of the
        /// lesson is walked, and the only thing the hint layer is allowed to have drawing components on
        /// it is the plate with its two labels.
        ///
        /// The MOUNT is asserted at the same time, and asserted to be empty (<c>LevelView.BeatAnchor</c>):
        /// it is where Катя's animations will stand, and an anchor that quietly grew an Image again
        /// would be the button back — the same trap the title's НАЧАТЬ left behind.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTutorial_DrawsNothingButItsPlates()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            AssertOnlyThePlateDraws(screen, "бит наводки");

            yield return GameTestHarness.NoticeSomething(fake, screen);
            Assert.AreEqual(TutorialBeat.Crank, screen.Beat);
            AssertOnlyThePlateDraws(screen, "бит сбора");

            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.Idle(fake, 4);
            Assert.AreEqual(TutorialBeat.Swipe, screen.Beat);
            AssertOnlyThePlateDraws(screen, "бит отгона");

            // …and the отгон's mount is on the sensor panel, which is the one thing this beat is about
            // that is not on the screen at all.
            Vector2 mount = StandTestHarness.Stage().DesignRectOf(screen.View.BeatAnchor).center;
            Assert.Less(Vector2.Distance(mount, LevelScreen.SensorsCue), 1.5f,
                "Якорь бита отгона уехал с панели датчиков (" + mount +
                " против " + LevelScreen.SensorsCue + ") — анимациям Кати вставать некуда.");

            yield return GameTestHarness.SwipeUntil(fake,
                () => screen.Runtime.Field.Thoughts.Count == 0, "мысль отбита");
            yield return GameTestHarness.Frames(3);
            Assert.AreEqual(TutorialBeat.Whole, screen.Beat);
            AssertOnlyThePlateDraws(screen, "финальный бит");

            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertOnlyThePlateDraws(LevelScreen screen, string beat)
        {
            HintPlate plate = screen.View.BeatPlate;
            Assert.IsTrue(plate.IsShown, beat + ": плашка обучения не показана.");

            // The mount draws nothing at all — no Image, no Text, no Graphic of any kind.
            foreach (UnityEngine.UI.Graphic drawn in
                     screen.View.BeatAnchor.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Assert.Fail(beat + ": на якоре бита снова что-то рисуется (" + drawn.name +
                    ", " + drawn.GetType().Name + ") — стрелки и кнопки убраны решением founder 2026-09-22.");

            // …and on the hint layer itself, the only graphics are the plate's own three: the rule, the
            // slab inside it, and the two labels.
            var allowed = new System.Collections.Generic.HashSet<Transform> { plate.Rect };
            foreach (Transform child in plate.Rect) allowed.Add(child);

            foreach (UnityEngine.UI.Graphic drawn in
                     screen.View.MessageLayer.GetComponentsInChildren<UnityEngine.UI.Graphic>(false))
            {
                if (allowed.Contains(drawn.transform)) continue;
                Assert.Fail(beat + ": поверх обучения рисуется «" + drawn.name + "» (" +
                    drawn.GetType().Name + ") — на экранах обучения остаются только плашки.");
            }
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
        /// The words of the отгон beat: «Это навязчивые мысли…» on a teaching plate, beside the
        /// thought, off the art.
        ///
        /// The beat had no words at all until 2026-08-08 — the drop shipped no «ТРЯСИ» — and the
        /// founder, playing it, did not know what the arrow was asking her to do. An arrow that ends on
        /// a piece of furniture names a PLACE; the sentence names the movement, and the movement is the
        /// whole mechanic. On 2026-09-22 the arrow went too, and the sentence is what the beat is.
        ///
        /// Two tests stood beside this one and are gone with the pictures they measured: the отгон
        /// stroke's own geometry (turquoise, clear of everything the thought paints, always landing on
        /// the same point of the sensor panel) and «ТАЩИ» riding the travelling detail off the ring and
        /// off the thread. What survives of both is in this file — the plate covers no art, and
        /// <see cref="TheTutorial_DrawsNothingButItsPlates"/> holds the line that nothing else is
        /// drawn.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSwipeBeat_SaysWhatToDo_ClearOfTheArt()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelDefinition level = screen.Level;

            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.Idle(fake, 4);
            Assert.AreEqual(TutorialBeat.Swipe, screen.Beat, "Бит отгона не начался.");

            HintPlate card = screen.View.BeatPlate;
            Assert.IsTrue(card.IsShown, "На бите отгона нет подписи — игрок снова видит одну стрелку.");
            Assert.AreEqual(GameTexts.SwipeHint, card.Label.text,
                "Подпись бита отгона пишется мимо реестра GameTexts.");
            Assert.AreEqual(GameTexts.SwipeHintOnDesk, card.Bracket.text,
                "ПК-скобка бита отгона пишется мимо реестра GameTexts.");
            StandTestHarness.AssertVisible(card.Rect, "Плашка «" + GameTexts.SwipeHint + "»");

            Rect plate = StandTestHarness.Stage().DesignRectOf(card.Rect);
            foreach (ArtDetail detail in level.Details)
                Assert.IsFalse(StandTestHarness.Overlaps(plate, LevelCatalog.RectOf(detail)),
                    "Подпись отгона накрыла деталь «" + detail.Name + "».");
            Assert.IsFalse(StandTestHarness.Overlaps(plate, LevelCatalog.VesselRectOf(level)),
                "Подпись отгона накрыла сосуд.");
            Assert.IsFalse(StandTestHarness.Overlaps(plate, LevelCatalog.VesselBarRectOf(level)),
                "Подпись отгона накрыла полосу наполнения сосуда.");
            // The dial itself went with the founder's list of 2026-09-22, but its corner of the plate
            // stays reserved: it is the one part of each composition the art drop kept empty.
            Assert.IsFalse(StandTestHarness.Overlaps(plate, LevelCatalog.CrankRectOf(level)),
                "Подпись отгона встала в угол, который дроп оставил пустым под индикатор.");

            // …and it is clear of the thought it is ABOUT — of everything the thought paints, pips
            // and their discs included. The pips hang BELOW the blob's rectangle, and until
            // 2026-09-22 the only thing that pushed the plate off them was the arrow corridor the
            // plate had to dodge. The arrow is gone; the measurement is not (LevelScreen.PaintedRectOf).
            Assert.AreEqual(1, screen.Runtime.Field.Thoughts.Count);
            Rect painted = LevelScreen.PaintedRectOf(screen.Runtime.Field.Thoughts[0]);
            Assert.IsFalse(StandTestHarness.Overlaps(plate, painted),
                "Подпись отгона легла на саму мысль (плашка " + plate + ", мысль с пипсами " +
                painted + ") — пипсы рисуются НИЖЕ её прямоугольника.");

            // …and the beat gives way to the fourth one, which is new on 2026-09-22: the goal, over a
            // level that is already being played. The отгон's own sentence must be gone from it — one
            // plate, one beat — and the new sentence up in its place.
            yield return GameTestHarness.SwipeUntil(fake,
                () => screen.Runtime.Field.Thoughts.Count == 0, "мысль отбита");
            yield return GameTestHarness.Frames(3);
            Assert.AreEqual(TutorialBeat.Whole, screen.Beat,
                "После отбитой мысли не начался финальный бит обучения.");
            Assert.AreEqual(GameTexts.BeatWhole, card.Label.text,
                "Финальный бит не сказал, ради чего всё это.");
            Assert.IsTrue(card.IsShown, "Финальный бит обучения ничего не показывает.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// …and it is painted in the DROP's hint style, not in the greybox stand's.
        ///
        /// Blocker Б1 of the design gate, 2026-08-08. The lesson is four beats — НАВОДИ, КРУТИ РУЧКУ,
        /// ТАЩИ, and this one — and three of them are the designer's PNGs: a dark slab RGB(35, 52, 65),
        /// white caps set with tracking, a 2 px rule, square corners. The fourth was
        /// <see cref="HintCard"/>: white paper, rounded, a 4 px coloured border, 44 pt mixed case. One
        /// lesson cannot be written in two hands.
        ///
        /// What is checked here is the CONSTRUCTION (the composited pixels are
        /// <c>GameScreenshotTests.AssertTheSwipePlateIsDark</c> on frame 05), plus the two things a
        /// pixel measurement cannot see: that the greybox card is not on the game's screen at all any
        /// more, and that the line still fits the plate the placement search was given.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSwipeHint_IsTheDropsPlate_NotTheStandsWhitePaper()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.Idle(fake, 4);
            Assert.AreEqual(TutorialBeat.Swipe, screen.Beat, "Бит отгона не начался.");

            HintPlate plate = screen.View.BeatPlate;
            Assert.IsTrue(plate.IsShown, "Подписи отгона нет на экране.");

            // The slab, and the rule around it: dark fill, 2 px, the hand's own colour.
            Assert.Less(LevelOneData.RelativeLuminance(plate.Fill.color), 0.1f,
                "Заливка подсказки отгона светлая — это снова бумага грейбокс-стенда.");
            Assert.AreEqual(HintCard.ToneColour(HintTone.Swipe), plate.Plate.color,
                "Рамка подсказки потеряла цвет своей руки (бирюза отгона).");
            Assert.AreEqual(HintPlate.BorderWidth, plate.Fill.rectTransform.offsetMin.x, 0.01f,
                "Рамка подсказки не 2 px, как у нарисованных кнопок дропа.");

            // …white ink, and the registry's own line, verbatim.
            //
            // Not caps and not разрядка any more, and that is the founder's change of 2026-09-22, not
            // a regression of Б1: the beat's text went from a three-word label to a sentence, and caps
            // with letter-spacing over a sentence shouts. Б1 was about the SLAB — its darkness, its
            // 2 px rule, its square corners — and all three are asserted above.
            Assert.AreEqual(Color.white, plate.Label.color, "Текст подсказки не белый.");
            Assert.AreEqual(GameTexts.SwipeHint, plate.Label.text,
                "На плашке не та строка реестра.");
            Assert.AreEqual(plate.Label.text, plate.Label.text.TrimEnd(),
                "Строка на плашке набрана с висящим пробелом.");
            Assert.Greater(plate.Bracket.color.a, 0.3f, "ПК-скобка невидима.");
            Assert.Less(plate.Bracket.color.a, plate.Label.color.a,
                "ПК-скобка не приглушена — она обязана читаться как сноска.");
            Assert.Less(plate.Bracket.fontSize, plate.Label.fontSize,
                "ПК-скобка набрана не мельче основной строки.");

            // …and the line fits the plate the placement search was handed, so the rectangle the
            // overlap checks reason about is the rectangle on screen — in BOTH directions now that the
            // text wraps.
            Assert.AreEqual(HintPlate.SizeFor(GameTexts.SwipeHint, true).x, plate.Rect.sizeDelta.x, 0.5f,
                "Плашку нарисовали не той ширины, под которую искали место.");
            Assert.AreEqual(HintPlate.SizeFor(GameTexts.SwipeHint, true).y, plate.Rect.sizeDelta.y, 0.5f,
                "Плашку нарисовали не той высоты, под которую искали место.");
            Assert.LessOrEqual(plate.Label.preferredHeight, plate.Label.rectTransform.rect.height + 1f,
                "Набранная строка не помещается по высоте — оценка числа строк разошлась с набором.");

            // The greybox card is gone from the GAME. (On the stand it stays: that composition is
            // greybox all the way down, and there the white paper is the right paper.)
            Assert.IsNull(StandTestHarness.FindOrNull(StandTestHarness.Stage(), "HintCard"),
                "Белая карточка грейбокс-стенда снова рисуется в игре.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// A beat's hint is one plate with one sentence on it, and the sentence comes from the registry
        /// — so «which hint» is a question about WHICH LINE, and it is asked against
        /// <see cref="GameTexts"/> rather than against a literal.
        /// </summary>
        private static void AssertBeatPlateIs(LevelScreen screen, string line, string beat)
        {
            Assert.IsTrue(screen.View.BeatPlate.IsShown, "Плашки бита «" + beat + "» нет на экране.");
            StandTestHarness.AssertVisible(screen.View.BeatPlate.Rect, "Плашка бита «" + beat + "»");
            Assert.AreEqual(line, screen.View.BeatPlate.Label.text,
                "Подпись бита «" + beat + "» пишется мимо реестра GameTexts.");
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
            Assert.IsFalse(again.View.BeatPlate.IsShown, "На рестарте подсказок быть не должно.");

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
            Assert.IsFalse(screen.View.BeatPlate.IsShown,
                "Уровень " + (levelIndex + 1) + ": плашек обучения быть не должно.");
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
                () => screen.Runtime.DisplayProgress > 0.1f, "крутилка набрала заметную заливку");
            yield return GameTestHarness.Frames(1);

            StandTestHarness.AssertVisible(ring, "кольцо прогресса под крутилкой");

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
