using UnityEngine;

namespace Meditation.View
{
    /// <summary>
    /// A thought finishing its burst: SCREENS.md «Мысли (визуал состояния)» — «Последний пипс — мысль
    /// лопается (масштаб 1.1 → 0, 200 мс)».
    ///
    /// It lives entirely on the view side, and that is the point rather than a convenience. The thought
    /// leaves <see cref="ThoughtField"/> on the hit that spends its last pip, so screen coverage, shake
    /// targeting and the audio layer's «мыслей на экране» all stop counting it in the very frame the
    /// player earned — the geometry the game is played against never waits for an animation. What is
    /// left for the next fifth of a second is this: a picture of a thought that is no longer in the game.
    ///
    /// Which is also why the pop holds the <see cref="Thought"/> it came from instead of a snapshot —
    /// the object is out of the field by then, nothing ticks it any more, and the view needs its label,
    /// place and size to keep drawing what the player was looking at.
    /// </summary>
    public sealed class ThoughtPop
    {
        /// <summary>«200 мс».</summary>
        public const float DurationSeconds = 0.2f;

        /// <summary>«масштаб 1.1 → 0» — the burst starts a hair BIGGER than the thought was.</summary>
        public const float StartScale = 1.1f;

        public ThoughtPop(Mechanics.Thought thought, int slot)
        {
            Thought = thought;
            Slot = slot;
        }

        /// <summary>The thought as it was on its last hit — already out of the field.</summary>
        public Mechanics.Thought Thought { get; }

        /// <summary>Which of the view's burst views is drawing it.</summary>
        public int Slot { get; }

        public float Elapsed { get; private set; }

        public bool Finished => Elapsed >= DurationSeconds;

        /// <summary>Scale right now: 1.1 at the killing hit, 0 once the 200 ms are up.</summary>
        public float Scale => ScaleAt(Elapsed);

        public void Advance(float deltaTime) => Elapsed += Mathf.Max(0f, deltaTime);

        public static float ScaleAt(float elapsed) =>
            Mathf.Lerp(StartScale, 0f, Mathf.Clamp01(elapsed / DurationSeconds));
    }
}
