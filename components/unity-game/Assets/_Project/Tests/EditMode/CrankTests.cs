using Meditation.Mechanics;
using Meditation.Tuning;
using NUnit.Framework;

namespace Meditation.Tests
{
    /// <summary>MECHANICS §1 — the collecting hand: continuity, grace, reset vs melt, speed [toggle].</summary>
    public class CrankTests
    {
        private const float Dt = 1f / 60f;

        [SetUp]
        public void SetUp() => TuningConfig.ResetToDefaults();

        [TearDown]
        public void TearDown() => TuningConfig.ResetToDefaults();

        private static float Fast => TuningConfig.CrankThresholdDegPerSec * 2f;

        [Test]
        public void ContinuousCranking_CollectsInTheTunedTime()
        {
            TuningConfig.CollectSeconds = 4f;
            var collector = new CrankCollector();

            // Halfway through the tuned time the detail must be halfway down the thread.
            for (int i = 0; i < 120; i++) collector.Tick(Fast, Dt);
            Assert.AreEqual(0.5f, collector.Progress01, 0.02f, "Progress must track the tuned collect time.");

            for (int i = 0; i < 121; i++) collector.Tick(Fast, Dt);
            Assert.IsTrue(collector.IsComplete, "4 s of cranking must finish a 4 s detail.");
        }

        [Test]
        public void StallInsideGrace_KeepsProgress()
        {
            TuningConfig.CollectSeconds = 6f;
            TuningConfig.GraceMs = 300f;
            var collector = new CrankCollector();

            for (int i = 0; i < 60; i++) collector.Tick(Fast, Dt);
            float progress = collector.Progress01;
            Assert.Greater(progress, 0f);

            // 250 ms of nothing — inside the grace window.
            for (int i = 0; i < 15; i++) collector.Tick(0f, Dt);

            Assert.AreEqual(progress, collector.Progress01, 1e-4f, "Grace period must forgive a short stall.");
            Assert.IsTrue(collector.InGrace, "The ring should be in its warning state, not lost.");
            Assert.IsFalse(collector.SlippedThisTick);
        }

        [Test]
        public void StallPastGrace_ModeA_DropsProgressToZero()
        {
            TuningConfig.GraceMs = 200f;
            TuningConfig.StopMode = CrankStopMode.ResetToZero;
            var collector = new CrankCollector();

            for (int i = 0; i < 60; i++) collector.Tick(Fast, Dt);
            Assert.Greater(collector.Progress01, 0f);

            for (int i = 0; i < 30; i++) collector.Tick(0f, Dt);   // 500 ms
            Assert.AreEqual(0f, collector.Progress01, 1e-5f, "Mode A must reset progress to zero.");
        }

        [Test]
        public void StallPastGrace_ModeB_MeltsGraduallyAndCanBeSaved()
        {
            TuningConfig.CollectSeconds = 6f;
            TuningConfig.GraceMs = 100f;
            TuningConfig.StopMode = CrankStopMode.Decay;
            TuningConfig.DecayPerSecond = 0.5f;
            var collector = new CrankCollector();

            for (int i = 0; i < 180; i++) collector.Tick(Fast, Dt);   // ~0.5 progress
            float peak = collector.Progress01;

            for (int i = 0; i < 18; i++) collector.Tick(0f, Dt);      // 300 ms: 100 ms grace + 200 ms melt
            Assert.Less(collector.Progress01, peak, "Mode B must melt progress away.");
            Assert.Greater(collector.Progress01, 0f, "Mode B must NOT wipe it out at once — that is mode A.");
        }

        [Test]
        public void SpeedMode_B_CollectsFasterWhenTurningFaster()
        {
            TuningConfig.CollectSeconds = 6f;
            TuningConfig.CrankReferenceDegPerSec = 360f;

            TuningConfig.SpeedMode = CrankSpeedMode.ContinuityOnly;
            var slowSteady = new CrankCollector();
            for (int i = 0; i < 60; i++) slowSteady.Tick(720f, Dt);

            TuningConfig.SpeedMode = CrankSpeedMode.SpeedScaled;
            var fastScaled = new CrankCollector();
            for (int i = 0; i < 60; i++) fastScaled.Tick(720f, Dt);

            Assert.Greater(fastScaled.Progress01, slowSteady.Progress01 * 1.5f,
                "Mode B must let a fast hand pull the detail in faster; mode A must ignore speed.");
        }

        [Test]
        public void BelowThreshold_IsNotCranking()
        {
            TuningConfig.CrankThresholdDegPerSec = 120f;
            var collector = new CrankCollector();

            collector.Tick(119f, Dt);
            Assert.IsFalse(collector.IsSpinning);
            Assert.AreEqual(0f, collector.Progress01, 1e-5f);

            collector.Tick(121f, Dt);
            Assert.IsTrue(collector.IsSpinning);
            Assert.Greater(collector.Progress01, 0f);
        }

        [Test]
        public void SpeedMeter_SmoothsBurstyInput_SoAWheelStillCountsAsCranking()
        {
            var meter = new CrankSpeedMeter();

            // One 15° wheel tick every third frame = 300 °/s of real turning, but two of every three
            // frames report zero. The meter must not read that as a stall.
            float speed = 0f;
            for (int i = 0; i < 60; i++)
                speed = meter.Tick(i % 3 == 0 ? 15f : 0f, Dt);

            Assert.Greater(speed, 200f, "Bursty crank input must still read as continuous turning.");
            Assert.Less(speed, 400f, "…but not as more turning than actually happened.");

            for (int i = 0; i < 30; i++) speed = meter.Tick(0f, Dt);
            Assert.AreEqual(0f, speed, 1e-3f, "Hands off the crank must decay to zero.");
        }
    }
}
