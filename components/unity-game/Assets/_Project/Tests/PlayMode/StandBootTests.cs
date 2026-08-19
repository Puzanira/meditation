using System.Collections;
using System.IO;
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
using UnityEngine.UI;

namespace Meditation.Tests
{
    /// <summary>
    /// Done contract §1, §8, §9: every stand scene boots clean, the screen is laid out exactly as
    /// SCREENS.md says, and the cabinet's "в меню" button gets out of any scenette instantly.
    /// </summary>
    public class StandBootTests
    {
        [SetUp]
        public void SetUp()
        {
            // The panel now persists on every interaction, and these tests really do drag its sliders.
            StandTestHarness.IsolateTuningFile();
            TuningConfig.ResetToDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            StandTestHarness.ReleaseTuningFile();
            TuningConfig.ResetToDefaults();
        }

        [UnityTest]
        public IEnumerator EveryStandScene_Boots_WithoutConsoleErrors(
            [ValueSource(typeof(PreviewScenes), nameof(PreviewScenes.All))] string sceneName)
        {
            yield return StandTestHarness.LoadScene(sceneName);

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);

            DesignStage stage = StandTestHarness.Stage();
            Assert.IsNotNull(stage.Canvas, "The scene must have a canvas.");
            Assert.IsNotNull(Object.FindAnyObjectByType<ArcadeInputRunner>(),
                "Input must come from the shared arcade package.");
            Assert.IsNotNull(Object.FindAnyObjectByType<MenuButtonExit>(),
                "Every screen must honour the cabinet's menu button.");

            // The tuning panel is mouse-driven: without a live event module its sliders are dead.
            var eventSystem = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            Assert.IsNotNull(eventSystem, "The scene needs an EventSystem for the tuning panel.");
            Assert.IsTrue(eventSystem.isActiveAndEnabled, "The EventSystem must be active.");

            yield return null;
            yield return null;

            Assert.IsNotNull(eventSystem.currentInputModule,
                "The EventSystem has no input module — the tuning panel would not react to the mouse.");
            Assert.IsTrue(eventSystem.currentInputModule.IsModuleSupported(),
                "The input module does not support this platform.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LevelScreen_MatchesTheScreensSpecLayout()
        {
            yield return StandTestHarness.LoadScene(PreviewScenes.FullLevel);
            DesignStage stage = StandTestHarness.Stage();

            // SCREENS.md §S3 "Зоны": the numbers below are the spec's, verbatim.
            StandTestHarness.AssertDesignRect(stage, "Ground", new Rect(0f, 810f, 1920f, 270f));
            StandTestHarness.AssertDesignRect(stage, "Vessel", new Rect(840f, 855f, 240f, 150f));
            StandTestHarness.AssertDesignRect(stage, "CrankDial", new Rect(70f, 880f, 140f, 140f));

            // The HUD's row of detail slots left the stand on 2026-08-08 (founder: «убрать ряд
            // совсем»), same as the game — it must not quietly come back.
            Assert.IsNull(StandTestHarness.FindOrNull(stage, "Slot1"),
                "Ряд слотов вернулся на стенд — его вывели решением founder 2026-08-08.");
            StandTestHarness.AssertDesignRect(stage, "Building1", new Rect(100f, 250f, 230f, 560f));

            // Details sit on their authored positions (they breathe, so only the centre is pinned).
            Vector2 dandelion = StandTestHarness.DesignCentre(stage, "Detail_одуванчик");
            Assert.AreEqual(180f, dandelion.x, 2f, "Одуванчик must stay at its authored x.");
            Assert.AreEqual(760f, dandelion.y, 2f, "Одуванчик must stay at its authored y.");

            Vector2 sparrow = StandTestHarness.DesignCentre(stage, "Detail_воробей");
            Assert.AreEqual(1330f, sparrow.x, 2f);
            Assert.AreEqual(300f, sparrow.y, 2f);

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Menu_ListsFourScenettes_AndTheJoystickMovesTheSelection()
        {
            yield return StandTestHarness.LoadScene(PreviewScenes.Menu);
            FakeBackend fake = StandTestHarness.TakeOverInput();
            DesignStage stage = StandTestHarness.Stage();

            var menu = Object.FindAnyObjectByType<PreviewMenuController>();
            Assert.IsNotNull(menu, "The entry scene must be the stand menu.");

            for (int i = 1; i <= 4; i++)
                StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "Card" + i), "Card" + i);

            Assert.AreEqual(0, menu.Selected);

            // Push the stick down: the highlight moves to the next scenette.
            fake.Next = new BackendSnapshot { Joystick = new Vector2(0f, -1f) };
            yield return null;
            yield return null;
            Assert.AreEqual(1, menu.Selected, "Joystick down must move the selection.");

            fake.Next = new BackendSnapshot();
            yield return null;
            fake.Next = new BackendSnapshot { Joystick = new Vector2(0f, 1f) };
            yield return null;
            yield return null;
            Assert.AreEqual(0, menu.Selected, "Joystick up must move it back.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Menu_GreenButton_OpensTheHighlightedScenette_AndItComesBack()
        {
            yield return StandTestHarness.LoadScene(PreviewScenes.Menu);
            FakeBackend fake = StandTestHarness.TakeOverInput();

            var menu = Object.FindAnyObjectByType<PreviewMenuController>();
            fake.Next = new BackendSnapshot { Joystick = new Vector2(0f, -1f) };
            yield return null;
            yield return null;
            Assert.AreEqual(1, menu.Selected);

            fake.Next = new BackendSnapshot { GreenHeld = true };
            yield return null;
            yield return null;
            yield return null;

            Assert.AreEqual(PreviewScenes.ShakeAway, SceneManager.GetActiveScene().name,
                "The green button must open the highlighted scenette.");
            Assert.IsNotNull(Object.FindAnyObjectByType<Scenes.Scene2ShakeAway>());

            // …and the menu button brings the stand back without restarting the build.
            fake = StandTestHarness.TakeOverInput();
            fake.Next = new BackendSnapshot { MenuHeld = true };
            yield return null;
            yield return null;
            yield return null;

            Assert.AreEqual(PreviewScenes.Menu, SceneManager.GetActiveScene().name);

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MenuButton_LeavesAnyScenette_ImmediatelyAndCleanly()
        {
            yield return StandTestHarness.LoadScene(PreviewScenes.TwoHands);
            FakeBackend fake = StandTestHarness.TakeOverInput();

            bool exitRaised = false;
            void OnExit() => exitRaised = true;
            PreviewStandNav.ExitRequested += OnExit;

            try
            {
                fake.Next = new BackendSnapshot { MenuHeld = true };
                yield return null;
                yield return null;
                yield return null;

                Assert.IsTrue(exitRaised, "The cabinet hub's exit hook must fire.");
                Assert.AreEqual(PreviewScenes.Menu, SceneManager.GetActiveScene().name,
                    "«В меню» must land back in the stand menu, with no confirmation step.");
                Assert.IsNotNull(Object.FindAnyObjectByType<PreviewMenuController>(),
                    "…and the menu must be alive again straight away.");

                // Nothing of the scenette survived the exit — no leftovers, no DontDestroyOnLoad state.
                Assert.IsNull(Object.FindAnyObjectByType<Scenes.Scene3TwoHands>(),
                    "The scenette must be gone after the exit.");
            }
            finally
            {
                PreviewStandNav.ExitRequested -= OnExit;
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TuningValues_SurviveSwitchingBetweenScenettes()
        {
            yield return StandTestHarness.LoadScene(PreviewScenes.CrankCollect);
            TuningConfig.CollectSeconds = 9.5f;
            TuningConfig.DurabilityWeak = 7;

            yield return StandTestHarness.LoadScene(PreviewScenes.ShakeAway);
            Assert.AreEqual(9.5f, TuningConfig.CollectSeconds, 1e-3f,
                "Tuned values must survive a scenette change (done contract §7).");
            Assert.AreEqual(7, TuningConfig.DurabilityWeak);

            yield return StandTestHarness.LoadScene(PreviewScenes.FullLevel);
            Assert.AreEqual(9.5f, TuningConfig.CollectSeconds, 1e-3f);
            Assert.AreEqual(7, TuningConfig.DurabilityWeak);

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TuningPanel_IsOnScreen_AndCollapsesToGiveTheFrameBack()
        {
            // The panel ships COLLAPSED (TuningConfig.Defaults.PanelVisible — the game opens as a game
            // on the cabinet), so a test about what the OPEN panel does has to open it, exactly like
            // its siblings below. What is being pinned here is the collapse, not the starting state.
            TuningConfig.PanelVisible = true;
            yield return StandTestHarness.LoadScene(PreviewScenes.CrankCollect);
            DesignStage stage = StandTestHarness.Stage();

            var panel = Object.FindAnyObjectByType<TuningPanel>();
            Assert.IsNotNull(panel, "Every scenette must show its [tune]/[toggle] panel.");

            RectTransform background = StandTestHarness.Find(stage, "Background");
            StandTestHarness.AssertVisible(background, "tuning panel background");
            Assert.Greater(stage.ReservedRight, 0f, "The visible panel must reserve its own strip.");

            float withPanel = stage.Scale;

            panel.SetVisible(false);
            yield return null;

            LogAssert.NoUnexpectedReceived();
            Assert.GreaterOrEqual(stage.Scale, withPanel,
                "Collapsing the panel must give the composition the whole window back.");
        }

        /// <summary>
        /// The founder's own Game view (~1346×636) and a smaller one still. The panel is full-height,
        /// so its layout depends only on the window's height — which is exactly what 1080-shaped
        /// authoring got wrong.
        /// </summary>
        private static readonly Vector2Int[] TightViewports =
        {
            new Vector2Int(1346, 636),
            new Vector2Int(1024, 576)
        };

        /// <summary>
        /// The bug from the founder's playtest: the parameter rows drew straight through the live
        /// readings block — «динамо: °/с», «кручение», «стик», «собрано 0/5», «статус» — two layers of
        /// text on top of each other, unreadable. It happened in EVERY scenette, because the readings
        /// were pinned to the panel's bottom edge while the rows ran down from its top, so any window
        /// shorter than the 1080 the layout was authored at let the two meet.
        ///
        /// The check is on DRAWN rectangles, clipped exactly as the mask clips them — not on activeSelf,
        /// which would have called this broken screen perfectly healthy.
        /// </summary>
        [UnityTest]
        public IEnumerator TuningPanel_RowsNeverDrawOverTheReadings_OnASmallViewport(
            [ValueSource(typeof(PreviewScenes), nameof(PreviewScenes.Scenettes))] string sceneName,
            [ValueSource(nameof(TightViewports))] Vector2Int viewport)
        {
            TuningConfig.PanelVisible = true;
            yield return StandTestHarness.LoadScene(sceneName);
            DesignStage stage = StandTestHarness.Stage();
            var panel = Object.FindAnyObjectByType<TuningPanel>();
            Assert.IsNotNull(panel, sceneName + " must show its [tune]/[toggle] panel.");

            Camera cam = StandTestHarness.ResizeCanvas(stage, viewport.x, viewport.y);
            try
            {
                yield return null;             // one Update: the panel re-measures its bands
                Canvas.ForceUpdateCanvases();

                Rect window = StandTestHarness.WorldRectOf(panel.RowsViewport);
                Rect readings = StandTestHarness.WorldRectOf(panel.ReadoutZone);

                Assert.Greater(window.height, 1f, "The rows have no window left to live in.");
                Assert.Greater(readings.height, 1f, "The readings block collapsed to nothing.");
                Assert.IsFalse(StandTestHarness.Overlaps(window, readings),
                    sceneName + " @ " + viewport.x + "×" + viewport.y +
                    ": the rows' window and the readings block occupy the same pixels.");

                int drawn = 0;
                for (int i = 0; i < panel.RowWidgets.Count; i++)
                {
                    RectTransform row = panel.RowWidgets[i];

                    // What the founder actually sees: the row's rectangle after the viewport clips it.
                    Rect visible = StandTestHarness.ClippedBy(StandTestHarness.WorldRectOf(row), window);
                    if (visible.width <= 0f || visible.height <= 0f) continue;   // scrolled out of sight

                    drawn++;
                    Assert.IsFalse(StandTestHarness.Overlaps(visible, readings),
                        sceneName + " @ " + viewport.x + "×" + viewport.y + ": row '" + row.name +
                        "' is drawn over the live readings.");
                }

                Assert.Greater(drawn, 0,
                    sceneName + ": not a single parameter row is visible — the panel is useless.");

                // Whatever does not fit has to be reachable, not lost: the list scrolls.
                Assert.IsNotNull(panel.Scroll, "The rows must live in a ScrollRect.");
                Assert.IsTrue(panel.Scroll.vertical, "The rows must scroll vertically.");
                if (panel.RowsContent.rect.height > window.height + 1f)
                    Assert.AreSame(panel.RowsContent, panel.Scroll.content,
                        "A list taller than its window must be the ScrollRect's content.");

                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                StandTestHarness.RestoreCanvas(stage, cam);
            }
        }

        /// <summary>The readings must still be readable, not squeezed to a sliver, at the same sizes.</summary>
        [UnityTest]
        public IEnumerator TuningPanel_KeepsTheReadingsBlockOnScreen_OnASmallViewport(
            [ValueSource(nameof(TightViewports))] Vector2Int viewport)
        {
            TuningConfig.PanelVisible = true;
            yield return StandTestHarness.LoadScene(PreviewScenes.FullLevel);
            DesignStage stage = StandTestHarness.Stage();
            var panel = Object.FindAnyObjectByType<TuningPanel>();

            Camera cam = StandTestHarness.ResizeCanvas(stage, viewport.x, viewport.y);
            try
            {
                yield return null;
                Canvas.ForceUpdateCanvases();

                RectTransform readout = StandTestHarness.Find(stage, "Readout");
                Rect drawn = StandTestHarness.WorldRectOf(readout);
                Rect panelRect = StandTestHarness.WorldRectOf((RectTransform)panel.transform);

                Assert.Greater(StandTestHarness.ToPanelPixels(panel, drawn.height), 80f,
                    "The readings block has to stay tall enough to read the numbers in.");
                Assert.GreaterOrEqual(drawn.yMin, panelRect.yMin - 1f,
                    "The readings block hangs below the panel.");
                Assert.LessOrEqual(drawn.yMax, panelRect.yMax + 1f,
                    "The readings block sticks out above the panel.");

                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                StandTestHarness.RestoreCanvas(stage, cam);
            }
        }

        [UnityTest]
        public IEnumerator TuningPanel_ChangingASliderOnScreen_ChangesTheMechanicImmediately()
        {
            // …and the same here: the founder drags a slider on the OPEN panel, so the test opens it.
            TuningConfig.PanelVisible = true;
            yield return StandTestHarness.LoadScene(PreviewScenes.CrankCollect);
            FakeBackend fake = StandTestHarness.TakeOverInput();
            DesignStage stage = StandTestHarness.Stage();
            var scene = Object.FindAnyObjectByType<Scenes.Scene1CrankCollect>();

            // Drag the on-screen slider — the UI control, not TuningConfig — to its slowest setting.
            var slider = StandTestHarness.Find(stage, "Slider_время сбора детали").GetComponent<Slider>();
            Assert.IsNotNull(slider, "The collect-time row must be a real slider on screen.");
            StandTestHarness.AssertVisible(slider.GetComponent<RectTransform>(), "collect-time slider");
            slider.value = slider.maxValue;
            Assert.AreEqual(slider.maxValue, TuningConfig.CollectSeconds, 1e-2f,
                "Moving the slider must write straight into the live config.");

            fake.Next = new BackendSnapshot { CrankDeltaDegrees = 20f };
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue(scene.Collector.IsSpinning, "Precondition: the crank is turning fast enough.");

            // Same hand, same seconds — only the slider differs. Progress per second must jump.
            float slowStart = scene.Collector.Progress01;
            float until = Time.time + 0.6f;
            while (Time.time < until) yield return null;
            float slowGain = scene.Collector.Progress01 - slowStart;

            slider.value = slider.minValue;
            Assert.AreEqual(slider.minValue, TuningConfig.CollectSeconds, 1e-2f);

            float fastStart = scene.Collector.Progress01;
            until = Time.time + 0.6f;
            while (Time.time < until) yield return null;
            float fastGain = scene.Collector.Progress01 - fastStart;

            Assert.Greater(fastGain, slowGain * 2f,
                "Moving the slider must change the mechanic immediately, with no rebuild " +
                "(slow " + slowGain.ToString("0.000") + " vs fast " + fastGain.ToString("0.000") + ").");

            // A/B rows are buttons: clicking one must flip the mechanic's mode, live.
            var modeButton = StandTestHarness.Find(stage, "Choice_при остановке_1").GetComponent<Button>();
            Assert.IsNotNull(modeButton, "The stop-mode [toggle] must be clickable on screen.");
            modeButton.onClick.Invoke();
            Assert.AreEqual(CrankStopMode.Decay, TuningConfig.StopMode,
                "Clicking variant B must switch the stop mode.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The incident, reproduced end to end: tune on the panel, "close the editor" (drop every
        /// static value), start again — the numbers have to come back. Live-config asserts alone let
        /// this through once already, because the values were only ever in memory.
        /// </summary>
        [UnityTest]
        public IEnumerator TuningPanel_ValuesSurviveClosingTheEditor()
        {
            Assert.IsTrue(TuningStore.IsRedirected, "Тест обязан писать в свой файл, не в UserSettings.");
            Assert.IsFalse(File.Exists(TuningStore.FilePath), "Предусловие: сохранённого файла ещё нет.");

            yield return StandTestHarness.LoadScene(PreviewScenes.CrankCollect);
            DesignStage stage = StandTestHarness.Stage();

            var slider = StandTestHarness.Find(stage, "Slider_время сбора детали").GetComponent<Slider>();
            slider.value = slider.maxValue;
            var modeButton = StandTestHarness.Find(stage, "Choice_при остановке_1").GetComponent<Button>();
            modeButton.onClick.Invoke();

            Assert.IsTrue(File.Exists(TuningStore.FilePath),
                "Панель обязана писать значения на диск сама — иначе они умирают вместе с редактором.");

            // Everything the process remembered is gone; only the file is left.
            TuningConfig.ResetToDefaults();
            Assert.IsTrue(TuningStore.Load(), "Стенд не смог прочитать собственный файл значений.");

            Assert.AreEqual(slider.maxValue, TuningConfig.CollectSeconds, 1e-2f,
                "Значение слайдера не пережило перезапуск.");
            Assert.AreEqual(CrankStopMode.Decay, TuningConfig.StopMode,
                "Выбранный вариант тогглера не пережил перезапуск.");

            LogAssert.NoUnexpectedReceived();
        }
    }
}
