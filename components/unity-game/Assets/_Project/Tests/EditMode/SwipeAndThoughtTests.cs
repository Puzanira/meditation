using System.Collections.Generic;
using Meditation.Mechanics;
using Meditation.Tuning;
using NUnit.Framework;
using UnityEngine;

namespace Meditation.Tests
{
    /// <summary>
    /// MECHANICS §3–§4 — the отгон on the height sensors, thought durability, targeting and coverage.
    ///
    /// Every claim below is about SENSOR readings (0..1), because that is what the game reads since
    /// 2026-08-07: the joystick is the aim and lands no hits at all, and the tests that used to shake
    /// it now wave over a sensor. The signal they feed the detector is the one the package produces —
    /// a value ramping up and down (HeightSimulator), not a stick snapping between its stops.
    /// </summary>
    public class SwipeAndThoughtTests
    {
        private const float Dt = 1f / 60f;

        [SetUp]
        public void SetUp() => TuningConfig.ResetToDefaults();

        [TearDown]
        public void TearDown() => TuningConfig.ResetToDefaults();

        /// <summary>
        /// A hand waving over sensor A: <paramref name="strokes"/> passes, each one covering the whole
        /// range at the emulated rise rate (1.25 units/s — the package's own keyboard mapping), turning
        /// around at the top and at the bottom.
        /// </summary>
        private static int WaveOverSensorA(SwipeDetector detector, int strokes, float rate = 1.25f)
        {
            float value = 0f;
            int direction = 1;
            for (int s = 0; s < strokes; s++)
            {
                float travelled = 0f;
                while (travelled < 1f)
                {
                    float step = Mathf.Min(rate * Dt, 1f - travelled);
                    travelled += step;
                    value = Mathf.Clamp01(value + direction * step);
                    detector.Tick(value, 0f, Dt);
                }
                direction = -direction;
            }

            return detector.TotalHits;
        }

        [Test]
        public void EveryPassOverASensor_IsExactlyOneHit()
        {
            var detector = new SwipeDetector();
            Assert.AreEqual(6, WaveOverSensorA(detector, 6),
                "Частота движения = частота ударов: один проход руки — один удар.");
        }

        /// <summary>
        /// The rule that replaced «holding the stick lands only the initial hit». A sensor reports a
        /// POSITION, so a hand that keeps going one way is one long movement, not a drum roll: the
        /// stroke lands its hit and then waits for the hand to turn around. Without this a slow steady
        /// rise would tick off a hit every 0.2 of travel — and on the keyboard, holding Q would play
        /// the whole отгон by itself.
        /// </summary>
        [Test]
        public void OneLongMovementInOneDirection_IsOneHit()
        {
            var detector = new SwipeDetector();

            float value = 0f;
            while (value < 1f)
            {
                value = Mathf.Min(1f, value + 1.25f * Dt);
                detector.Tick(value, 0f, Dt);
            }

            Assert.AreEqual(1, detector.TotalHits, "Одно движение — один удар, сколько бы оно ни длилось.");

            // …and a hand parked above the sensor is not a gesture at all.
            for (int i = 0; i < 60; i++) detector.Tick(1f, 0f, Dt);
            Assert.AreEqual(1, detector.TotalHits, "Рука, замершая над датчиком, ударов не даёт.");
        }

        [Test]
        public void SlowlyLoweringTheHand_LandsNoHits()
        {
            var detector = new SwipeDetector();

            // 0.25 units/s across the whole range: far past the amplitude, far below the sharpness.
            float value = 0f;
            for (int i = 0; i < 240; i++)
            {
                value = Mathf.Clamp01(value + 0.25f * Dt);
                detector.Tick(value, 0f, Dt);
            }

            Assert.AreEqual(0, detector.TotalHits,
                "Медленное движение над датчиком — это не взмах, а рука на весу.");
        }

        [Test]
        public void RippleBelowTheAmplitudeThreshold_LandsNoHits()
        {
            var detector = new SwipeDetector();

            // Quick, but tiny: ±0.05 of the range flipping every frame — a sensor's own jitter.
            for (int i = 0; i < 60; i++) detector.Tick(i % 2 == 0 ? 0.5f : 0.45f, 0f, Dt);
            Assert.AreEqual(0, detector.TotalHits, "Дрожь датчика не имеет права быть ударом.");
        }

        /// <summary>
        /// Both sensors are the отгон, and they add up: two hands over the panel are twice the отгон,
        /// one hand over one sensor is the game as designed. (The joystick, by contrast, cannot land a
        /// hit at all any more — <see cref="ArcadeContractTests"/> and the PlayMode suite hold that.)
        /// </summary>
        [Test]
        public void TheSecondSensor_CountsToo()
        {
            var onlyB = new SwipeDetector();
            float value = 0f;
            while (value < 1f)
            {
                value = Mathf.Min(1f, value + 1.25f * Dt);
                onlyB.Tick(0f, value, Dt);
            }
            Assert.AreEqual(1, onlyB.TotalHits, "Датчик B обязан работать сам по себе.");

            var both = new SwipeDetector();
            value = 0f;
            while (value < 1f)
            {
                value = Mathf.Min(1f, value + 1.25f * Dt);
                both.Tick(value, value, Dt);
            }
            Assert.AreEqual(2, both.TotalHits, "Взмах над обоими датчиками — два удара, а не один.");
        }

        // ---- «хаотичные махания быстро отгоняют, а лежащая рука — нет» (founder, 2026-08-08) --------

        /// <summary>
        /// How long each simulated hand is measured for, seconds — long enough that a rate is a rate.
        /// </summary>
        private const float MeasuredSeconds = 3f;

        /// <summary>
        /// The keyboard emulation, exactly as the arcade-controls package produces it: Q ramps the
        /// reading up at 1.25 units/s, letting go springs it back down at 2.0 (the project's own
        /// <c>default-keyboard-mapping.json</c>). The package's class is used rather than reproduced —
        /// a test that re-implements the signal it is judging can go green against a signal the game
        /// never sees.
        /// </summary>
        private static float HitsPerSecond(bool[] keyDownPerFrame)
        {
            var sensor = new AiGameStudio.ArcadeControls.HeightSimulator(1.25f, 1.25f, 0f, 2f);
            var detector = new SwipeDetector();

            for (int i = 0; i < keyDownPerFrame.Length; i++)
            {
                sensor.Update(keyDownPerFrame[i], false, Dt);
                detector.Tick(sensor.Value, 0f, Dt);
            }

            return detector.TotalHits / (keyDownPerFrame.Length * Dt);
        }

        /// <summary>
        /// A hand being jerked about over one sensor: Q mashed in bursts of 3…8 frames, deterministic
        /// so the number in the checkpoint log is the number the suite measures.
        /// </summary>
        private static bool[] ChaoticMashing(int frames)
        {
            var pressed = new bool[frames];
            var random = new System.Random(20260808);

            int at = 0;
            bool down = true;
            while (at < frames)
            {
                int hold = random.Next(3, 9);
                for (int i = 0; i < hold && at < frames; i++, at++) pressed[at] = down;
                down = !down;
            }

            return pressed;
        }

        /// <summary>
        /// The whole of the founder's sentence, in one measurement: «хаотичные махания должны быстро
        /// отгонять мысли» AND «неподвижная рука ударов не даёт».
        ///
        /// Both halves have to be here together, because either one alone is trivially satisfiable —
        /// a detector that fires on every frame passes the first, and one that never fires passes the
        /// second. What the retune of 2026-08-08 had to do is separate them, and the gap it opened is
        /// what this asserts: an order of magnitude between a hand that is moving and a key that is
        /// merely held.
        ///
        /// The floor of 6 hits/s is a floor, not the measurement (which is ~12–13): the point of the
        /// number is that a weak thought — three hits at the level-1 band — dies in well under half a
        /// second of waving, so the screen really does clear as fast as she asked.
        /// </summary>
        [Test]
        public void ChaoticWaving_LandsHitsFast_WhileAHeldKeyLandsAlmostNone()
        {
            int frames = Mathf.RoundToInt(MeasuredSeconds / Dt);

            float chaotic = HitsPerSecond(ChaoticMashing(frames));

            var held = new bool[frames];
            for (int i = 0; i < frames; i++) held[i] = true;
            float holding = HitsPerSecond(held);

            // Written out, not only asserted: these two numbers are what MECHANICS §3 quotes as the
            // start values' effect, and a number quoted in a doc has to come from a run.
            TestContext.WriteLine("отгон, замер на клавиатурной симуляции: хаотичное дёрганье " +
                                  chaotic.ToString("0.00") + " удара/с, зажатая клавиша " +
                                  holding.ToString("0.00") + " удара/с (" +
                                  (holding * MeasuredSeconds).ToString("0.0") + " за " +
                                  MeasuredSeconds + " с).");

            Assert.Greater(chaotic, 6f,
                "Хаотичное дёрганье одной клавиши даёт " + chaotic.ToString("0.0") +
                " удара/с — этого мало, чтобы «быстро отгонять» (founder, 2026-08-08).");

            Assert.LessOrEqual(holding * MeasuredSeconds, 1f,
                "Зажатая клавиша набила " + (holding * MeasuredSeconds).ToString("0.0") +
                " ударов за " + MeasuredSeconds + " с — пассивная рука обязана давать ноль. " +
                "ИНВАРИАНТ, снимать его нельзя.");

            Assert.Greater(chaotic, holding * 8f,
                "Разрыв между машущей рукой и зажатой клавишей схлопнулся: " +
                chaotic.ToString("0.0") + " против " + holding.ToString("0.0") + " удара/с.");
        }

        /// <summary>
        /// …and the rate ceiling is really the panel's row. Turning the cooldown up to a fifth of a
        /// second has to cap the same mashing at five hits a second — otherwise the row is a slider
        /// wired to nothing, which is how the joystick's old thresholds survived the move to the
        /// sensors.
        /// </summary>
        [Test]
        public void TheCooldown_IsTheRateCeiling_AndItIsThePanelsRow()
        {
            int frames = Mathf.RoundToInt(MeasuredSeconds / Dt);
            bool[] mashing = ChaoticMashing(frames);

            float shipped = HitsPerSecond(mashing);

            TuningConfig.SwipeCooldownMs = 200f;
            float capped = HitsPerSecond(mashing);

            TestContext.WriteLine("кулдаун: 45 мс → " + shipped.ToString("0.00") +
                                  " удара/с, 200 мс → " + capped.ToString("0.00") + " удара/с.");

            Assert.LessOrEqual(capped, 5.2f,
                "Кулдаун 200 мс обязан ограничить темп пятью ударами в секунду, а вышло " +
                capped.ToString("0.0") + ".");
            Assert.Less(capped, shipped,
                "Кулдаун не влияет на детект — строка панели никуда не подключена.");
        }

        /// <summary>
        /// The thresholds are the panel's, in the sensor's own units — a check that the two [tune]
        /// rows really are the ones the detector reads (they replaced the joystick pair, so they could
        /// have been left pointing at nothing).
        /// </summary>
        [Test]
        public void TheThresholds_AreThePanelsOwnNumbers()
        {
            var detector = new SwipeDetector();
            TuningConfig.SwipeAmplitude = 0.9f;

            // A stroke that would be a hit at the shipped 0.2 and is not one at 0.9.
            for (int i = 0; i < 30; i++) detector.Tick(i % 2 == 0 ? 0f : 0.5f, 0f, Dt);
            Assert.AreEqual(0, detector.TotalHits, "Порог амплитуды не влияет на детект.");

            TuningConfig.SwipeAmplitude = TuningConfig.Defaults.SwipeAmplitude;
            // The strokes above move at 30 units/s (half the range in one frame), so a threshold above
            // that is what proves the row is read at all.
            TuningConfig.SwipeSharpness = 40f;
            var strict = new SwipeDetector();
            for (int i = 0; i < 30; i++) strict.Tick(i % 2 == 0 ? 0f : 0.5f, 0f, Dt);
            Assert.AreEqual(0, strict.TotalHits, "Порог резкости не влияет на детект.");
        }

        [Test]
        public void ThoughtPops_AfterItsTypeDurability()
        {
            TuningConfig.DurabilityWeak = 3;
            TuningConfig.Targeting = HitTargeting.AllOnScreen;
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
            TuningConfig.Targeting = HitTargeting.AllOnScreen;
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
            TuningConfig.Targeting = HitTargeting.AllOnScreen;
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

            TuningConfig.Targeting = HitTargeting.NearestToCenter;
            field.ApplyHits(1, Vector2.zero);
            Assert.AreEqual(1, near.HitsTaken, "Variant B hits the thought nearest the centre…");
            Assert.AreEqual(0, far.HitsTaken, "…and only that one.");

            TuningConfig.Targeting = HitTargeting.AllOnScreen;
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
            TuningConfig.Targeting = HitTargeting.StickDirection;

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

            // Coverage is won by the NUMBER of thoughts (наплыв) and, since 2026-08-07, by their
            // GROWTH. Growth is switched off here so the arithmetic below is about the classes only —
            // 5 × 4 blobs of 420×320 tile 1920×1080.
            TuningConfig.ThoughtGrowthPercentPerSec = 0f;
            for (int row = 0; row < 4; row++)
            for (int col = 0; col < 5; col++)
            {
                Thought blob = field.Spawn(ThoughtStrength.Strong);
                blob.Position = new Vector2(210f + col * 400f, 140f + row * 270f);
            }

            field.Tick(Dt, Vector2.zero, 0, false);

            Assert.AreEqual(new Vector2(420f, 320f), field.Thoughts[0].SpawnSize,
                "L thoughts must be BORN at their spec size — nothing may shrink a blob below its class.");
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

        // ---- the defeat wallpaper (SCREENS S5: «экран ЦЕЛИКОМ закрыт мыслями») ----------------------

        /// <summary>
        /// «Целиком» was 79 % of the screen: the grid stopped at the frame, and each blob was a
        /// silhouette fitted inside its class box, so the wallpaper had seams and a fifth of the level
        /// showed through the thing that had just beaten the player. Mock 17 draws its blobs from −60
        /// to 1980 — off the edges — and that is what is checked here.
        /// </summary>
        [Test]
        public void TheDefeatWallpaper_ClosesTheWholeScreen_AndHangsOffItsEdges()
        {
            var field = new ThoughtField();
            int added = field.CoverScreen();

            Assert.Greater(added, 0, "Экран поражения не закрылся ничем.");
            Assert.GreaterOrEqual(field.OverlapPercent, 99f,
                "Экран поражения закрыт не целиком — сквозь мысли виден уровень.");

            bool pastLeft = false, pastRight = false, pastTop = false, pastBottom = false;
            foreach (Thought thought in field.Thoughts)
            {
                Rect r = thought.Rect;
                pastLeft |= r.xMin < 0f;
                pastRight |= r.xMax > ThoughtField.ScreenWidth;
                pastTop |= r.yMin < 0f;
                pastBottom |= r.yMax > ThoughtField.ScreenHeight;
                Assert.IsTrue(thought.Wallpaper, "Обои поражения обязаны быть помечены как обои.");
            }

            Assert.IsTrue(pastLeft && pastRight && pastTop && pastBottom,
                "Обои обязаны свисать за все четыре края кадра (кадр 17: −60…1980).");
        }

        [Test]
        public void TheDefeatWallpaper_SkipsWhatIsAlreadyCovered()
        {
            // The thoughts that actually beat the player stay where they beat them.
            var field = new ThoughtField();
            Thought own = field.SpawnAt(ThoughtStrength.Strong, "гора посуды", new Vector2(960f, 540f));
            field.CoverScreen();

            CollectionAssert.Contains(field.Thoughts, own, "Мысль, выигравшая уровень, обязана остаться.");
            Assert.IsFalse(own.Wallpaper, "Она не обои — её игрок и правда не отбил.");
        }

        // ---- рост мыслей со временем (решение founder 2026-08-07) -------------------------------------

        /// <summary>
        /// The invariant that replaced «размер жёстко зажат классом»: a thought is BORN at its class
        /// and only ever grows from there, up to the tuned ceiling.
        ///
        /// Both halves matter and the first one is the founder's own words — «мысли НИКОГДА не
        /// спавнить меньше класса». The tempting way to animate «разрастаются» is to start small and
        /// swell into the class box, and that would make the first seconds of every wave weaker than
        /// the composition the panel asked for. So this walks the growth from age 0 upwards and
        /// requires it to be monotonic, to start at exactly 1 and to stop at the cap.
        /// </summary>
        [Test]
        public void Thoughts_AreBornAtTheirClass_AndOnlyGrow()
        {
            TuningConfig.ThoughtGrowthPercentPerSec = 4f;
            TuningConfig.ThoughtGrowthCap = 1.5f;

            foreach (ThoughtStrength strength in new[]
                     { ThoughtStrength.Weak, ThoughtStrength.Medium, ThoughtStrength.Strong })
            {
                var thought = new Thought { Strength = strength };
                Vector2 born = Thought.SizeOf(strength);

                Assert.AreEqual(born, thought.Size,
                    strength + ": мысль обязана спавниться РОВНО своим классом.");

                float previous = 0f;
                for (int frame = 0; frame < 60 * 40; frame++)
                {
                    thought.Age += Dt;
                    Vector2 size = thought.Size;

                    Assert.GreaterOrEqual(size.x, born.x - 1e-3f,
                        strength + ": мысль стала УЖЕ своего класса на " + thought.Age + " с.");
                    Assert.GreaterOrEqual(size.y, born.y - 1e-3f,
                        strength + ": мысль стала НИЖЕ своего класса на " + thought.Age + " с.");
                    Assert.GreaterOrEqual(size.x, previous - 1e-3f, strength + ": мысль сжалась.");
                    Assert.LessOrEqual(size.x, born.x * TuningConfig.ThoughtGrowthCap + 1e-3f,
                        strength + ": мысль переросла потолок.");
                    previous = size.x;
                }

                // 4 %/с reaches ×1.5 in 12.5 s, so forty seconds in it is sitting on the ceiling.
                Assert.AreEqual(born.x * 1.5f, thought.Size.x, 0.5f,
                    strength + ": через 40 с мысль обязана стоять на потолке роста.");
            }
        }

        /// <summary>
        /// A negative growth knob is not a back door to a sub-class spawn: the floor is 1, whatever
        /// the panel says. (The slider does not go below 0, but the field is public static and the
        /// persisted file is a text file a human can edit.)
        /// </summary>
        [Test]
        public void Growth_CannotShrinkAThoughtBelowItsClass()
        {
            TuningConfig.ThoughtGrowthPercentPerSec = -50f;
            TuningConfig.ThoughtGrowthCap = 2f;

            var thought = new Thought { Strength = ThoughtStrength.Medium, Age = 10f };
            Assert.AreEqual(Thought.SizeOf(ThoughtStrength.Medium), thought.Size,
                "Отрицательный рост обязан упираться в класс, а не уводить мысль под него.");
        }

        /// <summary>
        /// The defeat wallpaper is exempt. Those blobs are sized to CLOSE their cell, and growing them
        /// would push the picture the retry rubs off out of the frame while the handle is on it.
        /// </summary>
        [Test]
        public void TheDefeatWallpaper_DoesNotGrow()
        {
            TuningConfig.ThoughtGrowthPercentPerSec = 10f;
            TuningConfig.ThoughtGrowthCap = 3f;

            var blob = new Thought { Strength = ThoughtStrength.Strong, Wallpaper = true, Age = 30f };
            Assert.AreEqual(1f, blob.GrowthScale, 1e-4f);
            Assert.AreEqual(blob.SpawnSize, blob.Size);
        }

        // ---- pips belong to a thought you can see (SCREENS «Мысли») -----------------------------------

        [Test]
        public void PipsAreForThoughtsInTheFrame()
        {
            Vector2 size = Thought.SizeOf(ThoughtStrength.Medium);

            Assert.AreEqual(1f, Meditation.View.ArtThoughtView.VisibleShare(new Vector2(960f, 540f), size),
                1e-3f, "Мысль посреди кадра видна целиком.");
            Assert.AreEqual(0f, Meditation.View.ArtThoughtView.VisibleShare(new Vector2(-400f, 540f), size),
                1e-3f, "Мысль за кадром не видна вовсе — и пипсам там висеть неоткуда.");

            float half = Meditation.View.ArtThoughtView.VisibleShare(
                new Vector2(-size.x * 0.5f + size.x * 0.5f * 0.5f, 540f), size);
            Assert.That(half, Is.InRange(0.2f, 0.35f), "Наполовину вышедшая мысль считается наполовину.");
            Assert.Less(half, Meditation.View.ArtThoughtView.MinVisibleShareForPips,
                "Едва торчащая из-за края мысль пипсов не показывает.");
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
