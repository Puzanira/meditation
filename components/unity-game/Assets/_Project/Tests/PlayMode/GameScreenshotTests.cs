using System.Collections;
using System.Collections.Generic;
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
    /// The design gate's evidence for the game, done contract §8. The set, `Game01…Game38`:
    ///
    /// <list type="bullet">
    /// <item>01 титул · 02 карточка уровня 1 · 30–32 карточки уровней 2–4 · 16 карточка уровня 5;</item>
    /// <item>03 обзор · 17 бит «НАВОДИ» · 04 бит «КРУТИ РУЧКУ» + «ТАЩИ» · 05 бит отгона ·
    ///       40 четвёртый бит «Заметь все объекты…» поверх уже идущей игры ·
    ///       27 мысль лопается на последнем пипсе (середина разрыва) ·
    ///       29 кадр сразу после отгона (волны пошли);</item>
    /// <item>06 игра · 18 срыв детали · 07 пик хаоса (L1) · 24 пик хаоса (L3) —
    ///       оба набраны ЖИВЫМИ волнами, не разложены сеткой, и оба на ОТГРУЖАЕМОМ пороге пика
    ///       (65 %, возврат дизайн-скептика 2026-09-22: раньше порог сдвигали в 32 и снимали его);</item>
    /// <item>09 · 10 (L3, виден индикатор сосуда) · 14 · 15 — уровни 2–5 в игре;
    ///       23 мысли на полосе переднего плана (L2);</item>
    /// <item>08 бит награды (L1) · 19–22 победы уровней 2–5 · 25 готовый экран победы;</item>
    /// <item>11 поражение · 12 ретрай под оборотами · 13 финал;</item>
    /// <item>26 луч на L5 · 28 луч на L2 — ранний уровень, полосу видно в первые минуты;</item>
    /// <item>33 неон-обводка деталей ВКЛЮЧЕНА (пара к 09, где она выключена) ·
    ///       34 разросшиеся мысли: одна свежая, одна старая — заказ founder 2026-08-07;</item>
    /// <item>35 неон-прицел на светлой шумной плите метро · 36 прицел ПОВЕРХ детали в библиотеке ·
    ///       37 · 38 крупные планы неон-обводки на скрепке и на тапках — фикс-раунд 2026-08-08.</item>
    /// </list>
    ///
    /// **Кадры игры снимаются в отгружаемом режиме выбора детали** (<c>NoticeMode.GazeJoystick</c>), с
    /// припаркованным кругом. До 2026-08-08 почти весь набор ставился в <c>FixedOrder</c> — вариант,
    /// который прицел ВЫКЛЮЧАЕТ, — и founder судила заказанную ею фичу по одному кадру из пятидесяти.
    /// FixedOrder остался там, где кадр не про выбор детали вовсе (луч, победы, поражение).
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

        // ---- claims made on the PIXELS of the frame that was just written -------------------------------

        /// <summary>
        /// How much ink a frame is STAGED to before it is shot — the model-side wait, in shares of the
        /// frame (<c>GameTestHarness.PaintedShare</c>: visible rectangles weighted by each sprite's own
        /// baked ink share).
        ///
        /// Three per cent, and the number comes from what a failure looked like: frames 06 and 09 of the
        /// 2026-08-08 gate carried 0.67 % of ink against 0.66 % on an EMPTY intro — three thoughts, all
        /// of them slivers hanging off the edge of the frame. One medium thought fully inside the frame
        /// is already about 2.8 %, so this says «at least one whole thought, or two half ones», which is
        /// the least a picture of a wave can be.
        /// </summary>
        private const float StagedThoughtInk = 0.03f;

        /// <summary>
        /// …and how much of it has to be VISIBLE on the written frame, which is a smaller number and
        /// has to be a separate one.
        ///
        /// The two are measured differently on purpose (that is the whole point of having both): the
        /// staging wait is arithmetic on rectangles and baked ink shares, while this counts pixels that
        /// actually MOVED when the thought layer was switched off. Since the halo came off on
        /// 2026-08-08 those two answers pulled apart, and in the direction the founder accepted when she
        /// asked for it: black hatching laid on the dark half of a night street moves the pixel by less
        /// than the 6/255 this counts as «closed», so a frame staged to 3 % of ink measures 1.8–2.9 % of
        /// picture depending on where the waves happened to land.
        ///
        /// 1.2 % is set under the worst of those measurements and still nearly twice the 0.67 % that the
        /// gate's own frame-with-no-waves scored — which is the failure this floor exists to catch.
        /// </summary>
        private const float MinThoughtInk = 0.012f;

        /// <summary>A pixel has to move by more than this (0…255) to count as covered by something.</summary>
        private const float PaintNoise = 6f;

        /// <summary>
        /// The frame really has thoughts ON it — measured by switching the thought layer off and counting
        /// what changed, on the very picture that was written to disk.
        ///
        /// The model-side wait (<see cref="GameTestHarness.WaitForInkOnScreen"/>) is what gets the frame
        /// staged; this is the claim. They are deliberately two different measurements: the wait is
        /// arithmetic about rectangles and baked ink shares, and arithmetic about a rectangle is exactly
        /// what said «три мысли на экране» about a frame with three slivers off its edge.
        /// </summary>
        private static void AssertTheThoughtsPaintThisFrame(LevelScreen screen, string frameName)
        {
            float share = ShareCoveredBy(screen.View.ThoughtsLayer.gameObject);
            Assert.Greater(share, MinThoughtInk,
                frameName + ": мыслей на кадре практически нет — закрашено " +
                (share * 100f).ToString("0.00") + " % при пороге " + (MinThoughtInk * 100f) +
                " %. Кадр волн без волн (дизайн-гейт 2026-08-08).");
        }

        /// <summary>…and the fill bar is not hidden under them (F5 of the same gate).</summary>
        private static void AssertTheFillBarIsVisibleInThisFrame(LevelScreen screen, string frameName)
        {
            RectInt box = StandTestHarness.PixelRectOf(StandTestHarness.Stage(),
                screen.View.VesselFillTrack.rectTransform);

            float share = ShareCoveredBy(screen.View.VesselFillTrack.gameObject, box);
            Assert.Greater(share, MinBarShare,
                frameName + ": полоса наполнения не видна на кадре — от её собственного места на экране " +
                "осталось " + (share * 100f).ToString("0.0") + " % при пороге " + (MinBarShare * 100f) +
                " %. Мысли закрывают HUD.");
        }

        /// <summary>How much of the bar's own rectangle has to be the bar and not what is over it.</summary>
        private const float MinBarShare = 0.5f;

        /// <summary>
        /// …and the other half of the same claim: NOTHING of the thought layer is drawn inside the
        /// bar's rectangle (blocker Б2, design gate 2026-08-08).
        ///
        /// The two are not the same measurement and the first one passed while the second failed for
        /// ten days. <see cref="AssertTheFillBarIsVisibleInThisFrame"/> asks «does the bar paint its own
        /// rectangle», and a translucent widget does: the wash still moves every pixel it lies on. What
        /// the gate actually caught is what the player sees THROUGH it — 23.2 % of the bar's pixels on
        /// Game23 were the white discs under the pips, 10.4 % on Game15 — and the only way to ask that
        /// is to switch the thoughts off and see whether anything inside the bar changes.
        ///
        /// SCREENS §S3, «Слой мыслей»: «HUD они не закрывают».
        /// </summary>
        private static void AssertNoThoughtsInsideTheFillBar(LevelScreen screen, string frameName)
        {
            RectInt box = StandTestHarness.PixelRectOf(StandTestHarness.Stage(),
                screen.View.VesselFillTrack.rectTransform);
            float share = ShareCoveredBy(screen.View.ThoughtsLayer.gameObject, box);

            Assert.Less(share, MaxThoughtInkInHud,
                frameName + ": сквозь полосу наполнения видны мысли — " +
                (share * 100f).ToString("0.0") + " % её собственных пикселей меняются, когда слой " +
                "мыслей выключают, при потолке " + (MaxThoughtInkInHud * 100f) +
                " %. SCREENS §S3: «HUD они не закрывают» (блокер Б2, дизайн-гейт 2026-08-08).");
        }

        /// <summary>
        /// …and the OTHER half of the founder's list of 2026-09-22: there is no dynamo indicator on
        /// this frame at all.
        ///
        /// The assertion used to be «мысли не проступают сквозь индикатор» — the same Б2 measurement,
        /// on the game's second HUD widget. The widget is gone («спидометр убрать из HUD игры»), so
        /// the claim inverts: what has to be true now is that nothing draws it. Checked by NAME on the
        /// composited stage rather than through the view's own property, because a widget can come back
        /// from anywhere and the property is exactly the thing that would be updated with it.
        /// </summary>
        private static void AssertThereIsNoSpeedometer(LevelScreen screen, string frameName)
        {
            Assert.IsNull(screen.View.CrankDial,
                frameName + ": индикатор динамо снова построен в игре (founder 2026-09-22: убрать).");

            DesignStage stage = StandTestHarness.Stage();
            foreach (string widget in new[] { "CrankDial", "CrankArc", "CrankNeedle", "CrankHalo" })
                Assert.IsNull(StandTestHarness.FindOrNull(stage, widget),
                    frameName + ": «" + widget + "» вернулся на экран игры — это спидометр, который " +
                    "основательница попросила убрать (плейтест 2026-09-22).");
        }

        /// <summary>
        /// How much of a HUD widget's own area the thoughts are allowed to move, 0…1.
        ///
        /// Not zero, and the slack is geometric rather than a tolerance for failure: the bar is drawn on
        /// a 7 px rounded sprite, so its four corners are background inside the rectangle a test can name
        /// — about 1.5 % of a 173×16 bar. Four per cent is above that and an order of magnitude below
        /// the 23.2 % the gate returned.
        /// </summary>
        private const float MaxThoughtInkInHud = 0.04f;

        /// <summary>
        /// The neon aim is IN this frame and visible against the plate underneath it.
        ///
        /// Measured the way the light sweep's frames are: the picture as shot, the picture with the aim
        /// switched off and nothing else touched, and the difference read on the pixels. A layout test
        /// («круг на месте, нужного размера») was green through the entire life of the invisible version
        /// of this feature.
        /// </summary>
        private static void AssertTheAimIsVisibleInThisFrame(LevelScreen screen, string frameName)
        {
            Assert.IsTrue(screen.View.Gaze.gameObject.activeInHierarchy,
                frameName + ": прицела нет на кадре — снято не в отгружаемом режиме выбора детали.");

            RectInt box = StandTestHarness.PixelRectOf(StandTestHarness.Stage(),
                screen.View.Gaze.rectTransform);
            float share = ShareCoveredBy(screen.View.Gaze.gameObject, box);

            Assert.Greater(share, MinAimShare,
                frameName + ": неон-прицел не читается на этой плите — он трогает " +
                (share * 100f).ToString("0.0") + " % своего квадрата при пороге " + (MinAimShare * 100f) +
                " %.");
        }

        /// <summary>
        /// How much of the aim's own quad it has to visibly change. The quad is 1.45× the circle, so the
        /// ring plus its glow is roughly a tenth of it; a tenth of that is a floor that a drawn circle
        /// clears everywhere and an invisible one cannot.
        /// </summary>
        private const float MinAimShare = 0.04f;

        // ---- Б1 (возврат скептика 2026-09-22): a teaching plate may not bury the detail it teaches about --

        /// <summary>
        /// The detail that is TRAVELLING on this frame can still be SEEN on it — its own ink, counted
        /// on the picture that was just written.
        ///
        /// Blocker Б1 of the design skeptic's return, 2026-09-22, on frame
        /// <c>Game04_L1_tutorial_crank</c>: the sentence of the сбор beat stood at x 511…1066 ·
        /// y 227…376 and the aeroplane drove up its thread underneath it, leaving 16 px of gap with
        /// the nose poking out. The cause was that the beat's obstacle was a POINT — a 160×160 box at
        /// where the detail stood when the beat opened — while the plate is placed once and never
        /// moves (<c>LevelScreen.AddDetailTrack</c> is the fix).
        ///
        /// A RATIO, not an absolute share, because ink is the detail's own business: the plane paints
        /// about a fifth of its 484×84 rectangle and the seagull most of its own. So the frame is
        /// measured twice — as shot, and with the beat's WHOLE hint layer taken off it and nothing
        /// else touched — and what is asserted is how much of the second survives in the first.
        ///
        /// The layer rather than the plate alone: the beat draws three things over this detail (the
        /// sentence, «КРУТИ РУЧКУ» at the thread and «ТАЩИ» beside it, each with its arrow), they are
        /// all placed by the same search against the same obstacles, and «which of the three buried
        /// it» is not a distinction the founder makes when she cannot see the object.
        /// </summary>
        private static void AssertTheTravellingDetailIsVisible(LevelScreen screen, string frameName)
        {
            int index = screen.Runtime.NoticedIndex;
            Assert.GreaterOrEqual(index, 0, frameName + ": на кадре бита сбора нет едущей детали.");
            Assert.Greater(screen.Runtime.Collector.Progress01, 0f,
                frameName + ": деталь ещё дома — на таком кадре бит сбора нечем судить.");

            var image = screen.View.DetailImages[index];
            RectInt box = StandTestHarness.PixelRectOf(StandTestHarness.Stage(), image.rectTransform);
            Assert.Greater(box.width * box.height, 0, frameName + ": едущая деталь вне кадра.");

            float shown = ShareCoveredBy(image.gameObject, box);

            GameObject hints = screen.View.MessageLayer.gameObject;
            bool was = hints.activeSelf;
            hints.SetActive(false);
            Canvas.ForceUpdateCanvases();
            float bare = ShareCoveredBy(image.gameObject, box);
            hints.SetActive(was);
            Canvas.ForceUpdateCanvases();

            Assert.Greater(bare, 0.02f,
                frameName + ": деталь «" + screen.Level.Details[index].Name + "» не красит даже " +
                "собственный прямоугольник без подсказок — мерить нечего.");

            float visible = shown / bare;
            TestContext.WriteLine(frameName + ": едущая деталь «" + screen.Level.Details[index].Name +
                "» видна на " + (visible * 100f).ToString("0.0") + " % своих чернил (" +
                (shown * 100f).ToString("0.00") + " % квадрата против " +
                (bare * 100f).ToString("0.00") + " % без подсказок).");
            Assert.Greater(visible, MinTravellingDetailVisible,
                frameName + ": подсказка бита хоронит едущую деталь «" +
                screen.Level.Details[index].Name + "» — от её чернил на кадре осталось " +
                (visible * 100f).ToString("0.0") + " % при пороге " +
                (MinTravellingDetailVisible * 100f).ToString("0") + " % (замерено " +
                (shown * 100f).ToString("0.00") + " % квадрата против " +
                (bare * 100f).ToString("0.00") + " % без подсказок). Препятствие бита — вся траектория " +
                "детали, а не точка её старта (блокер Б1, возврат дизайн-скептика 2026-09-22).");
        }

        /// <summary>
        /// How much of the travelling detail's ink has to survive the beat's plate, 0…1.
        ///
        /// 0.90 rather than 1.0 because the beat's OTHER hint is allowed to come close: «ТАЩИ» is
        /// aimed at the detail and its arrow stops 50 px short of the ink centroid, which on a sprite
        /// as long as the plane's is inside the rectangle. That is a few pixels of contrail, not a
        /// buried detail.
        ///
        /// Measured on the shot rather than guessed: the frame as it ships now scores **98.4 %**
        /// (20.46 % of the plane's box against 20.80 % with the hints off), and the negative control —
        /// the old point-obstacle put back, everything else untouched — scores **47.0 %** (9.57 %
        /// against 20.37 %). The floor sits between them with an order of magnitude of room.
        /// </summary>
        private const float MinTravellingDetailVisible = 0.9f;

        // ---- Б1: the отгон plate is the DROP's hint, not the greybox stand's paper ----------------------

        /// <summary>
        /// The plate under «Маши над датчиком!» is dark, measured on the written frame.
        ///
        /// Blocker Б1 of the design gate, 2026-08-08: that one beat of the four-beat lesson was drawn
        /// with <see cref="HintCard"/> — the greybox stand's white paper, luminance 253.6 — while the
        /// three around it carry the drop's dark slab. Asserting the colour of the Image would be
        /// asserting the constant back at itself; this reads the composited pixels, which is also where
        /// the gate read the 253.6.
        ///
        /// The MEDIAN, because the plate is white caps on a dark slab and about a sixth of it is
        /// lettering — a mean would be pulled up by the very text the slab exists to carry.
        /// </summary>
        private static void AssertTheSwipePlateIsDark(LevelScreen screen, string frameName) =>
            AssertTheBeatPlateIsDark(screen, frameName, GameTexts.SwipeHint);

        /// <summary>…the same measurement for whichever of the four beats is on this frame.</summary>
        private static void AssertTheBeatPlateIsDark(LevelScreen screen, string frameName, string line)
        {
            HintPlate plate = screen.View.BeatPlate;
            Assert.IsTrue(plate.IsShown, frameName + ": плашки бита нет на кадре.");

            RectInt box = StandTestHarness.PixelRectOf(StandTestHarness.Stage(), plate.Fill.rectTransform);
            Texture2D frame = StandTestHarness.Capture(Letterbox);
            try
            {
                Color[] pixels = StandTestHarness.PixelsOf(frame, box);
                Assert.Greater(pixels.Length, 0, frameName + ": плашка бита вне кадра.");

                var sorted = new float[pixels.Length];
                for (int i = 0; i < pixels.Length; i++) sorted[i] = Luminance(pixels[i]);
                System.Array.Sort(sorted);
                float median = sorted[sorted.Length / 2];

                Assert.Less(median, MaxSwipePlateLuminance,
                    frameName + ": заливка карточки «" + line + "» светимостью " +
                    median.ToString("0.0") + "/255 при потолке " + MaxSwipePlateLuminance +
                    " — это снова белая бумага грейбокс-стенда, а не тёмная плашка дропа " +
                    "(блокер Б1, дизайн-гейт 2026-08-08).");
            }
            finally
            {
                Object.DestroyImmediate(frame);
            }
        }

        /// <summary>
        /// The ceiling, 0…255. The drop's slab RGB(35, 52, 65) composites to about 61 over the brightest
        /// plate in the game; the white card it replaced measured 253.6. Ninety sits between them with
        /// room for the plate to be laid on anything.
        /// </summary>
        private const float MaxSwipePlateLuminance = 90f;

        /// <summary>
        /// A beat's SENTENCE is on this frame and can be read off it: the right line from the registry,
        /// on the drop's dark slab, with the lettering really painting the slab.
        ///
        /// The last claim is the one a layout assertion cannot make. The fourth beat («Заметь все
        /// объекты…», founder 2026-09-22) had no frame of its own until this round — it was visible
        /// only in the corner of <c>Game27_L1_thought_last_pip</c>, which is a frame about a bursting
        /// thought and guards nothing about the words. A plate that is shown, sized and placed can
        /// still carry no text at all.
        /// </summary>
        private static void AssertTheBeatSentenceIsReadable(LevelScreen screen, string frameName,
            string line)
        {
            HintPlate plate = screen.View.BeatPlate;
            Assert.IsTrue(plate.IsShown, frameName + ": плашки бита нет на кадре.");
            Assert.AreEqual(line, plate.Label.text,
                frameName + ": на плашке не та строка — тексты идут мимо реестра GameTexts.");
            StandTestHarness.AssertVisible(plate.Rect, "Плашка бита на кадре " + frameName);

            AssertTheBeatPlateIsDark(screen, frameName, line);

            RectInt box = StandTestHarness.PixelRectOf(StandTestHarness.Stage(), plate.Rect);
            float inked = ShareCoveredBy(plate.Label.gameObject, box);
            Assert.Greater(inked, MinBeatLetteringShare,
                frameName + ": букв на плашке нет — надпись красит " +
                (inked * 100f).ToString("0.0") + " % её прямоугольника при пороге " +
                (MinBeatLetteringShare * 100f) + " %. Плашка стоит пустая.");
        }

        /// <summary>
        /// How much of the plate the lettering has to move. Three lines of 30 pt inside a 560×196 plate
        /// with its padding work out at a few per cent of white on dark; two is under the thinnest of
        /// the four sentences and an order of magnitude above an empty slab.
        /// </summary>
        private const float MinBeatLetteringShare = 0.02f;

        // ---- Б2 (возврат скептика 2026-09-22): a peak frame has to be a peak on the SHIPPED threshold ----

        /// <summary>
        /// This frame really is «пик хаоса» as the game ships it — and the chaos is in the MIDDLE of
        /// the picture, not a frame of blobs round an empty plate.
        ///
        /// Blocker Б2 of the skeptic's return, 2026-09-22. Frames 07 and 24 were staged by moving the
        /// peak threshold itself down to 32 % and then waiting for it: the shipped value is 65
        /// (<see cref="TuningConfig.Defaults.PeakOverlapPercent"/>, loss at 85), so the founder was
        /// shown half the pressure her own number describes. The threshold is a knob and the staging
        /// wait is allowed to use it — what is NOT allowed is measuring the picture against the moved
        /// knob, so this measures the picture.
        ///
        /// Two claims, because the first one alone passed the frame she rejected. Coverage is computed
        /// on a grid over the WHOLE frame, so blobs hugging the four edges score a coverage that a
        /// centre-empty composition has no business scoring; the founder's word for that frame was
        /// «рамка с пустым центром». So the second claim reads the pixels of the middle quarter of the
        /// frame and asks what the thought layer actually paints there.
        /// </summary>
        private static void AssertTheFrameIsReallyAtThePeak(LevelScreen screen, string frameName)
        {
            // Both numbers are taken BEFORE either claim is made, so a failing frame is reported with
            // the whole measurement rather than with the half that tripped first.
            float overlap = screen.Runtime.Field.OverlapPercent;
            float centre = ShareCoveredBy(screen.View.ThoughtsLayer.gameObject, PeakCentreBox);
            TestContext.WriteLine(frameName + ": пик — перекрытие " + overlap.ToString("0.0") +
                " %, центр кадра закрашен на " + (centre * 100f).ToString("0.0") + " %, мыслей " +
                screen.Runtime.Field.Thoughts.Count + ".");

            Assert.GreaterOrEqual(overlap, MinPeakOverlapPercent,
                frameName + ": перекрытие на кадре " + overlap.ToString("0.0") +
                " % при пороге кадра " + MinPeakOverlapPercent.ToString("0") +
                " % (отгружаемый порог пика — " + TuningConfig.Defaults.PeakOverlapPercent.ToString("0") +
                " %). Кадр пика снят на заниженном пороге (блокер Б2, возврат скептика 2026-09-22).");

            Assert.Greater(centre, MinPeakCentreShare,
                frameName + ": центр кадра пуст — мысли красят " + (centre * 100f).ToString("0.0") +
                " % середины (640…1280 × 360…720) при пороге " + (MinPeakCentreShare * 100f) +
                " %. Это «рамка с пустым центром», которую основательница забраковала.");
        }

        /// <summary>The middle quarter of the frame, design px — where a peak has to be happening.</summary>
        private static readonly RectInt PeakCentreBox = new RectInt(640, 360, 640, 360);

        /// <summary>
        /// The floor for a peak frame's own coverage, per cent. Five under the shipped threshold of 65:
        /// the frame is shot a moment after the wait returns and the thoughts drift while it is being
        /// written, so the claim has to have the width of a few frames in it — and it is still twice
        /// the 32 the returned frames were staged against.
        /// </summary>
        private const float MinPeakOverlapPercent = 60f;

        /// <summary>…and how much of the middle of the picture the thought layer has to paint, 0…1.</summary>
        /// <remarks>
        /// Both floors are set off measurements, not off taste. On the shipped threshold the frames
        /// come out at 65.9 % / 82.3 % of centre (07) and 65.0 % / 57.3 % (24); the negative control —
        /// `PeakOverlapPercent = 32f` put back on frame 24, nothing else changed — comes out at
        /// 35.9 % / 34.1 %, i.e. it fails BOTH halves, which is what a frame the founder called
        /// «рамка с пустым центром» should do.
        /// </remarks>
        private const float MinPeakCentreShare = 0.4f;

        // ---- the defeat screen's copy has to be READABLE, not merely present -----------------------------

        /// <summary>
        /// Share of the plate that the three lines of neon and their underline actually paint. Measured
        /// off the render: 14.5 k lit pixels in a 1120×330 box is 4 %, so the top 3 % of the plate's
        /// luminances is the copy and nothing else.
        /// </summary>
        private const float DefeatCopyShare = 0.03f;

        /// <summary>
        /// …and this is where «what is behind the copy» is read: the 90th percentile of everything that
        /// is NOT the copy. Not the mean — the mean was 60/255 on the broken frame and would have called
        /// it fine. What makes the frame unreadable is the BRIGHTEST thing behind the letters, and on a
        /// screen half-dissolved over a field of thoughts that is a white outline running through them.
        /// </summary>
        private const float DefeatBehindPercentile = 0.87f;

        /// <summary>
        /// The floor, 0…255. The frame the design skeptic returned on 2026-08-08 measured 49: the copy's
        /// neon and the outlines of the thoughts under it were the same brightness and the underline-button
        /// was gone. The drawn screen at full opacity measures 144, the same screen half-wiped over the
        /// plate measures about 88 — so 70 fails the frame that was returned and passes the one with the
        /// plate, with room on both sides for the field to drift.
        /// </summary>
        private const float MinDefeatCopyContrast = 70f;

        /// <summary>
        /// «Мысли захватили тебя, ты не заметил жизнь вокруг. Попробуй ещё раз!» can be read off the
        /// frame that was just written — the words AND the underline that stands for the retry button.
        ///
        /// This is a claim about contrast, not about layout, because layout was never the problem: the
        /// screen was in place, at the right alpha, over the right level, and the words were invisible.
        /// The retry dissolves the drawn screen into the level the player just lost (SCREENS S5), so
        /// halfway through it the copy is a 60 %-opacity neon lying on marker hatching that has a white
        /// outline of its own — same luminance, no edge, nothing to read. What the assert measures is
        /// exactly that gap: the copy's own ink against the brightest thing behind it, inside the plate
        /// that <see cref="LevelView.DefeatTextPlate"/> puts there to be behind it.
        /// </summary>
        private static void AssertTheDefeatCopyIsReadable(string frameName)
        {
            Texture2D frame = StandTestHarness.Capture(Letterbox);
            try
            {
                Rect plate = LevelView.DefeatTextPlate;
                var box = new RectInt(Mathf.RoundToInt(plate.xMin), Mathf.RoundToInt(plate.yMin),
                    Mathf.RoundToInt(plate.width), Mathf.RoundToInt(plate.height));

                Color[] pixels = StandTestHarness.PixelsOf(frame, box);
                Assert.Greater(pixels.Length, 0, frameName + ": плашка текста вне кадра.");

                var sorted = new float[pixels.Length];
                for (int i = 0; i < pixels.Length; i++) sorted[i] = Luminance(pixels[i]);
                System.Array.Sort(sorted);

                int copyFrom = Mathf.Clamp(Mathf.FloorToInt(sorted.Length * (1f - DefeatCopyShare)),
                    0, sorted.Length - 1);
                float copy = 0f;
                for (int i = copyFrom; i < sorted.Length; i++) copy += sorted[i];
                copy /= sorted.Length - copyFrom;

                float behind = sorted[Mathf.Clamp(
                    Mathf.RoundToInt(sorted.Length * DefeatBehindPercentile), 0, sorted.Length - 1)];

                Assert.Greater(copy - behind, MinDefeatCopyContrast,
                    frameName + ": текст экрана поражения не читается — светимость букв " +
                    copy.ToString("0") + "/255 против " + behind.ToString("0") +
                    "/255 у того, что за ними, разница " + (copy - behind).ToString("0") +
                    " при пороге " + MinDefeatCopyContrast.ToString("0") +
                    ". Мысли уровня проступают сквозь надпись (возврат дизайн-скептика 2026-08-08).");
            }
            finally
            {
                Object.DestroyImmediate(frame);
            }
        }

        /// <summary>
        /// Share of the frame (or of <paramref name="box"/>) whose pixels change when
        /// <paramref name="what"/> is switched off — i.e. what that object is actually painting.
        /// </summary>
        private static float ShareCoveredBy(GameObject what, RectInt? box = null) =>
            StandTestHarness.ShareCoveredBy(what, Letterbox, box, PaintNoise);

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

            // 17 · обучение, бит 1: плашка «Наводи джойстиком на объект», круг-взгляд в кадре.
            // Кнопка «НАВОДИ» со стрелкой стояла тут до 2026-09-22 (заказ founder: «убрать стрелки все
            // с экранов обучений»), и кадр снимается ровно ради того, что осталось.
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "обзор закончился");
            yield return GameTestHarness.SettleScreen(fake);
            Assert.AreEqual(TutorialBeat.Aim, screen.Beat, "Кадр «наводи» снят не на своём бите.");
            StandTestHarness.AssertVisible(screen.View.Gaze.rectTransform, "Круг-взгляд");
            Shoot("Game17_L1_tutorial_aim");
            AssertTheBeatSentenceIsReadable(screen, "Game17_L1_tutorial_aim", GameTexts.BeatAim);

            // 04 · обучение, бит 2: одна плашка на обе руки, деталь едет по нити.
            yield return GameTestHarness.NoticeSomething(fake, screen);
            yield return GameTestHarness.CrankUntil(fake,
                () => screen.Runtime.Collector.Progress01 > 0.35f, "деталь в пути");
            fake.Next = new BackendSnapshot { CrankDeltaDegrees = GameTestHarness.CrankPerFrame };
            yield return GameTestHarness.Frames(2);
            Assert.AreEqual(TutorialBeat.Crank, screen.Beat);
            Shoot("Game04_L1_tutorial_crank");
            AssertTheBeatSentenceIsReadable(screen, "Game04_L1_tutorial_crank", GameTexts.BeatCollect);
            AssertTheTravellingDetailIsVisible(screen, "Game04_L1_tutorial_crank");

            // 05 · обучение, бит 3: мысль-кот сидит поверх следующей детали, плашка называет её и
            // говорит, куда вести руку. Дуговая стрелка к датчикам с кадра убрана.
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.Idle(fake, 4);
            Assert.AreEqual(TutorialBeat.Swipe, screen.Beat);
            Assert.AreEqual(1, screen.Runtime.Field.Thoughts.Count, "На кадре должна быть ровно одна мысль.");
            Shoot("Game05_L1_tutorial_swipe");
            AssertTheSwipePlateIsDark(screen, "Game05_L1_tutorial_swipe");

            // 40 · обучение, бит 4: «Заметь все объекты, перетащи их в ведёрко и не дай мыслям
            // помешать тебе» — заказ founder 2026-09-22, и до возврата скептика у него не было
            // собственного кадра. Он был виден только краем на 27-м (кадре лопающейся мысли), то есть
            // единственная подсказка игры, которая не блокирует ничего и says ЗАЧЕМ всё остальное,
            // стояла в контракте случайно и без единого гарда на текст.
            //
            // Кадр снимается ПОВЕРХ уже идущей игры, потому что бит именно такой: волны пущены
            // (TutorialAllowsSpawning пропускает их с этого бита), стрелок нет, на экране — уровень и
            // одно предложение над ним.
            yield return GameTestHarness.SwipeUntil(fake, () => screen.Beat == TutorialBeat.Whole,
                "мысль отбита, пошёл четвёртый бит");
            yield return GameTestHarness.Idle(fake, 3);

            Assert.AreEqual(TutorialBeat.Whole, screen.Beat, "Кадр 40 снят не на четвёртом бите.");
            Shoot("Game40_L1_tutorial_whole_beat");
            AssertTheBeatSentenceIsReadable(screen, "Game40_L1_tutorial_whole_beat", GameTexts.BeatWhole);

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
        /// дальше обычный play» (SCREENS §Обучение п.4; the clock that used to start here went out with
        /// the timer, 2026-08-07) — the arrow is gone and nothing of the tutorial is left on the screen.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSwipeBeat_LastPipAndTheFrameAfterIt()
        {
            // How many pips there are is level 1's own band (ApplyLevel copies it in on entry, so it is
            // not this test's to choose) — what the shot needs is only that the counter does not
            // quietly reset itself while it is being set up.
            TuningConfig.HitDecayEnabled = false;
            TuningConfig.Targeting = HitTargeting.AllOnScreen;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.NoticeSomething(fake, screen);
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.Idle(fake, 4);

            Assert.AreEqual(TutorialBeat.Swipe, screen.Beat, "Бит отгона не начался.");
            Assert.AreEqual(1, screen.Runtime.Field.Thoughts.Count, "На кадре должна быть ровно одна мысль.");
            Thought thought = screen.Runtime.Field.Thoughts[0];

            // First to the last pip.
            //
            // The hits are landed through the field's own ApplyHits — the very call the hand over the
            // sensor makes — rather than by waving until the counter looks right: a hand cannot be stopped
            // on a given pip. A hand leaving the sensor is itself a pass, so «махать, пока не
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

            // 29 · и тот же бит СЕКУНДОЙ ПОЗЖЕ, а не в то же мгновение, что 27.
            //
            // Снятый сразу, он показывал обломок только что лопнувшей мысли и красный индикатор динамо
            // (прибор горит, пока начатый сбор стоит) — и вместе это читалось как СРЫВ, тогда как кадр
            // про «мысль отбита → дальше обычный play» (дизайн-гейт 2026-08-08). Поэтому ждём, пока
            // разрыв догорит и прибор погаснет, и пока волны действительно пойдут: пустой кадр
            // доказывает «play» не лучше, чем кадр с обломком.
            yield return GameTestHarness.SwipeUntil(fake, () => screen.Beat == TutorialBeat.Done,
                "мысль отбита, обучение кончилось");

            fake.Next = new BackendSnapshot();
            yield return GameTestHarness.Until(
                () => screen.View.Pops.Count == 0 && screen.Runtime.Collector.Progress01 <= 0f &&
                      screen.Runtime.DisplayProgress <= 0.01f,
                "разрыв догорел и индикатор динамо погас");

            // …и «обычный play» — это play с прицелом: обучение шло в FixedOrder, чтобы биты вообще
            // можно было прогнать, а кадр после обучения обязан быть кадром отгружаемой игры.
            yield return GameTestHarness.NoticeByLooking(fake, screen, FirstUncollected(screen));
            yield return GameTestHarness.WaitForInkOnScreen(fake, screen, StagedThoughtInk);

            Assert.IsFalse(screen.View.BeatPlate.IsShown,
                "После отгона подсказки на кадре быть не должно.");
            Assert.AreEqual(TutorialBeat.Done, screen.Beat,
                "После отгона обучение обязано быть позади — иначе кадр не про это.");
            Assert.AreEqual(0, screen.View.Pops.Count,
                "На кадре 29 всё ещё догорает лопнувшая мысль — это читается как срыв.");
            Assert.LessOrEqual(screen.Runtime.DisplayProgress, 0.01f,
                "На кадре 29 индикатор динамо горит красным — кадр не про «дальше обычный play».");

            Shoot("Game29_L1_after_the_swipe_beat");
            AssertTheThoughtsPaintThisFrame(screen, "Game29_L1_after_the_swipe_beat");
            AssertTheAimIsVisibleInThisFrame(screen, "Game29_L1_after_the_swipe_beat");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- 33 неон-обводка · 34 рост мыслей (заказ founder 2026-08-07) ---------------------------------

        /// <summary>
        /// 33 · the neon outline SWITCHED ON, on the level whose ordinary frame (09) has it off.
        ///
        /// The pair is the point. The founder ordered this outline to compare it against the pulse and
        /// the light sweep and switch the losers off, and a comparison needs two frames of the same
        /// level that differ in exactly one thing — a single frame of a lit-up office answers «нравится
        /// ли», never «нужна ли она поверх двух других подсказок».
        /// </summary>
        [UnityTest]
        public IEnumerator TheNeonOutline_OnTheOfficeForComparison()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");

            // Staged like frame 09, its pair: the shipped way of choosing a detail, the aim parked on
            // one. A pair that differs in the outline AND in whether the aim is drawn compares nothing.
            yield return GameTestHarness.NoticeByLooking(fake, screen, FirstUncollected(screen));

            TuningConfig.DetailNeonOutline = true;
            screen.View.ApplyNeonOutline();

            // The sweep off, so the frame is about ONE of the three signals: a band of light crossing
            // the office while the rims burn would be the comparison already spoiled.
            screen.View.ApplySweep(false, 0f, 0f, 0f, -1);
            yield return GameTestHarness.SettleScreen(fake);
            screen.View.ApplySweep(false, 0f, 0f, 0f, -1);

            int lit = 0;
            for (int i = 0; i < screen.View.DetailOutlines.Count; i++)
                if (screen.View.DetailOutlines[i] != null &&
                    screen.View.DetailOutlines[i].gameObject.activeInHierarchy) lit++;
            Assert.AreEqual(screen.Level.DetailCount, lit,
                "На кадре 33 обводка обязана гореть на КАЖДОЙ несобранной детали.");

            Shoot("Game33_L2_neon_outline_on");

            // 37 · 38 · and the two details the gate could not judge from a whole-level frame, cut out
            // and blown up. Both are findings of 2026-08-08, and they are opposite failures of the same
            // widget: on the paperclip a 6 px dilation closed the gaps of a wire and the rim became a
            // FILL, and on the slippers the rim was sliced off by straight lines along the sprite's own
            // border because the quad had no room outside the drawing. Neither is visible at 40 px on a
            // 1920×1080 plate, which is why the gate saw them and the suite did not.
            ShootTheRimUpClose(screen, "L2/objects/paperclip", "Game37_L2_neon_rim_paperclip");
            ShootTheRimUpClose(screen, "L2/objects/slippers", "Game38_L2_neon_rim_slippers");

            TuningConfig.DetailNeonOutline = false;

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>Cut one detail out of the live frame and magnify it, so a 2 px rim can be judged.</summary>
        private static void ShootTheRimUpClose(LevelScreen screen, string spriteKey, string frameName)
        {
            for (int i = 0; i < screen.Level.DetailCount; i++)
            {
                if (screen.Level.Details[i].Sprite != spriteKey) continue;

                Rect box = LevelCatalog.RectOf(screen.Level.Details[i]);
                var around = new Rect(box.xMin - CloseUpAirPx, box.yMin - CloseUpAirPx,
                    box.width + 2f * CloseUpAirPx, box.height + 2f * CloseUpAirPx);
                StandTestHarness.ShootCloseUp(frameName, Letterbox, around);
                return;
            }

            Assert.Fail("Детали «" + spriteKey + "» нет на уровне — крупный план снимать не с чего.");
        }

        /// <summary>Plate left around a detail in a close-up, design px — the rim lives outside its box.</summary>
        private const float CloseUpAirPx = 26f;

        /// <summary>
        /// 34 · «мысли разрастаются со временем» — the frame that shows the SPREAD.
        ///
        /// One thought that has been on screen long enough to reach its ceiling beside one that has just
        /// arrived, so the difference the founder ordered is in a single picture rather than spread over
        /// two. The old one is aged through the field's OWN clock — a thought's size is a function of its
        /// age and nothing else, and posing it by setting sizes would be a frame of a rule the game does
        /// not have.
        ///
        /// What IS posed is where the two stand, and it has to be (design gate, 2026-08-08). Waiting for
        /// a natural wave gave a frame whose «свежая рядом» was a sliver hanging off the edge: thoughts
        /// enter from the borders and drift inwards, so the moment a new one exists is the moment it is
        /// least visible. Both are put down through <see cref="ThoughtField.SpawnAt"/> — the same call
        /// the tutorial's own thought arrives by — and then held to opposite halves of the frame, so the
        /// comparison the founder asked for is a comparison of two whole silhouettes.
        /// </summary>
        [UnityTest]
        public IEnumerator ThoughtsGrowingOverTheirLifetime()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);   // офис: крупный класс, светлая плита

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            ThoughtField field = screen.Runtime.Field;
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");

            // Nothing but the two this frame is about: the level's own waves would crowd the pair the
            // gate has to compare.
            TuningConfig.WaveIntervalSeconds = 300f;
            screen.Runtime.Restart();

            // …and this frame stages its own growth ceiling, which it did not have to before
            // 2026-09-22. The shipped ceiling is now ×12 — «разрастаются на весь экран», the founder's
            // own order — and a thought at ×12 is four times the frame: a picture COMPARING an old
            // thought with a fresh one cannot be taken at a size where the old one has no edges. So
            // the knob is wound the way the wave clock above it is wound, to the largest ceiling that
            // still leaves both blobs whole and apart, and the rate is wound up so getting there takes
            // seconds instead of two minutes. That the SHIPPED ceiling really covers the frame is a
            // separate claim, and it is arithmetic — LevelCatalogTests.TheShippedGrowthCeiling_….
            TuningConfig.ThoughtGrowthCap = 2.5f;
            TuningConfig.ThoughtGrowthPercentPerSec = 25f;

            // The aim parked out of the way, on the plate above the pair — the shipped mode draws the
            // circle, and a circle sitting on one of the two thoughts would be a third thing in a frame
            // about two.
            GameTestHarness.ParkTheAim(screen, new Vector2(960f, 170f));

            Thought oldest = field.SpawnAt(ThoughtStrength.Medium, "L2/thoughts/banknote",
                new Vector2(520f, 540f));

            float grownAt = TuningConfig.ThoughtGrowthCap;
            fake.Next = new BackendSnapshot();
            yield return GameTestHarness.Until(
                () => oldest.GrowthScale >= grownAt - 0.01f, "мысль разрослась до потолка", 90f);

            // …and a fresh one beside it, in the other half of the frame.
            Thought youngest = field.SpawnAt(ThoughtStrength.Medium, "L2/thoughts/palm",
                new Vector2(1400f, 540f));

            Assert.Less(youngest.GrowthScale, 1.1f, "Свежая мысль обязана быть ещё своего класса.");
            Vector2 box = Thought.SizeOf(youngest.Strength);
            Assert.GreaterOrEqual(youngest.SpawnSize.x, box.x - 0.5f,
                "Свежая мысль спавнится УЖЕ класса — прямой запрет founder.");
            Assert.GreaterOrEqual(youngest.SpawnSize.y, box.y - 0.5f,
                "Свежая мысль спавнится НИЖЕ класса — прямой запрет founder.");

            // Each thought against ITS OWN spawn size, not against the other one's: since the fit
            // covers the class instead of fitting inside it, two silhouettes of the same class are
            // legitimately different rectangles (the banknote is 411×220 where the palm is 280×273),
            // and comparing across sprites would be measuring the drop, not the growth.
            Assert.Greater(oldest.Size.x, oldest.SpawnSize.x * 1.2f,
                "На кадре 34 старая мысль не разрослась — показывать нечего.");
            Assert.Greater(oldest.GrowthScale, youngest.GrowthScale * 1.2f,
                "На кадре 34 разница между старой и свежей мыслью не видна.");

            // Both of them WHOLE and apart, right before the shutter: the drift has had half a minute
            // to move the old one, and its rectangle grew by 60 % underneath it while it did.
            StandThemApart(oldest, youngest);
            yield return GameTestHarness.Idle(fake, 2);

            AssertWhollyInFrame(oldest, "разросшаяся мысль");
            AssertWhollyInFrame(youngest, "свежая мысль");
            Assert.IsFalse(oldest.Rect.Overlaps(youngest.Rect),
                "На кадре 34 мысли наложились друг на друга — рост так не прочитать.");

            yield return GameTestHarness.SettleScreen(fake);
            Shoot("Game34_L2_thoughts_grown");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>Push the pair to opposite sides of the frame, each fully inside it.</summary>
        private static void StandThemApart(Thought left, Thought right)
        {
            left.Position = new Vector2(
                FramePadPx + left.Size.x * 0.5f,
                Mathf.Clamp(540f, left.Size.y * 0.5f, ThoughtField.ScreenHeight - left.Size.y * 0.5f));

            right.Position = new Vector2(
                ThoughtField.ScreenWidth - FramePadPx - right.Size.x * 0.5f,
                Mathf.Clamp(540f, right.Size.y * 0.5f, ThoughtField.ScreenHeight - right.Size.y * 0.5f));
        }

        /// <summary>Air left between a staged thought and the edge of the frame, design px.</summary>
        private const float FramePadPx = 24f;

        /// <summary>…and the claim itself: this thought is on the frame ENTIRELY, not by a corner.</summary>
        private static void AssertWhollyInFrame(Thought thought, string what)
        {
            float visible = ArtThoughtView.VisibleShare(thought.Position, thought.Size);
            Assert.GreaterOrEqual(visible, 0.999f,
                "На кадре 34 «" + what + "» видна только на " + (visible * 100f).ToString("0") +
                " % — за кромкой кадра сравнивать нечего.");
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
            AssertTheBandIsVisibleInThisFrame(screen, "Game28_L2_sweep");
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The frame that was just written has to SHOW the band — measured on the very picture the
        /// design gate opens, not on a rig beside it.
        ///
        /// This is the check whose absence cost the drop of 2026-08-07 a gate: frames 26 and 28 were
        /// staged off the sweep's own numbers («Active», «Strength ≥ 0.9×0.45», «centre inside a
        /// detail's rectangle»), all of which were true while the picture was judged «луча нет». The
        /// numbers were never the claim — the pixels are. So the shot state is captured, the band is
        /// switched off without moving anything else, and the detail the band is over has to have
        /// dropped by <see cref="MinSweepLift"/> when it went out.
        ///
        /// The whole rectangle's p95 is the ruler here, unlike the rig's median-over-lit-pixels: this
        /// is the number a human takes off the PNG in an image editor, and it is the number the design
        /// skeptic reported. Making the frame gate answer in the skeptic's own units is the point.
        /// </summary>
        private static void AssertTheBandIsVisibleInThisFrame(LevelScreen screen, string frameName)
        {
            LevelDefinition level = screen.Level;
            float centre = screen.Sweep.CentreX;
            float halfBand = TuningConfig.SweepWidthPx * 0.5f;

            Texture2D lit = StandTestHarness.Capture(Letterbox);
            screen.View.ApplySweep(false, 0f, 0f, 0f, -1);
            Texture2D dark = StandTestHarness.Capture(Letterbox);

            float best = float.MinValue;
            string bestName = "—";
            int counted = 0;

            try
            {
                for (int i = 0; i < level.DetailCount; i++)
                {
                    if (TuningConfig.SweepOnlyUnnoticed && i == screen.Runtime.NoticedIndex) continue;

                    Rect box = LevelCatalog.RectOf(level.Details[i]);
                    if (Mathf.Abs(centre - box.center.x) > halfBand + box.width * 0.5f) continue;

                    counted++;
                    float lift = P95Of(lit, box) - P95Of(dark, box);
                    if (lift <= best) continue;
                    best = lift;
                    bestName = level.Details[i].Name;
                }
            }
            finally
            {
                Object.DestroyImmediate(lit);
                Object.DestroyImmediate(dark);
            }

            Assert.Greater(counted, 0,
                frameName + ": полоса не накрывает ни одной незамеченной детали — кадр не про луч.");

            Assert.Greater(best, MinSweepLift,
                frameName + ": луч на кадре не виден. Лучшая из деталей под полосой — «" + bestName +
                "», +" + best.ToString("0.0") + " ед. p95 при пороге " + MinSweepLift +
                " (сила " + TuningConfig.SweepStrength.ToString("0.00") + ").");
        }

        /// <summary>
        /// Is the band on a detail the sweep is actually allowed to light? The shipped toggle «луч
        /// только по незамеченным» skips the one already on its thread, and a frame staged over THAT
        /// one shows nothing lit at all.
        ///
        /// «On» means the band's own middle is within <see cref="BandCoreU"/> of the detail's INK, not
        /// merely inside its rectangle. Two reasons, both learned off frames: the rectangle of a detail
        /// can be mostly empty (the plane's is 484 px of sky, the vine's is 658 px of wall), so a centre
        /// inside it is regularly a centre beside the object; and the falloff is (1 - d²)², which is
        /// down to a third of the light at the band's own edge. Waiting for the core is what turns
        /// «where did the pass happen to be when the shutter opened» into the same frame every run.
        /// </summary>
        private static bool BandIsOverAnUnnoticedDetail(LevelScreen screen)
        {
            float centre = screen.Sweep.CentreX;
            float core = TuningConfig.SweepWidthPx * 0.5f * BandCoreU;
            for (int i = 0; i < screen.Level.DetailCount; i++)
            {
                if (TuningConfig.SweepOnlyUnnoticed && i == screen.Runtime.NoticedIndex) continue;
                if (Mathf.Abs(centre - LevelCatalog.AnchorOf(screen.Level.Details[i]).x) <= core)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// How near the band's middle the detail's ink has to be for the frame to be «луч на детали»,
        /// as a share of the band's half-width. Half of it: the falloff there is still 0.56 of full,
        /// and demanding the exact middle would make the wait miss passes on a batch frame rate.
        /// </summary>
        private const float BandCoreU = 0.5f;

        // ---- луч: измеренная прибавка яркости, а не «материал получил число» ----------------------------

        /// <summary>
        /// The gate's own question, answered in pixels: does the SHIPPED strength put light on the art
        /// that a human can see — on EVERY detail of EVERY level, not on the best one of five?
        ///
        /// <c>TheLightSweep_ReachesEveryDetailsOwnMaterial</c> proves the number arrives at every
        /// detail's material, which is a different claim and was never the one in doubt — a band that
        /// reaches the material and lands one brightness unit on the sprite passes it and ships an
        /// invisible feature.
        ///
        /// Three things this test learned the hard way, each of them a way it USED to be green while a
        /// человек saw nothing:
        ///
        /// <list type="number">
        /// <item>It took the LOUDEST detail of one level. Four dead details and one lit one passed —
        ///       and «the band is over a detail» is not a thing the frame gets to choose. Now every
        ///       detail of every level has to clear the bar on its own.</item>
        /// <item>It measured the p95 of the RECTANGLE'S BRIGHTNESS, which is not the same question as
        ///       «did the band light this object». What the band does to «очки на скамье» is turn a
        ///       black rim silver, and the box's 95th percentile there is the translucent lens — pale
        ///       already, and by rights gaining little: +13 by that ruler against a rim that rose by
        ///       about +100. The lift is now the RISE itself, measured on the pixels that moved (see
        ///       <see cref="SweepLift"/>), with a floor on how much of the box they cover.</item>
        /// <item>It had no negative: a rig that reports a lift no matter what is a green light with no
        ///       claim behind it. The same measurement is taken at strength 0 on every level and has
        ///       to come back empty.</item>
        /// </list>
        ///
        /// Measured off <see cref="StandTestHarness.Capture"/> — the very path that writes the gate's
        /// frames — twice in ONE frame: band on, band off, nothing else moved between them. The band is
        /// driven onto each detail's own ink (<see cref="LevelCatalog.AnchorOf"/>) rather than waited
        /// for, so the measurement is the same every run.
        /// </summary>
        [UnityTest]
        public IEnumerator TheLightSweep_VisiblyBrightensEveryDetailItIsOver()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();

            for (int levelIndex = 0; levelIndex < LevelCatalog.Count; levelIndex++)
            {
                yield return GameTestHarness.EnterLevel(fake, levelIndex);

                var screen = (LevelScreen)GameTestHarness.Flow().Screen;
                LevelDefinition level = screen.Level;
                string where = "уровень " + level.Number + " («" + level.Title + "»)";

                // The plate as it is with no band anywhere — the baseline every lift is measured off.
                screen.View.ApplySweep(false, 0f, 0f, 0f, -1);
                Texture2D dark = StandTestHarness.Capture(Letterbox);

                try
                {
                    for (int i = 0; i < level.DetailCount; i++)
                    {
                        ArtDetail detail = level.Details[i];
                        Rect box = LevelCatalog.RectOf(detail);
                        // Centred on the INK, not on the rectangle: the plane's box is 484 px of sky
                        // and its middle is trail, so a band centred there is a band beside the object.
                        float centre = LevelCatalog.AnchorOf(detail).x;

                        screen.View.ApplySweep(true, centre, TuningConfig.SweepWidthPx,
                            TuningConfig.SweepStrength, -1);
                        Texture2D lit = StandTestHarness.Capture(Letterbox);

                        // …and the same pass with the knob at zero: the rig has to be able to say «no».
                        screen.View.ApplySweep(true, centre, TuningConfig.SweepWidthPx, 0f, -1);
                        Texture2D unlit = StandTestHarness.Capture(Letterbox);

                        try
                        {
                            float covered;
                            float lift = SweepLift(lit, dark, box, out covered);

                            Assert.Greater(covered, MinSweepCoverage,
                                "Луч штатной силы (" + TuningConfig.SweepStrength.ToString("0.00") +
                                ") почти ничего не задел на детали «" + detail.Name + "», " + where +
                                ": сдвинулось " + (covered * 100f).ToString("0.0") +
                                " % прямоугольника при пороге " + (MinSweepCoverage * 100f) + " %.");

                            Assert.Greater(lift, MinSweepLift,
                                "Луч штатной силы (" + TuningConfig.SweepStrength.ToString("0.00") +
                                ") не даёт заметной прибавки на детали «" + detail.Name + "», " +
                                where + ": +" + lift.ToString("0.0") + " ед. по подсвеченным пикселям " +
                                "при пороге " + MinSweepLift + ".");

                            float zeroCovered;
                            SweepLift(unlit, dark, box, out zeroCovered);
                            Assert.Less(zeroCovered, MinSweepCoverage * 0.1f,
                                "Замер врёт: при силе 0 на детали «" + detail.Name + "», " + where +
                                " всё равно сдвинулось " + (zeroCovered * 100f).ToString("0.00") +
                                " % прямоугольника — значит и «прибавка» выше меряет не луч.");

                            // …and it must stop at the sprite's edge: a strip of BARE plate inside the
                            // band's own column may not have moved at all, or the light is landing on
                            // the picture instead of on the objects. Skipped where the strip would
                            // catch another detail — that one is lit on purpose.
                            Rect plate = new Rect(box.xMin, Mathf.Max(0f, box.yMin - box.height - 40f),
                                box.width, box.height);
                            if (!TouchesADetail(level, plate))
                            {
                                float bleed = Mathf.Abs(P95Of(lit, plate) - P95Of(dark, plate));
                                Assert.Less(bleed, MaxSweepBleed,
                                    "Луч светит по плите, а не по спрайтам: пустой участок фона рядом " +
                                    "с «" + detail.Name + "», " + where + ", изменился на " +
                                    bleed.ToString("0.0") + " ед.");
                            }
                        }
                        finally
                        {
                            Object.DestroyImmediate(lit);
                            Object.DestroyImmediate(unlit);
                        }
                    }
                }
                finally
                {
                    screen.View.ApplySweep(false, 0f, 0f, 0f, -1);
                    Object.DestroyImmediate(dark);
                }
            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The lift the founder's own «заметно» was measured against: the shipped strength puts +37 on
        /// the office sticky note and +41 on the city's curtains in the gate's own frames, and +45 in
        /// the median across all 38 details of the run, so a floor of 20 fails long before a human
        /// would stop seeing it — and it fails immediately if the band ever stops reaching the sprites
        /// at all, which is the failure this test exists for.
        /// </summary>
        private const float MinSweepLift = 20f;

        /// <summary>
        /// How much of a detail's rectangle the band has to actually move. Without it a single stray
        /// pixel would be a «lit object»: the lift is a median over the pixels that moved, and a
        /// median over three pixels is a number about nothing. Two per cent is below the thinnest
        /// detail in the game (the glasses' wire fills 4 % of their box).
        /// </summary>
        private const float MinSweepCoverage = 0.02f;

        /// <summary>…and how much the bare plate beside a detail is allowed to move: nothing real.</summary>
        private const float MaxSweepBleed = 3f;

        /// <summary>
        /// How bright a highlight the band put on the object inside <paramref name="design"/>: the
        /// 95th percentile of the per-pixel RISE, in luminance units of 255, over the pixels the band
        /// actually moved. <paramref name="covered"/> comes back as the share of the rectangle those
        /// pixels are, so a three-pixel sparkle cannot be read as a lit object.
        ///
        /// Two choices in that sentence, both paid for on frames:
        ///
        /// The RISE, not a percentile of the lit picture's own brightness. A percentile of brightness
        /// asks «is this rectangle bright», and what a highlight does to «очки на скамье» is turn a
        /// BLACK rim silver — a change of about +100 that lives at the dark end of the box and moves
        /// its p95 by 13, because the p95 there is the translucent lens, which is already pale and by
        /// rights gains little. Judged that way the loudest highlight in the library reads as the
        /// dimmest detail of the game.
        ///
        /// The pixels that MOVED, not the whole rectangle. Half the details are ink in a mostly empty
        /// box — the vine fills a fifth of its 54×658, the plane a sixth of its 484×84 — and any
        /// percentile taken over the box there is a percentile of plate that never moves.
        /// </summary>
        private static float SweepLift(Texture2D lit, Texture2D dark, Rect design, out float covered)
        {
            Color[] after = PixelsOf(lit, design);
            Color[] before = PixelsOf(dark, design);
            covered = 0f;
            if (after.Length == 0 || after.Length != before.Length) return 0f;

            var risen = new List<float>();
            for (int i = 0; i < after.Length; i++)
            {
                float d = Luminance(after[i]) - Luminance(before[i]);
                if (d > SweepPixelNoise) risen.Add(d);
            }

            covered = risen.Count / (float)after.Length;
            if (risen.Count == 0) return 0f;

            risen.Sort();
            return risen[Mathf.Clamp(Mathf.FloorToInt(risen.Count * 0.95f), 0, risen.Count - 1)];
        }

        /// <summary>A pixel has to move by more than a rounding step to count as lit.</summary>
        private const float SweepPixelNoise = 1f;

        private static bool TouchesADetail(LevelDefinition level, Rect strip)
        {
            for (int i = 0; i < level.DetailCount; i++)
                if (strip.Overlaps(LevelCatalog.RectOf(level.Details[i]))) return true;
            return false;
        }

        /// <summary>95th percentile of luminance, 0…255, over a design-space rectangle of a frame.</summary>
        private static float P95Of(Texture2D frame, Rect design)
        {
            Color[] pixels = PixelsOf(frame, design);
            if (pixels.Length == 0) return 0f;

            var luminance = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) luminance[i] = Luminance(pixels[i]);

            System.Array.Sort(luminance);
            return luminance[Mathf.Clamp(Mathf.FloorToInt(luminance.Length * 0.95f), 0, luminance.Length - 1)];
        }

        /// <summary>The pixels of a DESIGN-space rectangle out of a captured frame, clipped to it.</summary>
        private static Color[] PixelsOf(Texture2D frame, Rect design)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(design.xMin), 0, frame.width);
            int y = Mathf.Clamp(Mathf.RoundToInt(design.yMin), 0, frame.height);
            var box = new RectInt(x, y,
                Mathf.Clamp(Mathf.RoundToInt(design.width), 0, frame.width - x),
                Mathf.Clamp(Mathf.RoundToInt(design.height), 0, frame.height - y));
            return StandTestHarness.PixelsOf(frame, box);
        }

        /// <summary>Brightness of a pixel, 0…255 — the flat mean the design gate reads frames with.</summary>
        private static float Luminance(Color pixel) => (pixel.r + pixel.g + pixel.b) * (255f / 3f);

        // ---- 18 срыв детали: красное кольцо и полёт обратно ---------------------------------------------

        /// <summary>
        /// The slip is a state the gate has to be able to judge and had no frame of: the ring closes
        /// RED and the detail flies back to its place over 0.6 s (SCREENS «Сбор», mock 14).
        /// </summary>
        [UnityTest]
        public IEnumerator LevelOne_SlipOfADetail()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
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
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            GameFlow flow = GameTestHarness.Flow();

            GameTestHarness.JumpTo(flow, GamePhase.Level, 0);
            yield return GameTestHarness.Until(() => flow.Phase == GamePhase.Level, "уровень 1");
            var screen = (LevelScreen)flow.Screen;
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");

            // Past the teaching beats — this frame is the ordinary loop, not the tutorial.
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.SwipeUntil(fake, () => screen.Beat == TutorialBeat.Done,
                "обучение пройдено");

            // 06 · игра: деталь едет по нити, мысли на экране — и всё это на ОТГРУЖАЕМОЙ полосе
            // уровня (3 слабых каждые 5 с после ретюна 2026-08-08). Прежняя постановка перебивала
            // интервал на 1.5 с и состав на 1+1, то есть кадр «что игрок видит» снимался с настройками,
            // которых нет ни у кого, кроме этого теста.
            //
            // Ждём не «две мысли в списке», а закрашенных ЧЕРНИЛ в кадре: мысли влетают с краёв, и три
            // штуки — это регулярно три полоски за кромкой (0.67 % чернил против 0.66 % на пустом
            // обзоре — дизайн-гейт 2026-08-08).
            TuningConfig.CollectSeconds = 8f;   // slow enough to be caught mid-thread
            yield return GameTestHarness.NoticeByLooking(fake, screen, FirstUncollected(screen));
            yield return GameTestHarness.WaitForInkOnScreen(fake, screen, StagedThoughtInk);

            // Only the crank here: shaking every frame would pop the waves as fast as they arrive, and
            // this frame is supposed to show what the player is up against.
            yield return GameTestHarness.CrankUntil(fake,
                () => screen.Runtime.Collector.Progress01 > 0.35f, "деталь в пути");
            fake.Next = new BackendSnapshot { CrankDeltaDegrees = GameTestHarness.CrankPerFrame };
            yield return GameTestHarness.Frames(2);
            Shoot("Game06_L1_play");
            AssertTheThoughtsPaintThisFrame(screen, "Game06_L1_play");
            AssertTheAimIsVisibleInThisFrame(screen, "Game06_L1_play");

            // 07 · пик хаоса: перекрытие выше СВОЕГО порога, но игра ещё идёт — края темнеют,
            // сеттинг проступает между мыслями. Кадр набирается ЖИВЫМИ волнами: прежняя постановка
            // раскладывала мысли сеткой 5×3, а такого кадра игра не выдаёт (дизайн-гейт 2026-08-07).
            //
            // И набирается он до ОТГРУЖАЕМОГО порога пика (65 %), а не до заниженного: до возврата
            // скептика 2026-09-22 тест ставил `PeakOverlapPercent = 32f` и ждал 32 % — то есть кадр
            // «вот что творится на пике» снимался при половине того давления, которое описывает её же
            // число. Порог не трогаем вовсе; ждём по Defaults, потому что живое значение уровня — это
            // и есть оно (ApplyLevel порог не переписывает).
            TuningConfig.ThoughtsCoverVessel = true;
            yield return GameTestHarness.CrowdTheScreenByPlaying(fake, screen,
                TuningConfig.Defaults.PeakOverlapPercent);

            Assert.Less(screen.Runtime.Field.OverlapPercent, TuningConfig.LossOverlapPercent,
                "Это уже поражение, а не пик — на кадре должна быть ещё играбельная сцена.");
            Assert.AreEqual(LevelStage.Play, screen.Stage, "Пик — состояние игры, а не исхода.");
            Shoot("Game07_L1_peak");
            AssertTheFrameIsReallyAtThePeak(screen, "Game07_L1_peak");

            // …and this is the frame that has to prove the fill bar survived the toggle: at the peak the
            // thought layer draws ABOVE the vessel, and until 2026-08-08 the bar drew with the vessel and
            // therefore vanished under the blobs at exactly the moment it is the only thing saying how
            // much of the level is left (design gate).
            AssertTheFillBarIsVisibleInThisFrame(screen, "Game07_L1_peak");
            AssertNoThoughtsInsideTheFillBar(screen, "Game07_L1_peak");
            AssertThereIsNoSpeedometer(screen, "Game07_L1_peak");

            // 08 · победа: мысли растворились, портфель ×2 в центре со всей добычей.
            //
            // Здесь выбор детали возвращается на FixedOrder, и это не откат правки этого раунда: кадры
            // 06 и 07 сняты, а кадр победы — не про прицел вовсе. Прицел ПАРКУЕТСЯ, то есть после
            // собранной детали он остаётся стоять там, где стоял, и следующую сам не выберет: чтобы
            // добрать оставшиеся четыре взглядом, тесту пришлось бы возить круг по плите, а это
            // постановка про частоту кадров, а не про игру.
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.ThoughtsCoverVessel = false;
            TuningConfig.CollectSeconds = GameTestHarness.FastCollectSeconds;
            // Stop feeding the field while the last details go in: the screen is already two thirds
            // covered from the peak frame, and more waves on top of it would end in a defeat. The LIVE
            // value, not the level's band — the level is open, and its band was copied in on entry.
            TuningConfig.WaveIntervalSeconds = 20f;
            TuningConfig.WaveWeak = 1;
            TuningConfig.WaveMedium = 0;
            TuningConfig.WaveStrong = 0;

            // …and the peak itself has to be beaten off before the level can be won, which is new with
            // the honest threshold: 65 % of the frame with everything on it still GROWING is four
            // fifths of the way to the loss at 85, and the five remaining details take longer to crank
            // than the blobs take to close the rest. Beaten off with the hand over the sensor — the
            // player's own way out of a peak — rather than by clearing the field from the test.
            yield return GameTestHarness.SwipeUntil(fake,
                () => screen.Runtime.Field.OverlapPercent < 25f, "пик разогнан взмахами", 90f);

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

            // …и наезд камеры на ведёрко (founder 2026-09-22, п.8) — первая половина нового перехода
            // победы, которая идёт ПОВЕРХ полутора секунд бита награды. Замеряется здесь, а не на
            // кадре 08: кадр 08 снимается через два кадра после того, как награда встала, то есть в
            // самом начале наезда, где его ещё нет (0.2 % — что этот же тест и вернул, когда проверка
            // стояла там). К моменту готового экрана зум прошёл целиком.
            Assert.AreEqual(1f, screen.View.ZoomedIn, 0.02f,
                "Наезда на ведёрко нет — сцена за бит награды так и не приблизилась.");
            Assert.AreEqual(LevelView.VictoryZoom, screen.View.SceneLayer.localScale.x, 1e-2f,
                "Слой сцены не отмасштабирован — «наезд на ведёрко» не нарисован.");
            Assert.AreEqual(LevelView.VictoryZoom, screen.View.VesselLayer.localScale.x, 1e-2f,
                "Сосуд в наезде не участвует — а наезд именно на него.");
            Assert.AreEqual(1f, screen.View.OutcomeLayer.localScale.x, 1e-3f,
                "Наезд утащил за собой слой готовых экранов — рендер будет обрезан.");
            Assert.AreEqual(1f, screen.View.HudLayer.localScale.x, 1e-3f,
                "Наезд утащил за собой HUD.");

            // 39 · СЕРЕДИНА ПЕРЕХОДА «круглая рябь» — заказ founder 2026-09-22, п.8, и кадр, который
            // она просила в контракте.
            //
            // Снимается на прогрессе около половины, где рябь и есть рябь: фронт вышел из ведёрка, но
            // кадр ещё не залит. На 0 и на 1 смотреть не на что — это чистый уровень и сплошная вода.
            GameFlow rippleFlow = GameTestHarness.Flow();
            yield return GameTestHarness.Until(() => rippleFlow.Rippling && rippleFlow.FadeAlpha > 0.35f,
                "рябь дошла до середины", 20f);
            Assert.Less(rippleFlow.FadeAlpha, 0.95f, "Рябь уже закрыла кадр — снимать нечего.");
            Shoot("Game39_ripple_mid_transition");
            AssertTheRippleIsReallyOnTheFrame(rippleFlow, "Game39_ripple_mid_transition");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The ripple is PAINTING this frame, and it is painting a round front out of the vessel —
        /// not a rectangle, and not the old fade to black.
        ///
        /// Three claims, because three different things could be true and still leave a frame that
        /// looks vaguely right: the widget could be up with no shader (the flat fallback, which is the
        /// old fade under a new name), the shader could be up and painting nothing, or it could be
        /// painting the whole quad (a wipe with no front). So: the shader is really loaded, the quad
        /// really moves pixels, and the share it moves is strictly between «nothing» and «everything».
        /// </summary>
        private static void AssertTheRippleIsReallyOnTheFrame(GameFlow flow, string frameName)
        {
            Assert.IsNotNull(flow.Ripple, frameName + ": перехода «рябь» нет в потоке.");
            Assert.IsTrue(flow.Rippling, frameName + ": переход идёт не рябью, а фейдом в чёрный.");
            Assert.IsTrue(flow.Ripple.HasShader,
                frameName + ": шейдер ряби не загрузился — на кадре плоская заглушка, то есть " +
                "прежний фейд под новым именем.");

            // The ripple leaves the vessel of the level that has just been won.
            Vector2 vessel = LevelCatalog.At(0).VesselCentre;
            Assert.AreEqual(vessel.x, flow.Ripple.Centre.x, 1f, frameName + ": рябь идёт не из ведёрка.");
            Assert.AreEqual(vessel.y, flow.Ripple.Centre.y, 1f, frameName + ": рябь идёт не из ведёрка.");

            float painted = ShareCoveredBy(flow.Ripple.Image.gameObject);
            Assert.Greater(painted, 0.05f,
                frameName + ": рябь не красит кадр — " + (painted * 100f).ToString("0.0") + " %.");
            Assert.Less(painted, 0.98f,
                frameName + ": рябь залила весь кадр — фронта на кадре нет, это уже не переход.");
        }

        // ---- 09 level 2, 10 level 3 with its overlay, 14 level 4, 15 level 5 -----------------------------

        /// <summary>
        /// The four later levels in play — on their OWN bands, with the SHIPPED way of choosing a detail.
        ///
        /// Both of those are fixes of 2026-08-08. The frames used to be staged in
        /// <see cref="NoticeMode.FixedOrder"/>, which is the variant that switches the neon aim off, so
        /// the gate judged a feature the founder had ordered «крупнее и заметнее» on one frame out of
        /// fifty — and that one was over empty sky. And the wave clock used to be wound to 1.5 s, i.e.
        /// the picture of «what the player is up against» was taken at a pressure nobody ships.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryLaterLevel_InPlay([Values(1, 2, 3, 4)] int levelIndex)
        {
            TuningConfig.CollectSeconds = 8f;

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

            // The aim, parked on the detail it is about to pick — the shipped variant, doing the shipped
            // thing, so the circle is IN the frame and standing on something.
            TuningConfig.CollectSeconds = 8f;
            yield return GameTestHarness.NoticeByLooking(fake, screen, FirstUncollected(screen));

            // …and the level's own band, waited out by the ink it actually paints.
            yield return GameTestHarness.WaitForInkOnScreen(fake, screen, StagedThoughtInk);

            // Only the crank, for the same reason frame 06 uses it: shaking every frame pops the
            // waves as fast as they arrive. This frame is about what the player is up against.
            yield return GameTestHarness.CrankUntil(fake,
                () => screen.Runtime.Collector.Progress01 > 0.3f, "деталь в пути");

            fake.Next = new BackendSnapshot { CrankDeltaDegrees = GameTestHarness.CrankPerFrame };
            yield return GameTestHarness.Frames(2);

            string frame = FrameNameOf(levelIndex);
            Shoot(frame);
            AssertTheThoughtsPaintThisFrame(screen, frame);
            AssertTheAimIsVisibleInThisFrame(screen, frame);
            AssertNoThoughtsInsideTheFillBar(screen, frame);
            AssertThereIsNoSpeedometer(screen, frame);
            LogAssert.NoUnexpectedReceived();
        }

        // ---- 35 · 36 неон-прицел на самых трудных плитах (дизайн-гейт 2026-08-08) ----------------------

        /// <summary>
        /// The neon aim, judged where it is hardest to see and where it is hardest to read.
        ///
        /// This is the finding the whole round turns on. The founder ordered the aim «крупнее и
        /// заметнее» and shipped it as the DEFAULT way of choosing a detail — and forty-nine of the
        /// fifty frames she was then shown were staged in <see cref="NoticeMode.FixedOrder"/>, the
        /// variant that switches the aim off. The one frame that had it (17) was the circle over empty
        /// sky on the emptiest plate of the game. So there was no evidence at all of the two cases that
        /// decide whether the feature works:
        ///
        /// <list type="number">
        /// <item>35 · the circle on a LIGHT, BUSY plate with nothing under it — the metro, whose plate
        ///       is the noisiest in the run. The spot is chosen off the rendered picture (the brightest
        ///       patch that no detail and no HUD widget stands on), because «читается ли неон» is a
        ///       question about the pixels the circle lands on and picking the spot by hand would be
        ///       picking the answer.</item>
        /// <item>36 · the circle standing ON a detail, in the library — the state the aim EXISTS for,
        ///       and the one case where the ring has to be told apart from the drawing under it.</item>
        /// </list>
        /// </summary>
        [UnityTest]
        public IEnumerator TheNeonAim_OnTheNoisiestPlates()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();

            // 35 · метро, светлая и шумная плита, круг на открытом месте.
            yield return GameTestHarness.EnterLevel(fake, 2);
            var metro = (LevelScreen)GameTestHarness.Flow().Screen;

            // Nothing else in the frame: this one is about the circle against the PLATE. Restart rather
            // than Clear, because the aim has been live since the level opened and may already be
            // resting on something — and a ring closing round a detail is a second subject.
            TuningConfig.WaveIntervalSeconds = 300f;
            metro.Runtime.Restart();
            GameTestHarness.ParkTheAim(metro, BrightestOpenSpot(metro));
            yield return GameTestHarness.Idle(fake, 2);

            Assert.Less(metro.Runtime.NoticedIndex, 0,
                "Кадр 35 — про круг на пустой плите, а взгляд что-то заметил.");
            Shoot("Game35_L3_aim_on_the_plate");
            AssertTheAimIsVisibleInThisFrame(metro, "Game35_L3_aim_on_the_plate");

            // 36 · библиотека, круг ПОВЕРХ детали — то, ради чего прицел и существует.
            yield return GameTestHarness.EnterLevel(fake, 3);
            var library = (LevelScreen)GameTestHarness.Flow().Screen;

            TuningConfig.WaveIntervalSeconds = 300f;
            library.Runtime.Restart();

            int target = FirstUncollected(library);
            yield return GameTestHarness.NoticeByLooking(fake, library, target);
            yield return GameTestHarness.Idle(fake, 2);

            Vector2 ink = LevelCatalog.AnchorOf(library.Level.Details[target]);
            Assert.Less(Vector2.Distance(library.Runtime.Gaze.Position, ink), 1f,
                "Кадр 36 снят с кругом не на детали.");
            Shoot("Game36_L4_aim_over_a_detail");
            AssertTheAimIsVisibleInThisFrame(library, "Game36_L4_aim_over_a_detail");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The brightest place on this level's plate where the aim stands on NOTHING — measured off the
        /// rendered frame, not guessed from the catalogue.
        ///
        /// Brightest because that is the hard case for added light: a neon drawn with
        /// <c>Blend SrcAlpha One</c> has the least to give on a plate that is already pale, and the whole
        /// question at the gate is whether the circle survives there.
        /// </summary>
        private static Vector2 BrightestOpenSpot(LevelScreen screen)
        {
            Rect[] taken = LevelCatalog.HintObstaclesOf(screen.Level);
            float radius = GazeSelector.Radius;

            Texture2D plate = StandTestHarness.Capture(Letterbox);
            try
            {
                Vector2 best = new Vector2(960f, 540f);
                float brightest = float.MinValue;

                for (float y = radius + 40f; y <= 1080f - radius - 40f; y += 60f)
                for (float x = radius + 40f; x <= 1920f - radius - 40f; x += 60f)
                {
                    var box = new Rect(x - radius, y - radius, radius * 2f, radius * 2f);

                    bool clear = true;
                    for (int i = 0; i < taken.Length && clear; i++) clear = !box.Overlaps(taken[i]);
                    if (!clear) continue;

                    float light = P95Of(plate, box);
                    if (light <= brightest) continue;
                    brightest = light;
                    best = new Vector2(x, y);
                }

                Assert.Greater(brightest, float.MinValue,
                    "На плите нет ни одного свободного места под прицел — кадр не поставить.");
                return best;
            }
            finally
            {
                Object.DestroyImmediate(plate);
            }
        }

        /// <summary>The first detail of this level still to be collected — what the aim is parked on.</summary>
        private static int FirstUncollected(LevelScreen screen)
        {
            for (int i = 0; i < screen.Level.DetailCount; i++)
                if (!screen.Runtime.Collected[i]) return i;

            Assert.Fail("На уровне не осталось несобранных деталей — прицел не на что ставить.");
            return 0;
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

            // The frame the design skeptic measured Б2 on: a strong blob sits on the mug, which is this
            // level's vessel, and the bar stands right under it. The strip may be covered; the HUD on it
            // may not.
            AssertTheFillBarIsVisibleInThisFrame(screen, "Game23_L2_thoughts_on_strip");
            AssertNoThoughtsInsideTheFillBar(screen, "Game23_L2_thoughts_on_strip");
            AssertThereIsNoSpeedometer(screen, "Game23_L2_thoughts_on_strip");

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
            TuningConfig.ThoughtsCoverVessel = true;

            // Отгружаемый порог пика, а не заниженный — см. кадр 07 и блокер Б2 возврата скептика
            // 2026-09-22.
            yield return GameTestHarness.CrowdTheScreenByPlaying(fake, screen,
                TuningConfig.Defaults.PeakOverlapPercent);

            Assert.Less(screen.Runtime.Field.OverlapPercent, TuningConfig.LossOverlapPercent,
                "Это уже поражение, а не пик.");
            Assert.AreEqual(LevelStage.Play, screen.Stage, "Пик — состояние игры, а не исхода.");
            Assert.Greater(screen.View.PeakEdges.color.a, 0.3f, "Края кадра в пике не темнеют.");

            Shoot("Game24_L3_peak");
            AssertTheFrameIsReallyAtThePeak(screen, "Game24_L3_peak");
            AssertTheFillBarIsVisibleInThisFrame(screen, "Game24_L3_peak");
            AssertNoThoughtsInsideTheFillBar(screen, "Game24_L3_peak");
            AssertThereIsNoSpeedometer(screen, "Game24_L3_peak");
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

            // Caught ON an object, like frame 28 — «somewhere in the middle of the frame» was the whole
            // staging until 2026-08-07, and the middle of a frame is where the band has nothing to
            // light as often as not. The city's middle happens to hold the curtains and a cloud, which
            // is why the shot looked staged at all; that was luck, and luck is not a gate.
            yield return GameTestHarness.Until(
                () => screen.Sweep.Active && BandIsOverAnUnnoticedDetail(screen) &&
                      screen.Sweep.Strength >= TuningConfig.SweepStrength * 0.9f,
                "луч дошёл до детали в полную силу", 30f);

            Shoot("Game26_L5_sweep");
            AssertTheBandIsVisibleInThisFrame(screen, "Game26_L5_sweep");
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
            AssertTheDefeatCopyIsReadable("Game11_defeat");

            // 12 · ретрай: несколько оборотов стёрли часть экрана — уровень проступает обратно.
            yield return GameTestHarness.CrankDegrees(fake, 360f * 4f);
            yield return GameTestHarness.Frames(2);

            Assert.Less(screen.DefeatCoverLeft01, 1f, "Обороты ничего не стёрли.");
            Assert.Greater(screen.DefeatCoverLeft01, 0f, "Стёрли всё — на кадре не видно, что идёт ретрай.");
            Shoot("Game12_defeat_retry");

            // …and the frame the skeptic returned: half the screen wiped away is where the copy has to
            // survive the level showing through it.
            Assert.IsTrue(screen.View.OutcomeTextPlate.gameObject.activeInHierarchy,
                "Плашки под текстом экрана поражения нет в кадре.");
            AssertTheDefeatCopyIsReadable("Game12_defeat_retry");

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
