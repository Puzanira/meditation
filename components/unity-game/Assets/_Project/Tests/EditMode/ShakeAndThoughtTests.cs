using System.Collections.Generic;
using Meditation.Mechanics;
using Meditation.Tuning;
using NUnit.Framework;
using UnityEngine;

namespace Meditation.Tests
{
    /// <summary>MECHANICS §3–§4 — the shaking hand, thought durability, targeting and screen coverage.</summary>
    public class ShakeAndThoughtTests
    {
        private const float Dt = 1f / 60f;

        [SetUp]
        public void SetUp() => TuningConfig.ResetToDefaults();

        [TearDown]
        public void TearDown() => TuningConfig.ResetToDefaults();

        private static int ShakeFor(ShakeDetector detector, int reversals)
        {
            // A real shake: full deflections flipping side to side, one frame apart.
            for (int i = 0; i < reversals; i++)
            {
                detector.Tick(new Vector2(i % 2 == 0 ? 1f : -1f, 0f), Dt);
            }
            return detector.TotalHits;
        }

        [Test]
        public void SharpDeflection_CountsAsHit_AndReversalsKeepCounting()
        {
            var detector = new ShakeDetector();
            Assert.AreEqual(6, ShakeFor(detector, 6), "Every sharp reversal is one hit.");
        }

        [Test]
        public void HoldingTheStick_LandsOnlyTheInitialHit()
        {
            var detector = new ShakeDetector();
            detector.Tick(new Vector2(1f, 0f), Dt);
            for (int i = 0; i < 60; i++) detector.Tick(new Vector2(1f, 0f), Dt);

            Assert.AreEqual(1, detector.TotalHits, "Holding a deflection is not shaking.");
        }

        [Test]
        public void SlowGazeTilt_LandsNoHits()
        {
            TuningConfig.ShakeGestureSpeed = 3f;
            var detector = new ShakeDetector();

            // Ease the stick over 1.5 s — this is the gaze gesture, not a shake.
            for (int i = 0; i < 90; i++) detector.Tick(new Vector2(i / 90f, 0f), Dt);

            Assert.AreEqual(0, detector.TotalHits,
                "A smooth tilt must move the gaze without knocking thoughts out (SCREENS: разводка жестов).");
        }

        [Test]
        public void DeflectionBelowAmplitudeThreshold_LandsNoHits()
        {
            TuningConfig.ShakeAmplitude = 0.8f;
            var detector = new ShakeDetector();

            for (int i = 0; i < 20; i++) detector.Tick(new Vector2(i % 2 == 0 ? 0.6f : -0.6f, 0f), Dt);
            Assert.AreEqual(0, detector.TotalHits, "Small wiggles must not count as hits.");
        }

        [Test]
        public void ThoughtPops_AfterItsTypeDurability()
        {
            TuningConfig.DurabilityWeak = 3;
            TuningConfig.Targeting = ShakeTargeting.AllOnScreen;
            TuningConfig.HitDecayEnabled = false;

            var field = new ThoughtField();
            int popped = 0;
            field.Popped += _ => popped++;
            Thought thought = field.Spawn(ThoughtStrength.Weak);

            field.ApplyHits(2, Vector2.zero);
            Assert.AreEqual(1, field.Thoughts.Count, "Two hits must not finish a 3-pip thought.");
            Assert.AreEqual(1, thought.HitsRemaining);

            field.ApplyHits(1, Vector2.zero);
            Assert.AreEqual(0, field.Thoughts.Count, "The third hit pops it.");
            Assert.AreEqual(1, popped);
        }

        [Test]
        public void StrongerThoughts_TakeMoreHits()
        {
            TuningConfig.DurabilityWeak = 2;
            TuningConfig.DurabilityStrong = 7;
            TuningConfig.Targeting = ShakeTargeting.AllOnScreen;
            TuningConfig.HitDecayEnabled = false;

            var field = new ThoughtField();
            field.Spawn(ThoughtStrength.Weak);
            field.Spawn(ThoughtStrength.Strong);

            field.ApplyHits(2, Vector2.zero);
            Assert.AreEqual(1, field.Thoughts.Count, "The weak one is gone, the strong one is not.");
            Assert.AreEqual(ThoughtStrength.Strong, field.Thoughts[0].Strength);
        }

        [Test]
        public void HitDecay_ResetsTheCounterAfterThePause_WhenEnabled()
        {
            TuningConfig.DurabilityMedium = 5;
            TuningConfig.Targeting = ShakeTargeting.AllOnScreen;
            TuningConfig.HitDecayEnabled = true;
            TuningConfig.HitDecayMs = 400f;

            var field = new ThoughtField();
            Thought thought = field.Spawn(ThoughtStrength.Medium);
            field.ApplyHits(3, Vector2.zero);
            Assert.AreEqual(2, thought.HitsRemaining);

            for (int i = 0; i < 36; i++) field.Tick(Dt, Vector2.zero, 0, false);   // 600 ms of nothing
            Assert.AreEqual(5, thought.HitsRemaining, "The counter must reset after the tuned pause.");

            TuningConfig.HitDecayEnabled = false;
            field.ApplyHits(3, Vector2.zero);
            for (int i = 0; i < 36; i++) field.Tick(Dt, Vector2.zero, 0, false);
            Assert.AreEqual(2, thought.HitsRemaining, "With decay off the damage must stick.");
        }

        [Test]
        public void Targeting_A_HitsEverything_B_HitsOnlyTheNearestToCentre()
        {
            TuningConfig.DurabilityWeak = 4;
            TuningConfig.HitDecayEnabled = false;
            TuningConfig.ThoughtDrift = false;

            var field = new ThoughtField();
            Thought near = field.Spawn(ThoughtStrength.Weak);
            Thought far = field.Spawn(ThoughtStrength.Weak);
            near.Position = new Vector2(960f, 560f);
            far.Position = new Vector2(200f, 120f);

            TuningConfig.Targeting = ShakeTargeting.NearestToCenter;
            field.ApplyHits(1, Vector2.zero);
            Assert.AreEqual(1, near.HitsTaken, "Variant B hits the thought nearest the centre…");
            Assert.AreEqual(0, far.HitsTaken, "…and only that one.");

            TuningConfig.Targeting = ShakeTargeting.AllOnScreen;
            field.ApplyHits(1, Vector2.zero);
            Assert.AreEqual(2, near.HitsTaken, "Variant A sweeps the whole screen.");
            Assert.AreEqual(1, far.HitsTaken);
        }

        [Test]
        public void Targeting_C_FollowsTheStickDirection()
        {
            TuningConfig.DurabilityWeak = 4;
            TuningConfig.HitDecayEnabled = false;
            TuningConfig.ThoughtDrift = false;
            TuningConfig.Targeting = ShakeTargeting.StickDirection;

            var field = new ThoughtField();
            Thought left = field.Spawn(ThoughtStrength.Weak);
            Thought right = field.Spawn(ThoughtStrength.Weak);
            left.Position = new Vector2(300f, 540f);
            right.Position = new Vector2(1600f, 540f);

            field.ApplyHits(1, new Vector2(1f, 0f));
            Assert.AreEqual(1, right.HitsTaken, "Stick right must hit the thought on the right.");
            Assert.AreEqual(0, left.HitsTaken);
        }

        [Test]
        public void OverlapPercent_MeasuresHowMuchOfTheScreenIsHidden()
        {
            TuningConfig.ThoughtDrift = false;
            var field = new ThoughtField();

            Assert.AreEqual(0f, field.OverlapPercent, 0.01f, "An empty screen hides nothing.");

            // Coverage is won by the NUMBER of thoughts (наплыв): sizes are fixed at S/M/L, so a
            // full screen means a grid of L blobs — 5 × 4 of 420×320 tiles 1920×1080.
            for (int row = 0; row < 4; row++)
            for (int col = 0; col < 5; col++)
            {
                Thought blob = field.Spawn(ThoughtStrength.Strong);
                blob.Position = new Vector2(210f + col * 400f, 140f + row * 270f);
            }

            field.Tick(Dt, Vector2.zero, 0, false);

            Assert.AreEqual(new Vector2(420f, 320f), field.Thoughts[0].Size,
                "L thoughts must keep their spec size — nothing may inflate a blob.");
            Assert.Greater(field.OverlapPercent, 95f, "A wave-grid of L thoughts hides the screen.");

            float halfCovered = 0f;
            var half = new ThoughtField();
            for (int col = 0; col < 5; col++)
            {
                Thought blob = half.Spawn(ThoughtStrength.Strong);
                blob.Position = new Vector2(210f + col * 400f, 300f);
            }
            half.Tick(Dt, Vector2.zero, 0, false);
            halfCovered = half.OverlapPercent;
            Assert.Less(halfCovered, 40f, "Fewer thoughts must mean the setting still shows through.");
        }

        [Test]
        public void Waves_ArriveOnTheTunedInterval_AndStopDuringABreather()
        {
            TuningConfig.WaveIntervalSeconds = 1f;
            TuningConfig.WaveWeak = 1;
            TuningConfig.WaveMedium = 1;
            TuningConfig.WaveStrong = 0;

            var field = new ThoughtField();
            field.ResetWaveTimer(1f);

            for (int i = 0; i < 61; i++) field.Tick(Dt, Vector2.zero, 0, true);
            Assert.AreEqual(2, field.Thoughts.Count, "One wave per interval.");

            for (int i = 0; i < 120; i++) field.Tick(Dt, Vector2.zero, 0, false);
            Assert.AreEqual(2, field.Thoughts.Count, "Nothing spawns while spawning is blocked.");
        }

        [Test]
        public void WaveComposition_SendsExactlyTheTunedMixOfTypes()
        {
            TuningConfig.WaveWeak = 2;
            TuningConfig.WaveMedium = 1;
            TuningConfig.WaveStrong = 3;

            var field = new ThoughtField();
            field.SpawnWave();

            int weak = 0, medium = 0, strong = 0;
            for (int i = 0; i < field.Thoughts.Count; i++)
            {
                switch (field.Thoughts[i].Strength)
                {
                    case ThoughtStrength.Weak: weak++; break;
                    case ThoughtStrength.Medium: medium++; break;
                    default: strong++; break;
                }
            }

            Assert.AreEqual(2, weak, "Wave composition must be the panel's, not a random roll.");
            Assert.AreEqual(1, medium);
            Assert.AreEqual(3, strong);

            TuningConfig.WaveWeak = 0;
            TuningConfig.WaveMedium = 0;
            TuningConfig.WaveStrong = 0;
            field.Clear();
            field.SpawnWave();
            Assert.AreEqual(1, field.Thoughts.Count,
                "An all-zero composition must not silently switch the pressure off.");
        }

        [Test]
        public void PressureRamp_ShortensTheIntervalWaveByWave_WhenSwitchedOn()
        {
            TuningConfig.WaveIntervalSeconds = 10f;
            TuningConfig.PressureRampPercent = 20f;
            TuningConfig.WaveWeak = 1;
            TuningConfig.WaveMedium = 0;
            TuningConfig.WaveStrong = 0;

            var field = new ThoughtField();

            TuningConfig.PressureRamp = false;
            field.SpawnWave();
            field.SpawnWave();
            Assert.AreEqual(10f, field.CurrentIntervalSeconds, 0.01f,
                "With the ramp off the interval must not move.");

            TuningConfig.PressureRamp = true;
            Assert.AreEqual(6.4f, field.CurrentIntervalSeconds, 0.05f,
                "Two waves at −20 % each: 10 → 8 → 6.4 s.");

            for (int i = 0; i < 30; i++) field.SpawnWave();
            Assert.GreaterOrEqual(field.CurrentIntervalSeconds, 0.5f,
                "The ramp must bottom out instead of spawning every frame.");
        }

        [Test]
        public void Drift_MovesThoughtsTowardsTheCentre()
        {
            TuningConfig.ThoughtDrift = true;
            TuningConfig.DriftPxPerSec = 60f;

            var field = new ThoughtField();
            Thought thought = field.Spawn(ThoughtStrength.Weak);
            var centre = new Vector2(ThoughtField.ScreenWidth * 0.5f, ThoughtField.ScreenHeight * 0.5f);
            float before = (thought.Position - centre).magnitude;

            for (int i = 0; i < 60; i++) field.Tick(Dt, Vector2.zero, 0, false);
            float after = (thought.Position - centre).magnitude;

            Assert.Less(after, before - 50f, "Drifting thoughts must close in on the centre.");

            TuningConfig.ThoughtDrift = false;
            Vector2 frozen = thought.Position;
            for (int i = 0; i < 60; i++) field.Tick(Dt, Vector2.zero, 0, false);
            Assert.AreEqual(frozen, thought.Position, "With drift off they must stand still.");
        }

        // ---- labels ----------------------------------------------------------------------------

        [Test]
        public void Labels_NeverRepeatWhileTheRegistryStillHasUnusedLines()
        {
            var field = new ThoughtField();
            int pool = LevelOneData.ThoughtLabels.Length;

            var seen = new List<string>();
            for (int i = 0; i < pool; i++) seen.Add(field.Spawn(ThoughtStrength.Weak).Label);

            CollectionAssert.AllItemsAreUnique(seen,
                "Two identical captions in one frame read as a rendering bug; the registry has enough " +
                "lines to fill it, and widening it is not the view's decision.");
            CollectionAssert.AreEquivalent(LevelOneData.ThoughtLabels, seen,
                "The captions come from the walkthrough's registry, verbatim and whole.");
        }

        [Test]
        public void Labels_ComeBackOnlyAfterTheThoughtThatHeldThemIsGone()
        {
            var field = new ThoughtField();
            int pool = LevelOneData.ThoughtLabels.Length;

            var first = new List<Thought>();
            for (int i = 0; i < pool; i++) first.Add(field.Spawn(ThoughtStrength.Weak));

            // Knock the oldest one out: its caption is the only one free, so it is the one handed out.
            string freed = first[0].Label;
            field.RemoveOldest();
            Assert.AreEqual(freed, field.Spawn(ThoughtStrength.Weak).Label,
                "A caption becomes available again exactly when its thought leaves the screen.");
        }

        [Test]
        public void Labels_ConsecutiveSpawnsDifferEvenPastTheRegistrySize()
        {
            // More thoughts on screen than the registry has lines is arithmetic, not a bug — but even
            // then two blobs spawned one after the other must not read the same.
            var field = new ThoughtField();
            string previous = null;
            for (int i = 0; i < LevelOneData.ThoughtLabels.Length * 3; i++)
            {
                string label = field.Spawn(ThoughtStrength.Weak).Label;
                Assert.AreNotEqual(previous, label, "Two blobs in a row carry the same caption.");
                previous = label;
            }
        }
    }
}
