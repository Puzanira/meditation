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
        /// <summary>A pixel counts as ink when it differs from its plate by this much of 255.</summary>
        private const float InkThreshold = 40f / 255f;

        /// <summary>
        /// Share of a slot the silhouette has to cover, and the contrast it has to reach — the design
        /// gate's own criterion for «читается с метра» (ink ≥ 8 %, контраст ≥ 90 из 255).
        /// </summary>
        private const float MinInkShare = 0.08f;

        private const float MinContrast = 90f / 255f;

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

        // ---- S9 + N2: the slots read at a metre, empty and full ----------------------------------------

        [UnityTest]
        public IEnumerator EveryHudSlot_ReadsAtAMetre_OnEveryLevel([Values(0, 1, 2, 3, 4)] int levelIndex)
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, levelIndex);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            DesignStage stage = StandTestHarness.Stage();
            yield return GameTestHarness.SettleScreen(fake);

            Texture2D frame = StandTestHarness.Capture(Color.black);
            try
            {
                for (int i = 0; i < screen.Level.DetailCount; i++)
                {
                    RectTransform slot = StandTestHarness.Find(stage, "Slot" + (i + 1));
                    AssertSlotReads(frame, stage, slot,
                        screen.Level.Title + " · слот " + (i + 1) + " («" +
                        screen.Level.Details[i].Name + "», пустой)");
                }
            }
            finally
            {
                Object.DestroyImmediate(frame);
            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// Ink and contrast inside one slot: how much of it differs from the plate, and by how much.
        /// The plate is taken as the slot's own median colour — the silhouette is never the majority
        /// of a slot, and reading the plate off the frame keeps this honest when the plate changes.
        /// </summary>
        private static void AssertSlotReads(Texture2D frame, DesignStage stage, RectTransform slot,
            string what)
        {
            RectInt box = StandTestHarness.PixelRectOf(stage, slot);
            box = new RectInt(box.x + 4, box.y + 4, Mathf.Max(1, box.width - 8), Mathf.Max(1, box.height - 8));

            Color[] pixels = StandTestHarness.PixelsOf(frame, box);
            Assert.Greater(pixels.Length, 100, what + ": слот не попал в кадр.");

            Color plate = Median(pixels);

            int ink = 0;
            float loudest = 0f;
            for (int i = 0; i < pixels.Length; i++)
            {
                float distance = MaxChannelDistance(pixels[i], plate);
                if (distance > InkThreshold) ink++;
                if (distance > loudest) loudest = distance;
            }

            float share = ink / (float)pixels.Length;
            Assert.GreaterOrEqual(share, MinInkShare,
                what + ": силуэт закрывает " + (share * 100f).ToString("0.0") +
                " % плашки — с метра это пустая плашка (нужно " + (MinInkShare * 100f) + " %).");
            Assert.GreaterOrEqual(loudest, MinContrast,
                what + ": контраст силуэта к плашке " + (loudest * 255f).ToString("0") +
                " из 255 — нужно " + (MinContrast * 255f).ToString("0") + ".");
        }

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
                Assert.Greater(wipedAway, MinDefeatChange,
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

        // ---- N3: the marker thoughts read against every plate -------------------------------------------

        /// <summary>
        /// Risk the drop was handed over with: «чёрное по тёмному может сливаться» — the evening street,
        /// the metro car and the dark office.
        ///
        /// Measured on the frames before the backing landed, the hatching sat at 1.6–3.5:1 against the
        /// plates, median per level, with 40–97 % of its ink under 3:1.
        ///
        /// What is measured here is the ink that landed WHERE THE PROBLEM IS — on a piece of plate too
        /// dark to read black against (local brightness under <see cref="DarkPlate"/>, i.e. under 2.4:1
        /// to the drop's #0D0D0D). Everywhere else the question does not arise. Of that ink, the claim
        /// is that it brings its own light: the brightest pixel within a few px on the FINISHED frame.
        ///
        /// Stated any looser the gate has no teeth, and this was checked rather than assumed — the
        /// first form of it (local contrast ratio anywhere on the frame) went green with the backing
        /// switched off, because a hatch line always has some light plate a few pixels away and because
        /// even a zero-width backing leaks a grey fringe through the ink's antialiasing.
        ///
        /// The thought layer is switched off and on to find the ink: the plates have blacks of their
        /// own (the L4 railing, the L5 monitors), and without the difference the measurement would be
        /// judging the scenery.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryLevelsThoughts_ReadAgainstItsPlate([Values(0, 1, 2, 3, 4)] int levelIndex)
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, levelIndex);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelView view = screen.View;

            GameTestHarness.CrowdTheScreen(screen, 6);
            yield return GameTestHarness.SettleScreen(fake);
            Assert.Greater(screen.Runtime.Field.Thoughts.Count, 0, "На кадре нет мыслей.");

            Texture2D withThoughts = StandTestHarness.Capture(Color.black);
            view.ThoughtsLayer.gameObject.SetActive(false);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Texture2D plateOnly = StandTestHarness.Capture(Color.black);
            view.ThoughtsLayer.gameObject.SetActive(true);

            try
            {
                int w = ScanBox.width, h = ScanBox.height;
                Color[] after = withThoughts.GetPixels(ScanBox.x, ScanBox.y, w, h);
                Color[] before = plateOnly.GetPixels(ScanBox.x, ScanBox.y, w, h);

                var light = new float[after.Length];
                var plate = new float[before.Length];
                for (int i = 0; i < after.Length; i++)
                {
                    light[i] = LevelOneData.RelativeLuminance(after[i]);
                    plate[i] = LevelOneData.RelativeLuminance(before[i]);
                }

                float[] surround = LocalMax(light, w, h, SurroundRadius);
                float[] plateAround = LocalMax(plate, w, h, SurroundRadius);

                var carried = new List<float>(1 << 16);
                for (int i = 0; i < after.Length; i++)
                {
                    // The stroke: a pixel the thought layer drew, and drew dark…
                    if (light[i] > InkLuminance) continue;
                    if (Mathf.Abs(after[i].r - before[i].r) < 0.02f &&
                        Mathf.Abs(after[i].g - before[i].g) < 0.02f &&
                        Mathf.Abs(after[i].b - before[i].b) < 0.02f) continue;

                    // …on a piece of plate that had nothing to offer it.
                    if (plateAround[i] >= DarkPlate) continue;

                    carried.Add(surround[i]);
                }

                Assert.Greater(carried.Count, 2000,
                    screen.Level.Title + ": штриховки на тёмных местах плиты почти нет — мерить нечего (" +
                    carried.Count + " px).");

                carried.Sort();
                float median = carried[carried.Count / 2];
                Assert.GreaterOrEqual(median, MinCarriedLight,
                    screen.Level.Title + ": штриховка на тёмном месте плиты не несёт своего света — " +
                    "медианная яркость рядом со штрихом " + median.ToString("0.000") +
                    " при пороге " + MinCarriedLight.ToString("0.000") + ".");
            }
            finally
            {
                Object.DestroyImmediate(withThoughts);
                Object.DestroyImmediate(plateOnly);
            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>Below this relative luminance a thought's pixel is its ink rather than its backing.</summary>
        private const float InkLuminance = 0.03f;

        /// <summary>How far around a stroke counts as «рядом», px — a little wider than the halo.</summary>
        private const int SurroundRadius = 4;

        /// <summary>
        /// Plate brightness under which black ink has no chance of its own: 2.4:1 against #0D0D0D,
        /// below the 3:1 the walkthrough's note asks for. All five plates have such places; the metro
        /// windows and the office wall are mostly made of them.
        /// </summary>
        private const float DarkPlate = 0.08f;

        /// <summary>
        /// The light a stroke on a dark plate has to have next to it, WCAG relative luminance.
        ///
        /// Set between two measured worlds, both of them run rather than guessed: with the halo taken
        /// down to nothing the ink's brightest neighbour is only the grey fringe its own antialiasing
        /// leaks — 0.232…0.247 across the five levels, and all five go red. With the shipped 3 px halo
        /// it is the backing itself: 0.871 on every level. Three tenths clears the leak and leaves the
        /// picture a factor of three.
        /// </summary>
        private const float MinCarriedLight = 0.30f;

        /// <summary>
        /// Brightest value within <paramref name="radius"/> of each pixel, separably (rows, then
        /// columns). Two passes of 2r+1 instead of one of (2r+1)² — the naive form is 17× the work on a
        /// 1200×800 window, and this gate already runs five times.
        /// </summary>
        private static float[] LocalMax(float[] source, int width, int height, int radius)
        {
            var rows = new float[source.Length];
            for (int y = 0; y < height; y++)
            {
                int line = y * width;
                for (int x = 0; x < width; x++)
                {
                    float best = 0f;
                    int from = Mathf.Max(0, x - radius), to = Mathf.Min(width - 1, x + radius);
                    for (int i = from; i <= to; i++) best = Mathf.Max(best, source[line + i]);
                    rows[line + x] = best;
                }
            }

            var box = new float[source.Length];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float best = 0f;
                    int from = Mathf.Max(0, y - radius), to = Mathf.Min(height - 1, y + radius);
                    for (int i = from; i <= to; i++) best = Mathf.Max(best, rows[i * width + x]);
                    box[y * width + x] = best;
                }
            }

            return box;
        }

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

        // ---- pixel arithmetic --------------------------------------------------------------------------

        private static float MaxChannelDistance(Color a, Color b) =>
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

        /// <summary>Per-channel median — the plate, whatever is drawn on top of it.</summary>
        private static Color Median(Color[] pixels)
        {
            var r = new float[pixels.Length];
            var g = new float[pixels.Length];
            var b = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                r[i] = pixels[i].r;
                g[i] = pixels[i].g;
                b[i] = pixels[i].b;
            }

            System.Array.Sort(r);
            System.Array.Sort(g);
            System.Array.Sort(b);
            int mid = pixels.Length / 2;
            return new Color(r[mid], g[mid], b[mid]);
        }
    }
}
