using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// The row of dots under a thought: one per hit it can still take (SCREENS.md «Мысли (визуал
    /// состояния)» — «под мыслью ряд точек-пипсов»). A spent pip is an EMPTY outlined circle, not a
    /// faded dot: at museum distance a pale fill reads as «still there, just dimmer».
    ///
    /// Shared by the stand's greybox blob and the game's art silhouette, because it is the same
    /// readout of the same rule in both.
    /// </summary>
    public sealed class PipRow
    {
        public const int MaxPips = 12;
        private const float PipRadius = 9f;
        private const float PipStep = 28f;

        /// <summary>How far the backing disc sticks out past the pip, design px.</summary>
        private const float BackingHaloPx = 3f;

        private readonly List<Image> _pips = new List<Image>(MaxPips);
        private readonly List<Image> _backings = new List<Image>(MaxPips);
        private readonly RectTransform _row;

        /// <param name="parent">The blob; the row anchors to its bottom edge.</param>
        /// <param name="offsetY">How far below the blob's bottom edge the row sits, design px.</param>
        /// <param name="backing">
        /// A light disc under each pip, or null for none. The greybox stand does not need it — its
        /// blobs are pastel FILLS and the pips sit on them. The art thoughts are hatching with nothing
        /// behind it, so their pips land on the bare plate and need the same backing the strokes get
        /// (see <see cref="ArtThoughtView.BackingColour"/>).
        /// </param>
        public PipRow(Transform parent, float offsetY = -18f, Color? backing = null)
        {
            var rowGo = new GameObject("Pips", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            _row = (RectTransform)rowGo.transform;
            _row.anchorMin = new Vector2(0.5f, 0f);
            _row.anchorMax = new Vector2(0.5f, 0f);
            _row.pivot = new Vector2(0.5f, 0.5f);
            _row.sizeDelta = new Vector2(MaxPips * PipStep, PipRadius * 2f);
            _row.anchoredPosition = new Vector2(0f, offsetY);
            OffsetY = offsetY;

            // Every backing first, then every pip: siblings draw in order, so one row of discs has to
            // be complete before the row of pips starts, or pip 3's disc would sit on top of pip 2.
            if (backing.HasValue)
            {
                for (int i = 0; i < MaxPips; i++)
                {
                    Image disc = Ui.Circle(_row, "PipBacking" + i, 0f, 0f, PipRadius + BackingHaloPx,
                        backing.Value, backing.Value);
                    disc.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    disc.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    disc.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    disc.gameObject.SetActive(false);
                    _backings.Add(disc);
                }
            }

            for (int i = 0; i < MaxPips; i++)
            {
                Image pip = Ui.Circle(_row, "Pip" + i, 0f, 0f, PipRadius, Color.black, Color.black);
                pip.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                pip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                pip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                pip.gameObject.SetActive(false);
                _pips.Add(pip);
            }
        }

        public float OffsetY { get; private set; }

        public RectTransform Rect => _row;

        /// <summary>Hide the whole row (a thought that is not really on screen has no readout).</summary>
        public void SetShown(bool shown) => _row.gameObject.SetActive(shown);

        /// <summary>
        /// Nudge the row away from its default place under the blob. UI axes: +Y up, so a positive Y
        /// pulls the row up INTO the blob — which is what a blob hanging off the bottom of the frame
        /// needs if its pips are to be seen at all.
        /// </summary>
        public void SetOffset(Vector2 offset)
        {
            _shift = offset;
            _row.anchoredPosition = new Vector2(_shift.x, OffsetY + _shift.y);
        }

        private Vector2 _shift;

        /// <summary>Show <paramref name="durability"/> pips, of which <paramref name="remaining"/> are unspent.</summary>
        public void Set(int durability, int remaining, Color colour)
        {
            int total = Mathf.Clamp(durability, 0, MaxPips);
            int left = Mathf.Clamp(remaining, 0, MaxPips);
            _row.anchoredPosition = new Vector2(_shift.x, OffsetY + _shift.y);

            for (int i = 0; i < MaxPips; i++)
            {
                bool used = i < total;
                _pips[i].gameObject.SetActive(used);
                if (!used) continue;

                Vector2 at = new Vector2((i - (total - 1) * 0.5f) * PipStep, 0f);
                _pips[i].rectTransform.anchoredPosition = at;
                _pips[i].sprite = i >= left ? UiSprites.Ring : UiSprites.Circle;
                _pips[i].color = colour;

                if (_backings.Count == 0) continue;
                _backings[i].rectTransform.anchoredPosition = at;
            }

            for (int i = 0; i < _backings.Count; i++)
                _backings[i].gameObject.SetActive(i < total);
        }
    }
}
