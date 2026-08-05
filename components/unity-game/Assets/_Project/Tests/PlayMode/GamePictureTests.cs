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

        /// <summary>
        /// …and how bright, for a silhouette shot on black: below this it is an edge or a pip.
        /// Kept for the vessel/haul measurements; the defeat gate stopped using it with the marker
        /// drop, because black hatching has no pixel above it at all.
        /// </summary>
        private const float BlobBody = 0.55f;

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
        /// The victory screen judged whole, on one staging: every slot of the filled row reads, and
        /// every cell of the haul is inside the vessel's silhouette.
        ///
        /// One test rather than two because staging it costs a scene load and a full-frame render per
        /// level, and a gate that takes minutes starts failing on patience rather than on the picture.
        /// </summary>
        [UnityTest]
        public IEnumerator TheVictoryPicture_ReadsAtAMetre_AndKeepsTheHaulInTheVessel(
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

            // S9 + N2: the row of filled slots, on the picture as it ships.
            Texture2D victory = StandTestHarness.Capture(Color.black);
            try
            {
                for (int i = 0; i < screen.Level.DetailCount; i++)
                {
                    RectTransform slot = StandTestHarness.Find(stage, "VictorySlot" + (i + 1));
                    AssertSlotReads(victory, stage, slot,
                        screen.Level.Title + " · слот победы " + (i + 1) + " («" +
                        screen.Level.Details[i].Name + "», заполненный)");
                }
            }
            finally
            {
                Object.DestroyImmediate(victory);
            }

            // B2: where the haul is, before anything is hidden.
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

        // ---- M13: the defeat screen READS (art drop 2026-08-05) -----------------------------------------

        /// <summary>
        /// The defeat screen with the marker thoughts on it: «Вдохни» readable, the picture not black.
        ///
        /// This used to be a saturation band — 0.07…0.26, mock 17 measured on the turquoise silhouettes.
        /// The drop of 2026-08-05 made that unmeasurable rather than merely wrong: black hatching has
        /// saturation 0 by construction, and the old measurement did not even find it (it counted only
        /// pixels brighter than <see cref="BlobBody"/>, of which a black scribble has none — the test
        /// failed with «на кадре нет мыслей», which is the truest thing it could have said).
        ///
        /// So the claim is restated as the one the founder actually cares about and SCREENS S5 spells
        /// out: the loss frame is a screen you can read a sentence on, not a black rectangle.
        /// </summary>
        [UnityTest]
        public IEnumerator DefeatScreen_ReadsAtAMetre_OnTheRenderedPixel()
        {
            TuningConfig.AutoRetry = false;

            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            yield return GameTestHarness.EnterLevel(fake, 0);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            yield return GameTestHarness.Until(() => screen.Stage == LevelStage.Play, "уровень пошёл");

            GameTestHarness.BuryTheScreen(screen);
            yield return GameTestHarness.Idle(fake, 4);
            Assert.AreEqual(LevelStage.Lose, screen.Stage, "Уровень не проигран — мерить нечего.");
            yield return GameTestHarness.Frames(2);
            Canvas.ForceUpdateCanvases();

            DesignStage stage = StandTestHarness.Stage();
            Texture2D frame = StandTestHarness.Capture(Color.black);
            try
            {
                // 1. The sentence against the plate it stands on. The plate is read off the frame, not
                //    off the constant, so a change to either side is caught by the same number.
                RectInt plate = StandTestHarness.PixelRectOf(stage, screen.View.MessagePlateRect);
                Color[] pixels = StandTestHarness.PixelsOf(frame, plate);
                Assert.Greater(pixels.Length, 1000, "Подложка сообщения не попала в кадр.");

                Color paper = Brightest(pixels, 0.9f);
                Color ink = Brightest(pixels, 0.02f);
                float textContrast = LevelOneData.ContrastRatio(paper, ink);

                Assert.GreaterOrEqual(textContrast, LevelOneData.MinTextContrast,
                    "«" + GameTexts.DefeatBig + "» не читается на своей подложке: контраст " +
                    textContrast.ToString("0.0") + ":1 при пороге " +
                    LevelOneData.MinTextContrast.ToString("0.0") + ":1.");

                // 2. …and the picture around it is not «просто чёрный экран» (SCREENS S5, заметка про
                //    арт 2026-08-05). Median, not mean: a bright plate in the middle must not be able to
                //    carry a black frame.
                float dark = MedianLuminanceOutside(frame, plate);
                Assert.GreaterOrEqual(dark, MinDefeatLuminance,
                    "Экран поражения ушёл в чёрный: медианная яркость сцены " + dark.ToString("0.000") +
                    " при полу " + MinDefeatLuminance.ToString("0.000") + ".");
            }
            finally
            {
                Object.DestroyImmediate(frame);
            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The floor under the defeat picture, WCAG relative luminance. Frame 17 draws the wallpaper in
        /// light pastels (≈0.60); the marker wallpaper measures 0.096 with the backing under it and
        /// 0.077 without, so the floor is set at half of what the shipped frame does — enough headroom
        /// for a tuning pass, tight enough that a genuinely black loss screen fails.
        /// </summary>
        private const float MinDefeatLuminance = 0.045f;

        /// <summary>The colour at a percentile of brightness — the plate's paper (high) or its ink (low).</summary>
        private static Color Brightest(Color[] pixels, float quantile)
        {
            var byLight = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) byLight[i] = LevelOneData.RelativeLuminance(pixels[i]);

            var order = new int[pixels.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            System.Array.Sort(byLight, order);

            int at = Mathf.Clamp(Mathf.RoundToInt(quantile * (order.Length - 1)), 0, order.Length - 1);
            return pixels[order[at]];
        }

        /// <summary>Median relative luminance of the frame with <paramref name="hole"/> cut out of it.</summary>
        private static float MedianLuminanceOutside(Texture2D frame, RectInt hole)
        {
            // Every eighth pixel in both axes: a median does not need 2 M samples, and a full GetPixels
            // of a 1920×1080 frame is 33 MB of garbage.
            var samples = new List<float>(1 << 16);
            for (int y = 0; y < frame.height; y += 8)
            {
                for (int x = 0; x < frame.width; x += 8)
                {
                    if (hole.Contains(new Vector2Int(x, y))) continue;
                    samples.Add(LevelOneData.RelativeLuminance(frame.GetPixel(x, y)));
                }
            }

            samples.Sort();
            return samples[samples.Count / 2];
        }

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

        private static void ShowOnlyTheThoughts(LevelView view, bool alone)
        {
            view.SceneLayer.gameObject.SetActive(!alone);
            view.DetailsLayer.gameObject.SetActive(!alone);
            view.ThreadLayer.gameObject.SetActive(!alone);
            view.VesselLayer.gameObject.SetActive(!alone);
            view.PeakLayer.gameObject.SetActive(!alone);
            view.HudLayer.gameObject.SetActive(!alone);
            view.MessageLayer.gameObject.SetActive(!alone);
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
