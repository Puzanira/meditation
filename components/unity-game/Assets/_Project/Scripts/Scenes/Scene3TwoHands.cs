using System.Collections.Generic;
using Meditation.Mechanics;
using Meditation.Stand;
using Meditation.Tuning;
using Meditation.View;
using UnityEngine;

namespace Meditation.Scenes
{
    /// <summary>
    /// Scenette 3 «Две руки» (MECHANICS §7.3): collecting and thought waves at the same time — the
    /// conflict of attention the whole game exists for (walkthrough frames 12–16). No timer here: the
    /// only question is how the two hands feel together, so the scenette loops forever and just shows
    /// where the coverage threshold would have ended the level.
    /// </summary>
    [AddComponentMenu("Meditation/Scene 3 — Two Hands")]
    public sealed class Scene3TwoHands : PreviewSceneController
    {
        private const float ReplayDelay = 2.5f;

        private CollectionRuntime _runtime;
        private float _replayTimer;

        public CollectionRuntime Runtime => _runtime;

        protected override string Title => "Сценка 3 · Две руки";

        protected override StageOptions Options => new StageOptions
        {
            ShowDetails = true, ShowThoughts = true, TimerRunning = false
        };

        protected override bool CrankAlarm =>
            _runtime != null && !_runtime.Collector.IsSpinning &&
            (_runtime.Collector.Progress01 > 0f || _runtime.DisplayProgress > 0.01f);

        protected override IList<TuningParam> Parameters() => TuningCatalog.TwoHands();

        protected override void OnBuilt()
        {
            _runtime = new CollectionRuntime(Composition);
            _runtime.Restart();
            ShowGazeHint();
        }

        /// <summary>
        /// Registry string, verbatim: «Оглядись — наклони стик». Shown once, until the first detail
        /// is noticed — and only in the gaze variant, since that is the gesture it teaches.
        /// </summary>
        private void ShowGazeHint()
        {
            if (TuningConfig.Notice != NoticeMode.GazeJoystick)
            {
                Composition.HideHint();
                return;
            }

            // Card of walkthrough frame 9 (rect 880,250 · 640×110), arrow towards the gaze circle.
            Composition.ShowHint("Оглядись — наклони стик", HintTone.Gaze,
                new Vector2(1200f, 305f), _runtime.Gaze.Position, GazeSelector.Radius + 20f);
        }

        protected override void Tick(float deltaTime)
        {
            if (_replayTimer > 0f)
            {
                _replayTimer -= deltaTime;
                if (_replayTimer > 0f) return;
                _runtime.Restart();
                Composition.ShowMessage("", "");
                ShowGazeHint();
                return;
            }

            _runtime.Tick(deltaTime, Stick, Hits, CrankSpeed, true);

            if (_runtime.NoticedIndex >= 0) Composition.HideHint();
            else if (Composition.Hint.IsShown) ShowGazeHint();

            if (_runtime.CollectedCount < _runtime.Collected.Length) return;

            Composition.ShowMessage("Собрано: " + LevelOneData.LevelTitle, "");
            Composition.HideHint();
            _replayTimer = ReplayDelay;
        }

        protected override string Readout()
        {
            string notice;
            switch (TuningConfig.Notice)
            {
                case NoticeMode.FixedOrder: notice = "A: порядок"; break;
                case NoticeMode.AutoNearest: notice = "C: авто"; break;
                default: notice = "B: взгляд"; break;
            }

            return HandsReadout() + _runtime.Readout() + "выбор детали: " + notice;
        }
    }
}
