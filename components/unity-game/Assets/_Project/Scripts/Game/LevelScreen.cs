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
        /// <summary>S4: dissolve → silence → the vessel lifts with the haul → the drawn screen.</summary>
        Win = 2,
        /// <summary>S5: the drawn defeat screen, and the crank wipes it away.</summary>
        Lose = 3
    }

    /// <summary>
    /// Scripted beats of the level-1 tutorial (SCREENS §Обучение, walkthrough Э6).
    ///
    /// The order changed with the drop of 2026-08-07 and it changed for a reason: the drop ships three
    /// drawn buttons — НАВОДИ, КРУТИ РУЧКУ, ТАЩИ — and they name the hands in the order the player
    /// actually uses them. The old first beat noticed the first detail FOR the player and asked only
    /// for the handle, so the gaze (the shipped way of choosing a detail) was taught last, after the
    /// tutorial was over. Now it is taught first, with the button that says so.
    /// </summary>
    public enum TutorialBeat
    {
        /// <summary>«НАВОДИ» — aim the gaze at a detail. Nothing else happens on screen.</summary>
        Aim = 0,
        /// <summary>«КРУТИ РУЧКУ» at the dynamo, «ТАЩИ» beside the detail on its thread.</summary>
        Crank = 1,
        /// <summary>The first thought lands on the next detail; an arrow at the sensors, no words.</summary>
        Swipe = 2,
        /// <summary>Taught. Ordinary play, and the clock starts.</summary>
        Done = 3
    }

    /// <summary>
    /// S3 — a whole level: the art composition, the two-handed loop, the timer, both outcome screens,
    /// the victory's reward beat, and (on level 1 only) the three teaching beats.
    ///
    /// The rules underneath are the ones the preview stand already proved: <see cref="CollectionRuntime"/>,
    /// <see cref="LevelRules"/>, <see cref="ThoughtField"/>. What this screen adds is the level's own
    /// composition and the state machine around the loop.
    /// </summary>
    public sealed class LevelScreen : GameScreen
    {
        private const float IntroSeconds = 2f;      // SCREENS S3 state 1
        private const float DissolveSeconds = 0.5f; // мысли растворяются
        private const float SilenceSeconds = 1f;    // секунда чистой сцены

        /// <summary>
        /// How long the reward beat holds: «сосуд плавно в центр… внутри его силуэта — все детали
        /// уровня (1.5 с)» (SCREENS S4). The founder kept this beat on top of the drawn screen because
        /// it is the only moment in the whole run where the player sees what they collected.
        /// </summary>
        private const float TableauSeconds = 1.5f;

        /// <summary>…and then the drawn «Отлично!» screen, before the next level's card.</summary>
        private const float CompleteScreenSeconds = 2f;

        private const float AutoRetrySeconds = 4f;  // S5 [toggle]
        private const float DegreesPerTurn = 360f;

        /// <summary>Each turn of the crank wipes ~10 % of the defeat screen (SCREENS S5).</summary>
        public const float WipeSharePerTurn = 0.1f;

        /// <summary>
        /// Where the arrow of the отгон beat POINTS, and it is a fixed place: the middle of the frame's
        /// bottom edge, because that is where the panel with the two height sensors physically is
        /// (founder, 2026-08-07: «отгоняем на датчики движения»). There is no drawn «ТРЯСИ» in the drop,
        /// so this beat is an arrow and a stroke and nothing else.
        ///
        /// Fixed, and that is the fix of 2026-08-08. The tip used to hang off the thought — 240 px below
        /// whatever the cat happened to be sitting on — so it ended in empty sky and its X moved with the
        /// blob: an arrow that points at a different place every time it is drawn is not naming
        /// anything. Now the tail moves and the tip does not, which is also what a gesture towards a
        /// piece of furniture looks like.
        /// </summary>
        public static readonly Vector2 SensorsCue = new Vector2(960f, 1046f);

        /// <summary>
        /// The stroke of that arrow — the gesture, drawn. It swings ALONG its own axis, not across it:
        /// a height sensor is answered by a hand passing up and down over it, so the stroke travels up
        /// and down the way the hand is meant to. (Until 2026-08-07 the swing was sideways, because the
        /// gesture then was a stick being shaken from side to side.) Since 2026-08-08 it is the TAIL
        /// that swings, the tip staying on the panel — the same 60 px, the same beat.
        /// </summary>
        private const float SwipeWobblePx = 60f;
        private const float SwipeWobbleHz = 2.5f;

        /// <summary>
        /// Air between everything the thought PAINTS and the tail of the arrow, design px.
        ///
        /// Measured off the whole drawing, not off the blob's rectangle: the pips hang below the blob on
        /// their own discs, so a gap measured from the rectangle left the arrow growing straight out of
        /// them (frame 05 at the design gate of 2026-08-08 had no gap at all).
        /// </summary>
        private const float SwipeArrowGap = 26f;

        /// <summary>The shortest the stroke gets at the top of its swing — still an arrow, not a dash.</summary>
        private const float SwipeArrowMinLength = 100f;

        private readonly LevelDefinition _level;
        private readonly int _levelIndex;
        private readonly LevelView _view;
        private readonly CollectionRuntime _runtime;
        private readonly LevelRules _rules = new LevelRules();
        private readonly DetailSweep _sweep = new DetailSweep();
        private readonly bool _teaches;

        private float _stageSeconds;
        private float _wipeTurns;
        private bool _wasSlipping;
        private bool _completeScreenUp;
        private Thought _tutorialThought;
        private int _swipeBeatDetail = -1;
        private float _dragHintSide = StartingDragSide;

        /// <summary>Everything a teaching plate may not cover on THIS level — the same list all beat.</summary>
        private readonly Rect[] _hintObstacles;
        private readonly System.Collections.Generic.List<Rect> _blockedForDragHint =
            new System.Collections.Generic.List<Rect>();

        /// <summary>…and the same list for the отгон card, built once per beat.</summary>
        private readonly System.Collections.Generic.List<Rect> _blockedForSwipeCard =
            new System.Collections.Generic.List<Rect>();

        public LevelScreen(GameFlow flow, int levelIndex) : base(flow, "LevelScreen")
        {
            _levelIndex = levelIndex;
            _level = LevelCatalog.At(levelIndex);

            // «Обучение не повторяется после поражения» (founder, 2026-08-07). Level 1 teaches on the
            // FIRST attempt only; a player who has just watched three scripted beats and then lost is
            // told the same three things again before being allowed to try, which is the shape of a
            // punishment rather than of a lesson. The flow is what remembers — a screen cannot, it is
            // built fresh for every attempt.
            _teaches = levelIndex == 0 && !flow.TutorialAlreadyGiven;

            // Marked on ENTRY, not when the beats finish: a player who lost while being taught has
            // still been taught, and «обучение не повторяется после поражения» has to hold for that
            // attempt too. The flag is a property of the RUN — GameFlow.StartRun clears it, so the
            // next person at the cabinet gets the tutorial the first player got.
            if (levelIndex == 0) flow.NoteTutorialGiven();

            TuningConfig.ActiveLevelIndex = levelIndex;
            TuningConfig.ApplyLevel(levelIndex);

            _view = new LevelView(Root, _level);
            _sweep.LevelIndex = levelIndex;
            _hintObstacles = LevelCatalog.HintObstaclesOf(_level);

            var homes = new Vector2[_level.DetailCount];
            var names = new string[_level.DetailCount];
            var threadOffsets = new Vector2[_level.DetailCount];
            for (int i = 0; i < _level.DetailCount; i++)
            {
                homes[i] = _level.Details[i].Home;
                names[i] = _level.Details[i].Name;
                threadOffsets[i] = LevelCatalog.AnchorOffsetOf(_level.Details[i]);
            }

            _runtime = new CollectionRuntime(_view, homes, _level.VesselCentre, names);

            // The thread is tied to the ink, not to the middle of the rectangle (LevelCatalog.AnchorOf).
            _runtime.SetThreadOffsets(threadOffsets);

            // The art drop puts real details in the foreground strip — the seashell at y 1021, the fish
            // at 991, the mouse at 1029. The gaze has to be able to go down there.
            _runtime.Gaze.MaxY = DesignStage.DesignHeight;

            _runtime.Field.Labels = _level.ThoughtSprites;
            _runtime.Field.ArtFitter = ArtLibrary.FitThought;
            _runtime.DetailCollected += OnDetailCollected;

            Restart();
        }

        public LevelStage Stage { get; private set; }

        public TutorialBeat Beat { get; private set; }

        public LevelView View => _view;

        public LevelRules Rules => _rules;

        public CollectionRuntime Runtime => _runtime;

        public LevelDefinition Level => _level;

        /// <summary>The light band's clock — the panel tunes it, the suite reads it.</summary>
        public DetailSweep Sweep => _sweep;

        /// <summary>The detail the teaching thought sits on, or -1 — named so the suite can check it.</summary>
        public int SwipeBeatDetailIndex => _swipeBeatDetail;

        /// <summary>Is this attempt the one that teaches? Level 1, first time through.</summary>
        public bool Teaches => _teaches;

        /// <summary>True while the reward beat — the vessel in the centre with its haul — is on screen.</summary>
        public bool VictoryPresented =>
            Stage == LevelStage.Win && _stageSeconds >= DissolveSeconds + SilenceSeconds;

        /// <summary>True once the drawn «Отлично!» screen has come over the reward beat.</summary>
        public bool CompleteScreenShown => _completeScreenUp;

        /// <summary>
        /// How much of the drawn defeat screen is still up, 1 → 0. This is the retry's own progress
        /// bar: every turn of the handle takes <see cref="WipeSharePerTurn"/> off it, and at zero the
        /// level restarts.
        /// </summary>
        public float DefeatCoverLeft01 => _view.OutcomeAlpha;

        // ---- lifecycle ---------------------------------------------------------------------------

        private void Restart()
        {
            _runtime.Restart();
            _rules.Restart(_level.DetailCount);
            _stageSeconds = 0f;
            _wipeTurns = 0f;
            _wasSlipping = false;
            _completeScreenUp = false;
            _tutorialThought = null;
            _swipeBeatDetail = -1;
            _dragHintSide = StartingDragSide;
            Beat = _teaches ? TutorialBeat.Aim : TutorialBeat.Done;
            _sweep.Reset();

            EnterStage(LevelStage.Intro);

            _view.HideHint();
            _view.HideSecondHint();
            _view.HideSwipeCard();
            _view.HideOutcomeScreen();
            _view.SetHudVisible(true);
            _view.SetDesaturated(false);
            _view.ClearDefeatStaging();
            _view.SetThoughtsAlpha(1f);
            _view.ApplyNeonOutline();

            // «Сразу в игру»: a repeat of level 1 skips the 2 s обзор along with the beats. The обзор
            // is the tutorial's own opening — «сцена без мыслей, все детали пульсируют разом» is what
            // introduces a level you have not seen, and the player who is retrying has seen it.
            if (SkipsTheIntro) EnterStage(LevelStage.Play);
        }

        /// <summary>
        /// True when this attempt goes straight to <see cref="LevelStage.Play"/>: level 1, taught
        /// already, i.e. the restart after a defeat.
        /// </summary>
        public bool SkipsTheIntro => _levelIndex == 0 && !_teaches;

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

        // ---- звук (MECHANICS §8) --------------------------------------------------------------------

        /// <summary>
        /// «Медитация» is the reward for a collection that is GOING WELL — noticed, on its thread, and
        /// the handle above the threshold (§8). A slipping detail is not going well, which is exactly
        /// the contrast the layer exists to make audible.
        /// </summary>
        private bool CollectingWell =>
            Stage == LevelStage.Play && _runtime.NoticedIndex >= 0 &&
            _runtime.Collector.IsSpinning && !_runtime.Slipping;

        public override AudioScene Audio =>
            new AudioScene(Stage == LevelStage.Play || Stage == LevelStage.Intro, _levelIndex, false,
                CollectingWell, _runtime.Field.Thoughts.Count, _runtime.Field.OverlapPercent);

        // ---- intro -------------------------------------------------------------------------------

        private void TickIntro(float deltaTime)
        {
            // «Сцена без мыслей, все детали пульсируют разом». The loop is not running yet, so the
            // pulse is driven straight off the view.
            _view.TickPulse(deltaTime, _runtime.Collected, -1);
            TickSweep(deltaTime);
            if (_stageSeconds < IntroSeconds) return;

            EnterStage(LevelStage.Play);
            if (_teaches) BeginAimBeat();
        }

        // ---- play --------------------------------------------------------------------------------

        private void TickPlay(float deltaTime, in Hands hands)
        {
            bool spawningAllowed = _rules.SpawningAllowed && TutorialAllowsSpawning;

            // The отгон beat blocks collection outright (SCREENS §Обучение, п. 3): the cat is ON the
            // next detail and nothing is collected until it is beaten off. Covering alone would only
            // produce that under the fixed order — with the shipped gaze the player would look past it.
            _runtime.CollectionSuspended = CollectionBlockedByTutorial;

            // Unlike the old order, the gaze is in play from the FIRST beat: «наводи» is what beat 1
            // asks for, and the tutorial cannot ask for a hand it has switched off.
            _runtime.GazeInPlay = true;

            _runtime.Tick(deltaTime, hands.Stick, hands.Hits, hands.CrankSpeed, spawningAllowed);
            NoteSlipForTheAudio();

            _rules.Tick(deltaTime, _runtime.Field.OverlapPercent);

            TickSweep(deltaTime);
            _view.ApplyNeonOutline();

            if (_teaches)
            {
                TickTutorial();
                AimTheHints();
            }

            if (_rules.Outcome == LevelOutcome.Win) BeginWin();
            else if (_rules.Outcome == LevelOutcome.Lose) BeginLose();
        }

        /// <summary>The waves are held back while it teaches; from «Done» the level breathes on its own.</summary>
        private bool TutorialAllowsSpawning => !_teaches || Beat >= TutorialBeat.Done;

        /// <summary>
        /// True while the teaching thought must be beaten off before anything else can be collected.
        /// Public because the suite has to be able to name the state it is testing.
        /// </summary>
        public bool CollectionBlockedByTutorial =>
            _teaches && Beat == TutorialBeat.Swipe && StillOnScreen(_tutorialThought);

        private void OnDetailCollected(int index)
        {
            _rules.OnDetailCollected();
            Flow.Audio?.Mix.NoteDetailLanded();
            if (_teaches && Beat == TutorialBeat.Crank) BeginSwipeBeat(index);
        }

        /// <summary>A detail that slipped ends the meditation layer at once, tail and all (§8).</summary>
        private void NoteSlipForTheAudio()
        {
            bool slipping = _runtime.Slipping;
            if (slipping && !_wasSlipping) Flow.Audio?.Mix.NoteCollectionBroken();
            _wasSlipping = slipping;
        }

        // ---- луч-подсветка ---------------------------------------------------------------------

        private void TickSweep(float deltaTime)
        {
            // «Гаснет на пике хаоса» — a band of light and the peak's darkened edges are two pictures
            // arguing about one frame.
            bool peak = _runtime.Field.OverlapPercent >= TuningConfig.PeakOverlapPercent;
            _sweep.Tick(deltaTime, peak);

            int skip = TuningConfig.SweepOnlyUnnoticed ? _runtime.NoticedIndex : -1;
            _view.ApplySweep(_sweep.Active, _sweep.CentreX, TuningConfig.SweepWidthPx,
                _sweep.Strength, skip);
        }

        // ---- tutorial ----------------------------------------------------------------------------

        private void BeginAimBeat()
        {
            Beat = TutorialBeat.Aim;

            // «Кнопка НАВОДИ у первой детали — замечаем взглядом» (SCREENS §Обучение п.1). Nothing is
            // noticed for the player any more: the button names the hand and the arrow names the thing.
            ShowBeatHint(ArtScreens.ButtonAim, HintTone.Gaze, LevelCatalog.AnchorOf(_level.Details[0]));
        }

        private void BeginCrankBeat()
        {
            Beat = TutorialBeat.Crank;

            // Two buttons at once, as SCREENS п.2 asks: «КРУТИ РУЧКУ» at the hand that does it (the
            // dynamo indicator in the HUD) and «ТАЩИ» beside the detail that is now on its thread.
            ShowBeatHint(ArtScreens.ButtonCrank, HintTone.Crank, _level.CrankIndicatorCentre);

            _dragHintSide = StartingDragSide;
            Vector2 detail = DetailPosition(_runtime.NoticedIndex);
            _view.ShowSecondHint(ArtScreens.ButtonDrag, HintTone.Crank, DragHintSpot(detail), detail);
        }

        /// <summary>
        /// Where «ТАЩИ» stands right now: beside the detail it names, clear of the progress ring and of
        /// the thread (<see cref="HintPlacement.BesideTheThread"/>), and clear of the other button of
        /// the same beat — two plates of one beat overlapping is one plate.
        /// </summary>
        private Vector2 DragHintSpot(Vector2 detail)
        {
            // Rebuilt in place rather than allocated: this runs every frame the detail is moving.
            _blockedForDragHint.Clear();
            for (int i = 0; i < _hintObstacles.Length; i++) _blockedForDragHint.Add(_hintObstacles[i]);
            if (_view.Hint.IsShown) _blockedForDragHint.Add(_view.Hint.ButtonRect);

            // The whole SPRITE of the detail it names, where that sprite is right now — not the ink
            // around its centroid, and not the rectangle it left behind at home.
            //
            // «ТАЩИ» came down 1 px off the plane's contrail on frame 18 (design gate, 2026-08-07) and
            // the trail read as though it broke against the plate. The centroid is the whole reason:
            // the plane's ink centre is at 0.25 of its rectangle, so a plate placed at arm's length
            // from the centroid still stands inside the 484 px box, over the 600 px of trail behind
            // the fuselage. The trail is drawn art like any other, so it is an obstacle like any other.
            if (_runtime.NoticedIndex >= 0 && _runtime.NoticedIndex < _level.DetailCount)
            {
                ArtDetail spec = _level.Details[_runtime.NoticedIndex];
                Rect sprite = HintPlacement.Centred(detail - LevelCatalog.AnchorOffsetOf(spec), spec.Size);
                _blockedForDragHint.Add(HintPlacement.Inflate(sprite, HintPlacement.DraggedSpriteMargin));
            }

            return HintPlacement.BesideTheThread(
                ButtonHint.SizeOf(ArtLibrary.Get(ArtScreens.ButtonDrag)),
                detail, _level.VesselCentre, _view.RingRadiusOf(_runtime.NoticedIndex),
                _blockedForDragHint, ref _dragHintSide);
        }

        /// <summary>Which side of the thread «ТАЩИ» opens on before the search has an opinion.</summary>
        private const float StartingDragSide = 90f;


        private void BeginSwipeBeat(int justCollected)
        {
            Beat = TutorialBeat.Swipe;
            _view.HideSecondHint();

            // The cat lands ON the next detail, so collection really is blocked until it is beaten off
            // (SCREENS: «сбор заблокирован её появлением поверх следующей детали»).
            _swipeBeatDetail = FirstUncollectedThatFitsAThought(justCollected);
            Vector2 over = _swipeBeatDetail >= 0
                ? LevelCatalog.AnchorOf(_level.Details[_swipeBeatDetail])
                : new Vector2(960f, 420f);

            _tutorialThought = _runtime.Field.SpawnAt(ThoughtStrength.Weak, _level.ThoughtSprites[0], over);

            // An arrow at the sensors — and, since 2026-08-08, the words to go with it.
            AimTheSwipeArrow(0f);
            ShowSwipeCard();
        }

        /// <summary>
        /// Put «Маши над датчиком!» beside the thought that has to be beaten off — once, at the start
        /// of the beat, and not again.
        ///
        /// Once, and then only again if the picture moves out from under it: the stroke swings and the
        /// thought drifts, and a plate re-placed every frame against a search that breaks ties by
        /// nearest-clear-spot walks around the screen. So it is placed at the start of the beat and
        /// left alone until the stroke would actually cross it (see <see cref="AimTheHints"/>).
        ///
        /// The stroke is handed to the search as an obstacle for the same reason the details are — the
        /// beat is one arrow and one sentence, and a sentence lying across its own arrow is neither. As
        /// a chain of small squares along it, not as its bounding box: the stroke runs diagonally
        /// across most of the frame, and its box is most of the frame. On level 1 the teaching thought
        /// can sit on the gull in the top right corner, and with the box as an obstacle there was no
        /// clear spot anywhere — the search fell back to «on top of the target», i.e. the card landed
        /// on the gull it was supposed to avoid.
        /// </summary>
        private void ShowSwipeCard()
        {
            Vector2 thought = _tutorialThought != null
                ? _tutorialThought.Position
                : new Vector2(960f, 420f);

            _blockedForSwipeCard.Clear();
            for (int i = 0; i < _hintObstacles.Length; i++) _blockedForSwipeCard.Add(_hintObstacles[i]);
            if (_tutorialThought != null)
                _blockedForSwipeCard.Add(HintPlacement.Centred(thought, _tutorialThought.Size));
            AddSwipeArrowCorridor(_blockedForSwipeCard);

            Vector2 size = HintCard.SizeFor(GameTexts.SwipeHint);
            Vector2 spot = HintPlacement.Beside(size, thought, _blockedForSwipeCard);
            _view.ShowSwipeCard(GameTexts.SwipeHint, HintTone.Swipe, spot);
        }

        /// <summary>
        /// The stroke of the отгон, as squares laid along it from the panel cue up to everything the
        /// thought paints. Sampled closer together than the squares are wide, so the chain has no gaps
        /// for a plate to slip through.
        ///
        /// The swing is not added to it: the swing only ever SHORTENS the stroke (it slides the tail
        /// down its own axis), so the stroke at rest already contains every stroke the beat draws.
        /// </summary>
        private void AddSwipeArrowCorridor(System.Collections.Generic.List<Rect> into)
        {
            Vector2 tail = SwipeArrowAnchor();
            var square = new Vector2(SwipeCorridorWidth, SwipeCorridorWidth);

            for (int i = 0; i <= SwipeCorridorSamples; i++)
                into.Add(HintPlacement.Centred(
                    Vector2.Lerp(tail, SensorsCue, i / (float)SwipeCorridorSamples), square));
        }

        /// <summary>How wide the stroke's chain of squares is, design px — the arrow plus its bow.</summary>
        private const float SwipeCorridorWidth = 90f;

        /// <summary>…and how many of them, so the spacing stays under that width on a full-height stroke.</summary>
        private const int SwipeCorridorSamples = 16;

        /// <summary>
        /// The отгон beat's arrow: a stroke that leaves the thought and lands on the sensor panel at the
        /// bottom edge of the frame, swinging along its own length — the hand passing over the sensor.
        ///
        /// Two rewrites, and the second one is the point. It first ran from the MIDDLE of the thought to
        /// (960, 1046) — 1047 px of brick-red line through the drawn art, a barrier rather than a
        /// gesture. The answer then was to make it short: 240 px down from the blob and stop. That
        /// bought the wrong thing — the tip now ended in empty sky and, worse, its X came off whatever
        /// the cat was sitting on, so the one hint of this beat pointed somewhere different every run
        /// (design gate, 2026-08-08).
        ///
        /// So the two ends have different jobs now. The TIP is <see cref="SensorsCue"/> and never moves:
        /// it names a thing that exists in the room — the panel under the screen — and a name has to be
        /// the same word twice. The TAIL starts clear of everything the thought paints (pips included)
        /// and is what the swing moves, up and down its own axis, because a height sensor reads how high
        /// the hand is. Length is what is left between them, and the stroke stays off the art either
        /// way: it begins outside the drawing and travels away from it.
        /// </summary>
        /// <param name="slide">
        /// How far past the gap the tail has slid this frame, 0…<see cref="SwipeWobblePx"/>. One-sided
        /// on purpose: the swing may shorten the stroke, never lengthen it back INTO the drawing it was
        /// just measured clear of.
        /// </param>
        private void AimTheSwipeArrow(float slide)
        {
            Vector2 tip = SensorsCue;
            Vector2 anchor = SwipeArrowAnchor();

            Vector2 away = tip - anchor;
            float span = away.magnitude;
            if (span < 1f)
            {
                _view.ShowArrowHint(HintTone.Swipe, tip - new Vector2(0f, SwipeArrowMinLength), tip);
                return;
            }

            Vector2 axis = away / span;

            // The gap is air between the DRAWING and the tail, so on a slanted stroke it takes more than
            // its own length of travel to buy it — the floor on the divisor keeps a nearly horizontal
            // arrow (a thought already down by the panel) from backing off half the frame for it.
            float clearance = SwipeArrowGap / Mathf.Max(0.4f, axis.y);
            float back = Mathf.Clamp(span - clearance - Mathf.Max(0f, slide),
                SwipeArrowMinLength, Mathf.Max(SwipeArrowMinLength, span));

            _view.ShowArrowHint(HintTone.Swipe, tip - axis * back, tip);
        }

        /// <summary>
        /// The point on the thought the stroke has to clear: the middle of the lowest line it PAINTS.
        /// Falls back to a plain stroke above the panel once the thought has been beaten off.
        /// </summary>
        private Vector2 SwipeArrowAnchor()
        {
            if (_tutorialThought == null) return SensorsCue - new Vector2(0f, SwipeArrowMinLength * 2f);

            return new Vector2(
                _tutorialThought.Position.x,
                ArtThoughtView.DrawnBottomY(_tutorialThought.Position, _tutorialThought.Size));
        }

        /// <summary>
        /// Which detail the teaching thought sits on: the first one still to be collected that a weak
        /// blob can cover WHOLE. It used to be «Details[1]», which only worked because level 1 happened
        /// to be a level whose second detail stood in the middle of the frame — with the renumbering it
        /// would have put the cat half off the top of the sky.
        /// </summary>
        private int FirstUncollectedThatFitsAThought(int justCollected)
        {
            Vector2 blob = Thought.SizeOf(ThoughtStrength.Weak);
            int fallback = -1;

            for (int i = 0; i < _level.DetailCount; i++)
            {
                if (i == justCollected || _runtime.Collected[i]) continue;
                if (fallback < 0) fallback = i;

                // The anchor, because that is where the thought is actually put down.
                Vector2 home = LevelCatalog.AnchorOf(_level.Details[i]);
                bool fits = home.x >= blob.x * 0.5f && home.x <= DesignStage.DesignWidth - blob.x * 0.5f &&
                            home.y >= blob.y * 0.5f && home.y <= DesignStage.DesignHeight - blob.y * 0.5f;
                if (fits) return i;
            }

            return fallback;
        }

        private void FinishTeaching()
        {
            Beat = TutorialBeat.Done;
            _tutorialThought = null;
            _view.HideHint();
            _view.HideSecondHint();
            _view.HideSwipeCard();
        }

        private void TickTutorial()
        {
            switch (Beat)
            {
                case TutorialBeat.Aim:
                    // Whatever the player looked at is the detail the tutorial goes on with.
                    if (_runtime.NoticedIndex < 0) return;
                    BeginCrankBeat();
                    return;

                case TutorialBeat.Swipe:
                    if (StillOnScreen(_tutorialThought)) return;
                    // «Мысль отбита → таймер запускается, дальше обычный play» (SCREENS §Обучение п.4).
                    FinishTeaching();
                    return;
            }
        }

        /// <summary>
        /// Keep the teaching arrows on the things they teach about — one of which MOVES: the detail on
        /// its thread travels to the vessel as the handle turns and flies back home when it slips. The
        /// gate's own frame once caught an arrow pointing at an empty road while the detail was
        /// half-way to the vessel; the button is a placement, the arrow is a live aim.
        /// </summary>
        private void AimTheHints()
        {
            if (Beat == TutorialBeat.Swipe)
            {
                // The arrow swings the way the hand is meant to — the tail sliding down the stroke and
                // back, the tip staying on the sensor panel.
                AimTheSwipeArrow(
                    (1f + Mathf.Sin(Age * SwipeWobbleHz * Mathf.PI * 2f)) * 0.5f * SwipeWobblePx);

                // The thought DRIFTS towards the centre, and the stroke is anchored to it — so a card
                // that was clear of the arrow when the beat opened can be crossed by it half a minute
                // later. Re-placed only then: a plate that moves every frame is worse than one that
                // moves twice.
                if (_view.SwipeCard.IsShown &&
                    HintPlacement.SegmentHits(_view.Hint.Arrow.From, _view.Hint.Arrow.To,
                        _view.SwipeCard.CardRect))
                    ShowSwipeCard();
                return;
            }

            if (Beat != TutorialBeat.Crank || _runtime.NoticedIndex < 0) return;

            // The button FOLLOWS, it does not merely re-aim: it names the detail that is moving.
            Vector2 detail = DetailPosition(_runtime.NoticedIndex);
            _view.MoveSecondHint(DragHintSpot(detail), detail);
        }

        /// <summary>
        /// Where a detail is right now — its alpha centroid at home, or somewhere on its way into the
        /// vessel. The centroid rather than the rectangle's middle for the same reason the ring uses it
        /// (<see cref="LevelCatalog.AnchorOf"/>): the arrow of a hint must land on the thing, not on the
        /// empty half of its box.
        /// </summary>
        private Vector2 DetailPosition(int index)
        {
            if (index < 0 || index >= _level.DetailCount) return _level.VesselCentre;

            ArtDetail spec = _level.Details[index];
            Vector2 offset = LevelCatalog.AnchorOffsetOf(spec);
            if (_runtime.NoticedIndex != index) return spec.Home + offset;
            return Vector2.Lerp(spec.Home, _level.VesselCentre,
                Mathf.Clamp01(_runtime.DisplayProgress)) + offset;
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
        /// Place a teaching button: beside the thing it teaches about, and on top of NOTHING.
        ///
        /// It used to be an offset («230 px above») clamped back into the frame, and an offset is a
        /// guess about a picture it cannot see: on the набережная that put the 468 px «КРУТИ РУЧКУ»
        /// plate over the whole shark fin and the left edge of the bucket. Приёмка п.3 forbids a HUD
        /// widget covering a detail or a vessel, and the design gate extended it here — a hint that
        /// hides the level teaches with the level switched off. So the spot is SEARCHED against the
        /// catalogue's own rectangles (<see cref="LevelCatalog.HintObstaclesOf"/>).
        /// </summary>
        private void ShowBeatHint(string buttonKey, HintTone tone, Vector2 target)
        {
            Vector2 spot = HintPlacement.Beside(
                ButtonHint.SizeOf(ArtLibrary.Get(buttonKey)), target,
                _hintObstacles, LevelCatalog.ArtRectsOf(_level));

            _view.ShowHint(buttonKey, tone, spot, target);
        }

        // ---- victory -----------------------------------------------------------------------------

        private void BeginWin()
        {
            EnterStage(LevelStage.Win);
            _view.SetHudVisible(false);
            _view.HideHint();
            _view.HideSecondHint();
            _view.ApplySweep(false, 0f, 0f, 0f, -1);
            _view.ApplyNeonOutline(false);
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

            // The reward beat: the vessel in the centre with everything that was collected in it.
            _view.StageVictory();

            if (_stageSeconds < DissolveSeconds + SilenceSeconds + TableauSeconds) return;

            // …and then the drawn screen the designer sent, over the top of it.
            if (!_completeScreenUp)
            {
                _completeScreenUp = true;
                _view.ShowOutcomeScreen(ArtScreens.LevelComplete);
            }

            if (_stageSeconds >= DissolveSeconds + SilenceSeconds + TableauSeconds + CompleteScreenSeconds)
                Flow.LevelWon(_levelIndex);
        }

        // ---- defeat ------------------------------------------------------------------------------

        private void BeginLose()
        {
            EnterStage(LevelStage.Lose);
            _view.SetHudVisible(false);
            _view.HideHint();
            _view.HideSecondHint();
            _view.ApplySweep(false, 0f, 0f, 0f, -1);
            _view.ApplyNeonOutline(false);

            // The wallpaper of thoughts used to BE the defeat screen, and the level had to be buried
            // under it before the crank had anything to wipe. The designer has drawn that screen now
            // («Мысли захватили тебя, ты не заметил жизнь вокруг»), so the picture is the picture and
            // the field is left exactly as the player lost it — which is what shows through as the
            // handle wipes the screen away.
            _view.StageDefeat();
            // …with a plate under its copy: the picture is what dissolves, and half-dissolved it puts
            // the level's own marker hatching straight through the words «Мысли захватили тебя…»
            // (design skeptic, 2026-08-08 — frame Game12).
            _view.ShowOutcomeScreen(ArtScreens.GameOver, withTextPlate: true);
            _view.OutcomeAlpha = 1f;
            _wipeTurns = 0f;
        }

        private void TickLose(float deltaTime, in Hands hands)
        {
            _view.SyncThoughts(_runtime.Field.Thoughts, deltaTime);

            // «Каждый оборот динамо стирает ~10 % штриховки с экрана» — so about ten turns clear it,
            // and a partial turn clears its share: the screen dissolves under the hand rather than in
            // steps, which is the difference between a meditative retry and a ratchet.
            _wipeTurns += Mathf.Abs(hands.CrankDelta) / DegreesPerTurn;
            _view.OutcomeAlpha = 1f - _wipeTurns * WipeSharePerTurn;

            bool cleared = _view.OutcomeAlpha <= 0f;
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
                   "перекрытие: " + _runtime.Field.OverlapPercent.ToString("0") + " % / " +
                   TuningConfig.LossOverlapPercent.ToString("0") + " %\n" +
                   "передышка: " + _rules.BreatherLeft.ToString("0.0") + " с\n" +
                   "луч: " + (_sweep.Active ? "идёт" : "ждёт") +
                   ", период " + _sweep.PeriodOfThisLevel.ToString("0.0") + " с";
        }
    }
}
