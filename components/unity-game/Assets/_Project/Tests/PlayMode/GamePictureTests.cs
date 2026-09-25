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
using UnityEngine.UI;

namespace Meditation.Tests
{
    /// <summary>
    /// Claims about the PICTURE, measured off the rendered frame.
    ///
    /// The rest of the suite checks numbers that exist before anything is drawn — where a rectangle is,
    /// which layer sits above which. Three of the design gate's findings could not have been caught
    /// that way, and were not: a slot silhouette is only «нечитаемый» once it is composited on its
    /// plate, a haul is only «вылез из сосуда» against the vessel's SILHOUETTE rather than its
    /// rectangle, and the defeat desaturation was green in the suite while it was raising the frame's
    /// saturation, because the test measured the greybox stand's palette and the game does not use it.
    ///
    /// So these tests render the same way the design gate's frames are rendered
    /// (<see cref="StandTestHarness.Capture"/>) and count pixels.
    /// </summary>
    [Category("Visual")]
    public class GamePictureTests
    {
        /// <summary>Backdrop for a shot of one layer alone — nothing in the drop is magenta.</summary>
        private static readonly Color Nothing = new Color(1f, 0f, 1f);

        /// <summary>How far from the backdrop a pixel has to be to count as the object itself.</summary>
        private const float SolidlyDrawn = 0.35f;

        /// <summary>The part of the frame the drained-thought measurement scans (blobs are crowded in).</summary>
        private static readonly RectInt ScanBox = new RectInt(360, 140, 1200, 800);

        [SetUp]
        public void SetUp()
        {
            StandTestHarness.IsolateTuningFile();
            TuningConfig.ResetToDefaults();
            TuningConfig.PanelVisible = false;
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

        // ---- S9 + N2: «слоты читаются с метра» -------------------------------------------------
        //
        // «EveryHudSlot_ReadsAtAMetre_OnEveryLevel» and its AssertSlotReads stood here until
        // 2026-08-08. They were the guard on the flat-ink silhouettes stamped into the HUD's row of
        // detail slots — 34 details over five levels, measured at ink ≥ 8 % of the slot and contrast
        // ≥ 90 of 255. The founder took the row out of the game («убрать ряд совсем»), and a
        // readability guard on a widget that is not drawn is a test that can only ever be green.
        //
        // What survives of that work is the ICON CROP it forced (LevelCatalog.ArtDetail.IconCrop): the
        // haul inside the vessel asks the same question of the same sprites at the same size, and
        // TheRewardBeat_KeepsTheWholeHaulInsideTheVessel below is where it is now asked.

        // ---- N2 + B2: the victory picture — filled slots, and a haul inside the vessel's SILHOUETTE ----

        /// <summary>
        /// The reward beat, measured against the vessel's SILHOUETTE: every cell of the haul really
        /// lies inside the vessel, not merely inside its bounding rectangle.
        ///
        /// This gate matters more than it did, not less. The row of filled slots that used to stand
        /// under «Собрано: …» is gone with the drop (the drawn «Отлично!» screen replaced both), so the
        /// vessel with its haul is now the ONLY place in the whole run where the player is shown what
        /// they collected — and it is on screen for a second and a half.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRewardBeat_KeepsTheWholeHaulInsideTheVessel(
            [Values(0, 1, 2, 3, 4)] int levelIndex)
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, levelIndex);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelView view = screen.View;
            DesignStage stage = StandTestHarness.Stage();

            StageTheVictory(screen);
            yield return GameTestHarness.SettleScreen(fake);

            // Where the haul is, before anything is hidden.
            var cells = new RectInt[view.VesselContents.Count];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = StandTestHarness.PixelRectOf(stage, view.VesselContents[i].rectTransform);

            // …and now the vessel ALONE, so its silhouette is whatever is not the backdrop.
            ShowOnlyTheVessel(view, true);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Texture2D frame = StandTestHarness.Capture(Nothing);
            try
            {
                for (int i = 0; i < cells.Length; i++)
                {
                    Color[] pixels = StandTestHarness.PixelsOf(frame, cells[i]);
                    Assert.Greater(pixels.Length, 4,
                        screen.Level.Title + ": ячейка добычи " + (i + 1) + " не попала в кадр.");

                    int outside = 0;
                    for (int p = 0; p < pixels.Length; p++)
                        if (MaxChannelDistance(pixels[p], Nothing) < SolidlyDrawn) outside++;

                    Assert.AreEqual(0, outside,
                        screen.Level.Title + " · «" + screen.Level.Details[i].Name + "» в сосуде: " +
                        (outside * 100f / pixels.Length).ToString("0.0") +
                        " % ячейки лежит ВНЕ силуэта сосуда (габарит сосуда — не его форма).");
                }
            }
            finally
            {
                Object.DestroyImmediate(frame);
                ShowOnlyTheVessel(view, false);
            }

            LogAssert.NoUnexpectedReceived();
        }

        // ---- «розовый квадрат в ведре» (founder, живая сессия 2026-09-22) ------------------------------

        /// <summary>
        /// The haul is the DETAIL, never a magenta placeholder — including after the drop's sprites have
        /// been unloaded under a running game.
        ///
        /// The founder played the build on 2026-09-22 and found a pink square sitting in the bucket among
        /// the things she had collected. Nothing in the contract frames showed it: the magenta scan over
        /// the whole shot set was clean, because a batchmode run loads the art once, shoots, and exits.
        ///
        /// The square is drawn on purpose — <c>LevelView.CollectDetail</c> paints a cell
        /// <see cref="Color.magenta"/> when its icon comes back null, so a missing asset is loud instead
        /// of invisible. The question was therefore never «why is it magenta» but «why did the icon come
        /// back null for a sprite that is plainly on disk», and the answer is in the CACHE rather than in
        /// the asset: <c>ArtLibrary</c> kept its entries in a dictionary that was never cleared and asked
        /// it only whether the key was present. A destroyed <c>UnityEngine.Object</c> is a present key
        /// and a null value at the same time — so once an entry died, every later collection of that
        /// detail was a pink square for the rest of the session.
        ///
        /// The entries that die are the <c>Sprite.Create</c> ones (icon crops, level 3's baked vessel):
        /// they belong to no scene and no asset file, and the stand's level launcher does a single-mode
        /// <c>SceneManager.LoadScene</c> on every run with <c>Resources.UnloadUnusedAssets</c> behind it.
        ///
        /// The mechanism itself is pinned in EditMode, where it can be provoked without a game running
        /// (<c>LevelCatalogTests.TheArtCache_HandsBackALiveSprite_AfterACachedOneIsUnloaded</c>). What is
        /// checked here is the picture the founder was looking at: the beat played the ordinary way, the
        /// haul built out of it, and the close frame of the vessel her report asked for.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHaul_IsTheDetailItself_AndNeverAMagentaSquare()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            // Level 1 opens in the lesson, and the lesson gates the loop: the first detail is the сбор
            // beat, and the NEXT one cannot start until the отгон beat has been answered. So the
            // tutorial is played out rather than skipped — the same staging Game06_L1_play uses — and
            // only then does the ordinary loop run. Collecting in a bare loop hangs on the second
            // detail, which is how this test first went red.
            //
            // The wave interval is left alone until the lesson is done, for the same reason: the отгон
            // beat needs a thought to be there before it can be answered.
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.SwipeUntil(fake, () => screen.Beat == TutorialBeat.Done,
                "обучение пройдено");

            // Now nothing more may spawn: this frame is about the vessel.
            TuningConfig.WaveIntervalSeconds = 300f;

            for (int i = 0; i < 2; i++)
                yield return GameTestHarness.CollectOneDetail(fake, screen);

            Assert.AreEqual(3, screen.View.VesselContents.Count,
                "Три детали собраны, а в сосуде лежит другое число.");
            AssertNoMagentaInTheHaul(screen, "три детали в ведре");

            yield return GameTestHarness.SettleScreen(fake);

            // The founder's own view: the vessel up close, with the haul in it.
            Rect vessel = LevelCatalog.VesselRectOf(screen.Level);
            StandTestHarness.ShootCloseUp("Game41_L1_haul_in_the_vessel", Letterbox, vessel, 2);

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// Every cell of the haul carries a live sprite and is tinted white — the two halves of «это
        /// деталь, а не заглушка», checked on the object rather than on the pixel so the message can
        /// name the detail that failed.
        /// </summary>
        private static void AssertNoMagentaInTheHaul(LevelScreen screen, string when)
        {
            IReadOnlyList<Image> haul = screen.View.VesselContents;
            for (int i = 0; i < haul.Count; i++)
            {
                string what = screen.Level.Details[i].Name + " (" + when + ")";

                // Unity's own null, not NUnit's: a destroyed Image is a live C# reference.
                Assert.IsTrue(haul[i] != null, what + ": ячейка добычи исчезла из сосуда.");
                Assert.IsTrue(haul[i].sprite != null,
                    what + ": в сосуде лежит ячейка без спрайта — это и есть розовый квадрат.");
                Assert.IsTrue(haul[i].sprite.texture != null,
                    what + ": спрайт ячейки есть, а его текстура выгружена.");
                Assert.AreNotEqual(Color.magenta, haul[i].color,
                    what + ": ячейка добычи покрашена в мадженту (заглушка отсутствующего ассета).");
            }
        }

        /// <summary>The letterbox the game's own frames are shot on — black, as everywhere else.</summary>
        private static readonly Color Letterbox = Color.black;

        // ---- S5: the defeat screen IS the drawn screen, and the crank wipes it ------------------------

        /// <summary>
        /// The loss frame, measured on the rendered pixel.
        ///
        /// Two earlier versions of this gate are worth remembering, because both were measuring the
        /// wrong thing by the time they ran. The first fixed a saturation band (0.07…0.26) taken off
        /// mock 17's turquoise silhouettes; the drop of 2026-08-05 made that unmeasurable rather than
        /// merely wrong — black hatching has saturation 0 by construction. The second measured the
        /// contrast of «Мысли захватили всё. Вдохни.» against its own white plate; that line is
        /// withdrawn, and the sentence on this screen is now pixels the designer set.
        ///
        /// What is left to check is what the flow is actually responsible for: the drawn screen really
        /// covers the frame, it is not a black rectangle, and the crank really wipes it back to the
        /// level underneath. Everything about how it LOOKS is the render's business now.
        /// </summary>
        [UnityTest]
        public IEnumerator DefeatScreen_CoversTheFrame_AndTheCrankWipesItBackToTheLevel()
        {
            TuningConfig.AutoRetry = false;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");

            // The level as it stands, for the «wiped back to» comparison.
            yield return GameTestHarness.SettleScreen(fake);
            Texture2D beforeLoss = StandTestHarness.Capture(Color.black);

            GameTestHarness.BuryTheScreen(screen);
            yield return GameTestHarness.Idle(fake, 4);
            Assert.AreEqual(LevelStage.Lose, screen.Stage, "Уровень не проигран — мерить нечего.");
            yield return GameTestHarness.Frames(2);
            Canvas.ForceUpdateCanvases();

            Texture2D lost = StandTestHarness.Capture(Color.black);
            Texture2D wiped = null;
            try
            {
                // 1. It really is a different picture — the drawn screen is on, whole.
                float changed = MeanDifference(beforeLoss, lost);
                Assert.Greater(changed, MinDefeatChange,
                    "Кадр поражения почти не отличается от кадра уровня (" + changed.ToString("0.000") +
                    ") — готовый экран не встал.");

                // 2. …and it is not «просто чёрный экран». Measured at the 95th percentile, not at
                //    the median: the designer's loss screen IS mostly dark (a wall of black hatching
                //    over the scene — median luminance 0.006 in the file itself), and what makes it a
                //    screen rather than a void is the light that survives it: the glowing sentence and
                //    the green leaf. A missing sprite falls back to flat black, where every percentile
                //    is zero, and that is the failure this catches.
                float light = LuminanceQuantile(lost, 0.95f);
                Assert.GreaterOrEqual(light, MinDefeatHighlight,
                    "На экране поражения не осталось света: 95-й процентиль яркости " +
                    light.ToString("0.000") + " при поле " + MinDefeatHighlight.ToString("0.000") + ".");

                // 3. Five turns, half the screen: the retry is a dissolve the player drives, and it
                //    has to be visible on the PIXEL, not only in the number. What it dissolves back
                //    into is the level as it was LOST — thoughts and all — so the comparison is
                //    against the loss frame rather than against the clean level.
                yield return GameTestHarness.CrankDegrees(fake, 360f * 5f);
                yield return GameTestHarness.Frames(2);
                Canvas.ForceUpdateCanvases();

                Assert.That(screen.DefeatCoverLeft01, Is.InRange(0.3f, 0.7f),
                    "Пять оборотов должны стереть примерно половину экрана поражения.");

                wiped = StandTestHarness.Capture(Color.black);
                float wipedAway = MeanDifference(lost, wiped);
                Assert.Greater(wipedAway, MinWipeChange,
                    "Пять оборотов не изменили картинку (" + wipedAway.ToString("0.000") +
                    ") — экран поражения не стирается, а только считается стёртым.");
            }
            finally
            {
                Object.DestroyImmediate(beforeLoss);
                Object.DestroyImmediate(lost);
                if (wiped != null) Object.DestroyImmediate(wiped);
            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// How different two frames have to be before «другая картинка» is a fair description. The
        /// shipped pair measures far above it; the floor is set where a half-transparent overlay would
        /// still pass and a missing sprite (nothing drawn at all) would not.
        /// </summary>
        private const float MinDefeatChange = 0.08f;

        /// <summary>
        /// …and the same question for the WIPE, which is a smaller number and has to be.
        ///
        /// The retry dissolves one dark picture into another: the designer's loss screen is a wall of
        /// black hatching, and what shows through it is a level buried in black hatching of its own.
        /// Since 2026-08-08 that second half is darker still — the thoughts lost their light halo
        /// («мысли чисто чёрные», founder) — so five turns of the handle now move the mean pixel by
        /// 0.044 where they used to move it past 0.08. Nothing about the wipe changed; what changed is
        /// what it uncovers. The floor is set at 0.03: an order of magnitude above a frame where the
        /// handle did nothing (the thoughts' own drift over two frames, ~0.00x), and comfortably under
        /// the measurement.
        /// </summary>
        private const float MinWipeChange = 0.03f;

        /// <summary>Mean absolute difference of two captured frames, 0..1, sampled every 8th pixel.</summary>
        private static float MeanDifference(Texture2D a, Texture2D b)
        {
            double sum = 0;
            int count = 0;
            for (int y = 0; y < a.height; y += 8)
            for (int x = 0; x < a.width; x += 8)
            {
                Color p = a.GetPixel(x, y);
                Color q = b.GetPixel(x, y);
                sum += Mathf.Abs(p.r - q.r) + Mathf.Abs(p.g - q.g) + Mathf.Abs(p.b - q.b);
                count += 3;
            }

            return count == 0 ? 0f : (float)(sum / count);
        }

        /// <summary>Relative luminance at a percentile of a whole frame, sampled every 8th pixel.</summary>
        private static float LuminanceQuantile(Texture2D frame, float quantile)
        {
            // Every eighth pixel in both axes: a median does not need 2 M samples, and a full
            // GetPixels of a 1920×1080 frame is 33 MB of garbage.
            var samples = new List<float>(1 << 16);
            for (int y = 0; y < frame.height; y += 8)
            for (int x = 0; x < frame.width; x += 8)
                samples.Add(LevelOneData.RelativeLuminance(frame.GetPixel(x, y)));

            samples.Sort();
            int at = Mathf.Clamp(Mathf.RoundToInt(quantile * (samples.Count - 1)), 0, samples.Count - 1);
            return samples[at];
        }

        /// <summary>
        /// The light that has to survive on the defeat picture, WCAG relative luminance at the 95th
        /// percentile. The render itself measures 0.072 there and 0.166 at the 99th; a black frame
        /// measures zero at every percentile. Half of what the shipped screen does leaves room for a
        /// darker render without letting a blank one through.
        /// </summary>
        private const float MinDefeatHighlight = 0.035f;

        // ---- N3: the marker thoughts are black, and the halo is a switch ------------------------------

        /// <summary>
        /// «Мысли чисто чёрные» (founder, 2026-08-08) — held on the pixels, in both directions.
        ///
        /// This is the same gate as before, turned inside out. It used to prove the light halo under the
        /// hatching was doing its job: the ink that lands on a piece of plate too dark to read black
        /// against had to bring its own light with it, measured as the brightest pixel within a few px.
        /// Every level passed at 0.871 with the halo and failed at 0.23–0.25 without it, so the number
        /// was real — and then the founder looked at the game and decided the halo costs more than the
        /// contrast buys. That does not make the measurement wrong, it makes it the TOGGLE's.
        ///
        /// So the claim is now two claims:
        ///
        /// 1. **as shipped** — the thought layer paints a substantial amount of ink DARKER than the plate
        ///    under it, and no meaningful amount of light. That is what «чёрные каракули» is, in pixels;
        ///    it is also the thing that would silently break if a future sprite pass shipped the drop's
        ///    scribbles on their own white canvases.
        /// 2. **negative control** — switching <see cref="TuningConfig.ThoughtBacking"/> back on VISIBLY
        ///    brightens the same frame around the same strokes. Without this half, «подложки нет» would
        ///    be satisfied by a shader that quietly stopped loading, and the founder's own way of
        ///    comparing the two would be broken with the suite green.
        ///
        /// The thought layer is switched off and on to find the ink: the plates have blacks of their own
        /// (the L4 railing, the L5 monitors), and without the difference the measurement would be
        /// judging the scenery.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryLevelsThoughts_AreBlackInk_AndTheHaloIsAToggle(
            [Values(0, 1, 2, 3, 4)] int levelIndex)
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, levelIndex);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelView view = screen.View;
            string where = screen.Level.Title;

            Assert.IsFalse(TuningConfig.ThoughtBacking,
                "Подложка приезжает включённой — решение founder 2026-08-08 было обратным.");

            GameTestHarness.CrowdTheScreen(screen, 6);
            yield return GameTestHarness.SettleScreen(fake);
            Assert.Greater(screen.Runtime.Field.Thoughts.Count, 0, "На кадре нет мыслей.");

            // Every halo object really is off — the state behind the pixels, so a red pixel test below
            // can be told apart from a level that simply drew no thoughts.
            foreach (ArtThoughtView thought in view.ThoughtViews)
                Assert.IsFalse(HaloOf(thought).activeInHierarchy,
                    where + ": подложка мысли всё ещё рисуется при выключенном тогглере.");

            Texture2D black = StandTestHarness.Capture(Color.black);
            view.ThoughtsLayer.gameObject.SetActive(false);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Texture2D plateOnly = StandTestHarness.Capture(Color.black);
            view.ThoughtsLayer.gameObject.SetActive(true);
            yield return null;

            TuningConfig.ThoughtBacking = true;
            yield return GameTestHarness.Frames(2);
            Canvas.ForceUpdateCanvases();
            Texture2D haloed = StandTestHarness.Capture(Color.black);

            try
            {
                // 1. as shipped: dark ink, and hardly any light.
                Counted ink = Compare(plateOnly, black);
                Assert.Greater(ink.Darker, MinThoughtInkPixels,
                    where + ": слой мыслей затемнил всего " + ink.Darker +
                    " px — чёрных каракулей на кадре практически нет (порог " +
                    MinThoughtInkPixels + ").");
                Assert.Less(ink.Brighter, ink.Darker * MaxLightShareOfInk,
                    where + ": слой мыслей высветлил " + ink.Brighter + " px против " + ink.Darker +
                    " затемнённых — на мыслях снова белое.");

                // 2. negative control: the halo comes back and it is LIGHT.
                Counted halo = Compare(black, haloed);
                Assert.Greater(halo.Brighter, MinHaloPixels,
                    where + ": тогглер подложки включён, а кадр не посветлел (" + halo.Brighter +
                    " px при пороге " + MinHaloPixels + ") — подложка сломана, сравнить их нечем.");
                // …and it is LIGHT, not merely different. Not a clean sweep, and it should not be
                // asked to be: the halo is drawn under the ink and dilated past it, so on the plates'
                // own bright patches (the metro's lit windows) an off-white halo replaces something
                // brighter than itself. Twice as much light as shadow is the shape of «подложка», and
                // the metro — the level that measures worst — comes in at 3.2×.
                Assert.Greater(halo.Brighter, halo.Darker * 2f,
                    where + ": подложка не светлее того, что была под ней.");
            }
            finally
            {
                TuningConfig.ThoughtBacking = TuningConfig.Defaults.ThoughtBacking;
                Object.DestroyImmediate(black);
                Object.DestroyImmediate(plateOnly);
                Object.DestroyImmediate(haloed);
            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>The halo image of a thought — a SIBLING built just before it, so it draws under it.</summary>
        private static GameObject HaloOf(ArtThoughtView thought)
        {
            Transform parent = thought.Rect.parent;
            int at = thought.Rect.GetSiblingIndex();
            Assert.Greater(at, 0, "У мысли нет объекта подложки перед ней.");
            return parent.GetChild(at - 1).gameObject;
        }

        /// <summary>How many pixels of one frame got lighter / darker than the same pixel of another.</summary>
        private struct Counted
        {
            public int Brighter;
            public int Darker;
        }

        /// <summary>Compare two captures inside <see cref="ScanBox"/>, by relative luminance.</summary>
        private static Counted Compare(Texture2D before, Texture2D after)
        {
            Color[] a = before.GetPixels(ScanBox.x, ScanBox.y, ScanBox.width, ScanBox.height);
            Color[] b = after.GetPixels(ScanBox.x, ScanBox.y, ScanBox.width, ScanBox.height);

            var counted = new Counted();
            for (int i = 0; i < a.Length; i++)
            {
                float delta = LevelOneData.RelativeLuminance(b[i]) - LevelOneData.RelativeLuminance(a[i]);
                if (delta > LuminanceStep) counted.Brighter++;
                else if (delta < -LuminanceStep) counted.Darker++;
            }
            return counted;
        }

        /// <summary>How far a pixel has to move in luminance to count as changed at all.</summary>
        private const float LuminanceStep = 0.02f;

        /// <summary>Ink a screen of six thoughts owes the frame, px — a floor, not a measurement.</summary>
        private const int MinThoughtInkPixels = 5000;

        /// <summary>…and the light the halo owes it when it is switched back on.</summary>
        private const int MinHaloPixels = 5000;

        /// <summary>
        /// How much light the thoughts may still carry with the halo off, as a share of their own ink.
        ///
        /// Not zero, and deliberately: the row of pips under a thought keeps its light discs — it is a
        /// READOUT of how many hits are left, and a black dot on a night street is not one. The founder's
        /// sentence was about the hatching's outline, which is the thing that made a scribble look like a
        /// white blob; the pips are twelve px across. A fifth leaves them room and still fails the moment
        /// the strokes themselves get anything white back.
        /// </summary>
        private const float MaxLightShareOfInk = 0.2f;

        // ---- staging helpers ---------------------------------------------------------------------------

        /// <summary>
        /// The victory picture without playing the level out: every detail into the vessel, the tableau
        /// up, the HUD away. What is under test is the composition, not the road to it — the road has
        /// its own test.
        /// </summary>
        private static void StageTheVictory(LevelScreen screen)
        {
            LevelView view = screen.View;
            for (int i = 0; i < screen.Level.DetailCount; i++) view.CollectDetail(i);
            view.SetHudVisible(false);
            view.StageVictory();
            Canvas.ForceUpdateCanvases();
        }

        private static void ShowOnlyTheVessel(LevelView view, bool alone)
        {
            view.SceneLayer.gameObject.SetActive(!alone);
            view.DetailsLayer.gameObject.SetActive(!alone);
            view.ThreadLayer.gameObject.SetActive(!alone);
            view.ThoughtsLayer.gameObject.SetActive(!alone);
            view.PeakLayer.gameObject.SetActive(!alone);
            view.HudLayer.gameObject.SetActive(!alone);
            view.MessageLayer.gameObject.SetActive(!alone);

            // The haul itself has to go too, or every cell would be «inside» its own picture.
            for (int i = 0; i < view.VesselContents.Count; i++)
                if (view.VesselContents[i] != null) view.VesselContents[i].enabled = !alone;
        }

        // ---- неон-прицел (заказ founder 2026-08-07) ---------------------------------------------------

        /// <summary>
        /// «Круг взгляда крупнее и заметнее» — measured on the rendered plate, because that is the one
        /// place the old one failed.
        ///
        /// The circle it replaces was three UGUI primitives (a 0.25-alpha disc, a dashed ring, an arc),
        /// and every layout test it had was green: the rect was there, the right size, in the right
        /// place. On a photographic plate it moved the pixel by a handful of values and the founder
        /// could not find it. So this asks the only question that was ever the point — how much of the
        /// frame CHANGED when the aim came on, and by how much — with the aim off as the baseline and
        /// the same shot at glow 0 as the proof that the rig can say «no».
        /// </summary>
        [UnityTest]
        public IEnumerator TheNeonAim_IsVisiblyOnThePlate_OnEveryLevel([Values(0, 1, 2, 3, 4)] int levelIndex)
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, levelIndex);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelView view = screen.View;
            string where = "уровень " + screen.Level.Number + " («" + screen.Level.Title + "»)";
            yield return GameTestHarness.SettleScreen(fake);

            // A patch of the plate the aim is put on, and the same patch with no aim at all.
            var spot = new Vector2(960f, 420f);
            var box = new RectInt(960 - 220, 420 - 220, 440, 440);

            view.SetGaze(spot, 0f, false);
            Texture2D bare = StandTestHarness.Capture(Color.black);

            view.SetGaze(spot, 0f, true);
            Texture2D lit = StandTestHarness.Capture(Color.black);

            float glowWas = TuningConfig.GazeNeonGlow;
            TuningConfig.GazeNeonGlow = 0f;
            view.SetGaze(spot, 0f, true);
            Texture2D dark = StandTestHarness.Capture(Color.black);
            TuningConfig.GazeNeonGlow = glowWas;

            try
            {
                float moved = MovedShare(lit, bare, box, out float lift);

                Assert.Greater(moved, 0.03f,
                    where + ": неон-прицел почти не тронул кадр — сдвинулось " +
                    (moved * 100f).ToString("0.00") + " % площадки при пороге 3 %.");
                Assert.Greater(lift, 24f,
                    where + ": неон-прицел слишком бледный — +" + lift.ToString("0.0") +
                    " ед. по сдвинувшимся пикселям при пороге 24.");

                float zeroMoved = MovedShare(dark, bare, box, out _);
                Assert.Less(zeroMoved, moved * 0.5f,
                    where + ": замер врёт — при свечении 0 кадр изменился почти так же.");
            }
            finally
            {
                Object.DestroyImmediate(bare);
                Object.DestroyImmediate(lit);
                Object.DestroyImmediate(dark);
            }

            // …and «крупнее» is the other half of the order: the drawn circle has to be bigger than
            // the 90 px SCREENS drew for the greybox.
            Rect drawn = StandTestHarness.Stage().DesignRectOf(view.Gaze.rectTransform);
            Assert.Greater(drawn.width * 0.5f / LevelView.GazeQuadMargin, GazeSelector.ScreensRadius,
                where + ": круг взгляда не стал крупнее прежних 90 px.");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- неон-обводка деталей [toggle] -------------------------------------------------------------

        /// <summary>
        /// The outline ships OFF and draws NOTHING while it is off — then visibly rims the detail when
        /// the founder turns it on.
        ///
        /// Both halves are the order («по умолчанию ВЫКЛ, чтобы founder сравнила с пульсом и лучом»),
        /// and the off half is the one worth a pixel test: an outline at strength 0 that still shifts
        /// the frame would quietly be a third highlight in every comparison she is trying to make.
        /// </summary>
        [UnityTest]
        public IEnumerator TheNeonOutline_DrawsNothingUntilItIsSwitchedOn_ThenRimsTheDetail()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 1);   // офис: компактные детали на плите

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelView view = screen.View;
            yield return GameTestHarness.SettleScreen(fake);

            Assert.IsFalse(TuningConfig.DetailNeonOutline, "Обводка обязана приезжать выключенной.");
            for (int i = 0; i < view.DetailOutlines.Count; i++)
                if (view.DetailOutlines[i] != null)
                    Assert.IsFalse(view.DetailOutlines[i].gameObject.activeSelf,
                        "Выключенная обводка обязана быть НЕ нарисована, а не нарисована прозрачной.");

            Texture2D off = StandTestHarness.Capture(Color.black);

            TuningConfig.DetailNeonOutline = true;
            view.ApplyNeonOutline();
            yield return GameTestHarness.Frames(2);
            Texture2D on = StandTestHarness.Capture(Color.black);

            try
            {
                // The snail toy: 205×115 of solid ink with a clear edge — the shape a rim shows on.
                Rect detail = LevelCatalog.RectOf(screen.Level.Details[3]);
                var box = new RectInt(
                    Mathf.RoundToInt(detail.xMin) - 20, Mathf.RoundToInt(detail.yMin) - 20,
                    Mathf.RoundToInt(detail.width) + 40, Mathf.RoundToInt(detail.height) + 40);

                float moved = MovedShare(on, off, box, out float lift);
                Assert.Greater(moved, 0.02f,
                    "Включённая обводка не тронула кадр вокруг детали — сдвинулось " +
                    (moved * 100f).ToString("0.00") + " %.");
                Assert.Greater(lift, 14f,
                    "Обводка слишком бледная: +" + lift.ToString("0.0") + " ед. по сдвинувшимся пикселям.");

                // …and a patch of bare plate well away from any detail must not have moved: the rim is
                // the sprite's own alpha, so it may not be painting the level.
                var elsewhere = new RectInt(700, 760, 160, 120);
                float bled = MovedShare(on, off, elsewhere, out _);
                Assert.Less(bled, 0.01f,
                    "Обводка светит по плите, а не по спрайтам: пустой участок фона изменился на " +
                    (bled * 100f).ToString("0.00") + " %.");
            }
            finally
            {
                TuningConfig.DetailNeonOutline = false;
                Object.DestroyImmediate(off);
                Object.DestroyImmediate(on);
            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// Share of <paramref name="box"/> whose pixels moved between the two frames, and the p95 rise
        /// among the ones that did.
        ///
        /// p95 of the MOVED pixels rather than of the whole box, and that distinction is the lesson of
        /// the light sweep's gate (2026-08-07): a bright ring crossing a mostly-unchanged patch is a
        /// huge rise in a small minority of pixels, and any statistic over the whole box averages it
        /// back down to «ничего не произошло» — which was exactly the frame a human read as «луча нет».
        /// </summary>
        private static float MovedShare(Texture2D after, Texture2D before, RectInt box, out float lift)
        {
            Color[] a = StandTestHarness.PixelsOf(after, box);
            Color[] b = StandTestHarness.PixelsOf(before, box);
            Assert.Greater(a.Length, 100, "Площадка замера не попала в кадр.");
            Assert.AreEqual(a.Length, b.Length);

            var rises = new List<float>();
            for (int i = 0; i < a.Length; i++)
            {
                float delta = MaxChannelDistance(a[i], b[i]) * 255f;
                if (delta >= 6f) rises.Add(delta);
            }

            lift = 0f;
            if (rises.Count > 0)
            {
                rises.Sort();
                lift = rises[Mathf.Clamp(Mathf.RoundToInt(rises.Count * 0.95f) - 1, 0, rises.Count - 1)];
            }

            return rises.Count / (float)a.Length;
        }

        // ---- Б2: «HUD они не закрывают», measured, with the negative control ---------------------------

        /// <summary>
        /// A thought put exactly ON the fill bar changes nothing inside it — and the same measurement
        /// goes red the moment the HUD is dropped below the thought layer.
        ///
        /// Blocker Б2 of the design gate, 2026-08-08: SCREENS §S3 says «мысли HUD не закрывают», and
        /// the gate's own frames measured 23.2 % (Game23) and 10.4 % (Game15) of the bar's pixels being
        /// the white discs under the pips. The layer order was already right — the bar has been on the
        /// HUD layer since that morning — so a Z-order assert would have been green through the whole
        /// life of the bug. What was wrong was OPACITY: a 22 %-black track is a window.
        ///
        /// The negative control is the point of the test. A guard that has never been seen to fail is a
        /// guard nobody knows the sign of, and this one is measured through two captures and a boolean:
        /// there are several ways for it to read zero for reasons that have nothing to do with the bar
        /// (an empty frame, a box off screen, a thought that drifted away). So the same call is made
        /// with the HUD deliberately under the thoughts, and it has to come back well over the ceiling.
        /// </summary>
        [UnityTest]
        public IEnumerator TheFillBar_IsOpaqueOverTheThoughts_AndTheGuardCatchesItWhenItIsNot()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelView view = screen.View;

            // Staged, not hoped for: a strong blob laid on the bar's own centre through the real field.
            Rect bar = LevelCatalog.VesselBarRectOf(screen.Level);
            screen.Runtime.Field.SpawnAt(ThoughtStrength.Strong, screen.Level.ThoughtSprites[0],
                bar.center);
            yield return GameTestHarness.Idle(fake, 3);
            Assert.GreaterOrEqual(screen.Runtime.Field.Thoughts.Count, 1, "Мысль на полосе не появилась.");

            DesignStage stage = StandTestHarness.Stage();
            RectInt box = StandTestHarness.PixelRectOf(stage, view.VesselFillTrack.rectTransform);
            Assert.Greater(box.width * box.height, 100, "Полоса наполнения не попала в кадр.");

            float through = StandTestHarness.ShareCoveredBy(view.ThoughtsLayer.gameObject, Color.black, box);
            Assert.Less(through, MaxThoughtInkInHud,
                "Сквозь полосу наполнения видно мысль: " + (through * 100f).ToString("0.0") +
                " % её пикселей при потолке " + (MaxThoughtInkInHud * 100f) + " %.");

            // …and the control: the same bar, the same blob, the HUD one layer lower.
            int hud = view.HudLayer.GetSiblingIndex();
            view.HudLayer.SetSiblingIndex(view.ThoughtsLayer.GetSiblingIndex());
            Canvas.ForceUpdateCanvases();

            float under = StandTestHarness.ShareCoveredBy(view.ThoughtsLayer.gameObject, Color.black, box);

            view.HudLayer.SetSiblingIndex(hud);
            Canvas.ForceUpdateCanvases();

            Assert.Greater(under, MaxThoughtInkInHud * 2f,
                "Гард слепой: полосу опустили ПОД слой мыслей, а замер остался " +
                (under * 100f).ToString("0.0") + " %. Он не о том, что происходит с картинкой.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>How much of a HUD widget's own area the thought layer may move — see the guard above.</summary>
        private const float MaxThoughtInkInHud = 0.04f;

        // ---- прятки: «проявление при захвате» (контракт 2026-09-25) ----------------------------------

        /// <summary>
        /// One hideaway, and how it is staged so the two frames differ in exactly one thing.
        /// </summary>
        private struct Hideaway
        {
            public int LevelIndex;
            public string Sprite;

            /// <summary>How far along the thread the second frame is taken.</summary>
            public float Progress;

            /// <summary>
            /// How many times more of the frame the reveal has to paint than the fragment does.
            ///
            /// Per hideaway, because the two are not the same kind of reveal and one floor for both
            /// would be the looser one. The shark is a fin that turns into an animal — composited off
            /// the catalogue's own numbers it goes from 3 657 painted pixels to 21 952, i.e. ×6.0. The
            /// goose's fragment is already most of a goose (head, neck and a segment of body, 17 486 px)
            /// and what appears is the rest of it (28 582 px), so the honest ceiling there is ×1.63 and
            /// the floor has to sit under it. Numbers measured 2026-09-25; the floors leave each about
            /// a fifth of room.
            /// </summary>
            public float MinGrowth;

            public string RestFrame;
            public string HaulFrame;
        }

        /// <summary>
        /// The three of the drop, with the progress each one's reveal frame needs.
        ///
        /// The numbers are not a taste: level 3's goose is off the LEFT EDGE of the frame at rest
        /// («тело за пределами экрана», founder), so a frame taken the instant the thread goes tight
        /// would be a frame of a goose still mostly outside the picture — it needs about a third of the
        /// way to the bag to be wholly inside it. Level 5's curtain has to have travelled far enough to
        /// be OFF the window it covers, or «под ними окно с дамой» is a claim about a woman nobody can
        /// see. Level 1's shark needs nothing: it is revealed in place.
        /// </summary>
        private static readonly Hideaway[] Hideaways =
        {
            new Hideaway
            {
                LevelIndex = 0, Sprite = "L1/objects/shark-fin", Progress = 0.08f, MinGrowth = 3.0f,
                RestFrame = "Game42_L1_shark_at_rest", HaulFrame = "Game43_L1_shark_hauled"
            },
            new Hideaway
            {
                LevelIndex = 2, Sprite = "L3/objects/goose", Progress = 0.32f, MinGrowth = 1.35f,
                RestFrame = "Game44_L3_goose_at_rest", HaulFrame = "Game45_L3_goose_hauled"
            },
            new Hideaway
            {
                LevelIndex = 4, Sprite = "L5/objects/curtains", Progress = 0.30f, MinGrowth = 0f,
                RestFrame = "Game46_L5_window_curtained", HaulFrame = "Game47_L5_window_uncovered"
            }
        };

        /// <summary>
        /// A hideaway shows what it hides ONLY while it is being hauled — measured on the pixel, with a
        /// negative control, on all three of them.
        ///
        /// This is the gate for the mechanic the founder ordered on 2026-09-25 («видим всю акулу только,
        /// когда начинаем её перетаскивать»), and it has to be a picture test for the same reason the
        /// haul-inside-the-vessel gate is: the state is decidable from the objects, but «полный арт
        /// реально виден» is a statement about how much of the frame the drawing paints, and a swap that
        /// re-points a sprite at a rectangle the wrong size would satisfy every state check and show
        /// nothing.
        ///
        /// Two claims per hideaway, and both are differences rather than levels:
        ///
        ///   * the detail's own image paints substantially MORE of the frame while it is hauled than at
        ///     rest — for the kind whose fragment turns into the whole thing (шарк, гусь);
        ///   * the drawing waiting underneath paints NOTHING at rest and most of its own rectangle once
        ///     the thing over it has moved off — for the kind that merely covers something (шторы).
        ///
        /// …and the negative control is an ordinary detail of the SAME level, measured in the same two
        /// frames: it has to paint the same amount in both. Without it a test that only ever says «the
        /// frame changed» would pass on a frame that changed for any other reason — a wave arriving, the
        /// pulse breathing, the light band crossing. All three are switched off here for exactly that
        /// reason, and the control is what proves they are.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryHideaway_ShowsWhatItHides_OnlyWhileItIsHauled([Values(0, 1, 2)] int which)
        {
            Hideaway staged = Hideaways[which];

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, staged.LevelIndex);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;

            // Level 1 opens in the lesson and the lesson gates the loop, so it is played out first —
            // the same staging the haul gate uses. Its собор beat takes the plane (fixed order, index
            // 0); the shark is the fourth detail and is still lying in the water afterwards.
            if (staged.LevelIndex == 0)
            {
                yield return GameTestHarness.CollectOneDetail(fake, screen);
                yield return GameTestHarness.SwipeUntil(fake, () => screen.Beat == TutorialBeat.Done,
                    "обучение пройдено");
            }

            QuietTheLevelDownForAPicture(staged.LevelIndex);

            int index = IndexOfDetail(screen.Level, staged.Sprite);
            int control = ControlDetail(screen, index);
            ArtDetail spec = screen.Level.Details[index];

            GameObject picture = screen.View.DetailImages[index].gameObject;
            GameObject other = screen.View.DetailImages[control].gameObject;

            // ---- 1. покой ---------------------------------------------------------------------------
            yield return NoticeExactly(fake, screen, index);
            yield return GameTestHarness.Idle(fake, 2);

            Assert.AreEqual(index, screen.Runtime.NoticedIndex,
                "Заметили не ту деталь — кадр покоя снимать не с чего.");
            Assert.IsFalse(screen.View.IsCapturedNow(index),
                "«" + spec.Name + "»: деталь только замечена, а прятка уже раскрыта — покоя не осталось.");

            float restPicture = StandTestHarness.ShareCoveredBy(picture, Letterbox);
            float restBehind = ShareOfWhatIsHiding(screen, index);
            float restControl = StandTestHarness.ShareCoveredBy(other, Letterbox);
            StandTestHarness.Shoot(staged.RestFrame, Letterbox);

            // ---- 2. захват --------------------------------------------------------------------------
            yield return GameTestHarness.CrankUntil(fake,
                () => screen.Runtime.Collector.Progress01 >= staged.Progress,
                "«" + spec.Name + "» вытянута на " + (staged.Progress * 100f).ToString("0") + " %");
            yield return GameTestHarness.Frames(2);

            Assert.IsTrue(screen.View.IsCapturedNow(index),
                "«" + spec.Name + "»: нить натянута, а прятка не раскрылась.");

            float haulPicture = StandTestHarness.ShareCoveredBy(picture, Letterbox);
            float haulBehind = ShareOfWhatIsHiding(screen, index);
            float haulControl = StandTestHarness.ShareCoveredBy(other, Letterbox);
            StandTestHarness.Shoot(staged.HaulFrame, Letterbox);

            // ---- 3. что с этого видно ---------------------------------------------------------------
            if (!string.IsNullOrEmpty(spec.CaptureSprite))
            {
                Assert.Greater(haulPicture, restPicture * staged.MinGrowth,
                    "«" + spec.Name + "»: при захвате деталь рисует " +
                    (haulPicture * 100f).ToString("0.00") + " % кадра против " +
                    (restPicture * 100f).ToString("0.00") + " % в покое (нужно ×" + staged.MinGrowth.ToString("0.00") + ") — полного арта на кадре нет.");
            }

            if (!string.IsNullOrEmpty(spec.BehindSprite))
            {
                Assert.Less(restBehind, MaxHiddenPaint,
                    "«" + spec.Name + "»: то, что должно быть СПРЯТАНО, уже нарисовано в покое (" +
                    (restBehind * 100f).ToString("0.0") + " % своего прямоугольника).");
                Assert.Greater(haulBehind, MinUncoveredPaint,
                    "«" + spec.Name + "»: штору утащили, а под ней пусто — " +
                    (haulBehind * 100f).ToString("0.0") + " % при пороге " +
                    (MinUncoveredPaint * 100f) + " %.");
            }

            // …and the control: an ordinary detail of the same level in the same two frames.
            Assert.Greater(restControl, 0f, "Контрольная деталь не рисует ничего — сравнивать нечем.");
            Assert.AreEqual(restControl, haulControl, restControl * MaxControlDrift,
                "Гард слепой: соседняя деталь «" + screen.Level.Details[control].Name +
                "» тоже изменилась между кадрами (" + (restControl * 100f).ToString("0.00") + " % → " +
                (haulControl * 100f).ToString("0.00") + " %), значит замер ловит не прятку.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// «Бросил — обратно фрагмент»: the shark dives the moment the thread goes slack.
        ///
        /// The other half of the contract's sentence, and the half a reveal keyed to the wrong signal
        /// gets wrong in the expensive direction — a shark left whole on the plate after the player has
        /// stopped cranking is a detail that has visibly changed and is nevertheless back at square one.
        /// Shot at the SHIPPED grace and stop mode (300 ms, «сброс в ноль»), because the question is
        /// what happens in the game and not what happens under a staging knob.
        /// </summary>
        [UnityTest]
        public IEnumerator AHideawayThatIsDropped_GoesBackToBeingAFragment()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.SwipeUntil(fake, () => screen.Beat == TutorialBeat.Done,
                "обучение пройдено");

            TuningConfig.WaveIntervalSeconds = 300f;
            TuningConfig.CollectSeconds = HaulSeconds;
            TuningConfig.GraceMs = TuningConfig.Defaults.GraceMs;
            TuningConfig.StopMode = TuningConfig.Defaults.StopMode;

            int index = IndexOfDetail(screen.Level, "L1/objects/shark-fin");
            GameObject picture = screen.View.DetailImages[index].gameObject;

            yield return NoticeExactly(fake, screen, index);
            yield return GameTestHarness.CrankUntil(fake,
                () => screen.Runtime.Collector.Progress01 >= 0.15f, "акулу потянули");

            Assert.IsTrue(screen.View.IsCapturedNow(index), "Акула не показалась целиком — нечего ронять.");
            float whole = StandTestHarness.ShareCoveredBy(picture, Letterbox);

            // Hands off the handle. The grace window forgives 300 ms, then the progress is reset and the
            // detail flies home — and the animal goes back under the water with it.
            fake.Next = new BackendSnapshot();
            yield return GameTestHarness.Until(() => !screen.View.IsCapturedNow(index),
                "акула ушла обратно под воду", 5f);

            // …and all the way home, so the frame is the resting one and not a fin mid-flight.
            yield return GameTestHarness.Until(() => !screen.Runtime.Slipping, "плавник вернулся на место",
                5f);
            yield return GameTestHarness.Idle(fake, 2);

            float fin = StandTestHarness.ShareCoveredBy(picture, Letterbox);
            Assert.Less(fin, whole / MinRevealGrowth,
                "Бросили акулу, а на плите всё ещё нарисовано " + (fin * 100f).ToString("0.00") +
                " % кадра против " + (whole * 100f).ToString("0.00") + " % под тягой.");

            Assert.AreEqual(screen.Level.Details[index].Size,
                (Vector2)screen.View.DetailImages[index].rectTransform.sizeDelta,
                "Плавник вернулся, а прямоугольник детали остался акульим.");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The ring of a REVEALED hideaway is judged against the animal, not against the fin it used to
        /// be (блокер 2, ревизия Codex 2026-09-25).
        ///
        /// «Кольцо не рисуется, пока деталь полностью закрыта мыслью» (founder, 2026-08-07) used to be
        /// asked about <c>HintPlacement.Centred(position, spec.Size)</c> — the catalogue's 196×63 fin. A
        /// medium blob is 280×220: it swallows the fin whole and cannot swallow the 292×180 shark, so the
        /// state the old arithmetic produced is a shark plainly visible on the plate with the crank's only
        /// feedback switched off «потому что деталь закрыта». Both halves are asserted here, on the live
        /// frame: the premise is measured off the field's own thoughts, and the ring is then required to
        /// be drawn.
        ///
        /// The control is the same claim in the other direction — a strong blob DOES cover the whole
        /// animal, and then the ring has to go. Without it this test would pass on a view that had simply
        /// stopped hiding rings at all.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRingOfARevealedHideaway_IsJudgedAgainstTheAnimal_NotTheFragment()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.CollectOneDetail(fake, screen);
            yield return GameTestHarness.SwipeUntil(fake, () => screen.Beat == TutorialBeat.Done,
                "обучение пройдено");

            // A patient grace, so letting go of the handle to place a blob is not letting go of the shark.
            QuietTheLevelDownForAPicture(0);

            int index = IndexOfDetail(screen.Level, "L1/objects/shark-fin");
            ArtDetail spec = screen.Level.Details[index];
            ThoughtField field = screen.Runtime.Field;

            yield return NoticeExactly(fake, screen, index);
            yield return GameTestHarness.CrankUntil(fake,
                () => screen.Runtime.Collector.Progress01 >= 0.1f, "акулу потянули");
            fake.Next = new BackendSnapshot();
            yield return GameTestHarness.Frames(2);

            Assert.IsTrue(screen.View.IsCapturedNow(index), "Акула не показалась — судить не о чем.");

            RectTransform ring = StandTestHarness.Find(StandTestHarness.Stage(), "Ring_" + spec.Name);

            // Where the two rectangles are RIGHT NOW: the fragment's, and the animal that is drawn there.
            // Spelled out from the catalogue's raw numbers rather than asked of
            // <see cref="LevelCatalog.DrawnRectAt"/> — a probe that shares the seam under test would
            // follow it back into the bug and stay green.
            Vector2 position = Vector2.Lerp(spec.Home, screen.Level.VesselCentre,
                screen.Runtime.DisplayProgress);
            Rect fragment = RectAround(position, spec.Size);
            Rect animal = RectAround(position + spec.CaptureOffset, spec.CaptureSize);

            // ---- 1. мысль накрыла ФРАГМЕНТ, но не акулу — кольцо обязано остаться ---------------------
            field.SpawnAt(ThoughtStrength.Medium, screen.Level.ThoughtSprites[0], position);
            yield return GameTestHarness.Frames(2);

            Assert.IsTrue(SomeThoughtCovers(field, fragment),
                "Премисса потеряна: ни одна мысль не накрывает плавник целиком — блокер 2 не " +
                "воспроизводится, мысль " + (field.Thoughts.Count > 0 ? field.Thoughts[0].Rect.ToString() : "—") +
                " против фрагмента " + fragment + ".");
            Assert.IsFalse(SomeThoughtCovers(field, animal),
                "Премисса потеряна: мысль накрыла и акулу целиком — тогда кольцо гасить правильно.");

            StandTestHarness.AssertVisible(ring,
                "кольцо прогресса раскрытой акулы (мысль накрыла только плавник, блокер 2)");

            // ---- 2. контроль: мысль накрыла ВСЮ акулу — кольцо обязано погаснуть ----------------------
            field.SpawnAt(ThoughtStrength.Strong, screen.Level.ThoughtSprites[0], animal.center);
            yield return GameTestHarness.Frames(2);

            Assert.IsTrue(SomeThoughtCovers(field, animal),
                "Контроль не поставлен: сильная мысль не накрыла акулу целиком.");
            Assert.IsFalse(ring.gameObject.activeInHierarchy,
                "Деталь целиком под мыслью, а кольцо всё равно нарисовано — правило founder " +
                "«кольцо не рисуется, пока деталь полностью закрыта мыслью» перестало работать вовсе.");

            LogAssert.NoUnexpectedReceived();
        }

        private static Rect RectAround(Vector2 centre, Vector2 size) =>
            new Rect(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f, size.x, size.y);

        /// <summary>Is any live thought covering <paramref name="what"/> whole?</summary>
        private static bool SomeThoughtCovers(ThoughtField field, Rect what)
        {
            for (int i = 0; i < field.Thoughts.Count; i++)
            {
                Rect over = field.Thoughts[i].Rect;
                if (over.xMin <= what.xMin && over.yMin <= what.yMin &&
                    over.xMax >= what.xMax && over.yMax >= what.yMax) return true;
            }

            return false;
        }

        /// <summary>
        /// …and «за шторкой» stays open: the woman in the city's window is still there once the curtain
        /// is in the briefcase.
        ///
        /// A curtain that has been collected does not swing back, and this is the claim that says so —
        /// the reveal is a change to the LOCATION, not an animation that plays while a detail is in
        /// flight. It is also the one state the collection loop never revisits (it stops calling
        /// <c>SetDetailProgress</c> for a collected detail), so nothing else in the suite would notice
        /// the window going dark again.
        /// </summary>
        [UnityTest]
        public IEnumerator TheWomanBehindTheCurtain_StaysUncoveredAfterItIsCollected()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 4);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            TuningConfig.WaveIntervalSeconds = 300f;

            int index = IndexOfDetail(screen.Level, "L5/objects/curtains");
            Assert.Less(ShareOfWhatIsHiding(screen, index), MaxHiddenPaint,
                "Дама видна ещё до того, как штору тронули.");

            yield return NoticeExactly(fake, screen, index);
            TuningConfig.CollectSeconds = GameTestHarness.FastCollectSeconds;
            yield return GameTestHarness.CrankUntil(fake, () => screen.Runtime.Collected[index],
                "штора уехала в дипломат");
            yield return GameTestHarness.Idle(fake, 2);

            Assert.IsFalse(screen.View.DetailImages[index].gameObject.activeInHierarchy,
                "Штора собрана, а всё ещё нарисована.");
            Assert.Greater(ShareOfWhatIsHiding(screen, index), MinUncoveredPaint,
                "Штора в дипломате, а окно снова пустое — проявление откатилось вместе с деталью.");

            StandTestHarness.ShootCloseUp("Game48_L5_window_after_the_curtain", Letterbox,
                WithAir(LevelCatalog.BehindRectOf(screen.Level.Details[index]), 40f), 4);

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// Stop the level from doing anything but the one thing these frames are about: no waves, no
        /// breathing details, no band of light, and a haul slow enough to be photographed halfway.
        ///
        /// The patient grace is the one that needs saying out loud. The shipped window is 300 ms, and a
        /// test that stops cranking to take a measurement loses the detail before the second capture has
        /// been read back. It is a staging knob and nothing else — what happens when the grace DOES run
        /// out has its own gate above, shot at the shipped numbers.
        /// </summary>
        private static void QuietTheLevelDownForAPicture(int levelIndex)
        {
            TuningConfig.SetWaveInterval(levelIndex, 300f);
            TuningConfig.WaveIntervalSeconds = 300f;
            TuningConfig.CollectSeconds = HaulSeconds;
            TuningConfig.GraceMs = PatientGraceMs;
            TuningConfig.DetailPulseAmplitude = 0f;
            TuningConfig.SweepStrength = 0f;
        }

        /// <summary>Long enough that a few seconds of cranking is a fraction of the thread.</summary>
        private const float HaulSeconds = 14f;

        /// <summary>…and long enough that letting go to read a frame is not letting go.</summary>
        private const float PatientGraceMs = 120000f;

        /// <summary>
        /// How far a DROPPED hideaway has to fall back — the mirror of <see cref="Hideaway.MinGrowth"/>,
        /// used by the drop gate, which is about the shark alone.
        /// </summary>
        private const float MinRevealGrowth = 3.0f;

        /// <summary>What a drawing that is supposed to be HIDDEN may paint of its own rectangle.</summary>
        private const float MaxHiddenPaint = 0.02f;

        /// <summary>…and what an uncovered one has to.</summary>
        private const float MinUncoveredPaint = 0.5f;

        /// <summary>How far the control detail may drift between the two frames, as a share of itself.</summary>
        private const float MaxControlDrift = 0.15f;

        /// <summary>
        /// How much of its own rectangle the drawing under a hideaway is painting — 0 while it is
        /// hidden, because an inactive object changes nothing when it is switched off again.
        /// </summary>
        private static float ShareOfWhatIsHiding(LevelScreen screen, int index)
        {
            Image behind = screen.View.DetailBehinds[index];
            if (behind == null) return 0f;

            RectInt box = StandTestHarness.PixelRectOf(StandTestHarness.Stage(), behind.rectTransform);
            if (box.width < 2 || box.height < 2) return 0f;
            return StandTestHarness.ShareCoveredBy(behind.gameObject, Letterbox, box);
        }

        /// <summary>
        /// Park the aim on ONE named detail and wait for that one to be noticed.
        ///
        /// Not <c>GameTestHarness.NoticeByLooking</c>, which waits for «что-нибудь замечено» and is
        /// therefore already satisfied on level 1: the lesson is played out in the fixed-order variant,
        /// which leaves the next detail in the queue noticed before the aim has been pointed anywhere.
        /// These frames are about one particular detail, so the wait has to name it.
        /// </summary>
        private static IEnumerator NoticeExactly(FakeBackend fake, LevelScreen screen, int index)
        {
            GameTestHarness.ParkTheAim(screen, LevelCatalog.AnchorOf(screen.Level.Details[index]));
            fake.Next = new BackendSnapshot();
            yield return GameTestHarness.Until(() => screen.Runtime.NoticedIndex == index,
                "взгляд заметил «" + screen.Level.Details[index].Name + "»");
        }

        private static int IndexOfDetail(LevelDefinition level, string sprite)
        {
            for (int i = 0; i < level.DetailCount; i++)
                if (level.Details[i].Sprite == sprite) return i;

            Assert.Fail("Детали «" + sprite + "» нет на уровне «" + level.Title + "».");
            return -1;
        }

        /// <summary>
        /// An ordinary, uncollected, non-hideaway detail of the same level — the thing that has to look
        /// the same in both frames.
        /// </summary>
        private static int ControlDetail(LevelScreen screen, int hideaway)
        {
            for (int i = 0; i < screen.Level.DetailCount; i++)
            {
                if (i == hideaway || screen.Runtime.Collected[i]) continue;
                if (screen.Level.Details[i].IsHideaway) continue;
                return i;
            }

            Assert.Fail("На уровне «" + screen.Level.Title + "» нет обычной детали под контроль.");
            return -1;
        }

        private static Rect WithAir(Rect box, float air) =>
            new Rect(box.xMin - air, box.yMin - air, box.width + 2f * air, box.height + 2f * air);

        // ---- pixel arithmetic --------------------------------------------------------------------------

        private static float MaxChannelDistance(Color a, Color b) =>
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

    }
}
