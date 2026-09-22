using AiGameStudio.ArcadeControls;
using Meditation.Mechanics;
using Meditation.Stand;
using Meditation.Tuning;
using Meditation.View;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.Game
{
    /// <summary>Which screen of SCREENS.md is on right now.</summary>
    public enum GamePhase
    {
        Title = 0,
        LevelCard = 1,
        Level = 2,
        Finale = 3
    }

    /// <summary>
    /// The whole game, in one scene: titles → level card → level → card → … → finale → out.
    ///
    /// One scene rather than one per screen, because SCREENS.md's flow is a state machine, not a set
    /// of destinations, and because the cabinet's contract is about the ENTRY scene: «в меню» has to be
    /// an instant clean exit from anywhere, and a flow that never leaves its scene has nothing to
    /// unwind — every screen owns one layer and is destroyed whole when the flow moves on.
    ///
    /// The tuning panel lives here rather than in a screen: the founder tunes across a whole run, and
    /// the values she moves are per level (TuningCatalog.Game), so the panel must outlive the levels.
    /// </summary>
    [AddComponentMenu("Meditation/Game Flow")]
    [DisallowMultipleComponent]
    public sealed class GameFlow : MonoBehaviour
    {
        /// <summary>Fade to black between screens (SCREENS «Общие правила»: 0.3 с).</summary>
        public const float FadeSeconds = 0.3f;

        private readonly CrankSpeedMeter _crankMeter = new CrankSpeedMeter();
        private readonly SwipeDetector _swipe = new SwipeDetector();

        private GameScreen _screen;
        private Image _fade;
        private RippleWipe _ripple;

        private GamePhase _pendingPhase;
        private int _pendingLevel;
        private bool _transitioning;
        private float _fadeSeconds;
        private bool _rippling;

        public DesignStage Stage { get; private set; }

        public TuningPanel Panel { get; private set; }

        /// <summary>
        /// The three sound layers of MECHANICS §8. They live on the FLOW, not on a screen: a card or a
        /// victory screen lasts two seconds, and a background track that is rebuilt on every one of
        /// them is a gap in the music five times a run. The screens only describe what they sound like
        /// (<see cref="GameScreen.Audio"/>) and the mix decides what that means.
        /// </summary>
        public GameAudio Audio { get; private set; }

        public GamePhase Phase { get; private set; } = GamePhase.Title;

        /// <summary>0-based index of the level being played (or about to be).</summary>
        public int LevelIndex { get; private set; }

        /// <summary>The screen currently on — the PlayMode suite's handle on the flow.</summary>
        public GameScreen Screen => _screen;

        /// <summary>True while the 0.3 s fade between two screens is running.</summary>
        public bool Transitioning => _transitioning;

        /// <summary>
        /// How black the screen is right now, 0..1. Public because "the screen has settled" is not
        /// something a caller can count in frames: a batch run draws hundreds of them a second, and a
        /// screenshot taken twenty frames after a transition came out three-quarters black.
        /// </summary>
        public float FadeAlpha => _rippling
            ? (_ripple != null ? _ripple.Progress : 0f)
            : (_fade != null ? _fade.color.a : 0f);

        /// <summary>Screens completed since boot — proves the flow really moved, not just re-rendered.</summary>
        public int ScreensShown { get; private set; }

        private void Awake()
        {
            EnsureArcadeInput();

            Stage = DesignStage.Create("GameCanvas");
            // The letterbox around a photographic plate is black: unlike the stand, this is the
            // cabinet's own picture, and a coloured border would read as part of the art.
            Stage.Backdrop.color = Color.black;

            _fade = Ui.BoxCentred(Stage.Frame, "ScreenFade", 960f, 540f, 1920f, 1080f,
                new Color(0f, 0f, 0f, 0f));

            // …and the other way a screen can be covered (founder, 2026-09-22, п.8). Built once, here,
            // for the same reason the fade is: it outlives every screen it is used between.
            _ripple = new RippleWipe(Stage.Frame);

            Audio = new GameAudio(transform);
            Panel = TuningPanel.Create(Stage, "Медитация в спешке", TuningCatalog.Game(), Readout);

            var exit = GetComponent<MenuButtonExit>();
            if (exit == null) exit = gameObject.AddComponent<MenuButtonExit>();
            // The game's «в меню» is the cabinet's exit, not a reload of the game (see ExitToLauncher).
            exit.ExitScene = PreviewScenes.Game;
            exit.ExitAction = ExitToLauncher;

            // …unless the stand asked for one level (founder 2026-08-19, «запускать уровни по
            // отдельности»). Then this boot of the game is a debug launch: it opens ON the level, and
            // its «в меню» goes back to the stand that opened it.
            int standLevel = StandLevelLaunch.Take();
            if (standLevel >= 0)
            {
                LaunchedFromStand = true;
                StandLevelIndex = Mathf.Clamp(standLevel, 0, LevelCatalog.Count - 1);
                exit.ExitScene = PreviewScenes.Menu;
                exit.ExitAction = ExitToStand;

                // «Без обучения»: the beats are level 1's opening for a player who has never seen the
                // game, and this door is not for that player. Said through the flow's own flag rather
                // than through a second rule inside LevelScreen — this is exactly the state a run is
                // in after level 1 has been played once.
                NoteTutorialGiven();

                Enter(GamePhase.Level, StandLevelIndex);
                return;
            }

            Enter(GamePhase.Title, 0);
        }

        /// <summary>
        /// True when this scene was opened by the stand's «Уровень N», not by the cabinet's launcher.
        ///
        /// It changes three things and nothing else: the flow starts ON the level instead of on the
        /// title, «в меню» lands in the stand's menu instead of handing the screen to the launcher,
        /// and the two ways a level ends lead back to the stand rather than on through the run. The
        /// LEVEL itself is untouched — same art, same rules, same three controllers, same tuning band
        /// (<see cref="Tuning.TuningConfig.ApplyLevel"/> runs from LevelScreen either way), because a
        /// debug door that opens a slightly different game is a debug door that teaches nothing.
        /// </summary>
        public bool LaunchedFromStand { get; private set; }

        /// <summary>Which level the stand asked for, 0-based; -1 on the shipped path.</summary>
        public int StandLevelIndex { get; private set; } = -1;

        private void OnDestroy()
        {
            _screen?.Dispose();
            _screen = null;
            Audio?.Dispose();
            Audio = null;
            _ripple?.Dispose();
            _ripple = null;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            // Three controllers, one line each (founder, 2026-08-07): the crank drags, the joystick
            // ONLY aims, and the two height sensors are the отгон.
            float crankSpeed = _crankMeter.Tick(ArcadeInput.Crank.DeltaDegrees, deltaTime);
            Vector2 stick = ArcadeInput.Joystick.Vector;
            _swipe.Tick(ArcadeInput.HeightA.Value, ArcadeInput.HeightB.Value, deltaTime);

            var hands = new Hands(
                crankSpeed,
                crankSpeed >= TuningConfig.CrankThresholdDegPerSec,
                ArcadeInput.Crank.DeltaDegrees,
                ArcadeInput.Crank.TotalDegrees,
                stick,
                _swipe.HitsThisTick);

            TickFade(deltaTime);
            _screen?.Advance(deltaTime, hands);

            // …and after the screen has had its frame, because what it sounds like is a consequence of
            // what it just did (a detail that landed this frame starts the meditation layer's tail).
            if (Audio != null)
                Audio.Tick(deltaTime, _screen != null ? _screen.Audio : AudioScene.Quiet(LevelIndex));
        }

        // ---- transitions ---------------------------------------------------------------------------

        /// <summary>
        /// Fade to black, swap the screen, fade back. The swap happens at the darkest point, so no
        /// frame of the game ever shows two screens at once — and a screen that asks to move on twice
        /// (a race between a timer and an input) is ignored the second time.
        /// </summary>
        public void Go(GamePhase phase, int levelIndex)
        {
            if (_transitioning) return;
            _transitioning = true;
            _rippling = false;
            _fadeSeconds = 0f;
            _pendingPhase = phase;
            _pendingLevel = levelIndex;
        }

        /// <summary>
        /// The same move, made of water: «победа → наезд камеры на ведёрко → круглая рябь → заставка
        /// следующего уровня, тем же переходом рябь входит в следующий экран» (founder, 2026-09-22).
        ///
        /// Only the victory uses it, and that is the point — a ripple out of the bucket is a sentence
        /// about the thing the player has just filled. A defeat still cuts to black (it is the drawn
        /// screen the crank rubs away, and it has its own picture), and so does every other move.
        /// </summary>
        /// <param name="from">Design-px point the ripple leaves from — the level's vessel.</param>
        public void GoWithRipple(GamePhase phase, int levelIndex, Vector2 from)
        {
            if (_transitioning) return;
            _ripple?.SetCentre(from);
            _transitioning = true;
            _rippling = true;
            _fadeSeconds = 0f;
            _pendingPhase = phase;
            _pendingLevel = levelIndex;
        }

        /// <summary>Half a transition — to the covered frame, or back from it — in seconds.</summary>
        private float HalfSeconds => _rippling
            ? Mathf.Max(0.05f, TuningConfig.RippleSeconds)
            : FadeSeconds * 0.5f;

        private void TickFade(float deltaTime)
        {
            if (!_transitioning)
            {
                if (_fadeSeconds <= 0f) return;

                // Uncovering after a swap — the ripple OPENS on the new screen, which is the second
                // half of the founder's sentence and the reason the flag survives the swap.
                _fadeSeconds = Mathf.Max(0f, _fadeSeconds - deltaTime);
                SetCover(_fadeSeconds / HalfSeconds);
                if (_fadeSeconds <= 0f) _rippling = false;
                return;
            }

            _fadeSeconds += deltaTime;
            float half = HalfSeconds;
            if (_fadeSeconds < half)
            {
                SetCover(_fadeSeconds / half);
                return;
            }

            SetCover(1f);
            _transitioning = false;
            _fadeSeconds = half;
            Enter(_pendingPhase, _pendingLevel);
        }

        /// <summary>
        /// How covered the frame is, 0…1 — through whichever of the two covers this transition uses.
        /// Exactly one of them is ever non-zero, so «экран закрыт» has one meaning at any moment.
        /// </summary>
        private void SetCover(float amount)
        {
            float a = Mathf.Clamp01(amount);

            if (_rippling)
            {
                SetFadeAlpha(0f);
                _ripple?.SetProgress(a);
                return;
            }

            _ripple?.SetProgress(0f);
            SetFadeAlpha(a);
        }

        private void SetFadeAlpha(float alpha)
        {
            Color c = _fade.color;
            c.a = Mathf.Clamp01(alpha);
            _fade.color = c;
            _fade.raycastTarget = false;
        }

        /// <summary>The ripple itself — the suite shoots the middle of a transition through it.</summary>
        public RippleWipe Ripple => _ripple;

        /// <summary>True while the transition on screen is the ripple rather than the black fade.</summary>
        public bool Rippling => _rippling;

        private void Enter(GamePhase phase, int levelIndex)
        {
            _screen?.Dispose();

            Phase = phase;
            LevelIndex = Mathf.Clamp(levelIndex, 0, LevelCatalog.Count - 1);

            switch (phase)
            {
                case GamePhase.Title:
                    // The title IS the end of a run: whatever the last player learned goes with them.
                    // Cleared here rather than in StartRun because every way back to the title is a
                    // new run — the finale, «в меню», and the launcher's fallback all pass through
                    // this line, and only one of them goes on to call StartRun.
                    TutorialAlreadyGiven = false;
                    _screen = new TitleScreen(this);
                    break;
                case GamePhase.LevelCard:
                    _screen = new LevelCardScreen(this, LevelCatalog.At(LevelIndex));
                    break;
                case GamePhase.Level:
                    _screen = new LevelScreen(this, LevelIndex);
                    break;
                default:
                    _screen = new FinaleScreen(this);
                    break;
            }

            ScreensShown++;

            // The cover sits on top of whatever the new screen just built — both of them, in the
            // order they are used: the ripple opens over the fade when a level has just been won.
            _fade.rectTransform.SetAsLastSibling();
            _ripple?.BringToFront();
        }

        // ---- what the screens ask for ---------------------------------------------------------------

        /// <summary>
        /// Has level 1 already opened in THIS run? Then it does not teach again — «обучение не
        /// повторяется после поражения» (founder, 2026-08-07).
        ///
        /// It lives on the flow because a level screen cannot remember anything: every attempt builds
        /// a new one, which is exactly what made the tutorial replay. And it is per RUN rather than
        /// per session, because the cabinet hands the same process to one stranger after another —
        /// a flag that survived <see cref="StartRun"/> would show the second player a game that
        /// silently assumes they watched the first one's tutorial.
        /// </summary>
        public bool TutorialAlreadyGiven { get; private set; }

        /// <summary>Level 1 has opened: from here on this run goes straight into play.</summary>
        public void NoteTutorialGiven() => TutorialAlreadyGiven = true;

        /// <summary>Two turns of the dynamo on the title: the run begins at level 1.</summary>
        public void StartRun() => Go(GamePhase.LevelCard, 0);

        /// <summary>The 2.5 s card is up — into the level it announced.</summary>
        public void LevelCardFinished() => Go(GamePhase.Level, LevelIndex);

        /// <summary>The victory tableau has been held: next level's card, or the finale.</summary>
        public void LevelWon(int levelIndex)
        {
            // A level opened from the stand is a level, not a run: it ends where it was started from.
            // Going on to level N+1 would quietly turn «покажи мне метро» into a playthrough.
            if (LaunchedFromStand)
            {
                ExitToStand();
                return;
            }

            // «Рябь» out of the bucket the player has just filled (founder, 2026-09-22, п.8).
            Vector2 vessel = LevelCatalog.At(levelIndex).VesselCentre;

            int next = levelIndex + 1;
            if (next >= LevelCatalog.Count) GoWithRipple(GamePhase.Finale, levelIndex, vessel);
            else GoWithRipple(GamePhase.LevelCard, next, vessel);
        }

        /// <summary>The screen was wiped clear (or the auto-retry fired): the same level, from its card.</summary>
        public void LevelFailed(int levelIndex)
        {
            // …and from the stand, straight back into the same level: the card announces a level the
            // player already asked for by name, and <see cref="Enter"/> builds the screen fresh, so
            // the retry is as clean as the first entry (new LevelScreen, new CollectionRuntime,
            // ApplyLevel run again).
            if (LaunchedFromStand)
            {
                Go(GamePhase.Level, levelIndex);
                return;
            }

            Go(GamePhase.LevelCard, levelIndex);
        }

        /// <summary>
        /// Back to the stand's menu — the way out of a level that the stand opened, whether the player
        /// pressed «в меню», or won it.
        ///
        /// Not <see cref="ExitToLauncher"/>: that one's fallback restarts the GAME at its title, and a
        /// door opened from the stand has to close back onto the stand. What it does instead is the
        /// stand's own «в меню», the launcher hook included — <see cref="PreviewStandNav.ExitToMenu"/>,
        /// the same call every scenette's menu button makes, because a level opened from the stand is
        /// a stand screen for as long as it is up.
        ///
        /// ARCADE_INTEGRATION_CONTRACT §5 is untouched: the launcher opens the game scene with nothing
        /// pending (<see cref="StandLevelLaunch"/>), so on the cabinet this branch is unreachable and
        /// the button still hands the screen back exactly as the contract says.
        ///
        /// The teardown is the same one either way, and it has to be: the run stops updating and its
        /// screen is disposed with its timers before the scene load, so nothing of this level runs on
        /// behind the stand's menu.
        /// </summary>
        public void ExitToStand()
        {
            if (Exited) return;
            Exited = true;

            Audio?.SilenceNow();
            _screen?.Dispose();
            _screen = null;
            _transitioning = false;
            _fadeSeconds = 0f;
            enabled = false;

            PreviewStandNav.ExitToMenu();
        }

        /// <summary>
        /// The one way out — the finale ending, and «в меню» from any screen.
        ///
        /// ARCADE_INTEGRATION_CONTRACT §5: no Application.Quit, and no «leaving» by reloading the entry
        /// scene either — on the cabinet that would restart the game the player just asked to leave.
        /// The exit is the launcher's hook (<see cref="PreviewStandNav.RequestExit"/>) plus a clean
        /// finish on our side: the screen with its timers is disposed and the flow stops updating, so
        /// nothing of this run survives into whatever loads next.
        ///
        /// When nobody is listening — the editor, or a standalone build of the game by itself — there
        /// is nothing to hand the screen to, so the game goes back to its own title instead. That is a
        /// separate, explicitly named path, not the exit.
        ///
        /// That fallback returns to the title IN PLACE and never loads a scene, and on the cabinet that
        /// is the part that matters. The arcade hub does not know this game's API, so it never
        /// subscribes to <see cref="PreviewStandNav.ExitRequested"/> — it watches the SAME MenuButton
        /// from its own launcher-owned watchdog and loads the hub menu itself. Two objects reacting to
        /// one press is fine; two <c>SceneManager.LoadScene</c> calls in one frame is not — the game
        /// would be racing the launcher over which scene the cabinet ends up in, and winning that race
        /// means restarting the game the player just asked to leave. So the game's fallback stays
        /// inside its own scene and lets the launcher own the navigation, exactly as the other packaged
        /// games on this cabinet do (Life Choices ends its run in place and loads nothing).
        /// </summary>
        public void ExitToLauncher()
        {
            if (Exited) return;

            // «Кнопка „в меню“ — мгновенная тишина вместе с выходом» (MECHANICS §8). Before anything
            // else: the launcher takes the screen back at once, and music playing over somebody else's
            // menu is the loudest possible way to leave a room.
            Audio?.SilenceNow();

            if (PreviewStandNav.RequestExit())
            {
                Exited = true;
                _screen?.Dispose();
                _screen = null;
                _transitioning = false;
                _fadeSeconds = 0f;
                enabled = false;   // no more Update: no timer of this run keeps running behind the launcher
                return;
            }

            RestartAtTitle();
        }

        /// <summary>
        /// The fallback's whole content: tear this run down and stand the title back up, without
        /// leaving the scene. Everything a run owns lives on its screen (timers included), so disposing
        /// it and entering <see cref="GamePhase.Title"/> is the same clean slate a scene reload used to
        /// give — minus the scene load.
        /// </summary>
        private void RestartAtTitle()
        {
            _transitioning = false;
            _rippling = false;
            _fadeSeconds = 0f;
            _ripple?.SetProgress(0f);
            SetFadeAlpha(0f);
            Enter(GamePhase.Title, 0);
        }

        /// <summary>True once the game has handed the screen back to the launcher and stopped.</summary>
        public bool Exited { get; private set; }

        private string Readout()
        {
            string screen = _screen != null ? _screen.Readout() : "—";
            return screen + "\n" +
                   "уровень для панели: У" + (TuningConfig.ActiveLevelIndex + 1) +
                   (LaunchedFromStand ? "\nзапуск со стенда: «в меню» → меню стенда" : "");
        }

        private static void EnsureArcadeInput()
        {
            if (FindAnyObjectByType<ArcadeInputRunner>() != null) return;
            var go = new GameObject("ArcadeInput");
            go.AddComponent<ArcadeInputRunner>();
        }
    }
}
