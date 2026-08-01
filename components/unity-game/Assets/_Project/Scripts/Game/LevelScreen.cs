using Meditation.Mechanics;
using Meditation.Stand;
using Meditation.Tuning;
using Meditation.View;
using UnityEngine;

namespace Meditation.Game
{
    /// <summary>The level's own state machine (SCREENS.md «Состояния S3»).</summary>
    public enum LevelStage
    {
        /// <summary>2 s of «обзор»: no thoughts, every detail pulsing, the timer held.</summary>
        Intro = 0,
        /// <summary>The loop of MECHANICS §1–§5. `peak` is a look, not a state, so it lives in here.</summary>
        Play = 1,
        /// <summary>S4: dissolve → silence → the vessel lifts with the haul.</summary>
        Win = 2,
        /// <summary>S5: the screen is thoughts, and the crank wipes them away.</summary>
        Lose = 3
    }

    /// <summary>Scripted beats of the level-1 tutorial (SCREENS §Обучение, walkthrough 4–9).</summary>
    public enum TutorialBeat
    {
        /// <summary>«Крути ручку!» — the first detail is already noticed, nothing else happens yet.</summary>
        Crank = 0,
        /// <summary>«Тряси джойстик!» — one thought, sitting on the next detail.</summary>
        Shake = 1,
        /// <summary>«Оглядись — наклони стик» — shown once; the timer is running by now.</summary>
        Gaze = 2,
        /// <summary>Taught. Ordinary play.</summary>
        Done = 3
    }

    /// <summary>
    /// S3 — a whole level: the art composition, the two-handed loop, the timer, both defeats, the
    /// victory tableau, and (on level 1 only) the three teaching beats.
    ///
    /// The rules underneath are the ones the preview stand already proved: <see cref="CollectionRuntime"/>,
    /// <see cref="LevelRules"/>, <see cref="ThoughtField"/>. What this screen adds is the level's own
    /// composition and the state machine around the loop.
    /// </summary>
    public sealed class LevelScreen : GameScreen
    {
        private const float IntroSeconds = 2f;      // SCREENS S3 state 1
        private const float DissolveSeconds = 0.5f; // мысли растворяются (mock 18)
        private const float SilenceSeconds = 1f;    // секунда чистой сцены
        private const float WinHoldSeconds = 3f;    // S4 держится 3 с
        private const float AutoRetrySeconds = 4f;  // S5 [toggle]
        private const float DegreesPerTurn = 360f;

        /// <summary>Each turn of the crank wipes ~10 % of the defeat screen (SCREENS S5).</summary>
        private const float WipeSharePerTurn = 0.1f;

        /// <summary>The gaze must reach the third hint before it stops being a hint and becomes noise.</summary>
        private const float GazeHintSeconds = 8f;

        /// <summary>How many thoughts the defeat screen is allowed to hold once it has closed over.</summary>
        private const int DefeatFillCap = ThoughtField.CoverColumns * ThoughtField.CoverRows;

        private readonly LevelDefinition _level;
        private readonly int _levelIndex;
        private readonly LevelView _view;
        private readonly CollectionRuntime _runtime;
        private readonly LevelRules _rules = new LevelRules();
        private readonly bool _teaches;

        private float _stageSeconds;
        private float _wipeDegrees;
        private int _defeatThoughts;
        private Thought _tutorialThought;

        public LevelScreen(GameFlow flow, int levelIndex) : base(flow, "LevelScreen")
        {
            _levelIndex = levelIndex;
            _level = LevelCatalog.At(levelIndex);
            _teaches = levelIndex == 0;

            TuningConfig.ActiveLevelIndex = levelIndex;
            TuningConfig.ApplyLevel(levelIndex);

            _view = new LevelView(Root, _level);

            var homes = new Vector2[_level.DetailCount];
            var names = new string[_level.DetailCount];
            for (int i = 0; i < _level.DetailCount; i++)
            {
                homes[i] = _level.Details[i].Home;
                names[i] = _level.Details[i].Name;
            }

            _runtime = new CollectionRuntime(_view, homes, _level.VesselCentre, names);

            // The art drop puts real details in the foreground strip — the dandelion at y 958, the
            // fish at 991, the mouse at 1029. The gaze has to be able to go down there.
            _runtime.Gaze.MaxY = DesignStage.DesignHeight;

            _runtime.Field.Labels = _level.ThoughtSprites;
            _runtime.Field.ArtSizer = t => ArtLibrary.FitThought(t.Label, t.Strength);
            _runtime.DetailCollected += OnDetailCollected;

            Restart();
        }

        public LevelStage Stage { get; private set; }

        public TutorialBeat Beat { get; private set; }

        public LevelView View => _view;

        public LevelRules Rules => _rules;

        public CollectionRuntime Runtime => _runtime;

        public LevelDefinition Level => _level;

        /// <summary>Does the sun-dial advance right now? The tutorial holds it [toggle].</summary>
        public bool TimerRunning =>
            Stage == LevelStage.Play && (!_teaches || !TuningConfig.TutorialTimerPaused ||
                                         Beat >= TutorialBeat.Gaze);

        /// <summary>True once the victory tableau of mock 18 is fully on screen.</summary>
        public bool VictoryPresented =>
            Stage == LevelStage.Win && _stageSeconds >= DissolveSeconds + SilenceSeconds;

        /// <summary>Thoughts still covering the defeat screen — the retry's own progress bar.</summary>
        public int DefeatThoughtsLeft => _runtime.Field.Thoughts.Count;

        // ---- lifecycle ---------------------------------------------------------------------------

        private void Restart()
        {
            _runtime.Restart();
            _rules.Restart(_level.DetailCount);
            _stageSeconds = 0f;
            _wipeDegrees = 0f;
            _tutorialThought = null;
            Beat = _teaches ? TutorialBeat.Crank : TutorialBeat.Done;

            EnterStage(LevelStage.Intro);

            _view.ShowMessage("", "");
            _view.HideHint();
            _view.SetHudVisible(true);
            _view.SetDesaturated(false);
            _view.ClearDefeatStaging();
            _view.SetThoughtsAlpha(1f);
            _view.SetTimer(1f, TuningConfig.LevelSeconds);
            _view.SetTimerRunning(false);
        }

        private void EnterStage(LevelStage stage)
        {
            Stage = stage;
            _stageSeconds = 0f;
            _view.SetIntroPulse(stage == LevelStage.Intro);
        }

        protected override void Tick(float deltaTime, in Hands hands)
        {
            _stageSeconds += deltaTime;
            _view.SetCrank(hands.CrankTotal, hands.CrankSpinning, CrankAlarm);

            switch (Stage)
            {
                case LevelStage.Intro: TickIntro(deltaTime); break;
                case LevelStage.Play: TickPlay(deltaTime, hands); break;
                case LevelStage.Win: TickWin(deltaTime); break;
                default: TickLose(deltaTime, hands); break;
            }
        }

        private bool CrankAlarm =>
            Stage == LevelStage.Play && !_runtime.Collector.IsSpinning &&
            (_runtime.Collector.Progress01 > 0f || _runtime.DisplayProgress > 0.01f);

        // ---- intro -------------------------------------------------------------------------------

        private void TickIntro(float deltaTime)
        {
            // «Сцена без мыслей, все детали пульсируют разом». The loop is not running yet, so the
            // pulse is driven straight off the view.
            _view.TickPulse(deltaTime, _runtime.Collected, -1);
            if (_stageSeconds < IntroSeconds) return;

            EnterStage(LevelStage.Play);
            if (_teaches) BeginCrankBeat();
        }

        // ---- play --------------------------------------------------------------------------------

        private void TickPlay(float deltaTime, in Hands hands)
        {
            bool spawningAllowed = _rules.SpawningAllowed && TutorialAllowsSpawning;

            // Beat 2 blocks collection outright (SCREENS «Обучение», п. 2): the bear is ON the next
            // detail and nothing is collected until it is shaken off. Covering alone would only produce
            // that under the fixed order — with the shipped gaze the player would look past the bear.
            _runtime.CollectionSuspended = CollectionBlockedByTutorial;

            // The gaze belongs to beat 3 and after (walkthrough: «Оглядись — наклони стик»).
            _runtime.GazeInPlay = !_teaches || Beat >= TutorialBeat.Gaze;

            _runtime.Tick(deltaTime, hands.Stick, hands.Hits, hands.CrankSpeed, spawningAllowed);

            // The tutorial holds the CLOCK, not the rules: victory and defeat keep being watched for
            // while it teaches. A zero-length tick would have switched both off with the timer.
            bool running = TimerRunning;
            _view.SetTimerRunning(running);
            _rules.ClockPaused = !running;
            _rules.Tick(deltaTime, _runtime.Field.OverlapPercent);
            _view.SetTimer(_rules.TimeLeft01, _rules.TimeLeft);

            if (_teaches)
            {
                TickTutorial();
                AimTheHint();
            }

            if (_rules.Outcome == LevelOutcome.Win) BeginWin();
            else if (_rules.Outcome == LevelOutcome.Lose) BeginLose();
        }

        /// <summary>Beats 1–2 hold the waves back; from beat 3 the level breathes on its own.</summary>
        private bool TutorialAllowsSpawning => !_teaches || Beat >= TutorialBeat.Gaze;

        /// <summary>
        /// True while the teaching thought must be beaten off before anything else can be collected.
        /// Public because the suite has to be able to name the state it is testing.
        /// </summary>
        public bool CollectionBlockedByTutorial =>
            _teaches && Beat == TutorialBeat.Shake && StillOnScreen(_tutorialThought);

        private void OnDetailCollected(int index)
        {
            _rules.OnDetailCollected();
            if (_teaches && Beat == TutorialBeat.Crank && index == 0) BeginShakeBeat();
        }

        // ---- tutorial ----------------------------------------------------------------------------

        private void BeginCrankBeat()
        {
            Beat = TutorialBeat.Crank;

            // Frame 4 opens with the thread to the dandelion already drawn: the first thing asked of
            // the player is to turn the handle, and nothing else.
            _runtime.ForceNotice(0);
            ShowBeatHint(GameTexts.TutorialCrank, HintTone.Crank, _level.Details[0].Home, -210f);
        }

        private void BeginShakeBeat()
        {
            Beat = TutorialBeat.Shake;

            // The plush bear lands ON the next detail, so collection really is blocked until it is
            // shaken off (SCREENS: «сбор заблокирован её появлением поверх следующей детали»).
            Vector2 over = _level.Details[1].Home;
            _tutorialThought = _runtime.Field.SpawnAt(ThoughtStrength.Weak, _level.ThoughtSprites[0], over);
            ShowBeatHint(GameTexts.TutorialShake, HintTone.Shake, over, -230f);
        }

        private void BeginGazeBeat()
        {
            Beat = TutorialBeat.Gaze;
            _tutorialThought = null;

            // The timer starts here — «мысль отбита → таймер запускается» — and the last card explains
            // the hand that has not been used yet.
            _stageSeconds = 0f;
            ShowBeatHint(GameTexts.TutorialGaze, HintTone.Gaze, _runtime.Gaze.Position, -240f);
        }

        private void TickTutorial()
        {
            switch (Beat)
            {
                case TutorialBeat.Shake:
                    if (StillOnScreen(_tutorialThought)) return;
                    BeginGazeBeat();
                    return;

                case TutorialBeat.Gaze:
                    // Gone the moment the player notices something on their own, or after a while —
                    // a card that never leaves stops being read.
                    if (_runtime.NoticedIndex < 0 && _stageSeconds < GazeHintSeconds) return;
                    Beat = TutorialBeat.Done;
                    _view.HideHint();
                    return;
            }
        }

        /// <summary>
        /// Keep the teaching arrow on the thing that is being taught, which on beat 1 is a detail in
        /// motion: it travels to the vessel as the handle turns and flies back home when it slips.
        /// The gate's frame 18 caught the arrow pointing at an empty road while the dandelion was
        /// half-way to the briefcase — the card is a placement, the arrow is a live aim.
        /// </summary>
        private void AimTheHint()
        {
            if (Beat != TutorialBeat.Crank || !_view.Hint.IsShown) return;

            _view.Hint.PointAt(DetailPosition(0));
        }

        /// <summary>Where a detail is right now: home, or somewhere on its way into the vessel.</summary>
        private Vector2 DetailPosition(int index)
        {
            Vector2 home = _level.Details[index].Home;
            if (_runtime.NoticedIndex != index) return home;
            return Vector2.Lerp(home, _level.VesselCentre, Mathf.Clamp01(_runtime.DisplayProgress));
        }

        private bool StillOnScreen(Thought thought)
        {
            if (thought == null) return false;
            System.Collections.Generic.IReadOnlyList<Thought> live = _runtime.Field.Thoughts;
            for (int i = 0; i < live.Count; i++)
                if (ReferenceEquals(live[i], thought)) return true;
            return false;
        }

        /// <summary>
        /// Place a teaching card: beside the thing it teaches about, never on it, never on the HUD.
        /// </summary>
        private void ShowBeatHint(string text, HintTone tone, Vector2 target, float verticalOffset)
        {
            Vector2 spot = HintSpot(text, target, verticalOffset);

            // …and the card may not sit on the HUD's slot row either. Beat 2's card («Тряси джойстик!»,
            // over the curtains at y 390) landed at y 160 and covered slots 7–10 — the player was being
            // taught with the level's own readout hidden. The other side of the target is free, so the
            // card goes there and the arrow still ends at the bear.
            Rect slots = LevelCatalog.SlotsRectOf(_level);
            if (HintCard.RectFor(text, spot).Overlaps(slots))
            {
                Vector2 flipped = HintSpot(text, target, -verticalOffset);
                if (!HintCard.RectFor(text, flipped).Overlaps(slots)) spot = flipped;
            }

            _view.ShowHint(text, tone, spot, target);
        }

        /// <summary>
        /// Put the card beside its target and inside the frame — the offset is applied and then clamped
        /// rather than trusted, because a detail near an edge would push the card off screen.
        /// </summary>
        private static Vector2 HintSpot(string text, Vector2 target, float verticalOffset)
        {
            var spot = new Vector2(
                Mathf.Clamp(target.x, 320f, 1600f),
                Mathf.Clamp(target.y + verticalOffset, 110f, 970f));

            // If clamping pulled the card back onto the target, push it to the other side instead.
            if (Mathf.Abs(spot.y - target.y) < 130f)
                spot.y = target.y < 540f ? target.y + 230f : target.y - 230f;

            return spot;
        }

        // ---- victory -----------------------------------------------------------------------------

        private void BeginWin()
        {
            EnterStage(LevelStage.Win);
            _view.SetHudVisible(false);
            _view.HideHint();
        }

        private void TickWin(float deltaTime)
        {
            _view.SyncThoughts(_runtime.Field.Thoughts, deltaTime);
            _view.TickPulse(deltaTime, _runtime.Collected, -1);

            if (_stageSeconds < DissolveSeconds)
            {
                _view.SetThoughtsAlpha(1f - _stageSeconds / DissolveSeconds);
                return;
            }

            _view.SetThoughtsAlpha(0f);
            if (_runtime.Field.Thoughts.Count > 0) _runtime.Field.Clear();

            if (_stageSeconds < DissolveSeconds + SilenceSeconds) return;   // секунда тишины

            _view.StageVictory();
            // Light ink: «Собрано: …» stands on the location itself, and all three plates are dark.
            _view.ShowMessage(GameTexts.Collected(_level.Title), "", false, 800f,
                GameTexts.CollectedSize, GameTexts.FinaleSmallSize, true);

            if (_stageSeconds >= DissolveSeconds + SilenceSeconds + WinHoldSeconds)
                Flow.LevelWon(_levelIndex);
        }

        // ---- defeat ------------------------------------------------------------------------------

        private void BeginLose()
        {
            EnterStage(LevelStage.Lose);
            _view.SetHudVisible(false);
            _view.HideHint();
            _view.SetDesaturated(true);
            // …and the wallpaper below goes OVER the vessel: «экран целиком закрыт мыслями» is not
            // honoured by a frame with the briefcase still bright in the middle of it.
            _view.StageDefeat();

            // The defeat screen IS thoughts (SCREENS S5: «экран целиком закрыт мыслями»), and the
            // retry is wiping them off with the crank — so a loss on the clock, where the screen may
            // be nearly clear, still has to close over before it can be cleared. Without this the
            // meditative retry would have nothing to act on and the screen would just sit there.
            _runtime.Field.CoverScreen(DefeatFillCap);
            _defeatThoughts = _runtime.Field.Thoughts.Count;
            _wipeDegrees = 0f;

            _view.ShowMessage(GameTexts.DefeatBig, GameTexts.DefeatSmall, true, 480f,
                GameTexts.DefeatBigSize, GameTexts.DefeatSmallSize);
        }

        private void TickLose(float deltaTime, in Hands hands)
        {
            _view.SyncThoughts(_runtime.Field.Thoughts, deltaTime);

            // «Каждый оборот динамо стирает ~10 % мыслей с экрана» — a share of what covered the
            // screen when it was lost, so a full screen takes about ten turns whatever level it is.
            _wipeDegrees += Mathf.Abs(hands.CrankDelta);
            int perTurn = Mathf.Max(1, Mathf.RoundToInt(_defeatThoughts * WipeSharePerTurn));
            while (_wipeDegrees >= DegreesPerTurn)
            {
                _wipeDegrees -= DegreesPerTurn;
                for (int i = 0; i < perTurn; i++)
                    if (!_runtime.Field.RemoveOldest()) break;
            }

            bool cleared = _runtime.Field.Thoughts.Count == 0;
            bool timedOut = TuningConfig.AutoRetry && _stageSeconds >= AutoRetrySeconds;
            if (cleared || timedOut) Flow.LevelFailed(_levelIndex);
        }

        // ---- readout -----------------------------------------------------------------------------

        public override string Readout()
        {
            string stage;
            switch (Stage)
            {
                case LevelStage.Intro: stage = "обзор"; break;
                case LevelStage.Win: stage = "ПОБЕДА"; break;
                case LevelStage.Lose: stage = "ПОРАЖЕНИЕ — " + _rules.LoseReason; break;
                // «Пик хаоса» has its OWN threshold: read against the loss one it could never be
                // reported, because at the loss threshold the level is already over.
                default: stage = _runtime.Field.OverlapPercent >= TuningConfig.PeakOverlapPercent
                    ? "пик хаоса"
                    : "игра"; break;
            }

            return "уровень " + _level.Number + " · " + _level.Title + "   [" + stage + "]\n" +
                   (_teaches ? "обучение: " + Beat + "\n" : "") +
                   _runtime.Readout() +
                   "таймер: " + _rules.TimeLeft.ToString("0") + " с" +
                   (TimerRunning ? "" : " (стоит)") + "\n" +
                   "передышка: " + _rules.BreatherLeft.ToString("0.0") + " с";
        }
    }
}
