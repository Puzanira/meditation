using System.Collections.Generic;
using AiGameStudio.ArcadeControls;
using Meditation.Mechanics;
using Meditation.Stand;
using Meditation.Tuning;
using Meditation.View;
using UnityEngine;

namespace Meditation.Scenes
{
    /// <summary>
    /// Scenette 4 «Уровень целиком» (MECHANICS §7.4): the whole level loop — victory with all five
    /// details in the vessel, defeat when the thoughts close over the screen, breathers between
    /// details, and a restart that never leaves the build (walkthrough frames 17–18).
    ///
    /// The clock left this loop on 2026-08-07 with the rest of the timer (see <see cref="LevelRules"/>),
    /// so the scenette is now what the game is: collect them all, or lose the screen.
    ///
    /// The defeat screen keeps the walkthrough's meditative retry: every turn of the crank wipes some
    /// of the thoughts away; clear them (or wait out the auto-retry [toggle]) and the level starts over.
    /// </summary>
    [AddComponentMenu("Meditation/Scene 4 — Full Level")]
    public sealed class Scene4FullLevel : PreviewSceneController
    {
        private const float DissolveSeconds = 0.5f;   // мысли растворяются (макет 18)
        private const float SilenceSeconds = 1f;      // секунда чистой сцены
        private const float WinHoldSeconds = 3f;      // сосуд ×2 с добычей на экране
        private const float AutoRetrySeconds = 4f;
        private const float DegreesPerWipe = 360f;

        /// <summary>True once the victory tableau of mock 18 is fully on screen.</summary>
        public bool VictoryPresented =>
            _rules.Outcome == LevelOutcome.Win && _outcomeSeconds >= DissolveSeconds + SilenceSeconds;

        private readonly LevelRules _rules = new LevelRules();
        private CollectionRuntime _runtime;
        private float _outcomeSeconds;
        private float _wipeDegrees;

        public LevelRules Rules => _rules;

        public CollectionRuntime Runtime => _runtime;

        protected override string Title => "Сценка 4 · Уровень целиком";

        protected override StageOptions Options => StageOptions.Full;

        protected override bool CrankAlarm =>
            _runtime != null && !_runtime.Collector.IsSpinning &&
            (_runtime.Collector.Progress01 > 0f || _runtime.DisplayProgress > 0.01f);

        protected override IList<TuningParam> Parameters() => TuningCatalog.FullLevel();

        protected override void OnBuilt()
        {
            _runtime = new CollectionRuntime(Composition);
            _runtime.DetailCollected += _ => _rules.OnDetailCollected();
            RestartLevel();
        }

        protected override void Tick(float deltaTime)
        {
            if (_rules.Outcome != LevelOutcome.Playing)
            {
                TickOutcome(deltaTime);
                return;
            }

            _runtime.Tick(deltaTime, Stick, Hits, CrankSpeed, _rules.SpawningAllowed);
            _rules.Tick(deltaTime, _runtime.Field.OverlapPercent);

            if (_rules.Outcome == LevelOutcome.Win)
            {
                // Победа отыгрывается по кадру 18: растворение → тишина → сосуд ×2 с добычей.
                _outcomeSeconds = 0f;
                Composition.SetHudVisible(false);
                Composition.HideHint();
            }
            else if (_rules.Outcome == LevelOutcome.Lose)
            {
                // Поражение по кадру 17: белая подложка под строками, цвета мыслей гаснут, HUD снят.
                Composition.SetHudVisible(false);
                Composition.SetDesaturated(true);
                Composition.ShowMessage("Мысли захватили всё. Вдохни.", "Крути крутилку — попробуй снова",
                    true, 480f);
                _outcomeSeconds = 0f;
                _wipeDegrees = 0f;
            }
        }

        private void TickOutcome(float deltaTime)
        {
            _outcomeSeconds += deltaTime;
            Composition.SyncThoughts(_runtime.Field.Thoughts, deltaTime);

            if (_rules.Outcome == LevelOutcome.Win)
            {
                TickVictory();
                if (_outcomeSeconds >= DissolveSeconds + SilenceSeconds + WinHoldSeconds) RestartLevel();
                return;
            }

            // Defeat: the crank wipes the screen clear, ~10 % of the thoughts per turn.
            _wipeDegrees += Mathf.Abs(ArcadeInput.Crank.DeltaDegrees);
            while (_wipeDegrees >= DegreesPerWipe)
            {
                _wipeDegrees -= DegreesPerWipe;
                if (!_runtime.Field.RemoveOldest()) break;
            }

            bool cleared = _runtime.Field.Thoughts.Count == 0;
            bool timedOut = TuningConfig.AutoRetry && _outcomeSeconds >= AutoRetrySeconds;
            if (cleared || timedOut) RestartLevel();
        }

        /// <summary>
        /// Walkthrough frame 18, in three beats: the thoughts dissolve (0.5 s), one second of clean
        /// scene — «тишина» — and then the vessel lifts to the centre at ×2 with all five details in
        /// it, the line «Собрано: …» underneath at (960, 800). The HUD is gone, exactly as the mock
        /// draws the outcome screens.
        /// </summary>
        private void TickVictory()
        {
            if (_outcomeSeconds < DissolveSeconds)
            {
                Composition.SetThoughtsAlpha(1f - _outcomeSeconds / DissolveSeconds);
                return;
            }

            Composition.SetThoughtsAlpha(0f);
            if (_runtime.Field.Thoughts.Count > 0) _runtime.Field.Clear();

            if (_outcomeSeconds < DissolveSeconds + SilenceSeconds) return;   // секунда тишины

            Composition.StageVictory();
            Composition.ShowMessage("Собрано: " + LevelOneData.LevelTitle, "", false, 800f);
        }

        private void RestartLevel()
        {
            _runtime.Restart();
            _rules.Restart(_runtime.Collected.Length);
            _outcomeSeconds = 0f;
            _wipeDegrees = 0f;
            Composition.ShowMessage("", "");
            Composition.SetHudVisible(true);
            Composition.SetDesaturated(false);
            Composition.SetThoughtsAlpha(1f);
        }

        protected override string Readout()
        {
            string outcome;
            switch (_rules.Outcome)
            {
                case LevelOutcome.Win: outcome = "ПОБЕДА"; break;
                case LevelOutcome.Lose: outcome = "ПОРАЖЕНИЕ — " + _rules.LoseReason; break;
                default: outcome = "идёт"; break;
            }

            return HandsReadout() + _runtime.Readout() +
                   "перекрытие: " + _runtime.Field.OverlapPercent.ToString("0") + " % / " +
                   TuningConfig.LossOverlapPercent.ToString("0") + " %\n" +
                   "передышка: " + _rules.BreatherLeft.ToString("0.0") + " с\n" +
                   "статус: " + outcome;
        }
    }
}
