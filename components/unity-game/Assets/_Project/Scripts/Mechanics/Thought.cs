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
        /// stand. Only ever set through <see cref="Meditation.View.ArtLibrary.FitThought(Thought)"/>,
        /// which scales the sprite until it COVERS the class box — see <see cref="SpawnSize"/>.
        /// </summary>
        public Vector2 ArtSize;

        /// <summary>
        /// How much of <see cref="Rect"/> this thought's sprite actually paints, 0…1
        /// (<see cref="Meditation.View.ArtLibrary.InkShareOf"/>). 1 on the greybox stand, where a blob
        /// really is a solid rectangle. Read through <see cref="Ink"/>.
        /// </summary>
        public float InkShare = 1f;

        /// <summary>
        /// The share of its rectangle this thought hides, as <see cref="ThoughtField.OverlapPercent"/>
        /// counts it.
        ///
        /// The defeat wallpaper answers 1 whatever its sprite paints: those blobs are not thoughts the
        /// player is fighting but the SCREENS S5 statement «экран целиком закрыт мыслями», and a
        /// wallpaper that measured its own hatching would report the lost screen as two thirds full.
        /// </summary>
        public float Ink => Wallpaper ? 1f : Mathf.Clamp01(InkShare);

        /// <summary>
        /// The size a thought is SPAWNED at: its class box (S/M/L), grown until the level's silhouette
        /// covers it — never a pixel of either side below the class.
        ///
        /// This is the floor and it is a hard one (founder, 2026-08-07): «мысли НИКОГДА не спавнить
        /// меньше класса». Growth (<see cref="GrowthScale"/>) is only ever allowed to make a thought
        /// BIGGER than this — the obvious way to animate «разрастаются» is to start small and swell
        /// into the class, and that is the one implementation that is forbidden, because it makes the
        /// first seconds of every wave weaker than the class the panel asked for.
        ///
        /// It used to take the MINIMUM of the two, which honoured the floor only for sprites that
        /// happened to be about as square as their box, and quietly broke it for every narrow one: the
        /// wine bottle spawned 42×140 in class S, a sixth of the class's area, and both the growth and
        /// the screen-coverage maths were then counted off that sliver (Codex review, 2026-08-08). The
        /// silhouette overhanging its box on the long side is the price, and it is the right one — a
        /// class is a claim about how big a thought looks.
        ///
        /// The defeat wallpaper is exempt: <see cref="ThoughtField.CoverScreen"/> sizes those blobs to
        /// CLOSE a grid cell, and a class floor over that would only push the picture the crank rubs
        /// off further out of frame.
        /// </summary>
        public Vector2 SpawnSize
        {
            get
            {
                Vector2 box = SizeOf(Strength);
                if (ArtSize.x < 1f || ArtSize.y < 1f) return box;
                if (Wallpaper) return ArtSize;
                return new Vector2(Mathf.Max(ArtSize.x, box.x), Mathf.Max(ArtSize.y, box.y));
            }
        }

        /// <summary>
        /// How much bigger than <see cref="SpawnSize"/> this thought is drawn right now — «мысли
        /// разрастаются со временем и заполняют экран» (founder, 2026-08-07).
        ///
        /// Starts at exactly 1 and climbs by <see cref="TuningConfig.ThoughtGrowthPercentPerSec"/> of
        /// the spawn size every second, up to <see cref="TuningConfig.ThoughtGrowthCap"/>. Clamped at
        /// the bottom to 1 as well as at the top: a negative growth knob must not be a way to spawn
        /// below the class.
        ///
        /// The defeat wallpaper is exempt. Those blobs are not thoughts the player is fighting — they
        /// are sized to close their cell (<see cref="ThoughtField.CoverScreen"/>), and growing them
        /// would push the wipe-me picture off the frame while the handle is rubbing it away.
        ///
        /// This supersedes the old invariant «размер жёстко зажат классом»: the class is now the
        /// STARTING size, and the ceiling is the class times the cap.
        /// </summary>
        public float GrowthScale
        {
            get
            {
                if (Wallpaper) return 1f;
                float cap = Mathf.Max(1f, TuningConfig.ThoughtGrowthCap);
                float grown = 1f + TuningConfig.ThoughtGrowthPercentPerSec * 0.01f * Mathf.Max(0f, Age);
                return Mathf.Clamp(grown, 1f, cap);
            }
        }

        /// <summary>The rectangle the thought occupies right now: its class box, grown by its age.</summary>
        public Vector2 Size
        {
            get
            {
                float k = GrowthScale;
                Vector2 spawn = SpawnSize;
                return k <= 1f ? spawn : spawn * k;
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
