using System.Collections;
using System.IO;
using AiGameStudio.ArcadeControls;
using Meditation.Mechanics;
using Meditation.Scenes;
using Meditation.Stand;
using Meditation.Tuning;
using Meditation.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Meditation.Tests
{
    /// <summary>
    /// The design gate's evidence: every stand screen AND every state the gate asks to see, rendered
    /// at 1920×1080 into <c>&lt;project&gt;/Screenshots/</c> (gitignored).
    ///
    /// A still opening frame proves nothing about a game about two hands, so the interesting states
    /// are played into existence here — cranking, slipping, shaking, coverage, victory, defeat — and a
    /// missing or flat frame fails the suite.
    ///
    /// The canvas is re-pointed at an off-screen camera for the shot, because the editor's screenshot
    /// API produces nothing in batch mode, and the suite has to be runnable headless.
    /// </summary>
    [Category("Visual")]
    public class StandScreenshotTests
    {
        /// <summary>Below the panel's 3 s floor on purpose: a shot must not cost 15 s of real time.</summary>
        private const float TestOnlyFastCollectSeconds = 0.5f;

        private const float CrankPerFrame = 20f;

        private static string Folder => Path.Combine(Application.dataPath, "..", "Screenshots");

        [SetUp]
        public void SetUp()
        {
            StandTestHarness.IsolateTuningFile();
            TuningConfig.ResetToDefaults();
            TuningConfig.PanelVisible = false;   // shoot the pure composition
            Directory.CreateDirectory(Folder);
        }

        [TearDown]
        public void TearDown()
        {
            StandTestHarness.ReleaseTuningFile();
            TuningConfig.ResetToDefaults();
        }

        // ---- the five screens as they open ---------------------------------------------------

        [UnityTest]
        public IEnumerator EveryScreen_RendersItsComposition(
            [ValueSource(typeof(PreviewScenes), nameof(PreviewScenes.All))] string sceneName)
        {
            yield return StandTestHarness.LoadScene(sceneName);
            yield return Frames(60);
            Shoot(sceneName);
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>One shot with the panel open — the founder's actual working view.</summary>
        [UnityTest]
        public IEnumerator TuningPanel_RendersItsRows()
        {
            TuningConfig.PanelVisible = true;
            yield return StandTestHarness.LoadScene(PreviewScenes.CrankCollect);
            yield return Frames(30);
            Shoot("Scene1_CrankCollect_panel");
            LogAssert.NoUnexpectedReceived();
        }

        // ---- 1–3 · scenette 1: collecting, slipping, collected --------------------------------

        [UnityTest]
        public IEnumerator Scenette1_CollectingSlippingAndCollected()
        {
            TuningConfig.CollectSeconds = 6f;
            TuningConfig.GraceMs = 300f;
            TuningConfig.StopMode = CrankStopMode.ResetToZero;

            yield return StandTestHarness.LoadScene(PreviewScenes.CrankCollect);
            FakeBackend fake = StandTestHarness.TakeOverInput();
            var scene = Object.FindAnyObjectByType<Scene1CrankCollect>();

            // 1 · frame 5: the detail is halfway down the thread, ring green.
            fake.Next = new BackendSnapshot { CrankDeltaDegrees = CrankPerFrame };
            float deadline = Time.time + 10f;
            while (Time.time < deadline && scene.Collector.Progress01 < 0.45f) yield return null;
            Assert.Greater(scene.Collector.Progress01, 0.4f, "The detail never got under way.");
            Shoot("Frame01_Scene1_collecting");

            // 2 · frame 14: hands off past the grace period — ring red, detail on its way home.
            fake.Next = new BackendSnapshot();
            deadline = Time.time + 5f;
            while (Time.time < deadline && scene.Collector.Progress01 > 0f) yield return null;
            yield return Frames(6);
            Shoot("Frame02_Scene1_slip");

            // 3 · frame 6: the detail inside the vessel, first HUD slot filled.
            TuningConfig.CollectSeconds = TestOnlyFastCollectSeconds;
            fake.Next = new BackendSnapshot { CrankDeltaDegrees = CrankPerFrame };
            DesignStage stage = StandTestHarness.Stage();
            deadline = Time.time + 10f;
            while (Time.time < deadline && !Exists(stage, "InVessel_одуванчик")) yield return null;
            Assert.IsTrue(Exists(stage, "InVessel_одуванчик"), "The detail never reached the vessel.");
            fake.Next = new BackendSnapshot();
            yield return Frames(2);
            Shoot("Frame03_Scene1_in_vessel");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- 4–5 · scenette 2: damaged pips, and the strong L blob ----------------------------

        [UnityTest]
        public IEnumerator Scenette2_DamagedPipsAndTheStrongBlob()
        {
            TuningConfig.HitDecayEnabled = false;
            TuningConfig.Targeting = ShakeTargeting.AllOnScreen;
            TuningConfig.WaveIntervalSeconds = 20f;

            yield return StandTestHarness.LoadScene(PreviewScenes.ShakeAway);
            FakeBackend fake = StandTestHarness.TakeOverInput();
            var scene = Object.FindAnyObjectByType<Scene2ShakeAway>();

            // 5 · the opening staging already carries all three types, the L "счёт ЖКХ" included.
            bool hasStrong = false;
            for (int i = 0; i < scene.Field.Thoughts.Count; i++)
                hasStrong |= scene.Field.Thoughts[i].Strength == ThoughtStrength.Strong;
            Assert.IsTrue(hasStrong, "The opening set must include a strong L thought.");
            yield return Frames(20);
            Shoot("Frame05_Scene2_strong_blob");

            // 4 · frames 8/13: pips partly knocked out, no blob popped yet.
            bool damaged = false;
            float deadline = Time.time + 5f;
            while (Time.time < deadline && !damaged)
            {
                fake.Next = new BackendSnapshot
                {
                    Joystick = new Vector2(Time.frameCount % 2 == 0 ? 1f : -1f, 0f)
                };
                yield return null;

                for (int i = 0; i < scene.Field.Thoughts.Count; i++)
                {
                    Thought thought = scene.Field.Thoughts[i];
                    if (thought.HitsTaken > 0 && thought.HitsRemaining > 0) damaged = true;
                }
            }

            fake.Next = new BackendSnapshot();
            yield return Frames(2);
            Assert.IsTrue(damaged, "No thought ended up with partly extinguished pips.");
            Shoot("Frame04_Scene2_pips_damaged");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- 6–7 · scenette 3: the conflict, and the peak of chaos ----------------------------

        [UnityTest]
        public IEnumerator Scenette3_ConflictAndPeak()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.CollectSeconds = 8f;
            TuningConfig.WaveIntervalSeconds = 1f;
            TuningConfig.WaveWeak = 1;
            TuningConfig.WaveMedium = 1;
            TuningConfig.WaveStrong = 0;
            TuningConfig.ThoughtDrift = false;   // keep the blobs where they spawn, on screen

            yield return StandTestHarness.LoadScene(PreviewScenes.TwoHands);
            FakeBackend fake = StandTestHarness.TakeOverInput();
            var scene = Object.FindAnyObjectByType<Scene3TwoHands>();

            // 6 · frames 12–13: collecting AND at least two thoughts on screen, both hands busy.
            float deadline = Time.time + 12f;
            while (Time.time < deadline &&
                   (scene.Runtime.Collector.Progress01 < 0.35f || scene.Runtime.Field.Thoughts.Count < 2))
            {
                fake.Next = new BackendSnapshot
                {
                    CrankDeltaDegrees = CrankPerFrame,
                    Joystick = new Vector2(Time.frameCount % 2 == 0 ? 1f : -1f, 0f)
                };
                yield return null;
            }

            Assert.Greater(scene.Runtime.Collector.Progress01, 0.3f, "Collection was not under way.");
            Assert.GreaterOrEqual(scene.Runtime.Field.Thoughts.Count, 2, "Fewer than two thoughts on screen.");

            // Random spawn positions can stack the blobs on top of each other; stage them where the
            // mock puts them (frames 12–13) so the shot reads as the conflict it is meant to show.
            var stagedAt = new[] { new Vector2(640f, 470f), new Vector2(1350f, 380f), new Vector2(1560f, 700f) };
            for (int i = 0; i < scene.Runtime.Field.Thoughts.Count && i < stagedAt.Length; i++)
                scene.Runtime.Field.Thoughts[i].Position = stagedAt[i];
            yield return Frames(2);
            Shoot("Frame06_Scene3_conflict");

            // 7 · frame 16: coverage past the threshold — light veil, vessel half hidden. Blobs keep
            // their spec sizes; the pressure comes from their NUMBER, and the setting shows between
            // them exactly as the mock draws the peak.
            TuningConfig.ThoughtsCoverVessel = true;
            TuningConfig.LossOverlapPercent = 60f;
            var peakSpots = new[]
            {
                new Vector2(300f, 230f), new Vector2(780f, 230f), new Vector2(1260f, 230f),
                new Vector2(1700f, 230f), new Vector2(520f, 560f), new Vector2(1020f, 560f),
                new Vector2(1520f, 560f), new Vector2(140f, 560f), new Vector2(300f, 870f),
                new Vector2(800f, 870f), new Vector2(1300f, 870f), new Vector2(1760f, 870f)
            };
            for (int i = 0; i < peakSpots.Length; i++)
            {
                Thought blob = scene.Runtime.Field.Spawn(
                    i % 3 == 0 ? ThoughtStrength.Medium : ThoughtStrength.Strong);
                blob.Position = peakSpots[i];
            }

            fake.Next = new BackendSnapshot { CrankDeltaDegrees = CrankPerFrame };
            yield return Frames(4);
            Assert.GreaterOrEqual(scene.Runtime.Field.OverlapPercent, TuningConfig.LossOverlapPercent,
                "The peak state was not reached.");
            Shoot("Frame07_Scene3_peak");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- 8–10 · scenette 4: timer running down, victory, defeat ---------------------------

        [UnityTest]
        public IEnumerator Scenette4_TimerVictoryAndDefeat()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.GraceMs = 800f;
            TuningConfig.WaveIntervalSeconds = 2f;
            TuningConfig.BreatherEnabled = false;

            // Short level + slow collecting: the dial has to visibly drain BEFORE anything is won,
            // otherwise a victory restarts the timer and the shot never catches it moving.
            TuningConfig.LevelSeconds = 12f;
            TuningConfig.CollectSeconds = 8f;

            yield return StandTestHarness.LoadScene(PreviewScenes.FullLevel);
            FakeBackend fake = StandTestHarness.TakeOverInput();
            var scene = Object.FindAnyObjectByType<Scene4FullLevel>();

            // 8 · the sun dial has visibly wound down while a detail is being dragged in.
            float deadline = Time.time + 20f;
            while (Time.time < deadline && scene.Rules.TimeLeft01 > 0.7f)
            {
                fake.Next = new BackendSnapshot { CrankDeltaDegrees = CrankPerFrame };
                yield return null;
            }

            Assert.Less(scene.Rules.TimeLeft01, 0.8f, "The timer did not run down.");
            Assert.AreEqual(LevelOutcome.Playing, scene.Rules.Outcome, "The level ended too early.");
            Shoot("Frame08_Scene4_timer_running");

            // 9 · frame 18: all five details in the vessel, every HUD slot filled.
            TuningConfig.CollectSeconds = TestOnlyFastCollectSeconds;
            deadline = Time.time + 25f;
            while (Time.time < deadline && scene.Rules.Outcome != LevelOutcome.Win)
            {
                fake.Next = new BackendSnapshot { CrankDeltaDegrees = CrankPerFrame };
                yield return null;
            }

            Assert.AreEqual(LevelOutcome.Win, scene.Rules.Outcome, "The level was never won.");

            // Wait out dissolve + silence: the shot has to catch the finished tableau, not half of it.
            deadline = Time.time + 8f;
            while (Time.time < deadline && !scene.VictoryPresented)
            {
                fake.Next = new BackendSnapshot();
                yield return null;
            }

            Assert.IsTrue(scene.VictoryPresented, "The victory tableau never finished.");
            Assert.AreEqual(0, scene.Runtime.Field.Thoughts.Count, "Thoughts must dissolve on a win.");
            yield return Frames(2);
            Shoot("Frame09_Scene4_victory");

            // 10 · frame 17: the screen taken over + «Мысли захватили всё. Вдохни.»
            TuningConfig.AutoRetry = false;
            TuningConfig.LossOverlapPercent = 85f;
            TuningConfig.ThoughtsCoverVessel = true;
            deadline = Time.time + 10f;
            while (Time.time < deadline && scene.Rules.Outcome != LevelOutcome.Playing)
            {
                fake.Next = new BackendSnapshot();
                yield return null;
            }

            StageDefeatWallpaper(scene.Runtime.Field);

            fake.Next = new BackendSnapshot();
            yield return Frames(4);
            Assert.AreEqual(LevelRules.LoseByThoughts, scene.Rules.LoseReason,
                "The defeat state was not reached.");
            Shoot("Frame10_Scene4_defeat");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- defeat wallpaper (mock 17) ---------------------------------------------------------

        /// <summary>Columns and rows of the covered screen; 24 blobs are what it takes to bury it.</summary>
        private const int DefeatColumns = 6;
        private const int DefeatRows = 4;

        /// <summary>±40 px of the cell — the offsets mock 17 draws its wallpaper with.</summary>
        private const float DefeatJitter = 40f;

        /// <summary>
        /// Blob gauges around the grid. Seven long against six columns, so the cycle drifts and neither
        /// a column nor a row repeats a size — mock 17's blobs are all different, a lattice of one
        /// gauge reads as wallpaper rather than as thoughts.
        /// </summary>
        private static readonly ThoughtStrength[] DefeatGauges =
        {
            ThoughtStrength.Strong, ThoughtStrength.Medium, ThoughtStrength.Strong, ThoughtStrength.Weak,
            ThoughtStrength.Strong, ThoughtStrength.Strong, ThoughtStrength.Medium
        };

        /// <summary>
        /// Bury the screen the way mock 17 does: blobs of mixed gauges on a grid that is knocked off
        /// square, not a lattice of identical slabs at a dead 400 px step. The jitter is a function of
        /// the cell index (not a random draw), so the frame is reproducible byte for byte; the field is
        /// cleared first so nothing a wave happened to drop earlier lands in the shot at a random spot.
        /// Labels come from the field itself, whose registry-order rule keeps neighbours different.
        /// </summary>
        private static void StageDefeatWallpaper(ThoughtField field)
        {
            field.Clear();

            // Clear() also rearms the wave clock at zero, and one more wave landing mid-shot would drop
            // two blobs at random spots into a frame that is supposed to be reproducible.
            field.ResetWaveTimer(60f);

            var staged = new Thought[DefeatRows * DefeatColumns];
            for (int row = 0; row < DefeatRows; row++)
            for (int col = 0; col < DefeatColumns; col++)
            {
                int cell = row * DefeatColumns + col;
                float jitterX = (cell * 37) % 81 - DefeatJitter;
                float jitterY = (cell * 53) % 81 - DefeatJitter;

                Thought blob = field.Spawn(DefeatGauges[cell % DefeatGauges.Length]);
                blob.Position = new Vector2(160f + col * 320f + jitterX, 140f + row * 270f + jitterY);
                staged[cell] = blob;
            }

            for (int row = 0; row < DefeatRows; row++)
            for (int col = 0; col < DefeatColumns; col++)
            {
                Thought blob = staged[row * DefeatColumns + col];
                if (col + 1 < DefeatColumns)
                    Assert.AreNotEqual(blob.Label, staged[row * DefeatColumns + col + 1].Label,
                        "Two neighbouring cells carry the same thought — the frame reads as a bug.");
                if (row + 1 < DefeatRows)
                    Assert.AreNotEqual(blob.Label, staged[(row + 1) * DefeatColumns + col].Label,
                        "Two cells above each other carry the same thought.");
            }
        }

        // ---- capture plumbing ------------------------------------------------------------------

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static bool Exists(DesignStage stage, string name)
        {
            RectTransform[] all = stage.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return true;
            return false;
        }

        /// <summary>Render the live canvas at full design resolution and write it to disk.</summary>
        private static void Shoot(string shotName)
        {
            DesignStage stage = StandTestHarness.Stage();

            var camGo = new GameObject("ShotCamera", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.76f, 0.74f, 0.69f);
            cam.transform.position = new Vector3(0f, 0f, -100f);

            var target = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = target;

            RenderMode previousMode = stage.Canvas.renderMode;
            stage.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            stage.Canvas.worldCamera = cam;
            stage.Canvas.planeDistance = 50f;
            Canvas.ForceUpdateCanvases();
            stage.Fit();
            Canvas.ForceUpdateCanvases();

            cam.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var shot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;

            string path = Path.Combine(Folder, shotName + ".png");
            File.WriteAllBytes(path, shot.EncodeToPNG());

            Object.DestroyImmediate(shot);
            cam.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(camGo);

            stage.Canvas.renderMode = previousMode;
            stage.Fit();

            Assert.IsTrue(File.Exists(path), "No frame was written for " + shotName);
            Assert.Greater(new FileInfo(path).Length, 5000, shotName + " rendered an empty frame.");
        }
    }
}
