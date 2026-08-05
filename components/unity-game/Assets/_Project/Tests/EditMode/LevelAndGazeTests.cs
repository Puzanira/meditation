using System.Collections.Generic;
using Meditation.Mechanics;
using Meditation.Stand;
using Meditation.Tuning;
using Meditation.View;
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

        /// <summary>
        /// The defeat drain on the path the GAME uses: an art thought, which is a coloured sprite.
        ///
        /// The test below this one measures <c>LevelOneData.Faded</c> — the greybox stand's palette,
        /// where a thought is a FILL colour. The shipped levels never go through it, and while it stayed
        /// green the art path was tinting #5ADFE6 with (0.72, 0.79, 0.79), which multiplies the strong
        /// channels as hard as the weak one and so RAISED the frame's measured saturation from 0.474 to
        /// 0.492. A green test over the wrong path is the whole finding, so the right path gets its own.
        ///
        /// The 2026-08-05 drop changed what there is to measure: the thoughts are #0D0D0D marker
        /// hatching, saturation 0, so «desaturation» is a no-op on them and the whole claim rests on
        /// the second half of SCREENS S5 — «слегка ВЫСВЕТЛЯЕТСЯ», the half a multiply tint could never
        /// do. That is what is asserted now, on the ink the drop actually ships.
        /// </summary>
        [Test]
        public void DefeatDrainOfAnArtThought_LiftsTheMarkerInk_AndNeverDarkensIt()
        {
            // Every one of the 25 thought PNGs is drawn in this one ink; measured off the files.
            var ink = new Color(13f / 255f, 13f / 255f, 13f / 255f);
            Color drained = ArtThoughtView.Drain(ink);

            Assert.Less(LevelOneData.SaturationOf(ink), 0.01f,
                "Мысли дропа 2026-08-05 обязаны быть нейтрально-чёрными — иначе это другой арт.");
            Assert.Greater(LevelOneData.Luminance(drained), LevelOneData.Luminance(ink),
                "«Цвета гаснут» — не «картинка темнеет»: штриховка на поражении обязана высветляться, " +
                "а осталась " + LevelOneData.Luminance(drained).ToString("0.000") + ".");
            Assert.Less(LevelOneData.SaturationOf(drained), 0.01f,
                "Высветление подкрасило штриховку — на экране поражения не должно появиться цвета.");

            // …and it must not go so far that the wallpaper stops being hatching. The drain lightens;
            // what makes the loss frame readable is the backing under the ink, not a grey wash.
            Assert.Less(LevelOneData.Luminance(drained), 0.5f,
                "Штриховка высветлена до полутона — от «начирканного маркером» ничего не осталось.");

            // …and the shader is handed exactly these numbers, so the picture cannot drift from the test.
            Assert.That(ArtThoughtView.DefeatDesaturate, Is.InRange(0f, 1f));
            Assert.That(ArtThoughtView.DefeatLighten, Is.InRange(0f, 0.5f));
        }

        /// <summary>
        /// The light the hatching is read against, and the ink of the pips on it.
        ///
        /// Both are constants rather than art, so they are the one place the «чёрное по тёмному»
        /// fix can silently rot — a backing quietly darkened to fit some other screen would take the
        /// thoughts down with it, and no frame test names the reason. The ratio is the reason.
        /// </summary>
        [Test]
        public void ThoughtBacking_CarriesBothTheHatchingAndItsPips()
        {
            var ink = new Color(13f / 255f, 13f / 255f, 13f / 255f);

            Assert.GreaterOrEqual(LevelOneData.ContrastRatio(ArtThoughtView.BackingColour, ink),
                LevelOneData.MinThoughtContrast * 2f,
                "Подложка под штриховкой недостаточно светлая: контраст к чернилам " +
                LevelOneData.ContrastRatio(ArtThoughtView.BackingColour, ink).ToString("0.0") + ":1.");

            Assert.GreaterOrEqual(
                LevelOneData.ContrastRatio(ArtThoughtView.BackingColour, ArtThoughtView.PipColour),
                LevelOneData.MinThoughtContrast * 2f,
                "Пипсы не читаются на своей подложке: контраст " +
                LevelOneData.ContrastRatio(ArtThoughtView.BackingColour, ArtThoughtView.PipColour)
                    .ToString("0.0") + ":1.");

            // The halo has to be a halo: wide enough to see at a metre, narrow enough that it does not
            // fill in the gaps the hatching is made of (SCREENS: экран под мыслями остаётся «дырявым»).
            Assert.That(ArtThoughtView.BackingHaloPx, Is.InRange(2f, 6f));
        }

        [Test]
        public void DefeatPalette_LandsInTheMocksSaturationBand()
        {
            // The GREYBOX stand's palette — flat fills, not sprites. Frame 17's wallpaper measures
            // 0.07–0.26 HSV saturation: «цвета гаснут», but the pastel is still recognisable. A grey
            // wash would pass a "colours are gone" eyeball test and lose the picture, so the band is
            // asserted from both ends.
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

        // ---- a detail under a thought is out of reach, whichever way picking works -------------------

        /// <summary>
        /// SCREENS spells the rule out for variant C («ближайшая видимая, не закрытая мыслями»), and
        /// the other two variants have to mean the same thing: the fixed order reached straight through
        /// a blob and the gaze noticed a detail it could not see. These three tests are the rule.
        /// </summary>
        private static CollectionRuntime TwoDetailsRuntime(out SilentView view)
        {
            TuningConfig.ThoughtDrift = false;   // deterministic: the blob stays where it is put
            view = new SilentView();
            return new CollectionRuntime(view,
                new[] { CoveredHome, OpenHome },
                new Vector2(960f, 900f),
                new[] { "под мыслью", "на виду" });
        }

        private static readonly Vector2 CoveredHome = new Vector2(400f, 400f);
        private static readonly Vector2 OpenHome = new Vector2(1500f, 400f);

        [Test]
        public void FixedOrder_WaitsWhileItsNextDetailIsUnderAThought_InsteadOfSkippingAhead()
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            CollectionRuntime runtime = TwoDetailsRuntime(out _);
            runtime.Field.SpawnAt(ThoughtStrength.Weak, "мишка", CoveredHome);

            for (int i = 0; i < 10; i++) runtime.Tick(Dt, Vector2.zero, 0, 0f, false);

            Assert.AreEqual(-1, runtime.NoticedIndex,
                "Цель под мыслью обязана ЖДАТЬ: ни собираться сквозь мысль, ни уступать очередь следующей.");

            runtime.Field.Clear();
            for (int i = 0; i < 3; i++) runtime.Tick(Dt, Vector2.zero, 0, 0f, false);
            Assert.AreEqual(0, runtime.NoticedIndex, "Мысль ушла — очередь обязана продолжиться с той же детали.");
        }

        [Test]
        public void AutoNearest_TakesTheOpenDetail_AndNothingWhenEveryDetailIsCovered()
        {
            TuningConfig.Notice = NoticeMode.AutoNearest;

            CollectionRuntime runtime = TwoDetailsRuntime(out _);
            runtime.Field.SpawnAt(ThoughtStrength.Weak, "мишка", CoveredHome);
            for (int i = 0; i < 5; i++) runtime.Tick(Dt, Vector2.zero, 0, 0f, false);
            Assert.AreEqual(1, runtime.NoticedIndex, "Автовыбор обязан взять открытую деталь.");

            CollectionRuntime buried = TwoDetailsRuntime(out _);
            buried.Field.SpawnAt(ThoughtStrength.Weak, "мишка", CoveredHome);
            buried.Field.SpawnAt(ThoughtStrength.Weak, "гора посуды", OpenHome);
            for (int i = 0; i < 5; i++) buried.Tick(Dt, Vector2.zero, 0, 0f, false);

            Assert.AreEqual(-1, buried.NoticedIndex,
                "Закрыты все — брать нечего; выход тут через тряску, а не через сбор сквозь мысль.");
        }

        [Test]
        public void TheGaze_CannotNoticeADetailThroughAThought()
        {
            TuningConfig.Notice = NoticeMode.GazeJoystick;
            TuningConfig.GazeDwellSeconds = 0.4f;
            CollectionRuntime runtime = TwoDetailsRuntime(out _);
            runtime.Field.SpawnAt(ThoughtStrength.Weak, "мишка", CoveredHome);
            runtime.Gaze.Position = CoveredHome;

            // Twice the dwell, resting straight on the covered detail.
            for (int i = 0; i < 48; i++)
            {
                runtime.Tick(Dt, Vector2.zero, 0, 0f, false);
                runtime.Gaze.Position = CoveredHome;
            }

            Assert.AreEqual(-1, runtime.NoticedIndex, "Взгляд заметил деталь сквозь мысль.");

            runtime.Field.Clear();
            for (int i = 0; i < 48 && runtime.NoticedIndex < 0; i++)
            {
                runtime.Tick(Dt, Vector2.zero, 0, 0f, false);
                runtime.Gaze.Position = CoveredHome;
            }

            Assert.AreEqual(0, runtime.NoticedIndex, "Мысль ушла — взгляд обязан замечать деталь снова.");
        }

        [Test]
        public void SuspendedCollection_NoticesNothing_AndTurnsNothing()
        {
            // What the tutorial's second beat sets: the thoughts live on and can be shaken off, but
            // nothing is noticed and the crank does nothing at all.
            TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.CollectSeconds = 3f;
            CollectionRuntime runtime = TwoDetailsRuntime(out _);

            for (int i = 0; i < 5; i++) runtime.Tick(Dt, Vector2.zero, 0, 0f, false);
            Assert.AreEqual(0, runtime.NoticedIndex, "Без блокировки первая деталь замечается.");

            runtime.CollectionSuspended = true;
            for (int i = 0; i < 120; i++) runtime.Tick(Dt, Vector2.zero, 0, 400f, false);

            Assert.AreEqual(-1, runtime.NoticedIndex, "Под блокировкой ничего не замечается.");
            Assert.AreEqual(0f, runtime.Collector.Progress01, 1e-4f, "Под блокировкой кручение не считается.");
            Assert.AreEqual(0, runtime.CollectedCount, "Под блокировкой ничего не собирается.");

            runtime.CollectionSuspended = false;
            for (int i = 0; i < 5; i++) runtime.Tick(Dt, Vector2.zero, 0, 400f, false);
            Assert.AreEqual(0, runtime.NoticedIndex, "Блокировка снята — цикл обязан продолжиться.");
        }

        /// <summary>A view that answers the loop and remembers nothing: the rules are what is on trial.</summary>
        private sealed class SilentView : ICollectionView
        {
            public void ResetCollected() { }
            public void SetDetailProgress(int index, float progress01, bool spinning, bool active, bool slipped) { }
            public void CollectDetail(int index) { }
            public void SetThread(Vector2 from, Vector2 to, bool visible) { }
            public void SetGaze(Vector2 position, float dwell01, bool visible) { }
            public void SetPeak(bool peak) { }
            public void SyncThoughts(IReadOnlyList<Thought> thoughts, float deltaTime) { }
            public void TickPulse(float deltaTime, IReadOnlyList<bool> collected, int noticedIndex) { }
        }
    }
}
