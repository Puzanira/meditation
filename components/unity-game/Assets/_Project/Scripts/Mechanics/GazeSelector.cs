using System.Collections.Generic;
using Meditation.Tuning;
using UnityEngine;

namespace Meditation.Mechanics
{
    /// <summary>
    /// SCREENS.md, "Выбор детали", variant B (base): a soft gaze circle driven by a stick tilt; rest it
    /// on a detail for the dwell time and the detail is noticed.
    ///
    /// **The stick is nothing but the aim since 2026-08-07** («джойстик — прицеливаешься», founder).
    /// The отгон moved to the height sensors, so the two gestures that used to share this stick no
    /// longer share anything: there is no «резкая смена направления» to tell apart from a tilt, and
    /// therefore nothing to freeze either. The <c>frozen</c> parameter went with it — a gaze that
    /// stops dead is a thing the player cannot explain when nothing on the stick caused it.
    /// </summary>
    public sealed class GazeSelector
    {
        /// <summary>
        /// The circle's radius, design px — a [tune] since 2026-08-07, not a constant.
        ///
        /// SCREENS fixed it at 90 for a greybox frame whose details were 50 px squares. On the art
        /// plates the founder asked for it «крупнее и заметнее», and «крупнее» is not only a look: the
        /// radius is what the hit test below uses, so a bigger circle is also a more forgiving aim.
        /// One number therefore, read live, rather than a constant in the rules and a slider in the
        /// view that would let the two drift apart.
        /// </summary>
        public static float Radius => Mathf.Max(10f, TuningConfig.GazeRadiusPx);

        /// <summary>The radius SCREENS drew, kept for the stand's own layout numbers.</summary>
        public const float ScreensRadius = 90f;

        /// <summary>Gaze centre in design px.</summary>
        public Vector2 Position = new Vector2(960f, 540f);

        /// <summary>
        /// How far down the gaze may travel. The stand's greybox details all live in the scene zone,
        /// so 810 was enough there — but the art drop puts real details in the foreground strip (the
        /// dandelion at y 958, the fish at 991, the mouse at 1029), and a gaze that stops at 810 simply
        /// cannot reach them. Levels raise this to the full screen; the stand keeps the scene zone.
        /// </summary>
        public float MaxY = LevelOneData.SceneHeight;

        /// <summary>How long the gaze has rested on <see cref="HoveredIndex"/>, seconds.</summary>
        public float Dwell { get; private set; }

        /// <summary>Detail currently under the gaze, or -1.</summary>
        public int HoveredIndex { get; private set; } = -1;

        /// <summary>True on the tick a detail became noticed.</summary>
        public bool NoticedThisTick { get; private set; }

        /// <summary>Detail noticed on this tick, or -1.</summary>
        public int NoticedIndex { get; private set; } = -1;

        /// <summary>0..1 dwell arc for the view.</summary>
        public float DwellProgress01 =>
            Mathf.Clamp01(Dwell / Mathf.Max(0.01f, TuningConfig.GazeDwellSeconds));

        public void Reset()
        {
            Position = new Vector2(960f, 540f);
            Dwell = 0f;
            HoveredIndex = -1;
            NoticedIndex = -1;
            NoticedThisTick = false;
        }

        /// <param name="stick">Joystick vector (Y up).</param>
        /// <param name="deltaTime">Frame time.</param>
        /// <param name="anchors">
        /// One point per detail, design px — what the whole test used to be, and what now only breaks
        /// ties between shapes the circle touches at once.
        /// </param>
        /// <param name="selectable">Per-detail: may it still be noticed (not collected / not covered)?</param>
        /// <param name="shapes">
        /// The rectangle each detail is DRAWN in, design px, or null where a composition never measured
        /// one (the greybox stand, the rule tests) — then the anchor is the whole of the detail.
        /// </param>
        public void Tick(Vector2 stick, float deltaTime, IReadOnlyList<Vector2> anchors,
            IReadOnlyList<bool> selectable, IReadOnlyList<Rect> shapes = null)
        {
            NoticedThisTick = false;
            NoticedIndex = -1;
            if (deltaTime <= 0f) return;

            // Design space grows downwards, so the stick's Y is inverted here.
            Position += new Vector2(stick.x, -stick.y) * (TuningConfig.GazeSpeedPxPerSec * deltaTime);
            Position.x = Mathf.Clamp(Position.x, 0f, ThoughtField.ScreenWidth);
            Position.y = Mathf.Clamp(Position.y, 0f, MaxY);

            // A detail is noticed when the circle touches the detail — ANY of it (founder, 2026-09-22:
            // «самолётик надо чтобы ловился во всей площади, а не только в центре самого самолётика»).
            //
            // Until now the test was the distance to the ANCHOR, i.e. to one point of a drawing, and on
            // a long thin silhouette that is most of the detail not answering at all: level 1's plane is
            // 484 × 84 px with its ink centre at 0.25 of the box, so with r = 130 the tail and the far
            // wingtip were 100+ px outside the circle while the player's aim was visibly ON them. The
            // rule is now the drawn RECTANGLE, which is what every other part of the game already treats
            // as «the detail» (the hint obstacles, the dragged-sprite margin, the cover fit).
            //
            // Box and not the ink itself: the sprites are trimmed to their alpha bounds on import
            // (LevelCatalog «Размеры»), so the rectangle IS roughly the silhouette, and the founder asked
            // for площадь rather than for pixel-exact edges.
            int hovered = -1;
            float reach = Radius * Radius;
            float best = float.MaxValue;
            float bestAtAnchor = float.MaxValue;
            for (int i = 0; i < anchors.Count; i++)
            {
                if (selectable != null && i < selectable.Count && !selectable[i]) continue;

                float toAnchor = (anchors[i] - Position).sqrMagnitude;
                float d = shapes != null && i < shapes.Count
                    ? SqrDistanceTo(shapes[i], Position)
                    : toAnchor;
                if (d > reach) continue;

                // Nearest by the SHAPE, as the founder's rule says — and two shapes the circle stands on
                // at once are both at distance zero, so the anchor breaks the tie: of two overlapping
                // drawings the one whose ink is nearer is the one the player is looking at.
                if (d > best || (d == best && toAnchor >= bestAtAnchor)) continue;
                best = d;
                bestAtAnchor = toAnchor;
                hovered = i;
            }

            if (hovered != HoveredIndex)
            {
                HoveredIndex = hovered;
                Dwell = 0f;
                return;
            }

            if (hovered < 0) return;

            Dwell += deltaTime;
            if (Dwell < TuningConfig.GazeDwellSeconds) return;

            Dwell = 0f;
            NoticedThisTick = true;
            NoticedIndex = hovered;
        }

        /// <summary>
        /// Squared distance from <paramref name="point"/> to the nearest place on <paramref name="rect"/>
        /// — zero anywhere inside it. Design space, so <c>yMin</c> is the rectangle's TOP; the arithmetic
        /// does not care which way the axis runs.
        /// </summary>
        public static float SqrDistanceTo(Rect rect, Vector2 point)
        {
            float dx = Mathf.Max(rect.xMin - point.x, 0f, point.x - rect.xMax);
            float dy = Mathf.Max(rect.yMin - point.y, 0f, point.y - rect.yMax);
            return dx * dx + dy * dy;
        }

        /// <summary>Does the aim circle standing at <paramref name="centre"/> touch this rectangle?</summary>
        public static bool CircleTouches(Rect rect, Vector2 centre) =>
            SqrDistanceTo(rect, centre) <= Radius * Radius;
    }
}
