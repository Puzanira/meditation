using System;
using System.Collections;
using AiGameStudio.ArcadeControls;
using Meditation.Game;
using Meditation.Mechanics;
using Meditation.Stand;
using Meditation.Tuning;
using NUnit.Framework;
using UnityEngine;

namespace Meditation.Tests
{
    /// <summary>
    /// PlayMode plumbing for the game: load the entry scene, drive both hands, wait for a state, and
    /// put the level into a situation that would otherwise take a real minute to reach.
    ///
    /// Nothing here reaches past the game's own API. The clock is drained by calling the real
    /// <see cref="LevelRules.Tick"/> with a large step and the screen is buried by spawning through the
    /// real <see cref="ThoughtField"/> — so a test never proves something the game cannot actually do.
    /// </summary>
    public static class GameTestHarness
    {
        /// <summary>Degrees of crank per frame while a test turns the handle.</summary>
        public const float CrankPerFrame = 20f;

        /// <summary>Collecting fast enough that a whole run fits inside a test.</summary>
        public const float FastCollectSeconds = 0.35f;

        /// <summary>Default patience for a wait before it counts as a hang.</summary>
        public const float DefaultPatienceSeconds = 30f;

        /// <summary>Thoughts to bury a screen under; well past any 85–100 % coverage threshold.</summary>
        private const int BurialBlobs = 60;

        public static IEnumerator LoadGame()
        {
            yield return StandTestHarness.LoadScene(PreviewScenes.Game);
        }

        /// <summary>
        /// The SHIPPED boot of the game scene: nothing pending, so the flow opens on the title.
        ///
        /// Explicit rather than implied by <see cref="LoadGame"/>, because since 2026-09-22 the two
        /// boots of this one scene are genuinely different products — the cabinet's has no tuning
        /// panel in it at all (<c>GameFlow.Panel</c>), the stand's does.
        /// </summary>
        public static IEnumerator LoadGameAsTheCabinetDoes()
        {
            StandLevelLaunch.Clear();
            yield return LoadGame();
        }

        /// <summary>
        /// …and the DEV boot: the game scene opened the way the stand's «Уровень N» opens it.
        ///
        /// Through <see cref="StandLevelLaunch.Open"/> itself rather than by poking the scene, so a
        /// test proves the founder's own path — the right-hand column of the preview menu — and not a
        /// state only a test can produce.
        /// </summary>
        public static IEnumerator LoadGameFromTheStand(int levelIndex)
        {
            StandLevelLaunch.Open(levelIndex);
            for (int i = 0; i < 4; i++) yield return null;

            Assert.AreEqual(PreviewScenes.Game,
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                "Запрос стенда не открыл сцену игры.");
        }

        public static GameFlow Flow()
        {
            var flow = UnityEngine.Object.FindAnyObjectByType<GameFlow>();
            Assert.IsNotNull(flow, "В сцене игры нет GameFlow.");
            return flow;
        }

        public static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        /// <summary>Let time pass with both hands at rest.</summary>
        public static IEnumerator Idle(FakeBackend fake, int frames)
        {
            fake.Next = new BackendSnapshot();
            yield return Frames(frames);
        }

        /// <summary>
        /// Wait until the screen has actually arrived: the transition is over AND the fade back from
        /// black has finished.
        ///
        /// Frames are not a unit of time here. A batch run draws hundreds a second, so «подожди 20
        /// кадров» after a transition can be four hundredths of a second — which is how the first
        /// finale screenshot came out three-quarters black and still passed every assertion about it.
        /// </summary>
        public static IEnumerator SettleScreen(FakeBackend fake)
        {
            GameFlow flow = Flow();
            fake.Next = new BackendSnapshot();
            yield return Until(() => !flow.Transitioning && flow.FadeAlpha <= 0.01f,
                "экран проявился из затемнения");
            yield return null;
        }

        /// <summary>Wait for a state, and fail by NAME rather than by timing out silently.</summary>
        public static IEnumerator Until(Func<bool> condition, string what,
            float patienceSeconds = DefaultPatienceSeconds)
        {
            float deadline = Time.realtimeSinceStartup + patienceSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), "Не дождались: " + what);
        }

        // ---- hands ---------------------------------------------------------------------------------

        /// <summary>Turn the handle by roughly this many degrees, then let go.</summary>
        public static IEnumerator CrankDegrees(FakeBackend fake, float degrees)
        {
            int frames = Mathf.Max(1, Mathf.CeilToInt(degrees / CrankPerFrame));
            fake.Next = new BackendSnapshot { CrankDeltaDegrees = CrankPerFrame };
            yield return Frames(frames);
            fake.Next = new BackendSnapshot();
            yield return null;
        }

        public static IEnumerator CrankUntil(FakeBackend fake, Func<bool> condition, string what,
            float patienceSeconds = DefaultPatienceSeconds)
        {
            float deadline = Time.realtimeSinceStartup + patienceSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                fake.Next = new BackendSnapshot { CrankDeltaDegrees = CrankPerFrame };
                yield return null;
            }

            fake.Next = new BackendSnapshot();
            Assert.IsTrue(condition(), "Не дождались (крутили): " + what);
        }

        /// <summary>Wave over the height sensors until something happens — the отгон, since 2026-08-07.</summary>
        public static IEnumerator SwipeUntil(FakeBackend fake, Func<bool> condition, string what,
            float patienceSeconds = DefaultPatienceSeconds)
        {
            float deadline = Time.realtimeSinceStartup + patienceSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                fake.Next = new BackendSnapshot { HeightA = Waving() };
                yield return null;
            }

            fake.Next = new BackendSnapshot();
            Assert.IsTrue(condition(), "Не дождались (махали над датчиком): " + what);
        }

        /// <summary>All three controllers at once — the game as it is meant to be played.</summary>
        public static IEnumerator PlayUntil(FakeBackend fake, Func<bool> condition, string what,
            float patienceSeconds = DefaultPatienceSeconds)
        {
            float deadline = Time.realtimeSinceStartup + patienceSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                fake.Next = new BackendSnapshot
                {
                    CrankDeltaDegrees = CrankPerFrame,
                    HeightA = Waving()
                };
                yield return null;
            }

            fake.Next = new BackendSnapshot();
            Assert.IsTrue(condition(), "Не дождались (тремя контролами): " + what);
        }

        /// <summary>
        /// A hand crossing the whole sensor every frame — one pass, one hit, which is the fastest a
        /// wave can possibly be. A test cannot wave at human speed and still fit in its patience
        /// window; what it must not do is fake a signal the sensor cannot produce, and 0 → 1 → 0 is
        /// exactly the range the package guarantees (<c>HeightControl</c> clamps to 0..1).
        /// </summary>
        private static float Waving() => Time.frameCount % 2 == 0 ? 1f : 0f;

        // ---- getting somewhere ------------------------------------------------------------------------

        /// <summary>Send the flow straight to a screen, through its own public transition.</summary>
        public static void JumpTo(GameFlow flow, GamePhase phase, int levelIndex)
        {
            flow.Go(phase, levelIndex);
        }

        /// <summary>Open a level and wait out its two-second survey, so play has actually begun.</summary>
        public static IEnumerator EnterLevel(FakeBackend fake, int levelIndex)
        {
            GameFlow flow = Flow();
            JumpTo(flow, GamePhase.Level, levelIndex);

            yield return Until(() => flow.Phase == GamePhase.Level && flow.LevelIndex == levelIndex,
                "уровень " + (levelIndex + 1) + " открылся");

            var screen = (LevelScreen)flow.Screen;
            yield return Idle(fake, 1);
            yield return Until(() => screen.Stage == LevelStage.Play,
                "обзор уровня " + (levelIndex + 1) + " закончился");
        }

        /// <summary>
        /// Crank one detail all the way into the vessel.
        /// </summary>
        /// <param name="pickForMe">
        /// Switch to the fixed-order variant so cranking alone is enough. Off when the test drives the
        /// choice itself — the tutorial's first beat is «НАВОДИ», and a variant that always has
        /// something noticed walks straight past it.
        /// </param>
        public static IEnumerator CollectOneDetail(FakeBackend fake, LevelScreen screen,
            bool pickForMe = true)
        {
            if (pickForMe) TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.CollectSeconds = FastCollectSeconds;

            int before = screen.Runtime.CollectedCount;
            yield return CrankUntil(fake, () => screen.Runtime.CollectedCount > before, "деталь в сосуде");
        }

        /// <summary>
        /// Notice a detail the way the tutorial's first beat asks for one to be noticed.
        ///
        /// The beat is «НАВОДИ» and the shipped way of aiming is the gaze, which a test cannot steer
        /// precisely enough to land on a 40 px paperclip inside a patience window. So the choice is
        /// handed to the fixed-order variant — the LOOP is the same either way, and what the tests
        /// after this point are about is what the beat does once something IS noticed.
        /// </summary>
        public static IEnumerator NoticeSomething(FakeBackend fake, LevelScreen screen)
        {
            TuningConfig.Notice = NoticeMode.FixedOrder;
            yield return Idle(fake, 2);
            yield return Until(() => screen.Runtime.NoticedIndex >= 0, "деталь замечена");
        }

        /// <summary>
        /// Notice a detail the way the SHIPPED game does: park the aim on it and let the dwell run out.
        ///
        /// The frames of the design gate have to be shot in this mode, because it is the one that ships
        /// (<c>TuningConfig.Defaults.Notice</c>) — and the round of 2026-08-08 found forty-nine of fifty
        /// frames staged in <see cref="NoticeMode.FixedOrder"/>, which is the variant that switches the
        /// aim OFF. The circle was therefore absent from almost every picture the founder was shown of a
        /// feature she had ordered made bigger and brighter.
        ///
        /// The circle is PARKED rather than steered: the joystick moves it at a speed in px/s and a test
        /// that flies it across the plate lands where the frame rate leaves it. Parking is the same call
        /// the player's hand makes — <see cref="GazeSelector.Position"/> is where the aim is — and
        /// everything that follows (dwell, the hit test, the drawn circle) is the game's own.
        /// </summary>
        public static IEnumerator NoticeByLooking(FakeBackend fake, LevelScreen screen, int detailIndex,
            float patienceSeconds = DefaultPatienceSeconds)
        {
            TuningConfig.Notice = NoticeMode.GazeJoystick;
            ParkTheAim(screen, LevelCatalog.AnchorOf(screen.Level.Details[detailIndex]));

            fake.Next = new BackendSnapshot();
            yield return Until(() => screen.Runtime.NoticedIndex >= 0,
                "взгляд заметил деталь «" + screen.Level.Details[detailIndex].Name + "»", patienceSeconds);
        }

        /// <summary>
        /// Put the aim down at <paramref name="spot"/> and leave it there — the stick is at rest, so
        /// nothing moves it afterwards.
        /// </summary>
        public static void ParkTheAim(LevelScreen screen, Vector2 spot)
        {
            TuningConfig.Notice = NoticeMode.GazeJoystick;
            screen.Runtime.GazeInPlay = true;
            screen.Runtime.Gaze.Position = spot;
        }

        // ---- how much of the frame the thoughts actually PAINT -------------------------------------

        /// <summary>
        /// The share of the frame the thoughts cover with their own ink, 0…1 — their visible rectangles
        /// weighted by how much of a rectangle each sprite paints.
        ///
        /// Waiting on <c>Thoughts.Count</c> is what gave the gate two «кадра волн» with no waves on them
        /// (06 and 09, 2026-08-08): thoughts enter from the edges, so three of them can be three slivers
        /// hanging off the frame — 0.67 % of ink against 0.66 % on an empty intro. This is the wait; the
        /// claim itself is made on the pixels of the written frame.
        /// </summary>
        public static float PaintedShare(LevelScreen screen)
        {
            System.Collections.Generic.IReadOnlyList<Thought> live = screen.Runtime.Field.Thoughts;
            float painted = 0f;
            for (int i = 0; i < live.Count; i++)
            {
                Vector2 size = live[i].Size;
                painted += size.x * size.y *
                           View.ArtThoughtView.VisibleShare(live[i].Position, size) * live[i].Ink;
            }

            return painted / (ThoughtField.ScreenWidth * ThoughtField.ScreenHeight);
        }

        /// <summary>
        /// Let the level's OWN band run until its thoughts paint <paramref name="share"/> of the frame.
        /// Hands off the sensors: an отгон would pop the waves as fast as they roll in.
        /// </summary>
        public static IEnumerator WaitForInkOnScreen(FakeBackend fake, LevelScreen screen, float share,
            float patienceSeconds = 90f)
        {
            fake.Next = new BackendSnapshot();
            yield return Until(() => PaintedShare(screen) >= share,
                "мысли закрасили " + (share * 100f).ToString("0.0") + " % кадра", patienceSeconds);
        }

        // ---- situations that would otherwise take real minutes ------------------------------------------

        /// <summary>
        /// Reach a coverage percentage by PLAYING the level: real waves, of the level's own thoughts,
        /// drifting in from the edges the way <see cref="ThoughtField.Spawn"/> sends them.
        ///
        /// This is what a peak frame has to be staged with. <see cref="CrowdTheScreen"/> lays its blobs
        /// on a 5×3 lattice with one label per cell, and the game has no way of producing that — the
        /// frame the design gate was given showed a mosaic nobody will ever see (gate, 2026-08-07).
        /// The numbers below are the panel's own knobs, which is the same thing the founder turns: the
        /// wave clock is wound to its floor and the composition made dense, and then the level is simply
        /// left to run until the coverage arrives.
        /// </summary>
        public static IEnumerator CrowdTheScreenByPlaying(FakeBackend fake, LevelScreen screen,
            float percent, float patienceSeconds = 90f)
        {
            // The live values, not the per-level band: the level is already open, and ApplyLevel has
            // already copied its band in. This is exactly what moving a slider mid-level does.
            TuningConfig.WaveIntervalSeconds = 0.4f;
            TuningConfig.WaveWeak = 2;
            TuningConfig.WaveMedium = 2;
            TuningConfig.WaveStrong = 1;
            // The ramp shortens an interval that is already at its floor — one less thing in the way.
            TuningConfig.PressureRamp = false;

            // …and the level's SUPPLY is lifted (founder 2026-09-22, «общий запас мыслей»). Since that
            // playtest a level sends a fixed number of thoughts and stops — five on level 1 — so a
            // helper that only winds the CLOCK would wait ninety seconds for a sixth blob that is
            // never coming. 0 is «no budget», the same value the greybox scenettes run on, and it is
            // a panel row like the other four this helper moves.
            TuningConfig.ThoughtBudget = 0;
            screen.Runtime.Field.ResetWaveTimer(0f);

            // Hands off the sensors: the отгон would pop the waves as fast as they roll in.
            fake.Next = new BackendSnapshot();

            yield return Until(
                () => screen.Runtime.Field.OverlapPercent >= percent || screen.Stage != LevelStage.Play,
                "волны закрыли " + percent.ToString("0") + " % экрана", patienceSeconds);

            Assert.AreEqual(LevelStage.Play, screen.Stage,
                "Уровень кончился раньше, чем кадр набрался — на нём уже не пик.");
            AssertNotAGrid(screen);
        }

        /// <summary>
        /// The frame is a naplyv, not a mosaic: a lattice betrays itself by putting many centres on the
        /// same column and the same row, to the pixel. Live thoughts enter from random points of random
        /// edges and drift, so their columns and rows are all their own.
        /// </summary>
        public static void AssertNotAGrid(LevelScreen screen)
        {
            System.Collections.Generic.IReadOnlyList<Thought> live = screen.Runtime.Field.Thoughts;
            Assert.GreaterOrEqual(live.Count, 6,
                "Мыслей слишком мало, чтобы судить, сетка это или наплыв.");

            var columns = new System.Collections.Generic.HashSet<int>();
            var rows = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < live.Count; i++)
            {
                columns.Add(Mathf.RoundToInt(live[i].Position.x));
                rows.Add(Mathf.RoundToInt(live[i].Position.y));
            }

            float bar = live.Count * 0.7f;
            Assert.Greater(columns.Count, bar,
                "Мысли стоят по колонкам (" + columns.Count + " на " + live.Count +
                ") — это сетка, а не живой наплыв.");
            Assert.Greater(rows.Count, bar,
                "Мысли стоят по рядам (" + rows.Count + " на " + live.Count +
                ") — это сетка, а не живой наплыв.");
        }

        /// <summary>
        /// Put a named number of thoughts on the screen on a lattice — for tests that only need the
        /// coverage NUMBER to cross a threshold (does the peak flag flip, does the vessel layer swap).
        ///
        /// Never for a frame. The game does not spawn on a lattice, so a screenshot staged with this
        /// shows a composition that cannot happen; use <see cref="CrowdTheScreenByPlaying"/> there.
        /// </summary>
        /// <returns>The coverage reached, in per cent.</returns>
        public static float CrowdTheScreen(LevelScreen screen, int count)
        {
            ThoughtField field = screen.Runtime.Field;
            string[] sprites = screen.Level.ThoughtSprites;

            const int columns = 5;
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
            float cellW = ThoughtField.ScreenWidth / columns;
            float cellH = ThoughtField.ScreenHeight / rows;

            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                var spot = new Vector2((column + 0.5f) * cellW, (row + 0.5f) * cellH);
                field.SpawnAt(ThoughtStrength.Strong, sprites[i % sprites.Length], spot);
            }

            return field.OverlapPercent;
        }

        /// <summary>
        /// Bury the screen under thoughts through the real field — the same call the defeat screen
        /// itself makes, so the test cannot reach a coverage the game has no way of producing.
        /// </summary>
        public static void BuryTheScreen(LevelScreen screen)
        {
            ThoughtField field = screen.Runtime.Field;
            field.CoverScreen();

            Assert.GreaterOrEqual(field.OverlapPercent, TuningConfig.LossOverlapPercent,
                "Не удалось закрыть экран мыслями.");
        }

        // ---- the clean-exit contract ----------------------------------------------------------------

        private static Action _launcher;
        private static int _launcherExits;

        /// <summary>How many times the stand-in launcher has been handed the screen.</summary>
        public static int LauncherExits => _launcherExits;

        /// <summary>
        /// Stand in for the cabinet's launcher: subscribe to the exit hook the way the hub will
        /// (ARCADE_INTEGRATION_CONTRACT §5). With a listener present the game must LEAVE — not reload
        /// its own entry scene, which on the cabinet would restart the game the player just quit.
        /// </summary>
        public static void PretendLauncherIsListening()
        {
            ForgetLauncher();
            _launcherExits = 0;
            _launcher = () => _launcherExits++;
            PreviewStandNav.ExitRequested += _launcher;
        }

        /// <summary>Unsubscribe the stand-in. A static event outliving a test would poison the next one.</summary>
        public static void ForgetLauncher()
        {
            if (_launcher != null) PreviewStandNav.ExitRequested -= _launcher;
            _launcher = null;
        }

        /// <summary>
        /// The exit itself: the launcher was told, and the game finished cleanly — its screen (with the
        /// timers on it) disposed and the flow no longer ticking. The flow reference is checked for
        /// having SURVIVED: had the game «left» by reloading its scene, this object would be destroyed
        /// and the assertion below would catch exactly the bug this path replaces.
        /// </summary>
        public static IEnumerator AssertHandedToLauncher(GameFlow flow, int expectedExits, string what)
        {
            yield return Frames(6);

            Assert.AreEqual(expectedExits, _launcherExits, what + ": лаунчеру не сказали, что игра уходит.");
            Assert.IsTrue(flow != null, what + ": игра перезагрузила свою сцену вместо выхода.");
            Assert.IsTrue(flow.Exited, what + ": игра не завершилась.");
            Assert.IsNull(flow.Screen, what + ": экран прогона пережил выход.");
            Assert.IsFalse(flow.enabled, what + ": поток продолжает тикать после выхода.");
        }

        /// <summary>
        /// The FALLBACK path only — no launcher is listening (editor / standalone), so the game has
        /// nowhere to hand the screen to and goes back to its own title.
        ///
        /// «Back to the title» is now an IN-PLACE restart: no <c>SceneManager.LoadScene</c> at all, so
        /// the flow object SURVIVES and is checked for having done so. That is the same assertion in
        /// spirit as the launcher path's «игра перезагрузила свою сцену вместо выхода», and it is the
        /// property the cabinet needs: the launcher's own watchdog answers the same MenuButton press
        /// with its own scene load, and a second one from here would be a race over where the cabinet
        /// lands. Checking the scene name alone would pass even if the flow came back mid-run, so the
        /// state of the run is checked too — the title is up, no level is carried, the game still ticks.
        /// </summary>
        public static IEnumerator AssertCleanRestart(string what)
        {
            yield return Frames(6);

            Assert.AreEqual(PreviewScenes.Game,
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                what + ": сцена сменилась — возврат на титул не должен грузить сцены.");

            GameFlow flow = Flow();
            Assert.IsTrue(flow != null, what + ": игра перезагрузила свою сцену вместо возврата на титул.");
            Assert.AreEqual(GamePhase.Title, flow.Phase, what + ": игра не начала заново с титула.");
            Assert.IsInstanceOf<TitleScreen>(flow.Screen, what + ": на титуле стоит не экран титула.");
            Assert.AreEqual(0, flow.LevelIndex, what + ": за возвратом тянется уровень прошлого прогона.");
            Assert.IsFalse(flow.Transitioning, what + ": возврат на титул завис в переходе.");
            Assert.IsFalse(flow.Exited, what + ": без лаунчера игра никуда не уходит — ей некуда.");
            Assert.IsTrue(flow.enabled, what + ": игра перестала тикать, хотя осталась у себя.");
        }
    }
}
