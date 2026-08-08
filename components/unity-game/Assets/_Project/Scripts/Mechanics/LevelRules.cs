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
    /// MECHANICS §5 — victory (all the details are in the vessel), defeat (the thoughts covered the
    /// screen) and the breather after each collected detail.
    ///
    /// **There is no clock any more** (founder, 2026-08-07: «давай уберём, он плохо работает»). The
    /// level used to end two ways, and one of them was arithmetic the player could not see coming: a
    /// sun in the corner ticking down while the thing the game is ABOUT — the screen filling up — went
    /// on underneath it. Removing the timer is not a simplification of the rules, it is the removal of
    /// a second, competing failure condition, so that «мысли заполнили экран» is the only way to lose
    /// and «все детали собраны» the only way to win. Pressure now comes from the waves and from the
    /// thoughts growing (<see cref="Thought.GrowthScale"/>), which is pressure the player can watch.
    ///
    /// Superseded by that decision: MECHANICS §5 «длительность уровня [tune 60–180 с]», §5 «поражение
    /// по истечении времени», §6 «длительность» in the per-level band, and SCREENS «Зоны: солнце-
    /// циферблат + подпись».
    /// </summary>
    public sealed class LevelRules
    {
        public const string LoseByThoughts = "Мысли захватили всё";

        public int TotalDetails { get; private set; } = 5;

        public int Collected { get; private set; }

        public LevelOutcome Outcome { get; private set; } = LevelOutcome.Playing;

        /// <summary>Why the level was lost (empty while playing / on a win).</summary>
        public string LoseReason { get; private set; } = string.Empty;

        /// <summary>Seconds of breather left; no thoughts spawn while it runs.</summary>
        public float BreatherLeft { get; private set; }

        public bool SpawningAllowed => Outcome == LevelOutcome.Playing && BreatherLeft <= 0f;

        public void Restart(int totalDetails)
        {
            TotalDetails = Mathf.Max(1, totalDetails);
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

            // Victory wins ties: the last detail dropping in on the frame the screen closes is a win.
            if (Collected >= TotalDetails)
            {
                Outcome = LevelOutcome.Win;
                return;
            }

            if (overlapPercent >= TuningConfig.LossOverlapPercent)
            {
                Outcome = LevelOutcome.Lose;
                LoseReason = LoseByThoughts;
            }
        }
    }
}
