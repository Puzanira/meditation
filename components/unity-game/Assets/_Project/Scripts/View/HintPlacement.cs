using System.Collections.Generic;
using UnityEngine;

namespace Meditation.View
{
    /// <summary>
    /// Where a teaching hint is allowed to stand — plain geometry on design-space rectangles, no scene.
    ///
    /// It exists because «рядом с объектом» was an OFFSET, and an offset is a guess about a picture it
    /// cannot see. «КРУТИ РУЧКУ» was placed 230 px above the dynamo indicator and clamped back into the
    /// frame, which put a 468×88 plate straight across the shark fin and the left edge of the bucket
    /// (design gate, 2026-08-07). Приёмка п.3 — «ни один виджет не перекрывает деталь или сосуд» — is
    /// about the drawn HUD, and the gate extended it to the teaching plates: a hint that hides the thing
    /// the level is about teaches with the level switched off.
    ///
    /// So the position is SEARCHED instead of assumed: every rectangle that must not be covered comes
    /// from <see cref="Mechanics.LevelCatalog.HintObstaclesOf"/>, and the nearest spot to the target
    /// that clears all of them wins. Same arithmetic the HUD clearance test uses, and for the same
    /// reason — it is decidable before a scene exists, so a test can hold it.
    ///
    /// Half of this class went on 2026-09-22 with the arrows and the caps buttons (founder: «убрать
    /// стрелки все с экранов обучений»). What it placed then was three things — a plate, a drawn
    /// button, and a bent arrow between the button and its subject — so it also had to sample the
    /// arrow's own curve (<c>ArrowCurve</c>), to trade a longer walk for an arrow that missed the art
    /// (<c>ArrowDetourBudget</c>), and to keep the travelling «ТАЩИ» button off the thread and out of
    /// the progress ring (<c>BesideTheThread</c>). One plate that never moves needs none of it.
    /// </summary>
    public static class HintPlacement
    {
        /// <summary>Air between a hint's plate and anything it must not touch, design px.</summary>
        public const float Clearance = 8f;

        /// <summary>How close a plate may come to the edge of the frame, design px.</summary>
        public const float FrameMargin = 12f;

        /// <summary>
        /// Air left round the WHOLE sprite of the detail a hint is riding, design px.
        ///
        /// Its own rectangle is an obstacle like any other, and it is not the ink zone round the
        /// centroid: the plane is drawn with 600 px of contrail behind the fuselage, so «рядом с
        /// деталью» measured from the ink puts the plate inside the picture. Frame 18 shipped with 1 px
        /// between the plate and the trail, which reads as the trail breaking against it — a gap has to
        /// be a gap, not a hairline.
        /// </summary>
        public const float DraggedSpriteMargin = 10f;

        /// <summary>Rings the search walks outward, design px.</summary>
        private const float FirstRadius = 120f;
        private const float LastRadius = 900f;
        private const float RadiusStep = 10f;

        /// <summary>
        /// The nearest spot to <paramref name="target"/> where a plate of <paramref name="size"/> covers
        /// none of <paramref name="obstacles"/> and stays inside the frame.
        ///
        /// Ties are broken upwards (<paramref name="preferDegrees"/> = −90): the mock stands its cards
        /// above what they point at, and the row of slots is the only thing in the way up there.
        /// </summary>
        public static Vector2 Beside(Vector2 size, Vector2 target, IReadOnlyList<Rect> obstacles,
            float preferDegrees = -90f)
        {
            for (float radius = FirstRadius; radius <= LastRadius; radius += RadiusStep)
            {
                for (int i = 0; i < AnglesAround.Length; i++)
                {
                    float degrees = preferDegrees + AnglesAround[i];
                    float radians = degrees * Mathf.Deg2Rad;
                    var spot = new Vector2(
                        target.x + Mathf.Cos(radians) * radius,
                        target.y + Mathf.Sin(radians) * radius);

                    Rect plate = Centred(spot, size);
                    if (!InsideFrame(plate)) continue;
                    if (Hits(plate, obstacles, Clearance)) continue;

                    return spot;
                }
            }

            return target;
        }

        /// <summary>Angles tried at each radius, ordered by how far they are from the preferred one.</summary>
        private static readonly float[] AnglesAround = BuildAnglesAround();

        private static float[] BuildAnglesAround()
        {
            var angles = new List<float> { 0f };
            for (float step = 10f; step <= 180f; step += 10f)
            {
                angles.Add(step);
                if (step < 180f) angles.Add(-step);
            }
            return angles.ToArray();
        }

        // ---- the geometry itself ---------------------------------------------------------------------

        public static Rect Centred(Vector2 centre, Vector2 size) =>
            new Rect(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f, size.x, size.y);

        public static Rect Inflate(Rect rect, float by) =>
            new Rect(rect.xMin - by, rect.yMin - by, rect.width + by * 2f, rect.height + by * 2f);

        public static bool InsideFrame(Rect plate) =>
            plate.xMin >= FrameMargin && plate.yMin >= FrameMargin &&
            plate.xMax <= DesignStage.DesignWidth - FrameMargin &&
            plate.yMax <= DesignStage.DesignHeight - FrameMargin;

        private static bool Hits(Rect plate, IReadOnlyList<Rect> obstacles, float clearance)
        {
            if (obstacles == null) return false;
            Rect grown = Inflate(plate, clearance);
            for (int i = 0; i < obstacles.Count; i++)
                if (grown.Overlaps(obstacles[i])) return true;
            return false;
        }

        /// <summary>
        /// Does the segment from <paramref name="a"/> to <paramref name="b"/> touch the rect?
        ///
        /// What is left of the arrow geometry, and it is kept because the THREAD is still a line across
        /// the picture: the сбор beat's plate has to be clear of the line the detail travels, and that
        /// claim is the one the suite makes with this.
        /// </summary>
        public static bool SegmentHits(Vector2 a, Vector2 b, Rect rect)
        {
            float t0 = 0f;
            float t1 = 1f;
            Vector2 d = b - a;

            if (!Slab(a.x, d.x, rect.xMin, rect.xMax, ref t0, ref t1)) return false;
            if (!Slab(a.y, d.y, rect.yMin, rect.yMax, ref t0, ref t1)) return false;
            return true;
        }

        private static bool Slab(float origin, float delta, float low, float high,
            ref float t0, ref float t1)
        {
            if (Mathf.Abs(delta) < 1e-6f) return origin >= low && origin <= high;

            float a = (low - origin) / delta;
            float b = (high - origin) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }

            t0 = Mathf.Max(t0, a);
            t1 = Mathf.Min(t1, b);
            return t0 <= t1;
        }
    }
}
