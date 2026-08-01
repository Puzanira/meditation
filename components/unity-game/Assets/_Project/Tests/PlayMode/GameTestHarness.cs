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

        public static IEnumerator ShakeUntil(FakeBackend fake, Func<bool> condition, string what,
            float patienceSeconds = DefaultPatienceSeconds)
        {
            float deadline = Time.realtimeSinceStartup + patienceSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                fake.Next = new BackendSnapshot { Joystick = Alternating() };
                yield return null;
            }

            fake.Next = new BackendSnapshot();
            Assert.IsTrue(condition(), "Не дождались (трясли): " + what);
        }

        /// <summary>Both hands at once — the game as it is meant to be played.</summary>
        public static IEnumerator PlayUntil(FakeBackend fake, Func<bool> condition, string what,
            float patienceSeconds = DefaultPatienceSeconds)
        {
            float deadline = Time.realtimeSinceStartup + patienceSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                fake.Next = new BackendSnapshot
                {
                    CrankDeltaDegrees = CrankPerFrame,
                    Joystick = Alternating()
                };
                yield return null;
            }

            fake.Next = new BackendSnapshot();
            Assert.IsTrue(condition(), "Не дождались (двумя руками): " + what);
        }

        /// <summary>A sharp reversal every frame — what the shake detector is looking for.</summary>
        private static Vector2 Alternating() =>
            new Vector2(Time.frameCount % 2 == 0 ? 1f : -1f, 0f);

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
        /// Switch to the fixed-order variant so cranking alone is enough. Off when the test is about
        /// the tutorial: variant A always has something noticed, which would retire the «Оглядись»
        /// card the moment it appeared and hide the very beat under test.
        /// </param>
        public static IEnumerator CollectOneDetail(FakeBackend fake, LevelScreen screen,
            bool pickForMe = true)
        {
            if (pickForMe) TuningConfig.Notice = NoticeMode.FixedOrder;
            TuningConfig.CollectSeconds = FastCollectSeconds;

            int before = screen.Runtime.CollectedCount;
            yield return CrankUntil(fake, () => screen.Runtime.CollectedCount > before, "деталь в сосуде");
        }

        // ---- situations that would otherwise take real minutes ------------------------------------------

        /// <summary>
        /// Run the clock out through the real rule, in one big step. The spec's own slider does not go
        /// below 60 s, and what is under test is what happens AT zero — not how long a minute lasts.
        /// </summary>
        public static void DrainTheClock(LevelScreen screen)
        {
            screen.Rules.Tick(TuningConfig.LevelSeconds + 1f, 0f);
        }

        /// <summary>
        /// Crowd the screen the way PLAY crowds it: ordinary thoughts of the level's own set, spread
        /// over the frame, each still fitted inside its size class. Not <see cref="BuryTheScreen"/> —
        /// that one lays the defeat WALLPAPER, whose blobs are sized to close their cells, and a peak
        /// staged with it comes out looking like a defeat with the level painted over.
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
