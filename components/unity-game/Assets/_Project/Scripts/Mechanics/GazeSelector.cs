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
        /// <param name="targets">Detail centres in design px.</param>
        /// <param name="selectable">Per-detail: may it still be noticed (not collected / not covered)?</param>
        public void Tick(Vector2 stick, float deltaTime, IReadOnlyList<Vector2> targets,
            IReadOnlyList<bool> selectable)
        {
            NoticedThisTick = false;
            NoticedIndex = -1;
            if (deltaTime <= 0f) return;

            // Design space grows downwards, so the stick's Y is inverted here.
            Position += new Vector2(stick.x, -stick.y) * (TuningConfig.GazeSpeedPxPerSec * deltaTime);
            Position.x = Mathf.Clamp(Position.x, 0f, ThoughtField.ScreenWidth);
            Position.y = Mathf.Clamp(Position.y, 0f, MaxY);

            int hovered = -1;
            float best = Radius * Radius;
            for (int i = 0; i < targets.Count; i++)
            {
                if (selectable != null && i < selectable.Count && !selectable[i]) continue;
                float d = (targets[i] - Position).sqrMagnitude;
                if (d > best) continue;
                best = d;
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
    }
}
