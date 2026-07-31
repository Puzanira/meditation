using Meditation.Tuning;
using UnityEngine;

namespace Meditation.Mechanics
{
    /// <summary>
    /// One thought-blob: a placeholder rounded rectangle with a label, a toughness class and a row of
    /// pips (= hits left). Sizes are the ones fixed in SCREENS.md: S 180×140, M 280×220, L 420×320.
    /// All coordinates are design pixels on the 1920×1080 screen, origin top-left, position = centre.
    /// </summary>
    public sealed class Thought
    {
        public ThoughtStrength Strength;
        public string Label;
        public Vector2 Position;
        public Vector2 DriftDirection;

        /// <summary>
        /// Derived, never assigned: SCREENS.md fixes three sizes and only three. Screen coverage is
        /// meant to be won by the NUMBER of thoughts rolling in (наплыв), so nothing — not gameplay,
        /// not a test — is allowed to inflate a blob into a slab to force the peak.
        /// </summary>
        public Vector2 Size => SizeOf(Strength);

        /// <summary>Hits landed since the counter was last (re)set.</summary>
        public int HitsTaken;

        /// <summary>Seconds since the last hit — drives the [toggle] hit-counter decay.</summary>
        public float SecondsSinceHit;

        /// <summary>Seconds since spawn (used for "newest" ordering and the pop animation).</summary>
        public float Age;

        public int Durability => TuningConfig.DurabilityOf(Strength);

        public int HitsRemaining => Mathf.Max(0, Durability - HitsTaken);

        public bool IsPopped => HitsTaken >= Durability;

        /// <summary>Screen rect in design px (top-left origin).</summary>
        public Rect Rect => new Rect(Position.x - Size.x * 0.5f, Position.y - Size.y * 0.5f, Size.x, Size.y);

        public static Vector2 SizeOf(ThoughtStrength strength)
        {
            switch (strength)
            {
                case ThoughtStrength.Weak: return new Vector2(180f, 140f);
                case ThoughtStrength.Medium: return new Vector2(280f, 220f);
                default: return new Vector2(420f, 320f);
            }
        }
    }
}
