using System.Collections;
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

        /// <summary>…and how bright, for a silhouette shot on black: below this it is an edge or a pip.</summary>
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

        // ---- M13: the defeat really drains the ART thoughts --------------------------------------------

        [UnityTest]
        public IEnumerator DefeatDesaturation_DrainsTheArtThoughts_OnTheRenderedPixel()
        {
            yield return GameTestHarness.LoadGame();
            FakeBackend fake = StandTestHarness.TakeOverInput();
            // Library, not street: level 1 spends its first beats teaching, and what is measured here
            // is an ordinary thought of an ordinary level.
            yield return GameTestHarness.EnterLevel(fake, 1);

            var screen = (LevelScreen)GameTestHarness.Flow().Screen;
            LevelView view = screen.View;

            GameTestHarness.CrowdTheScreen(screen, 5);
            yield return GameTestHarness.SettleScreen(fake);
            Assert.Greater(screen.Runtime.Field.Thoughts.Count, 0, "На кадре нет мыслей.");

            ShowOnlyTheThoughts(view, true);
            yield return null;
            Canvas.ForceUpdateCanvases();

            float live = MeanSaturationOfTheBlobs(out float liveLuminance);

            view.SetDesaturated(true);
            view.SyncThoughts(screen.Runtime.Field.Thoughts, 0f);
            yield return null;
            Canvas.ForceUpdateCanvases();

            float drained = MeanSaturationOfTheBlobs(out float drainedLuminance);

            ShowOnlyTheThoughts(view, false);
            view.SetDesaturated(false);

            Assert.Greater(live, 0.4f,
                "Живая мысль обязана быть насыщенно бирюзовой — иначе мерить нечего (было " +
                live.ToString("0.000") + ").");
            Assert.Less(drained, live,
                "Десатурация поражения ПОВЫСИЛА насыщенность: " + live.ToString("0.000") + " → " +
                drained.ToString("0.000") + ".");
            Assert.That(drained,
                Is.InRange(LevelOneData.DefeatMinSaturation, LevelOneData.DefeatMaxSaturation),
                "Погашенная мысль вне полосы кадра 17 (0.07…0.26): " + drained.ToString("0.000") + ".");
            Assert.GreaterOrEqual(drainedLuminance, liveLuminance,
                "«Цвета гаснут» не значит «картинка темнеет»: яркость упала с " +
                liveLuminance.ToString("0.000") + " до " + drainedLuminance.ToString("0.000") + ".");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// Mean saturation and mean luminance of the silhouettes themselves.
        ///
        /// Shot on black with only the thought layer up, and only the pixels a blob really fills are
        /// counted: below <see cref="BlobBody"/> are its own soft edge (half a blob over the backdrop)
        /// and the pips underneath it, and neither is what «цвета гаснут» is a claim about.
        /// </summary>
        private static float MeanSaturationOfTheBlobs(out float luminance)
        {
            Texture2D frame = StandTestHarness.Capture(Color.black);
            try
            {
                // A window, not the whole 1920×1080: two full-frame GetPixels() per measurement is
                // 33 MB of garbage each, and this gate is already the heaviest thing in the suite.
                Color[] pixels = frame.GetPixels(ScanBox.x, ScanBox.y, ScanBox.width, ScanBox.height);
                double saturation = 0d;
                double light = 0d;
                int counted = 0;

                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    if (c.maxColorComponent < BlobBody) continue;

                    saturation += LevelOneData.SaturationOf(c);
                    light += LevelOneData.Luminance(c);
                    counted++;
                }

                Assert.Greater(counted, 500, "На кадре нет мыслей — мерить нечего.");
                luminance = (float)(light / counted);
                return (float)(saturation / counted);
            }
            finally
            {
                Object.DestroyImmediate(frame);
            }
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
