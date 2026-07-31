using System.Collections.Generic;
using Meditation.Mechanics;
using Meditation.Tuning;
using NUnit.Framework;
using UnityEngine;

namespace Meditation.Tests
{
    /// <summary>MECHANICS §2 and §5 — noticing a detail with the gaze, and the level's win/lose rules.</summary>
    public class LevelAndGazeTests
    {
        private const float Dt = 1f / 60f;

        [SetUp]
        public void SetUp() => TuningConfig.ResetToDefaults();

        [TearDown]
        public void TearDown() => TuningConfig.ResetToDefaults();

        // ---- gaze ----------------------------------------------------------------------------

        [Test]
        public void Gaze_NoticesADetail_AfterTheTunedDwell()
        {
            TuningConfig.GazeDwellSeconds = 0.4f;
            var gaze = new GazeSelector();
            var targets = new List<Vector2> { new Vector2(960f, 400f) };
            var selectable = new List<bool> { true };

            gaze.Position = new Vector2(960f, 400f);
            for (int i = 0; i < 12; i++)   // 200 ms
            {
                gaze.Tick(Vector2.zero, Dt, targets, selectable, false);
                Assert.IsFalse(gaze.NoticedThisTick, "Half the dwell must not be enough.");
            }

            bool noticed = false;
            for (int i = 0; i < 20 && !noticed; i++)
            {
                gaze.Tick(Vector2.zero, Dt, targets, selectable, false);
                noticed = gaze.NoticedThisTick;
            }

            Assert.IsTrue(noticed, "Resting the gaze on a detail must notice it.");
            Assert.AreEqual(0, gaze.NoticedIndex);
        }

        [Test]
        public void Gaze_MovesWithTheStick_AndFreezesWhileShaking()
        {
            TuningConfig.GazeSpeedPxPerSec = 900f;
            var gaze = new GazeSelector();
            var targets = new List<Vector2>();
            var selectable = new List<bool>();

            Vector2 start = gaze.Position;
            for (int i = 0; i < 30; i++) gaze.Tick(new Vector2(1f, 0f), Dt, targets, selectable, false);
            Assert.Greater(gaze.Position.x, start.x + 300f, "A tilt must move the gaze.");

            Vector2 held = gaze.Position;
            for (int i = 0; i < 30; i++) gaze.Tick(new Vector2(1f, 0f), Dt, targets, selectable, true);
            Assert.AreEqual(held, gaze.Position, "The gaze holds still while the stick is shaking.");
        }

        [Test]
        public void Gaze_StaysInsideTheSceneZone()
        {
            var gaze = new GazeSelector();
            var targets = new List<Vector2>();
            var selectable = new List<bool>();

            for (int i = 0; i < 300; i++) gaze.Tick(new Vector2(1f, -1f), Dt, targets, selectable, false);

            Assert.LessOrEqual(gaze.Position.x, ThoughtField.ScreenWidth);
            Assert.LessOrEqual(gaze.Position.y, LevelOneData.SceneHeight,
                "The gaze must not wander into the foreground strip / HUD.");
        }

        // ---- level ---------------------------------------------------------------------------

        [Test]
        public void Level_IsWonWhenEveryDetailIsInTheVessel()
        {
            TuningConfig.LevelSeconds = 60f;
            var rules = new LevelRules();
            rules.Restart(5);

            for (int i = 0; i < 5; i++) rules.OnDetailCollected();
            rules.Tick(Dt, 0f);

            Assert.AreEqual(LevelOutcome.Win, rules.Outcome);
        }

        [Test]
        public void Level_IsLostWhenTheTimerRunsOut()
        {
            TuningConfig.LevelSeconds = 1f;
            var rules = new LevelRules();
            rules.Restart(5);

            for (int i = 0; i < 61; i++) rules.Tick(Dt, 0f);

            Assert.AreEqual(LevelOutcome.Lose, rules.Outcome);
            Assert.AreEqual(LevelRules.LoseByTimer, rules.LoseReason);
        }

        [Test]
        public void Level_IsLostWhenThoughtsCoverTheScreen()
        {
            TuningConfig.LevelSeconds = 120f;
            TuningConfig.LossOverlapPercent = 90f;
            var rules = new LevelRules();
            rules.Restart(5);

            rules.Tick(Dt, 89f);
            Assert.AreEqual(LevelOutcome.Playing, rules.Outcome, "Just under the threshold is still playable.");

            rules.Tick(Dt, 91f);
            Assert.AreEqual(LevelOutcome.Lose, rules.Outcome);
            Assert.AreEqual(LevelRules.LoseByThoughts, rules.LoseReason);
        }

        [Test]
        public void Breather_BlocksSpawningForTheTunedTime()
        {
            TuningConfig.LevelSeconds = 120f;
            TuningConfig.BreatherEnabled = true;
            TuningConfig.BreatherSeconds = 2f;

            var rules = new LevelRules();
            rules.Restart(5);
            Assert.IsTrue(rules.SpawningAllowed);

            rules.OnDetailCollected();
            Assert.IsFalse(rules.SpawningAllowed, "A collected detail buys a breather.");

            for (int i = 0; i < 60; i++) rules.Tick(Dt, 0f);
            Assert.IsFalse(rules.SpawningAllowed, "Still breathing after 1 s of a 2 s breather.");

            for (int i = 0; i < 70; i++) rules.Tick(Dt, 0f);
            Assert.IsTrue(rules.SpawningAllowed, "…and the waves come back afterwards.");
        }

        [Test]
        public void Breather_CanBeSwitchedOff()
        {
            TuningConfig.BreatherEnabled = false;
            var rules = new LevelRules();
            rules.Restart(5);

            rules.OnDetailCollected();
            Assert.IsTrue(rules.SpawningAllowed, "With the [toggle] off there is no breather at all.");
        }

        // ---- tuning state --------------------------------------------------------------------

        [Test]
        public void TuningValues_SurviveAsStaticState_AndResetOnDemand()
        {
            TuningConfig.CollectSeconds = 11.5f;
            TuningConfig.Targeting = ShakeTargeting.StickDirection;

            // Anything the stand does between scenettes reads the same static config.
            Assert.AreEqual(11.5f, TuningConfig.CollectSeconds, 1e-4f);
            Assert.AreEqual(ShakeTargeting.StickDirection, TuningConfig.Targeting);

            TuningConfig.ResetToDefaults();

            // Compared against the Defaults constant, never a literal: re-tuning after the playtest
            // is a balance decision, and it must not turn this test red.
            Assert.AreEqual(TuningConfig.Defaults.CollectSeconds, TuningConfig.CollectSeconds, 1e-4f);
            Assert.AreEqual(TuningConfig.Defaults.Targeting, TuningConfig.Targeting);
        }

        // ---- how the outcome screens read (walkthrough 16–17) ----------------------------------

        [Test]
        public void DefeatPalette_LandsInTheMocksSaturationBand()
        {
            // Frame 17's wallpaper measures 0.07–0.26 HSV saturation: «цвета гаснут», but the pastel is
            // still recognisable. A grey wash would pass a "colours are gone" eyeball test and lose the
            // picture, so the band is asserted from both ends.
            for (int i = 0; i < LevelOneData.ThoughtLabels.Length; i++)
            {
                string label = LevelOneData.ThoughtLabels[i];
                AssertInBand(LevelOneData.Faded(LevelOneData.ThoughtFill(label)), "fill of " + label);
                AssertInBand(LevelOneData.Faded(LevelOneData.ThoughtStroke(label)), "stroke of " + label);
            }
        }

        private static void AssertInBand(Color faded, string what)
        {
            float saturation = LevelOneData.SaturationOf(faded);
            Assert.GreaterOrEqual(saturation, LevelOneData.DefeatMinSaturation - 1e-4f,
                "The " + what + " went greyer than anything mock 17 draws.");
            Assert.LessOrEqual(saturation, LevelOneData.DefeatMaxSaturation + 1e-4f,
                "The " + what + " is still more saturated than mock 17's brightest blob.");
        }

        [Test]
        public void DefeatPalette_KeepsTheColoursApart()
        {
            // Draining the colour must not collapse the five pastels into one grey — the defeat screen
            // is still a picture of thoughts, not of static.
            Color first = LevelOneData.Faded(LevelOneData.ThoughtFill("гора посуды"));
            Color other = LevelOneData.Faded(LevelOneData.ThoughtFill("кровать"));
            float distance = Mathf.Abs(first.r - other.r) + Mathf.Abs(first.g - other.g) +
                             Mathf.Abs(first.b - other.b);
            Assert.Greater(distance, 0.1f, "Two different thoughts faded to the same colour.");
        }

        [Test]
        public void PeakVeil_ReproducesTheMocksCompositeThroughALinearCanvas()
        {
            // Mock 16 composites #3a3050 @0.12 in sRGB; the stand's canvas blends in linear space, where
            // the literal 0.12 lands ~4 % short and the gate measured the veil at half strength. The
            // alpha the stand uses is that same 0.12, restated for the renderer — this is the check that
            // it still lands on the mock's pixel.
            Color sky = LevelOneData.Sky;
            Color dim = LevelOneData.Dim;

            for (int channel = 0; channel < 3; channel++)
            {
                float mock = sky[channel] + (dim[channel] - sky[channel]) * LevelOneData.DimMockAlpha;
                float blended = Mathf.Lerp(
                    LevelOneData.SrgbToLinear(sky[channel]),
                    LevelOneData.SrgbToLinear(dim[channel]),
                    LevelOneData.DimAlpha);
                float rendered = LevelOneData.LinearToSrgb(blended);

                Assert.AreEqual(mock, rendered, 1.5f / 255f,
                    "The veil no longer renders as mock 16 draws it (channel " + channel + ").");
            }

            Assert.Greater(LevelOneData.DimAlpha, LevelOneData.DimMockAlpha,
                "A linear canvas needs MORE alpha than the SVG to reach the same picture.");
        }
    }
}
