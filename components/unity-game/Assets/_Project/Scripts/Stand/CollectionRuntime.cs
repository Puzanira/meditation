using System;
using System.Collections.Generic;
using Meditation.Mechanics;
using Meditation.Tuning;
using UnityEngine;

namespace Meditation.Stand
{
    /// <summary>
    /// The "both hands" loop: notice a detail (A/B/C), drag it in with the crank, and keep the thought
    /// field alive at the same time. Owns the view sync so every screen that plays this loop behaves
    /// identically where the design says it is identical.
    ///
    /// The loop is told its own composition — where the details start, where the vessel is, what they
    /// are called — instead of reading level 1 out of a static. That is what lets the preview stand's
    /// greybox scenettes 3–4 and all three art levels of the game run the SAME rules: the code the
    /// stand's tests pin is the code the levels play.
    /// </summary>
    public sealed class CollectionRuntime
    {
        private readonly ICollectionView _view;
        private readonly List<Vector2> _homes = new List<Vector2>();
        private readonly List<Vector2> _threadOffsets = new List<Vector2>();
        private readonly List<bool> _selectable = new List<bool>();
        private readonly string[] _names;
        private readonly Vector2 _vesselCentre;

        private float _displayProgress;

        /// <summary>The preview stand's composition: level 1 greybox out of <see cref="LevelOneData"/>.</summary>
        public CollectionRuntime(ICollectionView view)
            : this(view, HomesOfLevelOne(), LevelOneData.VesselCentre, NamesOfLevelOne())
        {
        }

        public CollectionRuntime(ICollectionView view, IReadOnlyList<Vector2> homes, Vector2 vesselCentre,
            IReadOnlyList<string> names)
        {
            _view = view;
            _vesselCentre = vesselCentre;

            _names = new string[homes.Count];
            for (int i = 0; i < homes.Count; i++)
            {
                _homes.Add(homes[i]);
                _selectable.Add(true);
                _names[i] = names != null && i < names.Count ? names[i] : "деталь " + (i + 1);
            }

            Collected = new bool[homes.Count];
        }

        /// <summary>
        /// Where each detail's THREAD is tied, as an offset from its home — the sprite's alpha centroid
        /// on a real level, zero on the greybox stand where every placeholder is its own centre.
        ///
        /// The end of the thread is a claim about the picture: level 1's plane is 484 px of sky with the
        /// aircraft in the left quarter, and a thread leaving the middle of that rectangle starts 145 px
        /// off the thing it is supposed to be pulling (design gate, 2026-08-07). It is an offset rather
        /// than a second set of positions so that a detail on its way to the vessel carries its own
        /// anchor with it instead of sliding along a different line than its sprite.
        /// </summary>
        public void SetThreadOffsets(IReadOnlyList<Vector2> offsets)
        {
            _threadOffsets.Clear();
            if (offsets == null) return;
            for (int i = 0; i < offsets.Count; i++) _threadOffsets.Add(offsets[i]);
        }

        /// <summary>Where the thread is tied on detail <paramref name="index"/> at rest, design px.</summary>
        public Vector2 ThreadAnchor(int index) =>
            index >= 0 && index < _homes.Count ? _homes[index] + OffsetOf(index) : _vesselCentre;

        private Vector2 OffsetOf(int index) =>
            index >= 0 && index < _threadOffsets.Count ? _threadOffsets[index] : Vector2.zero;

        public ThoughtField Field { get; } = new ThoughtField();

        public CrankCollector Collector { get; } = new CrankCollector();

        public GazeSelector Gaze { get; } = new GazeSelector();

        public bool[] Collected { get; }

        /// <summary>Where the vessel is on this screen — the thread's far end.</summary>
        public Vector2 VesselCentre => _vesselCentre;

        /// <summary>Detail home positions, design px.</summary>
        public IReadOnlyList<Vector2> Homes => _homes;

        /// <summary>Detail currently being dragged, or -1.</summary>
        public int NoticedIndex { get; private set; } = -1;

        /// <summary>Where the detail is drawn (lags behind on a slip, so it flies home over ~0.6 s).</summary>
        public float DisplayProgress => _displayProgress;

        /// <summary>True while a lost detail is travelling back to its place.</summary>
        public bool Slipping => _displayProgress > Collector.Progress01 + 0.001f;

        public int CollectedCount { get; private set; }

        /// <summary>
        /// Force the loop to treat a detail as already noticed. The level-1 tutorial opens on it: the
        /// walkthrough (frame 4) has the thread to the dandelion already drawn, so the very first thing
        /// the player is asked to do is turn the crank and nothing else.
        /// </summary>
        public void ForceNotice(int index)
        {
            Notice(index);
        }

        /// <summary>Raised when a detail lands in the vessel.</summary>
        public event Action<int> DetailCollected;

        /// <summary>
        /// Hard stop on noticing and collecting, while the thoughts keep living and can still be shaken
        /// off. The level-1 tutorial's second beat needs exactly this: SCREENS says the plush bear
        /// «блокирует сбор» until it is beaten off, and «the next detail» is a sentence only variant A
        /// can honour — under the gaze (variant B, the shipped default) the player could simply look at
        /// some OTHER detail and the beat would teach nothing. So the beat states the block itself
        /// instead of hoping the notice mode produces it.
        /// </summary>
        public bool CollectionSuspended { get; set; }

        /// <summary>
        /// Is the gaze circle in play at all? A flag rather than a constant because the tutorial used
        /// to open with the crank and introduce the gaze only later; since the drop of 2026-08-07 beat
        /// 1 IS «НАВОДИ», so the level switches it on from the first beat — but a screen that has to
        /// take the aim away still has one call to make instead of a special case in the loop.
        /// </summary>
        public bool GazeInPlay { get; set; } = true;

        public void Restart()
        {
            for (int i = 0; i < Collected.Length; i++) Collected[i] = false;
            CollectedCount = 0;
            NoticedIndex = -1;
            _displayProgress = 0f;
            Collector.Reset();
            Gaze.Reset();
            Field.Clear();
            Field.ResetWaveTimer(TuningConfig.WaveIntervalSeconds);
            _view.ResetCollected();
            _view.SetThread(Vector2.zero, Vector2.zero, false);
            _view.SetGaze(Vector2.zero, 0f, false);
            _view.SetPeak(false);
        }

        /// <param name="stick">The joystick, which since 2026-08-07 is the AIM and nothing else.</param>
        /// <param name="hits">Hits of the отгон this frame — swipes over the height sensors.</param>
        public void Tick(float deltaTime, Vector2 stick, int hits, float crankSpeed, bool spawningAllowed)
        {
            Field.Tick(deltaTime, stick, hits, spawningAllowed);

            if (CollectionSuspended)
            {
                // Nothing is noticed and nothing turns: the only hand that does anything is the one
                // over the sensors. The gaze stays where it is rather than disappearing — a circle that
                // blinks out and back reads as a glitch, not as a rule.
                Notice(-1);
                _view.SetGaze(Gaze.Position, 0f,
                    GazeInPlay && TuningConfig.Notice == NoticeMode.GazeJoystick);
                SyncView(deltaTime);
                return;
            }

            UpdateNotice(deltaTime, stick);

            if (NoticedIndex >= 0)
            {
                Collector.Tick(crankSpeed, deltaTime);
                if (Collector.CompletedThisTick)
                {
                    int index = NoticedIndex;
                    Collected[index] = true;
                    CollectedCount++;
                    _view.CollectDetail(index);
                    NoticedIndex = -1;
                    _displayProgress = 0f;
                    Collector.Reset();
                    DetailCollected?.Invoke(index);
                }
            }

            SyncView(deltaTime);
        }

        private void UpdateNotice(float deltaTime, Vector2 stick)
        {
            // A detail under a thought cannot be picked, whichever way picking works. SCREENS spells it
            // out for variant C («ближайшая видимая, не закрытая мыслями»); variants A and B were
            // reaching straight through a blob — the fixed order took the covered detail anyway and the
            // gaze noticed one it could not even see. Covering blocks the START of a collection only:
            // a blob drifting over the empty home of a detail already on its way does not cancel it.
            for (int i = 0; i < _selectable.Count; i++)
                _selectable[i] = !Collected[i] && !Field.IsCovered(_homes[i]);

            switch (TuningConfig.Notice)
            {
                case NoticeMode.FixedOrder:
                    if (NoticedIndex < 0 || Collected[NoticedIndex]) Notice(FirstUncollectedInTheOpen());
                    _view.SetGaze(Vector2.zero, 0f, false);
                    break;

                case NoticeMode.AutoNearest:
                    if (NoticedIndex < 0 || Collected[NoticedIndex]) Notice(NearestUncovered());
                    _view.SetGaze(Vector2.zero, 0f, false);
                    break;

                default:
                    // Variant B: the gaze circle picks the detail. Nothing interrupts it any more —
                    // the отгон is on the sensors, so the stick has one job and does it every frame.
                    if (!GazeInPlay)
                    {
                        // Not yet taught: the circle is neither drawn nor selecting.
                        _view.SetGaze(Gaze.Position, 0f, false);
                        break;
                    }

                    for (int i = 0; i < _selectable.Count; i++)
                        _selectable[i] = _selectable[i] && i != NoticedIndex;

                    Gaze.Tick(stick, deltaTime, _homes, _selectable);
                    if (Gaze.NoticedThisTick) Notice(Gaze.NoticedIndex);
                    _view.SetGaze(Gaze.Position, Gaze.DwellProgress01, true);
                    break;
            }
        }

        private void Notice(int index)
        {
            if (index == NoticedIndex) return;
            NoticedIndex = index;
            _displayProgress = 0f;
            Collector.Reset();
        }

        private int FirstUncollected()
        {
            for (int i = 0; i < Collected.Length; i++)
                if (!Collected[i]) return i;
            return -1;
        }

        /// <summary>
        /// Variant A: the authored order, with the current target WAITING while a thought sits on it —
        /// not skipped. Skipping ahead would quietly rewrite the order the designer authored («от
        /// заметной к спрятанной»), and would also let the player collect straight through the tutorial's
        /// plush bear.
        /// </summary>
        private int FirstUncollectedInTheOpen()
        {
            int first = FirstUncollected();
            if (first < 0) return -1;
            return Field.IsCovered(_homes[first]) ? -1 : first;
        }

        private int NearestUncovered()
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Collected.Length; i++)
            {
                if (Collected[i]) continue;
                if (Field.IsCovered(_homes[i])) continue;
                float d = (_homes[i] - _vesselCentre).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = i;
            }

            // Everything is under a thought: nothing is picked. That is not a deadlock but the rule of
            // variant C itself («автовыбор ближайшей ВИДИМОЙ детали») — the way out is the other hand.
            // The old fallback to the first uncollected detail reached through the blobs and made the
            // «не закрытая мыслями» half of the spec unobservable.
            return best;
        }

        private void SyncView(float deltaTime)
        {
            // The detail slides back over ~0.6 s instead of teleporting (SCREENS: "летит обратно").
            float target = Collector.Progress01;
            if (target >= _displayProgress) _displayProgress = target;
            else _displayProgress = Mathf.MoveTowards(_displayProgress, target, deltaTime / 0.6f);

            for (int i = 0; i < Collected.Length; i++)
            {
                if (Collected[i]) continue;
                if (i == NoticedIndex) continue;
                _view.SetDetailProgress(i, 0f, false, false, false);
            }

            if (NoticedIndex >= 0)
            {
                _view.SetDetailProgress(NoticedIndex, _displayProgress, Collector.IsSpinning, true, Slipping);
                Vector2 from = Vector2.Lerp(_homes[NoticedIndex], _vesselCentre, _displayProgress) +
                               OffsetOf(NoticedIndex);
                _view.SetThread(from, _vesselCentre, true);
            }
            else
            {
                _view.SetThread(Vector2.zero, Vector2.zero, false);
            }

            _view.SyncThoughts(Field.Thoughts, deltaTime);
            _view.TickPulse(deltaTime, Collected, NoticedIndex);
            _view.SetPeak(Field.OverlapPercent >= TuningConfig.PeakOverlapPercent);
        }

        public string Readout()
        {
            return
                "деталь: " + (NoticedIndex >= 0 ? _names[NoticedIndex] : "—") +
                "  прогресс " + (Collector.Progress01 * 100f).ToString("0") + "%\n" +
                "собрано: " + CollectedCount + " / " + Collected.Length + "\n" +
                "мыслей: " + Field.Thoughts.Count +
                "  перекрытие " + Field.OverlapPercent.ToString("0") + "%\n" +
                "следующая волна через " + Field.SecondsToNextWave.ToString("0.0") + " с\n";
        }

        private static Vector2[] HomesOfLevelOne()
        {
            var homes = new Vector2[LevelOneData.Details.Length];
            for (int i = 0; i < homes.Length; i++) homes[i] = LevelOneData.Details[i].Home;
            return homes;
        }

        private static string[] NamesOfLevelOne()
        {
            var names = new string[LevelOneData.Details.Length];
            for (int i = 0; i < names.Length; i++) names[i] = LevelOneData.Details[i].Name;
            return names;
        }
    }
}
