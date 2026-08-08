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

        /// <summary>
        /// A track of the drop's sound folder — <c>audio/level-3</c>, <c>audio/meditation</c>,
        /// <c>audio/thoughts</c> (MECHANICS §8).
        ///
        /// Same contract as <see cref="Get"/>: cached for the session, missing keys reported once. The
        /// clips are imported streaming and not preloaded, so this call costs a lookup rather than the
        /// 18 MB the library's track weighs — the load happens when the source starts playing.
        /// </summary>
        public static AudioClip Clip(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (Clips.TryGetValue(key, out AudioClip cached)) return cached;

            AudioClip clip = Resources.Load<AudioClip>(LevelCatalog.ArtRoot + key);
            if (clip == null && Reported.Add(key))
                Debug.LogError("[Meditation] Звуковой трек не найден: " + LevelCatalog.ArtRoot + key);

            Clips[key] = clip;
            return clip;
        }

        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        /// <summary>Drop every cached sprite (used between test scenes).</summary>
        public static void Clear()
        {
            Cache.Clear();
            Clips.Clear();
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
        /// The size an art thought is drawn at: its own silhouette aspect scaled until it COVERS the
        /// S/M/L box SCREENS.md fixes (180×140 / 280×220 / 420×320) — both sides at least the box's.
        ///
        /// Cover, not contain, and that is the whole point (Codex review, 2026-08-08). Contain was the
        /// obvious reading of «силуэт вписан в класс», and on a round sprite it is the same picture. On
        /// a narrow one it is not: the wine bottle's canvas is 454×1521, so containing it inside the S
        /// box gave a 42×140 sliver — a sixth of the class's area — and the guitar 54×140. That is
        /// exactly the thing the founder forbade («мысли НИКОГДА не спавнить меньше класса»), only
        /// hidden one level below <see cref="Thought.SpawnSize"/>, where the class-floor test could not
        /// see it: growth and screen coverage were both being counted off the sliver.
        ///
        /// The cost is that a narrow silhouette now stands TALLER than its box (the bottle is 180×603
        /// in class S). That is the correct trade: the class is a statement about how big a thought
        /// LOOKS, and a bottle drawn 42 px wide does not look like anything. What keeps the arithmetic
        /// honest is that coverage is no longer counted off the rectangle either — see
        /// <see cref="InkShareOf"/> and <see cref="Meditation.Mechanics.Thought.Ink"/>.
        /// </summary>
        public static Vector2 FitThought(string key, ThoughtStrength strength)
        {
            Vector2 box = Thought.SizeOf(strength);
            Sprite sprite = Get(key);
            if (sprite == null) return box;

            Vector2 native = sprite.rect.size;
            if (native.x <= 1f || native.y <= 1f) return box;

            float factor = Mathf.Max(box.x / native.x, box.y / native.y);
            return native * factor;
        }

        /// <summary>
        /// Hand a freshly spawned thought its art metrics — the hook <see cref="ThoughtField.ArtFitter"/>
        /// is given by the game (the greybox stand leaves it null and gets plain class boxes).
        ///
        /// One call rather than two because the two numbers are one fact about the same sprite, and a
        /// thought that got its size from the drop but kept the greybox's solid-rectangle ink would be
        /// counted as covering six times the screen it paints.
        /// </summary>
        public static void FitThought(Thought thought)
        {
            if (thought == null) return;
            thought.ArtSize = FitThought(thought.Label, thought.Strength);
            thought.InkShare = InkShareOf(thought.Label);
        }

        /// <summary>
        /// How much of its own rectangle a thought's sprite actually PAINTS, 0…1 — measured off each
        /// PNG as the share of pixels with alpha ≥ 128, the same way
        /// <see cref="Meditation.Mechanics.ArtDetail.AlphaCentroid"/> is measured off its own.
        ///
        /// The thoughts of the 2026-08-05 drop are black marker hatching, not filled silhouettes —
        /// «а scribble has no fill» (<see cref="ArtThoughtView"/>), and the numbers below say how
        /// little: the question marks of level 5 paint 15 % of their canvas, the banknote of level 2
        /// 64 %. Screen coverage that counts the whole rectangle is therefore counting mostly the
        /// level showing through the gaps, which is the opposite of what the defeat threshold means.
        ///
        /// Baked rather than sampled because the drop imports unreadable (a readable copy of 25
        /// canvases up to 2048² is ~175 MB of system memory in a cabinet that hosts seven games), and
        /// every canvas is trimmed to its content on import, so the sprite rect IS the ink's bounding
        /// box and one number per sprite is the whole story. The numbers are re-measured against the
        /// PNGs by <c>LevelCatalogTests.EveryThoughtsInkShare_MatchesItsPng</c>, so a re-drop cannot
        /// leave them stale.
        ///
        /// An unknown key answers 1 — the greybox stand's blobs are solid rectangles and are meant to
        /// count as such.
        /// </summary>
        public static float InkShareOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return 1f;
            return InkShares.TryGetValue(key, out float share) ? share : 1f;
        }

        /// <summary>Alpha threshold a pixel has to clear to count as ink (also used by the guard test).</summary>
        public const float InkAlpha = 128f / 255f;

        /// <summary>Every key <see cref="InkShareOf"/> knows — the guard test walks these.</summary>
        public static IEnumerable<string> InkKeys => InkShares.Keys;

        private static readonly Dictionary<string, float> InkShares = new Dictionary<string, float>
        {
            { "L1/thoughts/cat", 0.4972f },
            { "L1/thoughts/guitar", 0.4610f },
            { "L1/thoughts/wine-bottle", 0.3576f },
            { "L1/thoughts/butterfly", 0.4933f },
            { "L1/thoughts/umbrella", 0.3330f },

            { "L2/thoughts/banknote", 0.6371f },
            { "L2/thoughts/cake", 0.4782f },
            { "L2/thoughts/glasses", 0.2529f },
            { "L2/thoughts/palm", 0.4081f },
            { "L2/thoughts/paperplane", 0.3394f },

            { "L3/thoughts/phone-call", 0.2791f },
            { "L3/thoughts/soccer-ball", 0.5184f },
            { "L3/thoughts/exclamations", 0.2531f },
            { "L3/thoughts/beetle", 0.3918f },
            { "L3/thoughts/sock", 0.4294f },

            { "L4/thoughts/alarm-clock", 0.3432f },
            { "L4/thoughts/burger", 0.4944f },
            { "L4/thoughts/baby-head", 0.3360f },
            { "L4/thoughts/mushroom", 0.3702f },
            { "L4/thoughts/speech-bubble", 0.4678f },

            { "L5/thoughts/teddy-bear", 0.5212f },
            { "L5/thoughts/empty-bed", 0.3607f },
            { "L5/thoughts/cake", 0.5499f },
            { "L5/thoughts/question-marks", 0.1524f },
            { "L5/thoughts/coin-purse", 0.5848f }
        };

        // ---- how THICK a detail is drawn ---------------------------------------------------------

        /// <summary>
        /// The mean stroke thickness of a detail's own drawing, in the PNG's pixels — twice its ink
        /// area over its ink perimeter.
        ///
        /// That formula is the thickness of a STROKE, which is the quantity the neon rim has to respect:
        /// for a bar of width w and length L it comes out at w exactly (area wL, perimeter ≈ 2L), and for
        /// a round blob it comes out at the radius, i.e. large enough to mean «no constraint». It is also
        /// invariant to the empty margin around the drawing, so a re-export that trims differently
        /// changes nothing.
        ///
        /// Why it is needed: the rim is a dilation of the sprite's alpha, and a dilation wider than the
        /// stroke it is drawn around does not outline the shape, it FILLS it. At the shipped 6 px the
        /// office paperclip (a 10.7 px wire once drawn at 40×56) came out a solid turquoise blob and the
        /// librarian's glasses lost both lenses — the design skeptic's finding of 2026-08-08. See
        /// <see cref="Meditation.View.LevelView.RimRadiusPx"/> for what is done with the number.
        ///
        /// Baked for the same reason <see cref="InkShareOf"/> is: the drop imports unreadable. Re-measured
        /// off the PNGs by <c>LevelCatalogTests.EveryDetailsStrokeThickness_MatchesItsPng</c>.
        ///
        /// An unknown key answers <see cref="float.MaxValue"/> — «no constraint», so a sprite nobody has
        /// measured is drawn exactly as it was before this table existed.
        /// </summary>
        public static float StrokeThicknessOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return float.MaxValue;
            return StrokeThickness.TryGetValue(key, out float px) ? px : float.MaxValue;
        }

        /// <summary>Every key <see cref="StrokeThicknessOf"/> knows — the guard test walks these.</summary>
        public static IEnumerable<string> StrokeKeys => StrokeThickness.Keys;

        private static readonly Dictionary<string, float> StrokeThickness = new Dictionary<string, float>
        {
            { "L1/objects/airplane", 35.0f },
            { "L1/objects/bucket", 158.8f },
            { "L1/objects/seagull-large", 11.4f },
            { "L1/objects/seashell", 51.3f },
            { "L1/objects/shark-fin", 24.8f },
            { "L1/objects/ship", 35.1f },

            { "L2/objects/flower", 29.9f },
            { "L2/objects/mug", 125.8f },
            { "L2/objects/paperclip", 21.1f },
            { "L2/objects/slippers", 33.0f },
            { "L2/objects/snail-toy", 89.1f },
            { "L2/objects/sticky-note", 44.0f },

            { "L3/objects/ad-poster", 133.0f },
            { "L3/objects/fish", 99.1f },
            { "L3/objects/glove", 90.8f },
            { "L3/objects/goose", 125.1f },
            { "L3/objects/newspaper", 121.1f },
            { "L3/objects/sticker", 53.2f },

            { "L4/objects/backpack", 302.9f },
            { "L4/objects/bust", 145.2f },
            { "L4/objects/cat", 111.0f },
            { "L4/objects/glasses", 14.2f },
            { "L4/objects/lamp", 43.2f },
            { "L4/objects/mouse-book", 76.8f },
            { "L4/objects/open-book", 113.1f },
            { "L4/objects/sun-circle", 98.4f },
            { "L4/objects/valentine", 58.5f },

            { "L5/objects/bat", 37.4f },
            { "L5/objects/bird", 31.7f },
            { "L5/objects/briefcase", 196.6f },
            { "L5/objects/cloud-middle", 23.6f },
            { "L5/objects/curtains", 37.3f },
            { "L5/objects/dandelion", 17.2f },
            { "L5/objects/flowerpot", 31.5f },
            { "L5/objects/moon", 123.0f },
            { "L5/objects/vine", 15.5f }
        };
    }
}
