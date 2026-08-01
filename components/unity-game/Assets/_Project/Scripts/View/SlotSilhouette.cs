using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Meditation.View
{
    /// <summary>
    /// The dark shape a detail leaves in its HUD slot — and the outline it keeps once it is collected.
    ///
    /// Tinting the sprite grey, which is what the slots did before, is not a silhouette: an Image's
    /// colour MULTIPLIES, so what reaches the plate is the sprite's own alpha, and the drop's alpha is
    /// not a shape. The two clouds are drawn at alpha 0.64 and the puddle at 0.55, so their «silhouette»
    /// was a pale smear (2.7 % of the slot's pixels differed from the plate by more than 40 of 255, and
    /// never by more than 64); the vine is 111×1603, so fitted into a 58 px box it is a four-pixel
    /// splinter — 1.4 % ink. Six of the thirty-four details were unreadable tiles at a metre, in the HUD
    /// row and in the victory row alike.
    ///
    /// So the slot draws the sprite <see cref="Taps"/>+1 times in one flat ink, each copy nudged around
    /// a small circle. Stacking hardens what the drop left soft — k overlapping copies composite to
    /// 1−(1−a)^k, so alpha 0.25 becomes 0.92 — and the ring of offsets DILATES what the drop left thin,
    /// which is the only thing that can make a four-pixel splinter visible in a 58 px box. The radius is
    /// per-sprite: fat shapes get the 2 px that reads as an outline behind the collected picture, thin
    /// ones get up to 6.
    ///
    /// Dilation buys presence, not legibility: it can make a hairline visible without making it a
    /// vine. Where the sprite is so long that its own detail aliases away at slot size, the icon is a
    /// FRAGMENT of it instead (<see cref="Mechanics.ArtDetail.IconCrop"/>) and this stack then hardens
    /// that fragment — the two things solve different halves of the same slot.
    ///
    /// Measured over all 34 details of all five levels, in both states: ink ≥ 14 % of the slot and a
    /// contrast to the plate ≥ 159 of 255. <c>GameScreenshotTests</c> re-measures it off the rendered
    /// frame, because this is a claim about a picture.
    /// </summary>
    public sealed class SlotSilhouette
    {
        /// <summary>Copies around the ring; with the centre one that is <c>Taps + 1</c> images.</summary>
        public const int Taps = 8;

        /// <summary>Ink of an uncollected slot: dark enough to read on the 0.82 white plate.</summary>
        public static readonly Color Ink = new Color(0.16f, 0.17f, 0.19f, 1f);

        /// <summary>The thinnest a shape may get before it is dilated at all.</summary>
        public const float MinRadius = 2f;

        /// <summary>…and the fattest it is allowed to grow, so a silhouette never becomes a square.</summary>
        public const float MaxRadius = 6f;

        private readonly List<Image> _copies = new List<Image>();

        private SlotSilhouette()
        {
        }

        public IReadOnlyList<Image> Copies => _copies;

        /// <summary>
        /// How far the copies are pushed apart, in slot pixels: the narrower the shape comes out inside
        /// the slot, the more it needs. A 58 px moon needs 2 (an outline), a 4 px vine needs 6.
        /// </summary>
        public static float RadiusFor(Sprite sprite, float inner)
        {
            if (sprite == null) return MinRadius;

            Vector2 native = sprite.rect.size;
            if (native.x < 1f || native.y < 1f) return MinRadius;

            float fit = Mathf.Min(inner / native.x, inner / native.y);
            float thinnest = Mathf.Min(native.x, native.y) * fit;
            return Mathf.Clamp(Mathf.Round((inner - thinnest) / 12f), MinRadius, MaxRadius);
        }

        /// <summary>
        /// Build the stack inside <paramref name="slot"/>. Every copy fills the slot's inner box, so the
        /// dilation has room to spread — an Image sized to the fitted shape would clip it away.
        /// </summary>
        public static SlotSilhouette Build(Transform slot, string name, Sprite sprite, float padding,
            float inner, float radiusOverride = 0f)
        {
            var built = new SlotSilhouette();
            float radius = radiusOverride > 0f ? radiusOverride : RadiusFor(sprite, inner);

            for (int i = 0; i <= Taps; i++)
            {
                Vector2 offset = Vector2.zero;
                if (i > 0)
                {
                    float angle = i * 2f * Mathf.PI / Taps;
                    offset = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                }

                Image copy = Ui.NewImage(slot, name + "_" + i);
                copy.sprite = sprite;
                copy.preserveAspect = true;
                copy.color = Ink;
                copy.raycastTarget = false;

                RectTransform rt = copy.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(padding + offset.x, padding + offset.y);
                rt.offsetMax = new Vector2(-padding + offset.x, -padding + offset.y);

                built._copies.Add(copy);
            }

            return built;
        }
    }
}
