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
        /// The silhouette's own size once a level's art is behind the blob, or zero on the greybox
        /// stand. Only ever set through <see cref="Meditation.View.ArtLibrary.FitThought"/>, which
        /// fits the sprite INSIDE the class box — see <see cref="Size"/>.
        /// </summary>
        public Vector2 ArtSize;

        /// <summary>
        /// SCREENS.md fixes three sizes and only three. Screen coverage is meant to be won by the
        /// NUMBER of thoughts rolling in (наплыв), so nothing — not gameplay, not a test — is allowed
        /// to inflate a blob into a slab to force the peak. Art does not change that: a level's
        /// silhouette is fitted inside its class box and can only ever be SMALLER, which is why the
        /// art size is clamped down to the class here rather than trusted.
        /// </summary>
        public Vector2 Size
        {
            get
            {
                Vector2 box = SizeOf(Strength);
                if (ArtSize.x < 1f || ArtSize.y < 1f) return box;
                return new Vector2(Mathf.Min(ArtSize.x, box.x), Mathf.Min(ArtSize.y, box.y));
            }
        }

        /// <summary>
        /// Part of the defeat wallpaper (<see cref="ThoughtField.CoverScreen"/>) rather than a thought
        /// the player is fighting. The view draws these to COVER their cell instead of fitting inside
        /// it, and gives them no pips: on the defeat screen the pips are not a readout of anything —
        /// the crank wipes the screen by the turn — and forty rows of them drew dotted lines across it.
        /// </summary>
        public bool Wallpaper;

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
