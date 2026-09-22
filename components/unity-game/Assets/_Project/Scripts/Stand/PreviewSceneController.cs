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
    /// pump the THREE controllers from ArcadeInput, honour the "в меню" button. Subclasses only add
    /// rules. Since 2026-08-07 the three are the crank (dragging), the joystick (aiming, and nothing
    /// else) and the two height sensors (the отгон) — see <see cref="SwipeDetector"/>.
    ///
    /// All input in this file (and in every scenette) comes from ArcadeInput — no keyboard, no mouse,
    /// no Input System types anywhere in gameplay code (ARCADE_INTEGRATION_CONTRACT §4).
    /// </summary>
    public abstract class PreviewSceneController : MonoBehaviour
    {
        protected readonly CrankSpeedMeter CrankMeter = new CrankSpeedMeter();
        protected readonly SwipeDetector Swipe = new SwipeDetector();

        /// <summary>Smoothed crank speed this frame, deg/s.</summary>
        protected float CrankSpeed { get; private set; }

        /// <summary>Is the crank above the [tune] threshold this frame?</summary>
        protected bool CrankSpinning { get; private set; }

        /// <summary>The joystick — the aim, and only the aim.</summary>
        protected Vector2 Stick { get; private set; }

        /// <summary>Hits of the отгон registered this frame (swipes over the height sensors).</summary>
        protected int Hits { get; private set; }

        /// <summary>Hits since the scenette started (readout + tests).</summary>
        public int TotalHits => Swipe.TotalHits;

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

            // «Здесь уровня нет.» A scenette is a rig for ONE rule, and it runs on the values its own
            // panel shows — not on the band the last level launched off the stand menu left behind.
            // Without this line «Уровень 5» leaves ThoughtBudget at 25 and this rig stops sending
            // blobs after the twenty-fifth (Codex, 2026-09-22), which is not a rule the founder can
            // tune, only a scenette that quietly dies. One call, and every value a level band carries
            // comes back at once — including the ones added to it next time.
            TuningConfig.LeaveLevelBand();

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
            Swipe.Tick(ArcadeInput.HeightA.Value, ArcadeInput.HeightB.Value, deltaTime);
            Hits = Swipe.HitsThisTick;

            Composition.SetCrank(ArcadeInput.Crank.TotalDegrees, CrankSpinning, CrankAlarm);
            Composition.TickChrome(deltaTime);
            Tick(deltaTime);
        }

        /// <summary>Common first lines of every readout block — the three controllers, in numbers.</summary>
        protected string HandsReadout()
        {
            return
                "динамо: " + CrankSpeed.ToString("0") + " °/с  (порог " +
                TuningConfig.CrankThresholdDegPerSec.ToString("0") + ")\n" +
                "кручение: " + (CrankSpinning ? "ИДЁТ" : "стоит") + "\n" +
                "прицел (стик): " + Stick.x.ToString("0.0") + ", " + Stick.y.ToString("0.0") + "\n" +
                "датчики: A " + ArcadeInput.HeightA.Value.ToString("0.00") +
                "  B " + ArcadeInput.HeightB.Value.ToString("0.00") +
                "   взмах " + Swipe.GestureSpeed.ToString("0.0") + " ед/с\n" +
                "ударов всего: " + Swipe.TotalHits + "\n";
        }

        private static void EnsureArcadeInput()
        {
            if (Object.FindAnyObjectByType<ArcadeInputRunner>() != null) return;
            var go = new GameObject("ArcadeInput");
            go.AddComponent<ArcadeInputRunner>();
        }
    }
}
