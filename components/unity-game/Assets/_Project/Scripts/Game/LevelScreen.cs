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
    /// The order changed with the drop of 2026-08-07 and it changed for a reason: it is the order the
    /// player's hands actually come into play, and the old first beat noticed the first detail FOR the
    /// player and asked only for the крутилка — so the gaze, the shipped way of choosing a detail, was
    /// taught last, after the tutorial was over. Now it is taught first.
    ///
    /// Each beat is ONE dark plate with one sentence on it, and since 2026-09-22 that is all a beat
    /// draws (founder: «убрать стрелки все с экранов обучений… оставить только плашки с нашим
    /// текстом»). The drawn caps buttons that used to name the hand — НАВОДИ · КРУТИ РУЧКУ · ТАЩИ —
    /// and every turquoise arrow went with that sentence; see <c>LevelView.BeatAnchor</c>.
    /// </summary>
    public enum TutorialBeat
    {
        /// <summary>«Наводи джойстиком на объект» — aim the gaze. Nothing else happens on screen.</summary>
        Aim = 0,
        /// <summary>«Замечай детали вокруг. Крути крутилку и тащи объект» — one sentence, both hands.</summary>
        Crank = 1,
        /// <summary>The first thought lands on the next detail; the sentence names the sensors.</summary>
        Swipe = 2,
        /// <summary>
        /// «Заметь все объекты, перетащи их в ведёрко и не дай мыслям помешать тебе» — the goal,
        /// written over a level that is already being played (founder, 2026-09-22).
        ///
        /// A beat that blocks NOTHING and scripts nothing: the three verbs have been taught, the
        /// waves are running, and this is the sentence that says what they are for. It leaves on its
        /// own clock (<see cref="WholeBeatSeconds"/>) rather than on an action, because there is no
        /// one action that would mean the player has understood it.
        /// </summary>
        Whole = 3,
        /// <summary>Taught. Ordinary play.</summary>
        Done = 4
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

        /// <summary>
        /// …and then the drawn «Отлично!» screen, before the next level's card — «перебивка в конце
        /// уровня». A [tune] since the founder's playtest of 2026-09-22 («держать дольше»), where 2 s
        /// turned out to be less time than it takes to read what the render says.
        /// </summary>
        private static float CompleteScreenSeconds =>
            Mathf.Max(0.2f, TuningConfig.LevelCompleteSeconds);

        /// <summary>
        /// How long the fourth beat's sentence stays up. Not a [tune]: it is the same «read one line»
        /// budget as the other three beats, which have never had a clock either — they leave when the
        /// player does the thing. This one has nothing to wait for, so it waits for a reading.
        /// </summary>
        public const float WholeBeatSeconds = 6f;

        private const float AutoRetrySeconds = 4f;  // S5 [toggle]
        private const float DegreesPerTurn = 360f;

        /// <summary>Each turn of the crank wipes ~10 % of the defeat screen (SCREENS S5).</summary>
        public const float WipeSharePerTurn = 0.1f;

        /// <summary>
        /// Where the отгон beat is ABOUT: the middle of the frame's bottom edge, because that is where
        /// the panel with the two height sensors physically is (founder, 2026-08-07: «отгоняем на
        /// датчики движения»).
        ///
        /// It was the tip of the beat's arrow — a stroke that left the thought and landed here, swinging
        /// up and down its own axis the way the hand is meant to. The arrow is off the teaching screens
        /// (founder, 2026-09-22); the PLACE it named is not, because the sensors are still where the
        /// player's hand has to go, so this is now the mount the beat's animation stands on
        /// (<c>LevelView.BeatAnchor</c>).
        ///
        /// Fixed, and that has been the point since 2026-08-08: the arrow's tip used to hang 240 px
        /// under whatever the cat happened to be sitting on, so the one hint of this beat pointed
        /// somewhere different every run. A name has to be the same word twice.
        /// </summary>
        public static readonly Vector2 SensorsCue = new Vector2(960f, 1046f);

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
        private float _wholeBeatSeconds;

        /// <summary>Everything a teaching plate may not cover on THIS level — the same list all beat.</summary>
        private readonly Rect[] _hintObstacles;

        /// <summary>…and the working list a beat's plate is placed against, built once per beat.</summary>
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
            var hitShapes = new Rect[_level.DetailCount];
            for (int i = 0; i < _level.DetailCount; i++)
            {
                homes[i] = _level.Details[i].Home;
                names[i] = _level.Details[i].Name;
                threadOffsets[i] = LevelCatalog.AnchorOffsetOf(_level.Details[i]);
                hitShapes[i] = LevelCatalog.RectOf(_level.Details[i]);
            }

            _runtime = new CollectionRuntime(_view, homes, _level.VesselCentre, names);

            // The thread is tied to the ink, not to the middle of the rectangle (LevelCatalog.AnchorOf).
            _runtime.SetThreadOffsets(threadOffsets);

            // …and the aim catches the detail anywhere on it, not only near that ink. The rectangle is
            // the one LevelView draws the sprite in — `Ui.Place(image, Home, Size)` — so the area the
            // player can hit and the area they can see are the same rectangle by construction.
            _runtime.SetHitShapes(hitShapes);

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
            _wholeBeatSeconds = 0f;
            Beat = _teaches ? TutorialBeat.Aim : TutorialBeat.Done;
            _sweep.Reset();

            EnterStage(LevelStage.Intro);

            _view.HideBeatPlate();
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
                if (Beat == TutorialBeat.Whole) _wholeBeatSeconds += deltaTime;
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

            // «Наводи джойстиком на объект», beside the first detail. The drawn НАВОДИ button and its
            // arrow stood here until 2026-09-22 — see LevelView.BeatAnchor for why they went and what
            // is left in their place.
            Vector2 first = LevelCatalog.AnchorOf(_level.Details[0]);
            _view.MoveBeatAnchor(first);
            ShowBeatSentence(GameTexts.BeatAim, HintTone.Gaze, first);
        }

        private void BeginCrankBeat()
        {
            Beat = TutorialBeat.Crank;

            // One sentence for both hands, standing beside the detail that is now on its thread.
            //
            // Two drawn buttons stood here — «КРУТИ РУЧКУ» at the midpoint of the thread and «ТАЩИ»
            // walking along beside the travelling detail, each with its own arrow. Both are off the
            // teaching screens (founder, 2026-09-22). The mount stays on the thread's midpoint, which
            // is where the crank's own animation belongs: the крутилка is a thing in the room and the
            // game cannot draw it, but it can draw the line the крутилка pulls along.
            Vector2 detail = DetailPosition(_runtime.NoticedIndex);
            _view.MoveBeatAnchor(ThreadMidpoint(detail));

            // Placed once and never moved — so what it has to be clear of is not where the detail IS
            // but everywhere it will BE for the length of the beat (see AddDetailTrack).
            ShowBeatSentence(GameTexts.BeatCollect, HintTone.Crank, detail, _runtime.NoticedIndex);
        }

        /// <summary>
        /// Halfway along the thread from the detail to the vessel, nudged towards the vessel — the
        /// mount of the сбор beat, and what «КРУТИ РУЧКУ» pointed at while it existed.
        /// </summary>
        private Vector2 ThreadMidpoint(Vector2 detail) =>
            Vector2.Lerp(detail, _level.VesselCentre, 0.6f);

        private void BeginSwipeBeat(int justCollected)
        {
            Beat = TutorialBeat.Swipe;

            // The cat lands ON the next detail, so collection really is blocked until it is beaten off
            // (SCREENS: «сбор заблокирован её появлением поверх следующей детали»).
            _swipeBeatDetail = FirstUncollectedThatFitsAThought(justCollected);
            Vector2 over = _swipeBeatDetail >= 0
                ? LevelCatalog.AnchorOf(_level.Details[_swipeBeatDetail])
                : new Vector2(960f, 420f);

            _tutorialThought = _runtime.Field.SpawnAt(ThoughtStrength.Weak, _level.ThoughtSprites[0], over);

            // The mount goes on the sensor panel — the one thing this beat is about that is not on the
            // screen at all (see SensorsCue). The swinging arrow that used to run down to it is gone.
            _view.MoveBeatAnchor(SensorsCue);
            ShowSwipeCard();
        }

        /// <summary>
        /// Put «Это навязчивые мысли…» beside the thought that has to be beaten off — once, at the
        /// start of the beat, and not again.
        ///
        /// Once, and not again: the thought drifts, and a plate re-placed every frame against a search
        /// that breaks ties by nearest-clear-spot walks around the screen.
        ///
        /// It used to be re-placed on one condition — when the beat's own arrow swung across it. The
        /// arrow is gone (founder, 2026-09-22), and with it the only thing that ever moved under this
        /// plate after it was put down; the corridor of squares that kept the plate off that stroke
        /// went with it too.
        ///
        /// What did NOT go with it is the measurement that corridor was built on. The thought's PIPS
        /// hang below its rectangle on their own discs, and the first frame shot after the arrows were
        /// removed had the plate resting on the cat's three pips — because the corridor was the only
        /// thing that had ever pushed it clear of them. The blob's obstacle is therefore everything it
        /// PAINTS (<see cref="ArtThoughtView.DrawnBottomY"/>), which is the same finding the arrow's
        /// gap was made of, stated where it belongs.
        /// </summary>
        private void ShowSwipeCard()
        {
            Vector2 thought = _tutorialThought != null
                ? _tutorialThought.Position
                : new Vector2(960f, 420f);

            _blockedForSwipeCard.Clear();
            for (int i = 0; i < _hintObstacles.Length; i++) _blockedForSwipeCard.Add(_hintObstacles[i]);
            if (_tutorialThought != null)
                _blockedForSwipeCard.Add(PaintedRectOf(_tutorialThought));

            Vector2 size = HintPlate.SizeFor(GameTexts.SwipeHint);
            Vector2 spot = HintPlacement.Beside(size, thought, _blockedForSwipeCard);
            _view.ShowBeatPlate(GameTexts.SwipeHint, HintTone.Swipe, spot);
        }

        /// <summary>
        /// Everything a thought paints, as one rectangle: its own box grown down to the bottom of the
        /// pip discs that hang under it. Public because the suite makes the claim on this rectangle.
        /// </summary>
        public static Rect PaintedRectOf(Thought thought)
        {
            Rect box = HintPlacement.Centred(thought.Position, thought.Size);
            float painted = ArtThoughtView.DrawnBottomY(thought.Position, thought.Size);
            return new Rect(box.xMin, box.yMin, box.width, Mathf.Max(box.height, painted - box.yMin));
        }

        /// <summary>
        /// Put a beat's SENTENCE on the screen, beside the thing the beat is about and on top of
        /// nothing — searched against the catalogue's own rectangles
        /// (<see cref="LevelCatalog.HintObstaclesOf"/>). It is the only thing a beat draws now that the
        /// buttons and arrows are off the teaching screens, so there is no second hint to dodge.
        ///
        /// Searched rather than parked in a fixed band, and that is a decision with a scar behind it:
        /// a fixed band is exactly what the row of HUD slots used to be, and the one place on the
        /// frame that is reliably empty on all five plates does not exist — level 1's aeroplane flies
        /// through the top of the frame at y 134, which is where a «safe» top band would have stood.
        /// </summary>
        /// <param name="travellingDetail">
        /// The detail that will be MOVING under this plate for the length of the beat, or −1 when the
        /// beat is about something that stands still.
        /// </param>
        private void ShowBeatSentence(string line, HintTone tone, Vector2 about,
            int travellingDetail = -1)
        {
            _blockedForSwipeCard.Clear();
            for (int i = 0; i < _hintObstacles.Length; i++) _blockedForSwipeCard.Add(_hintObstacles[i]);

            // …and the thing the beat is ABOUT: a sentence about a detail, laid on that detail, is a
            // sentence about nothing.
            if (travellingDetail >= 0) AddDetailTrack(_blockedForSwipeCard, travellingDetail, about);
            else _blockedForSwipeCard.Add(HintPlacement.Centred(about, new Vector2(160f, 160f)));

            Vector2 size = HintPlate.SizeFor(line);
            Vector2 spot = HintPlacement.Beside(size, about, _blockedForSwipeCard);
            _view.ShowBeatPlate(line, tone, spot);
        }

        /// <summary>
        /// Everything the travelling detail will be standing on between now and the end of the beat:
        /// its whole sprite, laid along the thread from where it is to the vessel, as a chain of
        /// rectangles.
        ///
        /// The obstacle of a beat is not a POINT, and that is the return of the design skeptic of
        /// 2026-09-22 (blocker Б1, frame <c>Game04_L1_tutorial_crank</c>). The sentence of the сбор
        /// beat was placed against a 160×160 box at the detail's position AT THE START — and the
        /// detail then drove up its thread and straight under the plate, which is placed once and
        /// never moves. On level 1 that buries the aeroplane: home (881, 134), vessel (549, 846), and
        /// the plate landed at x 511…1066 · y 227…376, i.e. inside the corridor between them, with
        /// only the nose of the plane sticking out below it.
        ///
        /// A chain rather than the bounding box of the run, for the same reason the отгон's stroke is
        /// a chain (<see cref="AddSwipeArrowCorridor"/>): the box of a diagonal travel is most of the
        /// frame, and an obstacle that leaves no clear spot anywhere makes the search fall back to
        /// «on top of the target» — which is the very thing it is here to prevent. Sampled closer
        /// together than the sprite is short, so the chain has no gaps for a plate to slip through.
        ///
        /// The whole SPRITE and not the ink zone round the centroid, exactly as
        /// <see cref="DragHintSpot"/> takes it: the plane is drawn with 600 px of contrail behind the
        /// fuselage, and drawn art is drawn art wherever in its rectangle it happens to be.
        /// </summary>
        private void AddDetailTrack(System.Collections.Generic.List<Rect> into, int index, Vector2 from)
        {
            if (index < 0 || index >= _level.DetailCount)
            {
                into.Add(HintPlacement.Centred(from, new Vector2(160f, 160f)));
                return;
            }

            ArtDetail spec = _level.Details[index];
            Vector2 size = spec.Size;

            // `from` is where the INK is (the anchor); the sprite's own rectangle hangs off it by the
            // same offset. The travel itself is the sprite's centre walking from there to the vessel
            // — <see cref="DetailPosition"/> lerps the HOME and adds the offset, so the far end of the
            // run is the vessel's centre exactly.
            Vector2 sprite = from - LevelCatalog.AnchorOffsetOf(spec);
            Vector2 arrives = _level.VesselCentre;

            float span = Vector2.Distance(sprite, arrives);
            float step = Mathf.Max(24f, Mathf.Min(size.x, size.y) * 0.5f);
            int samples = Mathf.Clamp(Mathf.CeilToInt(span / step), 1, 64);

            for (int i = 0; i <= samples; i++)
                into.Add(HintPlacement.Inflate(
                    HintPlacement.Centred(Vector2.Lerp(sprite, arrives, i / (float)samples), size),
                    HintPlacement.DraggedSpriteMargin));
        }

        /// <summary>
        /// The fourth beat: the goal, over a level that is already running.
        ///
        /// Nothing is blocked and nothing is spawned — <see cref="TutorialAllowsSpawning"/> lets the
        /// waves through from here — so this is ordinary play with one sentence on it. It is aimed at
        /// the VESSEL, because that is the noun the sentence ends on and the one thing on the plate
        /// the player has not been pointed at yet.
        /// </summary>
        private void BeginWholeBeat()
        {
            Beat = TutorialBeat.Whole;
            _wholeBeatSeconds = 0f;
            _view.MoveBeatAnchor(_level.VesselCentre);
            ShowBeatSentence(GameTexts.BeatWhole, HintTone.Gaze, _level.VesselCentre);
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
            _view.HideBeatPlate();
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
                    // «Мысль отбита → дальше обычный play» (SCREENS §Обучение п.4) — and, since
                    // 2026-09-22, one last sentence over that play.
                    BeginWholeBeat();
                    return;

                case TutorialBeat.Whole:
                    if (_wholeBeatSeconds < WholeBeatSeconds) return;
                    FinishTeaching();
                    return;
            }
        }

        /// <summary>
        /// Keep the beat's MOUNT on the thing the beat is about — the one subject that moves is the
        /// detail on its thread, which travels to the vessel as the крутилка turns and flies back home
        /// when it slips.
        ///
        /// Only the mount moves now. Until 2026-09-22 this drove two live aims — the отгон's swinging
        /// stroke, and «ТАЩИ» walking along beside the travelling detail with its arrow re-pointed
        /// every frame — and both are off the teaching screens by the founder's word. The plate itself
        /// deliberately does NOT follow: it is placed once, clear of everywhere the detail will be
        /// (see <see cref="AddDetailTrack"/>), because a sentence that moves while you read it is not
        /// a sentence.
        /// </summary>
        private void AimTheHints()
        {
            if (Beat != TutorialBeat.Crank || _runtime.NoticedIndex < 0) return;
            _view.MoveBeatAnchor(ThreadMidpoint(DetailPosition(_runtime.NoticedIndex)));
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

        // ---- victory -----------------------------------------------------------------------------

        private void BeginWin()
        {
            EnterStage(LevelStage.Win);
            _view.SetHudVisible(false);
            // The teaching plate is the only hint left, and the fourth beat can still be up when the
            // last detail lands — it must not be standing on the reward tableau.
            _view.HideBeatPlate();
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

            // The reward beat: the vessel in the centre with everything that was collected in it —
            // and the camera pushing in on it as it is held (founder, 2026-09-22: «наезд на ведёрко»,
            // the first half of the ripple transition).
            _view.StageVictory();
            _view.SetVictoryZoom(
                Mathf.Clamp01((_stageSeconds - DissolveSeconds - SilenceSeconds) / TableauSeconds));

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
            _view.HideBeatPlate();
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
