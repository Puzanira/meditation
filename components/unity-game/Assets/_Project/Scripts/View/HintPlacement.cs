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
        /// How much further than the nearest clear spot the search may go to ALSO keep the arrow off the
        /// art. The plate is the hard rule; the arrow is a preference, and one that must not be allowed
        /// to walk the hint into the far corner of the frame — at which point it stops being «рядом».
        /// </summary>
        private const float ArrowDetourBudget = 220f;

        /// <summary>
        /// The nearest spot to <paramref name="target"/> where a plate of <paramref name="size"/> covers
        /// none of <paramref name="obstacles"/> and stays inside the frame.
        ///
        /// Ties are broken upwards (<paramref name="preferDegrees"/> = −90): the mock stands its cards
        /// above what they point at, and the row of slots is the only thing in the way up there.
        /// </summary>
        /// <param name="arrowBlockers">
        /// Rectangles the arrow should avoid crossing as well — the details and the vessel. Whatever
        /// contains the target is skipped: an arrow that points AT a detail must reach it.
        /// </param>
        public static Vector2 Beside(Vector2 size, Vector2 target, IReadOnlyList<Rect> obstacles,
            IReadOnlyList<Rect> arrowBlockers = null, float preferDegrees = -90f)
        {
            bool haveFallback = false;
            Vector2 fallback = target;
            float fallbackRadius = 0f;

            for (float radius = FirstRadius; radius <= LastRadius; radius += RadiusStep)
            {
                if (haveFallback && radius > fallbackRadius + ArrowDetourBudget) break;

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

                    if (!haveFallback)
                    {
                        haveFallback = true;
                        fallback = spot;
                        fallbackRadius = radius;
                    }

                    if (ArrowIsClear(spot, size, target, arrowBlockers)) return spot;
                }
            }

            return haveFallback ? fallback : target;
        }

        /// <summary>
        /// Where the «ТАЩИ» plate stands beside a detail that is MOVING: off to the side of the thread,
        /// outside the progress ring, and on the same side it was on last frame.
        ///
        /// The old placement put it once, in the detail's starting position, and left it there — so the
        /// detail drove out from under its own label and the plate sat across the progress ring, which
        /// is the only feedback the crank has (design gate, 2026-08-07). SCREENS §Обучение п.2 says the
        /// hint is «рядом с едущей деталью», and «едущей» is the whole word.
        ///
        /// The distance is the ring plus the plate's own half-diagonal, so no orientation of the plate
        /// can reach back into the ring; the thread is cleared by testing the segment itself rather than
        /// by trusting the angle.
        /// </summary>
        /// <param name="lastOffsetDegrees">
        /// The angle used last frame, relative to the thread, and the first candidate tried this frame —
        /// without it the plate hops from one side of the detail to the other as the geometry shifts by
        /// a pixel. Updated in place.
        /// </param>
        public static Vector2 BesideTheThread(Vector2 size, Vector2 anchor, Vector2 vessel,
            float ringRadius, IReadOnlyList<Rect> obstacles, ref float lastOffsetDegrees)
        {
            Vector2 along = vessel - anchor;
            float baseDegrees = along.sqrMagnitude > 1e-4f
                ? Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg
                : 0f;

            float reach = ringRadius + Clearance * 2f + size.magnitude * 0.5f;

            float bestPenalty = float.MaxValue;
            Vector2 best = anchor;
            float bestOffset = lastOffsetDegrees;
            bool haveBest = false;

            for (int s = 0; s < ThreadScales.Length; s++)
            {
                float radius = reach * ThreadScales[s];

                for (int i = -1; i < AsideTheThread.Length; i++)
                {
                    float offset = i < 0 ? lastOffsetDegrees : AsideTheThread[i];
                    float radians = (baseDegrees + offset) * Mathf.Deg2Rad;
                    var spot = new Vector2(
                        anchor.x + Mathf.Cos(radians) * radius,
                        anchor.y + Mathf.Sin(radians) * radius);

                    Rect plate = Centred(spot, size);
                    if (!InsideFrame(plate)) continue;
                    if (SegmentHits(anchor, vessel, Inflate(plate, Clearance * 2f))) continue;

                    float penalty = OverlapArea(plate, obstacles);
                    if (penalty <= 0f)
                    {
                        lastOffsetDegrees = offset;
                        return spot;
                    }

                    if (haveBest && penalty >= bestPenalty) continue;
                    haveBest = true;
                    bestPenalty = penalty;
                    best = spot;
                    bestOffset = offset;
                }
            }

            lastOffsetDegrees = bestOffset;
            return haveBest ? best : anchor;
        }

        /// <summary>Angles tried at each radius, ordered by how far they are from the preferred one.</summary>
        private static readonly float[] AnglesAround = BuildAnglesAround();

        /// <summary>…and the ones the «ТАЩИ» plate may take beside the thread — perpendicular first.</summary>
        private static readonly float[] AsideTheThread = BuildAsideTheThread();

        /// <summary>If nothing fits at arm's length, step back — never step ONTO the thread.</summary>
        private static readonly float[] ThreadScales = { 1f, 1.25f, 1.55f };

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

        private static float[] BuildAsideTheThread()
        {
            var angles = new List<float>();
            for (float step = 0f; step <= 90f; step += 10f)
            {
                angles.Add(90f + step);
                angles.Add(-90f - step);
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

        private static float OverlapArea(Rect plate, IReadOnlyList<Rect> obstacles)
        {
            if (obstacles == null) return 0f;
            float total = 0f;
            for (int i = 0; i < obstacles.Count; i++)
            {
                Rect o = obstacles[i];
                float w = Mathf.Min(plate.xMax, o.xMax) - Mathf.Max(plate.xMin, o.xMin);
                float h = Mathf.Min(plate.yMax, o.yMax) - Mathf.Max(plate.yMin, o.yMin);
                if (w > 0f && h > 0f) total += w * h;
            }
            return total;
        }

        /// <summary>
        /// The curve <see cref="HintArrow"/> actually draws — sampled, because it is a quadratic that
        /// bulges 18 % of its own length sideways, and a straight segment through the same two points
        /// misses what the bend passes over.
        /// </summary>
        public static Vector2[] ArrowCurve(Vector2 plateCentre, Vector2 size, Vector2 target,
            float standoff = 50f)
        {
            Vector2 from = EdgeTowards(plateCentre, size, target);
            Vector2 approach = target - from;
            float length = approach.magnitude;
            if (length < 1e-3f) return new[] { from, from };

            Vector2 to = from + approach / length * Mathf.Max(20f, length - Mathf.Max(0f, standoff));
            Vector2 straight = to - from;
            Vector2 perpendicular = new Vector2(-straight.y, straight.x).normalized;
            Vector2 control = (from + to) * 0.5f + perpendicular * (straight.magnitude * 0.18f);

            const int samples = 9;
            var curve = new Vector2[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (samples - 1f);
                float inv = 1f - t;
                curve[i] = inv * inv * from + 2f * inv * t * control + t * t * to;
            }
            return curve;
        }

        private static bool ArrowIsClear(Vector2 spot, Vector2 size, Vector2 target,
            IReadOnlyList<Rect> blockers)
        {
            if (blockers == null || blockers.Count == 0) return true;

            Vector2[] curve = ArrowCurve(spot, size, target);
            for (int b = 0; b < blockers.Count; b++)
            {
                Rect blocker = blockers[b];
                // The thing the arrow POINTS at cannot also be something it must not touch.
                if (blocker.Contains(target)) continue;
                for (int i = 0; i < curve.Length - 1; i++)
                    if (SegmentHits(curve[i], curve[i + 1], blocker)) return true;
            }
            return false;
        }

        private static Vector2 EdgeTowards(Vector2 centre, Vector2 size, Vector2 target)
        {
            Vector2 direction = target - centre;
            if (direction.sqrMagnitude < 1f) return centre;

            float halfW = Mathf.Max(1f, size.x * 0.5f);
            float halfH = Mathf.Max(1f, size.y * 0.5f);
            float scaleX = Mathf.Abs(direction.x) > 0.001f ? halfW / Mathf.Abs(direction.x) : float.MaxValue;
            float scaleY = Mathf.Abs(direction.y) > 0.001f ? halfH / Mathf.Abs(direction.y) : float.MaxValue;
            return centre + direction * Mathf.Min(scaleX, scaleY);
        }

        /// <summary>Does the segment from <paramref name="a"/> to <paramref name="b"/> touch the rect?</summary>
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
