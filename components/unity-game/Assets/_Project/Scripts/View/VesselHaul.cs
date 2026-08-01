using System.Collections.Generic;
using UnityEngine;

namespace Meditation.View
{
    /// <summary>
    /// Where the haul sits INSIDE a vessel — one rule, used by the level (a detail landing in the
    /// briefcase), by the victory tableau and by the finale's row of five.
    ///
    /// It exists because the first version was a single row: with ten details the row ran wider than
    /// the briefcase itself (the dandelion ended up 45 px to its left), an 802 px vine squeezed into a
    /// row slot became a meaningless 60 px dash, and on the finale's mug a flower sat on the rim. The
    /// canon is «деталь появляется ВНУТРИ сосуда» (SCREENS «Сбор»), so containment is the invariant
    /// here, and it is arithmetic — a grid inside a box that is inside the vessel, with every item
    /// capped at the size it had in the scene, so nothing is ever bigger inside the vessel than it was
    /// on the street.
    /// </summary>
    public static class VesselHaul
    {
        /// <summary>How much of the vessel's width the haul may use.</summary>
        public const float WidthShare = 0.72f;

        /// <summary>…and of its height. The rest is the vessel's own body: lid, handle, lock, rim.</summary>
        public const float HeightShare = 0.5f;

        /// <summary>The haul sits a little BELOW the middle — vessels open at the top.</summary>
        public const float DownShare = 0.06f;

        /// <summary>Cell padding, so neighbours never touch.</summary>
        private const float CellFill = 0.86f;

        /// <summary>
        /// How much of the vessel's measured solid box the haul is allowed to touch. The edge of a
        /// silhouette is a soft antialiased ramp, not a wall, so the box the items sit in stops short
        /// of it — the invariant being defended is «каждая ячейка на 100 % внутри силуэта».
        /// </summary>
        public const float SolidInset = 0.94f;

        /// <summary>The box a vessel with no measured silhouette falls back to (the greybox stand).</summary>
        public static Rect DefaultSolid =>
            new Rect(0.5f - WidthShare * 0.5f, 0.5f + DownShare - HeightShare * 0.5f,
                WidthShare, HeightShare);

        /// <summary>
        /// The box the haul lives in, in the vessel's own local space (UI axes: +Y up, origin at the
        /// vessel's centre).
        /// </summary>
        /// <param name="solid">
        /// The part of the vessel's rectangle that is solid vessel, in fractions of it and with Y
        /// counted DOWN from its top (<see cref="Mechanics.LevelDefinition.VesselSolid"/>). The haul is
        /// centred on THAT box rather than on the rectangle, because a rectangle is not a silhouette:
        /// the mug's includes its steam and its handle, and a haul centred on it hung half-way off.
        /// </param>
        public static Rect InnerBox(Vector2 vesselSize, Rect solid)
        {
            if (solid.width <= 0.01f || solid.height <= 0.01f) solid = DefaultSolid;

            float wShare = Mathf.Min(solid.width * SolidInset, WidthShare);
            float hShare = Mathf.Min(solid.height * SolidInset, HeightShare);

            float w = Mathf.Max(1f, vesselSize.x * wShare);
            float h = Mathf.Max(1f, vesselSize.y * hShare);

            // Design space counts Y down from the rectangle's top; the vessel's local UI space counts
            // it up from its centre.
            float centreY = (0.5f - solid.center.y) * vesselSize.y;
            float centreX = (solid.center.x - 0.5f) * vesselSize.x;
            return new Rect(centreX - w * 0.5f, centreY - h * 0.5f, w, h);
        }

        /// <summary>The greybox stand's own vessel, which never measured a silhouette.</summary>
        public static Rect InnerBox(Vector2 vesselSize) => InnerBox(vesselSize, DefaultSolid);

        /// <summary>
        /// Lay <paramref name="items"/> out inside <paramref name="vesselSize"/>. Each item is anchored
        /// to the vessel's centre, so a tableau that lifts the vessel to ×2 carries the haul with it.
        /// </summary>
        /// <param name="sceneSizes">
        /// The size each item had in the scene, or null. A detail may shrink to fit the vessel, never
        /// grow: sticking a briefcase-sized sticker over the lock is what the row layout used to do.
        /// </param>
        public static void Layout(IReadOnlyList<RectTransform> items, Vector2 vesselSize,
            IReadOnlyList<Vector2> sceneSizes = null)
        {
            Layout(items, vesselSize, DefaultSolid, sceneSizes);
        }

        /// <inheritdoc cref="Layout(IReadOnlyList{RectTransform},Vector2,IReadOnlyList{Vector2})"/>
        /// <param name="solid">The vessel's solid box — see <see cref="InnerBox(Vector2,Rect)"/>.</param>
        public static void Layout(IReadOnlyList<RectTransform> items, Vector2 vesselSize, Rect solid,
            IReadOnlyList<Vector2> sceneSizes = null)
        {
            if (items == null || items.Count == 0) return;

            int count = items.Count;
            Rect box = InnerBox(vesselSize, solid);

            int columns = ColumnsFor(count, box);
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
            float cellW = box.width / columns;
            float cellH = box.height / rows;
            float side = Mathf.Max(6f, Mathf.Min(cellW, cellH) * CellFill);

            for (int i = 0; i < count; i++)
            {
                RectTransform rt = items[i];
                if (rt == null) continue;

                int row = i / columns;
                int column = i % columns;
                int inThisRow = Mathf.Min(columns, count - row * columns);

                float itemSide = side;
                if (sceneSizes != null && i < sceneSizes.Count)
                {
                    float scene = Mathf.Max(sceneSizes[i].x, sceneSizes[i].y);
                    if (scene > 1f) itemSide = Mathf.Min(itemSide, scene);
                }

                // Rows are centred on their own count, so a last row of one sits in the middle.
                float x = box.center.x + (column - (inThisRow - 1) * 0.5f) * cellW;
                float y = box.center.y - (row - (rows - 1) * 0.5f) * cellH;

                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(itemSide, itemSide);
                rt.anchoredPosition = new Vector2(x, y);
            }
        }

        /// <summary>Columns that keep the cells as square as the box allows.</summary>
        public static int ColumnsFor(int count, Rect box)
        {
            if (count <= 1) return 1;
            float aspect = box.width / Mathf.Max(1f, box.height);
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count * aspect));
            return Mathf.Clamp(columns, 1, count);
        }
    }
}
