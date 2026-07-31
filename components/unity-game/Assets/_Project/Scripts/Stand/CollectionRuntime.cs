using System;
using System.Collections.Generic;
using Meditation.Mechanics;
using Meditation.Tuning;
using Meditation.View;
using UnityEngine;

namespace Meditation.Stand
{
    /// <summary>
    /// The "both hands" loop shared by scenette 3 and scenette 4: notice a detail (A/B/C), drag it in
    /// with the crank, and keep the thought field alive at the same time. Owns the view sync so the two
    /// scenettes stay identical where the design says they are identical.
    /// </summary>
    public sealed class CollectionRuntime
    {
        private readonly StageView _view;
        private readonly List<Vector2> _homes = new List<Vector2>();
        private readonly List<bool> _selectable = new List<bool>();

        private float _displayProgress;
        private float _gazeFreeze;

        public CollectionRuntime(StageView view)
        {
            _view = view;
            for (int i = 0; i < LevelOneData.Details.Length; i++)
            {
                _homes.Add(LevelOneData.Details[i].Home);
                _selectable.Add(true);
            }
            Collected = new bool[LevelOneData.Details.Length];
        }

        public ThoughtField Field { get; } = new ThoughtField();

        public CrankCollector Collector { get; } = new CrankCollector();

        public GazeSelector Gaze { get; } = new GazeSelector();

        public bool[] Collected { get; }

        /// <summary>Detail currently being dragged, or -1.</summary>
        public int NoticedIndex { get; private set; } = -1;

        /// <summary>Where the detail is drawn (lags behind on a slip, so it flies home over ~0.6 s).</summary>
        public float DisplayProgress => _displayProgress;

        /// <summary>True while a lost detail is travelling back to its place.</summary>
        public bool Slipping => _displayProgress > Collector.Progress01 + 0.001f;

        public int CollectedCount { get; private set; }

        /// <summary>Raised when a detail lands in the vessel.</summary>
        public event Action<int> DetailCollected;

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

        public void Tick(float deltaTime, Vector2 stick, int hits, float crankSpeed, bool spawningAllowed)
        {
            Field.Tick(deltaTime, stick, hits, spawningAllowed);

            if (hits > 0) _gazeFreeze = 0.15f;
            else _gazeFreeze = Mathf.Max(0f, _gazeFreeze - deltaTime);

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
            for (int i = 0; i < _selectable.Count; i++)
                _selectable[i] = !Collected[i];

            switch (TuningConfig.Notice)
            {
                case NoticeMode.FixedOrder:
                    if (NoticedIndex < 0 || Collected[NoticedIndex]) Notice(FirstUncollected());
                    _view.SetGaze(Vector2.zero, 0f, false);
                    break;

                case NoticeMode.AutoNearest:
                    if (NoticedIndex < 0 || Collected[NoticedIndex]) Notice(NearestUncovered());
                    _view.SetGaze(Vector2.zero, 0f, false);
                    break;

                default:
                    // Variant B: the gaze circle picks the detail; a shake freezes it in place.
                    for (int i = 0; i < _selectable.Count; i++)
                        _selectable[i] = !Collected[i] && i != NoticedIndex;

                    Gaze.Tick(stick, deltaTime, _homes, _selectable, _gazeFreeze > 0f);
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

        private int NearestUncovered()
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Collected.Length; i++)
            {
                if (Collected[i]) continue;
                if (Field.IsCovered(_homes[i])) continue;
                float d = (_homes[i] - LevelOneData.VesselCentre).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = i;
            }

            // Everything visible is covered — fall back to the first uncollected one so the loop
            // never deadlocks while the founder is tuning coverage.
            return best >= 0 ? best : FirstUncollected();
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
                _view.SetDetailProgress(i, 0f, false, false);
            }

            if (NoticedIndex >= 0)
            {
                _view.SetDetailProgress(NoticedIndex, _displayProgress, Collector.IsSpinning, true, Slipping);
                Vector2 from = Vector2.Lerp(_homes[NoticedIndex], LevelOneData.VesselCentre, _displayProgress);
                _view.SetThread(from, LevelOneData.VesselCentre, true);
            }
            else
            {
                _view.SetThread(Vector2.zero, Vector2.zero, false);
            }

            _view.SyncThoughts(Field.Thoughts, deltaTime);
            _view.TickPulse(deltaTime, Collected, NoticedIndex);
            _view.SetPeak(Field.OverlapPercent >= TuningConfig.LossOverlapPercent);
        }

        public string Readout()
        {
            return
                "деталь: " + (NoticedIndex >= 0 ? LevelOneData.Details[NoticedIndex].Name : "—") +
                "  прогресс " + (Collector.Progress01 * 100f).ToString("0") + "%\n" +
                "собрано: " + CollectedCount + " / " + Collected.Length + "\n" +
                "мыслей: " + Field.Thoughts.Count +
                "  перекрытие " + Field.OverlapPercent.ToString("0") + "%\n" +
                "следующая волна через " + Field.SecondsToNextWave.ToString("0.0") + " с\n";
        }
    }
}
