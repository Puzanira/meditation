using System.Collections.Generic;
using Meditation.Mechanics;
using Meditation.Stand;
using Meditation.Tuning;
using Meditation.View;
using UnityEngine;

namespace Meditation.Scenes
{
    /// <summary>
    /// Scenette 1 «Динамо-сбор» (MECHANICS §7.1): one detail, one vessel, no thoughts. The dandelion is
    /// already noticed and threaded to the briefcase — exactly the tutorial beat of walkthrough frames
    /// 4–6 — so the only thing under test is the crank: keep turning and it travels, stall past the
    /// grace period and the progress is lost the way the [toggle] says.
    /// </summary>
    [AddComponentMenu("Meditation/Scene 1 — Crank Collect")]
    public sealed class Scene1CrankCollect : PreviewSceneController
    {
        private const int DetailIndex = 0;
        private const float ReplayDelay = 1.6f;

        /// <summary>Card position of walkthrough frame 4 (card rect 290,540 · 420×110).</summary>
        private static readonly Vector2 HintCentre = new Vector2(500f, 595f);

        private readonly CrankCollector _collector = new CrankCollector();
        private float _displayProgress;
        private float _replayTimer;

        public CrankCollector Collector => _collector;

        protected override string Title => "Сценка 1 · Динамо-сбор";

        protected override StageOptions Options => new StageOptions
        {
            ShowDetails = true, ShowThoughts = false
        };

        /// <summary>
        /// Red dial while the hand is out of the zone AND a detail is at stake — including the flight
        /// home after a slip, which is exactly the moment SCREENS wants the indicator to shout.
        /// </summary>
        protected override bool CrankAlarm =>
            !_collector.IsSpinning && (_collector.Progress01 > 0f || _displayProgress > 0.01f);

        /// <summary>The detail is travelling backwards: progress was lost (mock 14).</summary>
        private bool Slipping => _displayProgress > _collector.Progress01 + 0.001f;

        protected override IList<TuningParam> Parameters() => TuningCatalog.CrankCollect();

        protected override void OnBuilt()
        {
            for (int i = 1; i < LevelOneData.Details.Length; i++) Composition.ShowDetail(i, false);
            ShowCrankHint(LevelOneData.Details[DetailIndex].Home);
        }

        /// <summary>
        /// Walkthrough frame 25 wrote it «Крути ручку!» — the stand says «крутилку», because the panel
        /// in the room does (system/CONTROLS_BRIEF.md; founder, 2026-09-22). The stand is a rig for the
        /// RULE, but it is a rig the founder reads, and two names for one control is the bug.
        /// </summary>
        private void ShowCrankHint(Vector2 target)
        {
            Vector2 home = LevelOneData.Details[DetailIndex].Home;
            // Стоп на подступе: деталь 50 px, стрелка не должна её накрывать (макет 4).
            Composition.ShowHint("Крути крутилку!", HintTone.Crank,
                HintCentre + (target - home) * 0.5f, target, 55f);
        }

        protected override void Tick(float deltaTime)
        {
            if (_replayTimer > 0f)
            {
                _replayTimer -= deltaTime;
                if (_replayTimer <= 0f) Replay();
                Composition.TickPulse(deltaTime, null, DetailIndex);
                return;
            }

            _collector.Tick(CrankSpeed, deltaTime);

            if (_collector.CompletedThisTick)
            {
                Composition.CollectDetail(DetailIndex);
                Composition.SetThread(Vector2.zero, Vector2.zero, false);
                Composition.HideHint();
                _replayTimer = ReplayDelay;
                _displayProgress = 0f;
                return;
            }

            float target = _collector.Progress01;
            if (target >= _displayProgress) _displayProgress = target;
            else _displayProgress = Mathf.MoveTowards(_displayProgress, target, deltaTime / 0.6f);

            Vector2 home = LevelOneData.Details[DetailIndex].Home;
            Vector2 position = Vector2.Lerp(home, LevelOneData.VesselCentre, _displayProgress);
            Composition.SetDetailProgress(DetailIndex, _displayProgress, _collector.IsSpinning, true, Slipping);
            Composition.SetThread(position, LevelOneData.VesselCentre, true);
            Composition.TickPulse(deltaTime, null, DetailIndex);

            // The card follows the detail it is talking about, so it never drifts onto the scene.
            ShowCrankHint(position);
        }

        private void Replay()
        {
            _collector.Reset();
            _displayProgress = 0f;
            Composition.ResetCollected();
            for (int i = 1; i < LevelOneData.Details.Length; i++) Composition.ShowDetail(i, false);
            ShowCrankHint(LevelOneData.Details[DetailIndex].Home);
        }

        protected override string Readout()
        {
            return HandsReadout() +
                   "прогресс: " + (_collector.Progress01 * 100f).ToString("0") + "%\n" +
                   "простой: " + (_collector.StalledSeconds * 1000f).ToString("0") + " мс" +
                   (_collector.InGrace ? "  (в grace)" : "") + "\n" +
                   "режим срыва: " + (TuningConfig.StopMode == CrankStopMode.ResetToZero ? "A в ноль" : "B тает");
        }
    }
}
