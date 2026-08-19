using System.Collections;
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
    /// Done contract §2–§6, played through the real scenes with all three controllers driven from code:
    /// cranking collects, stalling loses the detail, swipes over the height sensors pop thoughts, and
    /// the full level ends and restarts.
    /// </summary>
    public class ScenetteGameplayTests
    {
        private const float CrankPerFrame = 20f;   // ~1200 °/s through the smoothing window

        /// <summary>
        /// Collection times used to keep PlayMode tests short.
        ///
        /// <see cref="ReachableFastCollectSeconds"/> is the bottom of the panel's 3–15 s range, so it
        /// is what a founder could actually dial in. The two TestOnly values are deliberately BELOW
        /// that range: at a playable speed, "five details win the level" would run for half a minute
        /// of real time and "the detail reaches the vessel" for a third of it. They exercise the
        /// mechanic, never the balance — the balance lives in <see cref="TuningConfig.Defaults"/>,
        /// and no test asserts against these numbers.
        /// </summary>
        private const float ReachableFastCollectSeconds = 3f;
        private const float TestOnlyOneSecondCollect = 1f;
        private const float TestOnlyFastCollectSeconds = 0.4f;

        [SetUp]
        public void SetUp()
        {
            StandTestHarness.IsolateTuningFile();
            TuningConfig.ResetToDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            StandTestHarness.ReleaseTuningFile();
            TuningConfig.ResetToDefaults();
        }

        private static IEnumerator Crank(FakeBackend fake, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                fake.Next = new BackendSnapshot { CrankDeltaDegrees = CrankPerFrame };
                yield return null;
            }
        }

        /// <summary>The отгон: a hand passing over height sensor A, one pass per frame.</summary>
        private static IEnumerator Swipe(FakeBackend fake, float seconds)
        {
            float end = Time.time + seconds;
            bool up = true;
            while (Time.time < end)
            {
                fake.Next = new BackendSnapshot { HeightA = up ? 1f : 0f };
                up = !up;
                yield return null;
            }
        }

        private static IEnumerator CrankAndSwipe(FakeBackend fake, float seconds)
        {
            float end = Time.time + seconds;
            bool up = true;
            while (Time.time < end)
            {
                fake.Next = new BackendSnapshot
                {
                    CrankDeltaDegrees = CrankPerFrame,
                    HeightA = up ? 1f : 0f
                };
                up = !up;
                yield return null;
            }
        }

        private static IEnumerator Idle(FakeBackend fake, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                fake.Next = new BackendSnapshot();
                yield return null;
            }
        }

        // ---- scenette 1 ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Scenette1_CrankingDragsTheDetailIn_StallingDropsIt()
        {
            TuningConfig.CollectSeconds = ReachableFastCollectSeconds;
            TuningConfig.GraceMs = 200f;
            TuningConfig.StopMode = CrankStopMode.ResetToZero;

            yield return StandTestHarness.LoadScene(PreviewScenes.CrankCollect);
            FakeBackend fake = StandTestHarness.TakeOverInput();
            DesignStage stage = StandTestHarness.Stage();

            var scene = Object.FindAnyObjectByType<Scene1CrankCollect>();
            Assert.IsNotNull(scene);

            Vector2 home = StandTestHarness.DesignCentre(stage, "Detail_одуванчик");
            Assert.AreEqual(180f, home.x, 2f, "The detail starts at its authored position.");

            yield return Crank(fake, 1.2f);

            Assert.Greater(scene.Collector.Progress01, 0.2f, "Continuous cranking must make progress.");
            Assert.IsTrue(scene.Collector.IsSpinning, "The crank must read as turning.");

            Vector2 moved = StandTestHarness.DesignCentre(stage, "Detail_одуванчик");
            Assert.Greater(moved.x, home.x + 40f, "The detail must visibly travel towards the vessel.");
            Assert.Greater(moved.y, home.y, "…along the straight line to the vessel below.");

            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "Ring_одуванчик"), "progress ring");
            StandTestHarness.AssertVisible(StandTestHarness.Find(stage, "Thread"), "collection thread");

            // Hands off: past the grace period the progress is gone and the detail flies home.
            yield return Idle(fake, 1.5f);
            Assert.AreEqual(0f, scene.Collector.Progress01, 1e-3f, "Stalling past grace must drop the progress.");

            yield return Idle(fake, 0.8f);
            Vector2 back = StandTestHarness.DesignCentre(stage, "Detail_одуванчик");
            Assert.AreEqual(home.x, back.x, 6f, "The detail must return to its place.");
            Assert.AreEqual(home.y, back.y, 6f);

            LogAssert.NoUnexpectedReceived();
        }

        // Renamed 2026-08-08: the HUD's row of detail slots is gone (founder: «убрать ряд совсем»),
        // so this test's own name should not still promise one — what it actually pins, the detail
        // showing up inside the vessel, is unchanged.
        [UnityTest]
        public IEnumerator Scenette1_DetailReachesTheVessel_AndIsShownInsideIt()
        {
            TuningConfig.CollectSeconds = TestOnlyOneSecondCollect;
            TuningConfig.GraceMs = 800f;

            yield return StandTestHarness.LoadScene(PreviewScenes.CrankCollect);
            FakeBackend fake = StandTestHarness.TakeOverInput();
            DesignStage stage = StandTestHarness.Stage();

            yield return Crank(fake, 2f);

            RectTransform inVessel = StandTestHarness.Find(stage, "InVessel_одуванчик");
            StandTestHarness.AssertVisible(inVessel, "collected detail inside the vessel");

            Rect vessel = stage.DesignRectOf(StandTestHarness.Find(stage, "Vessel"));
            Rect collected = stage.DesignRectOf(inVessel);
            Assert.IsTrue(vessel.Overlaps(collected), "The collected detail must be shown inside the vessel.");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- scenette 2 ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Scenette2_SwipingOverTheSensors_PopsThoughts_ByTheirDurability()
        {
            TuningConfig.DurabilityWeak = 2;
            TuningConfig.DurabilityMedium = 3;
            TuningConfig.Targeting = HitTargeting.AllOnScreen;
            TuningConfig.HitDecayEnabled = false;
            TuningConfig.WaveIntervalSeconds = 20f;

            yield return StandTestHarness.LoadScene(PreviewScenes.ShakeAway);
            FakeBackend fake = StandTestHarness.TakeOverInput();
            DesignStage stage = StandTestHarness.Stage();

            var scene = Object.FindAnyObjectByType<Scene2ShakeAway>();
            Assert.IsNotNull(scene);
            Assert.Greater(scene.Field.Thoughts.Count, 0, "The scenette starts with thoughts on screen.");

            RectTransform blob = StandTestHarness.Find(stage, "Thought");
            StandTestHarness.AssertVisible(blob, "thought blob");

            int before = scene.Field.Thoughts.Count;
            yield return Swipe(fake, 1f);

            Assert.Greater(scene.Popped, 0, "Взмахи над датчиком обязаны сбивать мысли.");
            Assert.Less(scene.Field.Thoughts.Count, before, "…and take them off the screen.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Scenette2_TheJoystick_PopsNothingAtAll()
        {
            TuningConfig.DurabilityWeak = 2;
            TuningConfig.Targeting = HitTargeting.AllOnScreen;
            TuningConfig.WaveIntervalSeconds = 20f;

            yield return StandTestHarness.LoadScene(PreviewScenes.ShakeAway);
            FakeBackend fake = StandTestHarness.TakeOverInput();

            var scene = Object.FindAnyObjectByType<Scene2ShakeAway>();
            int before = scene.Field.Thoughts.Count;

            // Done contract §4: the joystick may not land a hit in ANY mode of choosing a detail — not
            // held over, not slammed from stop to stop. The second half is the one that used to be the
            // отгон itself, so it is the half worth playing here.
            float end = Time.time + 0.5f;
            while (Time.time < end)
            {
                fake.Next = new BackendSnapshot { Joystick = new Vector2(1f, 0f) };
                yield return null;
            }

            end = Time.time + 1f;
            bool right = true;
            while (Time.time < end)
            {
                fake.Next = new BackendSnapshot { Joystick = new Vector2(right ? 1f : -1f, 0f) };
                right = !right;
                yield return null;
            }

            Assert.AreEqual(before, scene.Field.Thoughts.Count,
                "Джойстик — только прицел: ни наклон, ни тряска не имеют права сбивать мысли.");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- scenette 3 ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Scenette3_BothHandsAtOnce_CollectAndFightThoughts()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.CollectSeconds = TestOnlyOneSecondCollect;
            TuningConfig.DurabilityWeak = 2;
            TuningConfig.DurabilityMedium = 2;
            TuningConfig.DurabilityStrong = 2;
            TuningConfig.Targeting = HitTargeting.AllOnScreen;
            TuningConfig.HitDecayEnabled = false;
            TuningConfig.WaveIntervalSeconds = 1f;
            TuningConfig.WaveWeak = 1;
            TuningConfig.WaveMedium = 0;
            TuningConfig.WaveStrong = 0;

            yield return StandTestHarness.LoadScene(PreviewScenes.TwoHands);
            FakeBackend fake = StandTestHarness.TakeOverInput();

            var scene = Object.FindAnyObjectByType<Scene3TwoHands>();
            Assert.IsNotNull(scene);

            yield return CrankAndSwipe(fake, 3f);

            Assert.Greater(scene.Runtime.CollectedCount, 0,
                "The crank hand must keep collecting while the other one waves over the sensor.");
            Assert.Greater(scene.TotalHits, 0, "Рука на датчике обязана набивать удары в это же время.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Scenette3_DroppingTheCrankForTheSensors_CostsTheDetail()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.CollectSeconds = TuningConfig.Defaults.CollectSeconds;
            TuningConfig.GraceMs = 100f;
            TuningConfig.StopMode = CrankStopMode.ResetToZero;
            TuningConfig.WaveIntervalSeconds = 20f;

            yield return StandTestHarness.LoadScene(PreviewScenes.TwoHands);
            FakeBackend fake = StandTestHarness.TakeOverInput();

            var scene = Object.FindAnyObjectByType<Scene3TwoHands>();
            yield return Crank(fake, 1.5f);
            Assert.Greater(scene.Runtime.Collector.Progress01, 0.1f);

            // Let go of the crank to deal with the thoughts — this is the whole conflict of the game.
            yield return Swipe(fake, 1.5f);

            Assert.AreEqual(0f, scene.Runtime.Collector.Progress01, 1e-3f,
                "Бросил ручку ради датчиков — деталь обязана сорваться.");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- scenette 4 ------------------------------------------------------------------------

        /// <summary>
        /// The auto-retry, and the absence of the clock that used to trigger it.
        ///
        /// This case was «таймер вышел → рестарт» until 2026-08-07. The timer is gone (founder), so the
        /// defeat it waits for is the only one left — the screen closing over — and the SUN it used to
        /// assert on is checked the other way round: it must not be on the stand either. A stand still
        /// drawing a dial would be teaching a rule the game does not have.
        /// </summary>
        [UnityTest]
        public IEnumerator Scenette4_ADefeat_RestartsTheLevelWithoutLeavingTheBuild()
        {
            TuningConfig.LossOverlapPercent = 85f;
            TuningConfig.WaveIntervalSeconds = 20f;
            TuningConfig.AutoRetry = true;

            yield return StandTestHarness.LoadScene(PreviewScenes.FullLevel);
            FakeBackend fake = StandTestHarness.TakeOverInput();

            var scene = Object.FindAnyObjectByType<Scene4FullLevel>();
            Assert.IsNotNull(scene);
            Assert.IsNull(StandTestHarness.FindOrNull(StandTestHarness.Stage(), "SunDial"),
                "На стенде остался циферблат таймера.");
            Assert.IsNull(StandTestHarness.FindOrNull(StandTestHarness.Stage(), "Sun"),
                "На стенде осталось солнце-таймер.");

            scene.Runtime.Field.CoverScreen();
            fake.Next = new BackendSnapshot();
            yield return null;

            bool lost = false;
            bool restarted = false;
            float deadline = Time.time + 10f;
            while (Time.time < deadline && !restarted)
            {
                fake.Next = new BackendSnapshot();
                if (scene.Rules.Outcome == LevelOutcome.Lose) lost = true;
                if (lost && scene.Rules.Outcome == LevelOutcome.Playing) restarted = true;
                yield return null;
            }

            Assert.IsTrue(lost, "Закрытый мыслями экран обязан быть поражением.");
            Assert.IsTrue(restarted, "After the defeat the scenette must restart in place.");
            Assert.AreEqual(PreviewScenes.FullLevel, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                "…without leaving the build.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Scenette4_AllFiveDetails_WinTheLevel()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.CollectSeconds = TestOnlyFastCollectSeconds;
            TuningConfig.GraceMs = 800f;
            TuningConfig.WaveIntervalSeconds = 30f;
            TuningConfig.BreatherEnabled = false;

            yield return StandTestHarness.LoadScene(PreviewScenes.FullLevel);
            FakeBackend fake = StandTestHarness.TakeOverInput();
            DesignStage stage = StandTestHarness.Stage();

            var scene = Object.FindAnyObjectByType<Scene4FullLevel>();

            bool won = false;
            float deadline = Time.time + 8f;
            while (Time.time < deadline && !won)
            {
                fake.Next = new BackendSnapshot { CrankDeltaDegrees = CrankPerFrame };
                won = scene.Rules.Outcome == LevelOutcome.Win;
                yield return null;
            }

            Assert.IsTrue(won, "Five details in the vessel must be a win.");

            // The victory is staged like mock 18 — dissolve, silence, then the tableau with its line.
            deadline = Time.time + 8f;
            while (Time.time < deadline && !scene.VictoryPresented)
            {
                fake.Next = new BackendSnapshot();
                yield return null;
            }

            Assert.IsTrue(scene.VictoryPresented, "The victory tableau never finished.");

            var big = StandTestHarness.Find(stage, "BigMessage").GetComponent<UnityEngine.UI.Text>();
            StringAssert.Contains("Собрано", big.text, "The victory line from the walkthrough must be shown.");
            StandTestHarness.AssertVisible(big.rectTransform, "victory message");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Scenette4_ThoughtsCoveringTheScreen_LoseTheLevel()
        {
            TuningConfig.LossOverlapPercent = 85f;
            TuningConfig.WaveIntervalSeconds = 30f;

            yield return StandTestHarness.LoadScene(PreviewScenes.FullLevel);
            FakeBackend fake = StandTestHarness.TakeOverInput();
            var scene = Object.FindAnyObjectByType<Scene4FullLevel>();

            // Blanket the screen the way a run of unanswered waves would: many L blobs, spec-sized.
            TuningConfig.ThoughtGrowthPercentPerSec = 0f;
            for (int row = 0; row < 4; row++)
            for (int col = 0; col < 5; col++)
            {
                Thought thought = scene.Runtime.Field.Spawn(ThoughtStrength.Strong);
                thought.Position = new Vector2(210f + col * 400f, 140f + row * 270f);
            }

            fake.Next = new BackendSnapshot();
            yield return null;
            yield return null;

            Assert.AreEqual(LevelRules.LoseByThoughts, scene.Rules.LoseReason,
                "A covered screen must end the level, not the timer.");

            LogAssert.NoUnexpectedReceived();
        }
    }
}
