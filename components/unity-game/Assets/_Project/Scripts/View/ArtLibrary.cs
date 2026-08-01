using System.Collections.Generic;
using Meditation.Mechanics;
using Meditation.Tuning;
using UnityEngine;

namespace Meditation.View
{
    /// <summary>
    /// Loads the art drop's sprites and remembers them for the session.
    ///
    /// The game's screens are built procedurally (no scene YAML to hold asset references), so the art
    /// is reached through <c>Resources</c>. ARCADE_INTEGRATION_CONTRACT §2 allows that only under a
    /// game-specific prefix — hence <c>Assets/_Project/Art/Resources/MeditationArt/…</c>: the files sit
    /// in the game's own Art folder, and their resource path cannot collide with another cabinet game.
    ///
    /// A missing sprite is reported once and then drawn as a plain magenta box by the caller: silence
    /// would let a level ship with an invisible detail, and one Debug.LogError per key keeps the
    /// PlayMode console assertions meaningful instead of drowning them.
    /// </summary>
    public static class ArtLibrary
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Reported = new HashSet<string>();

        /// <summary>Sprite for an art key such as <c>L1/objects/moon</c>, or null when it is missing.</summary>
        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            Sprite sprite = Resources.Load<Sprite>(LevelCatalog.ArtRoot + key);
            if (sprite == null && Reported.Add(key))
                Debug.LogError("[Meditation] Художественный ассет не найден: " +
                               LevelCatalog.ArtRoot + key);

            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// The sprite a detail is drawn as in a SLOT and in the haul — the whole thing, or the fragment
        /// the catalogue chose for it (<see cref="ArtDetail.IconCrop"/>).
        ///
        /// The scene always gets the whole sprite: this is about a 66 px square, where a ribbon fitted
        /// whole is a four-pixel column of aliasing. <c>Sprite.Create</c> only re-points UVs at a region
        /// of the same texture — no copy, no readable-texture flag — and <c>FullRect</c> keeps it that
        /// way (a tight mesh would want the pixels back).
        /// </summary>
        public static Sprite IconOf(ArtDetail detail)
        {
            Sprite whole = Get(detail.Sprite);
            if (whole == null || !detail.HasIconCrop) return whole;

            string key = detail.Sprite + "#icon";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            Rect full = whole.rect;
            Rect crop = detail.IconCrop;

            // The crop counts Y down from the sprite's top; texture space counts it up from the bottom.
            var region = new Rect(
                full.x + full.width * crop.x,
                full.y + full.height * (1f - crop.y - crop.height),
                full.width * crop.width,
                full.height * crop.height);

            Sprite icon = whole.texture == null || region.width < 1f || region.height < 1f
                ? whole
                : Sprite.Create(whole.texture, region, new Vector2(0.5f, 0.5f),
                    whole.pixelsPerUnit, 0, SpriteMeshType.FullRect);

            Cache[key] = icon;
            return icon;
        }

        /// <summary>Drop every cached sprite (used between test scenes).</summary>
        public static void Clear()
        {
            Cache.Clear();
            Reported.Clear();
        }

        /// <summary>
        /// A level's vessel as a sprite — including level 3's, whose bag is painted into the plate.
        ///
        /// The bag has no file of its own, but three screens need it as a picture: the level's victory
        /// tableau carries it to the centre at ×2, the level card shows what you are about to fill, and
        /// the finale stands all three vessels in a row. So the plate's own pixels become the sprite.
        /// <c>Sprite.Create</c> only references a region of the texture — no copy, no readable-texture
        /// flag, and the vessel on the card is literally the vessel in the level.
        /// </summary>
        public static Sprite VesselOf(LevelDefinition level)
        {
            if (level == null) return null;
            if (!level.VesselIsBaked) return Get(level.VesselSprite);

            string key = level.BackgroundSprite + "#vessel";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            Sprite crop = CropOfPlate(level);
            Cache[key] = crop;
            return crop;
        }

        private static Sprite CropOfPlate(LevelDefinition level)
        {
            Sprite plate = Get(level.BackgroundSprite);
            if (plate == null || plate.texture == null) return null;

            Texture2D texture = plate.texture;
            float scaleX = texture.width / DesignStage.DesignWidth;
            float scaleY = texture.height / DesignStage.DesignHeight;

            Vector2 centre = level.VesselCentre;
            Vector2 size = level.VesselSize;

            // Design space counts Y downwards from the top; texture space counts it up from the bottom.
            float x = (centre.x - size.x * 0.5f) * scaleX;
            float y = (DesignStage.DesignHeight - (centre.y + size.y * 0.5f)) * scaleY;
            var rect = new Rect(x, y, size.x * scaleX, size.y * scaleY);

            if (rect.xMin < 0f || rect.yMin < 0f || rect.xMax > texture.width || rect.yMax > texture.height)
                return null;

            return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        /// <summary>
        /// The size an art thought is drawn at: its own silhouette aspect, fitted INSIDE the S/M/L box
        /// SCREENS.md fixes (180×140 / 280×220 / 420×320). Contain, never cover — a thought is allowed
        /// to be smaller than its class, never bigger, because screen coverage is meant to be won by
        /// the number of thoughts rolling in and not by one blob growing into a slab.
        /// </summary>
        public static Vector2 FitThought(string key, ThoughtStrength strength)
        {
            Vector2 box = Thought.SizeOf(strength);
            Sprite sprite = Get(key);
            if (sprite == null) return box;

            Vector2 native = sprite.rect.size;
            if (native.x <= 1f || native.y <= 1f) return box;

            float factor = Mathf.Min(box.x / native.x, box.y / native.y);
            return native * factor;
        }
    }
}
