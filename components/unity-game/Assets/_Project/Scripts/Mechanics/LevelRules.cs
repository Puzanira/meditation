using Meditation.Tuning;
using UnityEngine;

namespace Meditation.Mechanics
{
    public enum LevelOutcome
    {
        Playing = 0,
        Win = 1,
        Lose = 2
    }

    /// <summary>
    /// MECHANICS §5 — timer, victory (all details in the vessel before time runs out), defeat (timer
    /// expired OR thoughts covered the screen), and the breather after each collected detail.
    /// </summary>
    public sealed class LevelRules
    {
        public const string LoseByTimer = "Время вышло";
        public const string LoseByThoughts = "Мысли захватили всё";

        public int TotalDetails { get; private set; } = 5;

        public float TimeLeft { get; private set; }

        public int Collected { get; private set; }

        public LevelOutcome Outcome { get; private set; } = LevelOutcome.Playing;

        /// <summary>Why the level was lost (empty while playing / on a win).</summary>
        public string LoseReason { get; private set; } = string.Empty;

        /// <summary>Seconds of breather left; no thoughts spawn while it runs.</summary>
        public float BreatherLeft { get; private set; }

        public bool SpawningAllowed => Outcome == LevelOutcome.Playing && BreatherLeft <= 0f;

        public float TimeLeft01 => Mathf.Clamp01(TimeLeft / Mathf.Max(1f, TuningConfig.LevelSeconds));

        public void Restart(int totalDetails)
        {
            TotalDetails = Mathf.Max(1, totalDetails);
            TimeLeft = TuningConfig.LevelSeconds;
            Collected = 0;
            Outcome = LevelOutcome.Playing;
            LoseReason = string.Empty;
            BreatherLeft = 0f;
        }

        /// <summary>One more detail landed in the vessel; starts the breather if it is enabled.</summary>
        public void OnDetailCollected()
        {
            if (Outcome != LevelOutcome.Playing) return;
            Collected++;
            if (TuningConfig.BreatherEnabled)
                BreatherLeft = Mathf.Max(0f, TuningConfig.BreatherSeconds);
        }

        public void Tick(float deltaTime, float overlapPercent)
        {
            if (Outcome != LevelOutcome.Playing || deltaTime <= 0f) return;

            if (BreatherLeft > 0f) BreatherLeft = Mathf.Max(0f, BreatherLeft - deltaTime);
            TimeLeft = Mathf.Max(0f, TimeLeft - deltaTime);

            // Victory wins ties: the last detail dropping in as the clock hits zero is a win.
            if (Collected >= TotalDetails)
            {
                Outcome = LevelOutcome.Win;
                return;
            }

            if (overlapPercent >= TuningConfig.LossOverlapPercent)
            {
                Outcome = LevelOutcome.Lose;
                LoseReason = LoseByThoughts;
                return;
            }

            if (TimeLeft <= 0f)
            {
                Outcome = LevelOutcome.Lose;
                LoseReason = LoseByTimer;
            }
        }
    }
}
