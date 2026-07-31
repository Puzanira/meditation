using System.Collections.Generic;
using AiGameStudio.ArcadeControls;
using Meditation.Mechanics;
using Meditation.Tuning;
using Meditation.View;
using UnityEngine;

namespace Meditation.Stand
{
    /// <summary>
    /// Shared skeleton of a preview scenette: build the 1920×1080 stage, build the tuning panel,
    /// pump the two hands from ArcadeInput, honour the "в меню" button. Subclasses only add rules.
    ///
    /// All input in this file (and in every scenette) comes from ArcadeInput — no keyboard, no mouse,
    /// no Input System types anywhere in gameplay code (ARCADE_INTEGRATION_CONTRACT §4).
    /// </summary>
    public abstract class PreviewSceneController : MonoBehaviour
    {
        protected readonly CrankSpeedMeter CrankMeter = new CrankSpeedMeter();
        protected readonly ShakeDetector Shake = new ShakeDetector();

        /// <summary>Smoothed crank speed this frame, deg/s.</summary>
        protected float CrankSpeed { get; private set; }

        /// <summary>Is the crank above the [tune] threshold this frame?</summary>
        protected bool CrankSpinning { get; private set; }

        protected Vector2 Stick { get; private set; }

        /// <summary>Shake hits registered this frame.</summary>
        protected int Hits { get; private set; }

        /// <summary>Shake hits since the scenette started (readout + tests).</summary>
        public int TotalHits => Shake.TotalHits;

        /// <summary>
        /// True only when a stall is actually costing the player a detail — the crank dial goes red
        /// on this, not merely on "the hand is not turning" (SCREENS: "вышла — красная подсветка").
        /// </summary>
        protected virtual bool CrankAlarm => false;

        public DesignStage Stage { get; private set; }

        public StageView Composition { get; private set; }

        public TuningPanel Panel { get; private set; }

        protected abstract string Title { get; }

        protected abstract StageOptions Options { get; }

        protected abstract IList<TuningParam> Parameters();

        protected abstract string Readout();

        /// <summary>Scene-specific setup after the stage exists.</summary>
        protected virtual void OnBuilt() { }

        /// <summary>Scene-specific per-frame rules.</summary>
        protected abstract void Tick(float deltaTime);

        protected virtual void Awake()
        {
            EnsureArcadeInput();

            Stage = DesignStage.Create("StandCanvas");
            Composition = new StageView(Stage, Options);
            Panel = TuningPanel.Create(Stage, Title, Parameters(), Readout);

            if (GetComponent<MenuButtonExit>() == null) gameObject.AddComponent<MenuButtonExit>();

            OnBuilt();
        }

        protected virtual void Update()
        {
            float deltaTime = Time.deltaTime;

            CrankSpeed = CrankMeter.Tick(ArcadeInput.Crank.DeltaDegrees, deltaTime);
            CrankSpinning = CrankSpeed >= TuningConfig.CrankThresholdDegPerSec;
            Stick = ArcadeInput.Joystick.Vector;
            Shake.Tick(Stick, deltaTime);
            Hits = Shake.HitsThisTick;

            Composition.SetCrank(ArcadeInput.Crank.TotalDegrees, CrankSpinning, CrankAlarm);
            Composition.TickChrome(deltaTime);
            Tick(deltaTime);
        }

        /// <summary>Common first lines of every readout block — the two hands, in numbers.</summary>
        protected string HandsReadout()
        {
            return
                "динамо: " + CrankSpeed.ToString("0") + " °/с  (порог " +
                TuningConfig.CrankThresholdDegPerSec.ToString("0") + ")\n" +
                "кручение: " + (CrankSpinning ? "ИДЁТ" : "стоит") + "\n" +
                "стик: " + Stick.x.ToString("0.0") + ", " + Stick.y.ToString("0.0") +
                "   жест " + Shake.GestureSpeed.ToString("0.0") + "\n" +
                "ударов всего: " + Shake.TotalHits + "\n";
        }

        private static void EnsureArcadeInput()
        {
            if (Object.FindAnyObjectByType<ArcadeInputRunner>() != null) return;
            var go = new GameObject("ArcadeInput");
            go.AddComponent<ArcadeInputRunner>();
        }
    }
}
