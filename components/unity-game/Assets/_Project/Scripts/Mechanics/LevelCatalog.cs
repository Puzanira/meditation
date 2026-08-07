using UnityEngine;

namespace Meditation.Mechanics
{
    /// <summary>One collectable detail of a real level: its sprite and the rectangle it occupies.</summary>
    public struct ArtDetail
    {
        /// <summary>Russian name — readouts, the tutorial line, the design gate.</summary>
        public string Name;

        /// <summary>Resources key under <c>MeditationArt/</c>, e.g. <c>L1/objects/dandelion</c>.</summary>
        public string Sprite;

        /// <summary>Home position, design px, centre.</summary>
        public Vector2 Home;

        /// <summary>Drawn size in design px — the size the sprite has in the author's превью.png.</summary>
        public Vector2 Size;

        /// <summary>
        /// The fragment of the sprite that stands for this detail in a 66 px slot and in the haul, in
        /// fractions of its rectangle (Y counted DOWN from its top). Empty — the whole sprite, which is
        /// what all but two details use.
        ///
        /// A slot is a square, and fitting a ribbon into it squeezes the ribbon's whole length through
        /// 66 px: the vine is 111×1603, so it arrives as a four-pixel column carrying fourteen bends of
        /// its own stem — that is a signal well above what 66 samples of bilinear can carry, and it
        /// comes out as television interference rather than as a plant. Cropping is not «zooming in»,
        /// it is choosing what the icon is OF: one bend of the vine with its leaf and flowers, the
        /// airplane without the 600 px of contrail that made its body a smudge.
        ///
        /// It is a per-detail decision and not a rule on the aspect ratio, because most long sprites
        /// are their own silhouette all the way along — the gull is a chevron, the puddle an ellipse,
        /// the glasses two rings — and a crop of those is a meaningless blob. Only where the fragment
        /// is MORE recognisable than the whole does a crop belong here (design skeptic, 2026-08-01).
        /// </summary>
        public Rect IconCrop;

        /// <summary>True when this detail's icon is a fragment rather than the whole sprite.</summary>
        public bool HasIconCrop => IconCrop.width > 0.01f && IconCrop.height > 0.01f;

        /// <summary>
        /// The sprite's ALPHA CENTROID — its ink's own middle — in fractions of
        /// <see cref="Size"/> (Y counted DOWN from the top). Measured off each PNG as the
        /// alpha-weighted mean of its pixels.
        ///
        /// The rectangle's centre is not the object. The plane of level 1 is drawn WITH its contrail:
        /// 484 px of sky whose middle lands in the trail, 121 px right of the fuselage — so the progress
        /// ring circled empty sky and the collection thread left from a point 145 px off the aircraft
        /// (design gate, 2026-08-07). Everything that has to ATTACH to a detail — the ring, the thread,
        /// the arrow of a teaching hint — asks for this point; everything that has to POSITION the
        /// sprite still uses <see cref="Home"/>, because that is where the drop draws it.
        ///
        /// Compact sprites measure out at their own centre (the moon and the open book land on 0.5000),
        /// so this is not a correction applied to every detail — it is the one number that tells the
        /// difference between a detail and the box it arrived in.
        /// </summary>
        public Vector2 AlphaCentroid;

        /// <summary>
        /// Explicit silhouette dilation radius for this detail's HUD slot, slot px; 0 = let
        /// <see cref="View.SlotSilhouette.RadiusFor"/> decide from the shape. The auto rule keys off
        /// how thin the shape FITS, so a square fragment of line art defeats it: the vine's leaf
        /// window fills its box yet is mostly air, and read 6.4 % ink at a metre where the readability
        /// guard demands 8 (GamePictureTests, 2026-08-01). Like <see cref="IconCrop"/>, this is a
        /// per-detail design decision, not a rule on sprite geometry.
        /// </summary>
        public float SilhouetteRadius;
    }

    /// <summary>A level as the art drop authored it (SCREENS.md «Сцены уровней», превью = канон).</summary>
    public sealed class LevelDefinition
    {
        /// <summary>1-based, as the player sees it («Уровень 2»).</summary>
        public int Number;

        /// <summary>Setting name — the level card and «Собрано: …» (text registry, walkthrough 25).</summary>
        public string Title;

        /// <summary>Word for this level's vessel in the finale line «портфель · рюкзак · сумка».</summary>
        public string VesselWord;

        public string BackgroundSprite;

        /// <summary>Vessel sprite, or null when the vessel is baked into the background (level 3).</summary>
        public string VesselSprite;

        public Vector2 VesselCentre;
        public Vector2 VesselSize;

        /// <summary>
        /// The part of <see cref="VesselRectOf"/> that is solid vessel, in fractions of that rectangle
        /// (0…1, Y counted DOWN from its top, like every other rectangle here).
        ///
        /// A vessel's rectangle is not its body: the mug's includes the steam above it and the handle
        /// beside it, the bucket is a trapezoid inside a box, the briefcase has a handle. The haul is
        /// laid out INSIDE this box (<see cref="View.VesselHaul"/>), so «деталь появляется ВНУТРИ
        /// сосуда» is measured against the silhouette rather than against the bounding rectangle —
        /// which is how the level-5 sticker ended up hanging half-way off the mug.
        ///
        /// Measured off the sprite's own alpha: the largest box, ANYWHERE in the rectangle, whose every
        /// pixel is opaque (alpha &gt; 0.9). «Anywhere» is the part that matters — a body need not be
        /// centred in its own rectangle, and forcing the box to be (which is how these were first
        /// measured) hands the haul the mug's handle side and 84 px of empty cup on the other.
        /// </summary>
        public Rect VesselSolid;

        public ArtDetail[] Details;

        /// <summary>
        /// HUD timer «солнце», centre in design px. Part of the per-level composition for the same
        /// reason the vessel is: SCREENS fixes a base (1800, 100), but the art drop owns the picture,
        /// and where a detail was painted under the widget the HUD is what moves — founder's decision
        /// of 2026-07-31 («подвинуть HUD — арт не трогаем»).
        /// </summary>
        public Vector2 SunCentre;

        /// <summary>HUD crank indicator, centre in design px. Base (140, 950); per level, see above.</summary>
        public Vector2 CrankIndicatorCentre;

        /// <summary>HUD detail-slot row, top-left of the first slot. Base (60, 40); per level.</summary>
        public Vector2 SlotsOrigin;

        /// <summary>Thought silhouettes of this level, Resources keys.</summary>
        public string[] ThoughtSprites;

        public int DetailCount => Details.Length;

        /// <summary>True when the vessel is part of the background plate and needs a fill overlay.</summary>
        public bool VesselIsBaked => string.IsNullOrEmpty(VesselSprite);
    }

    /// <summary>
    /// Levels 1–5 exactly as the designer assembled them (art root <c>docs/design/арт/уровни/</c>).
    ///
    /// Every position and size here was measured off the drop's own <c>превью.png</c> — the increment
    /// makes that file the authority over the coordinate tables ("при расхождении авторитет — превью"),
    /// so each sprite was located inside the preview by masked template matching and the whole
    /// catalogue was then re-composited and diffed back against the preview to prove it (mean |Δ| ≈ 6–12
    /// of 255, and every disagreement that was left is the preview's own soft-edged glow, not a
    /// misplacement). Where SCREENS.md's table and the preview differed, the preview won: the city's
    /// briefcase sits at y 904, not 935; the office mug at (1094, 891) and 292×280, not (1060, 935)
    /// and 250×230.
    ///
    /// **The numbers survived the renumbering of 2026-08-07 untouched.** The drop shipped its object
    /// and thought sprites byte-for-byte identical (checked by hash) and its plates as the same
    /// compositions darkened by a flat ×0.49 (checked per level: mean ratio 0.456…0.497 with the
    /// composition unchanged), so a scene that became level 1 instead of level 4 carries its own
    /// positions, sizes, icon crops, vessel silhouette and HUD shifts with it. Nothing here was
    /// re-measured, and that is the point — re-measuring would have been a chance to lose a decision.
    ///
    /// Sprites were trimmed to their alpha bounds on import, so <see cref="ArtDetail.Size"/> is the
    /// drawn shape itself — no invisible padding shifting a detail off its mark.
    /// </summary>
    public static class LevelCatalog
    {
        /// <summary>Root of every art key: <c>Assets/_Project/Art/Resources/MeditationArt</c>.</summary>
        public const string ArtRoot = "MeditationArt/";

        /// <summary>Scene zone 0,0 1920×810; foreground strip 0,810 1920×270 (SCREENS «Зоны»).</summary>
        public const float SceneHeight = 810f;

        /// <summary>
        /// Level order, 2026-08-07: the settings with the fewest objects come first — набережная (5) →
        /// офис (5) → метро (6) → библиотека (8) → город (8).
        ///
        /// What moved is the NUMBER, not the composition: the drop's object and thought sprites are
        /// byte-for-byte the ones measured off превью.png in July, so every position, size, icon crop,
        /// vessel silhouette and HUD shift below travelled with its scene rather than being re-measured.
        /// The plates are new — the same compositions multiplied by 0.49 (measured across all five) —
        /// so nothing they carry moved either.
        ///
        /// Two details left the game with the drop: the metro's puddle and the city's «отражение в
        /// луже». Their sprites are gone, so they are not decor like the baked cloud — they are simply
        /// not there. The metro's crank indicator was pushed right along the floor to clear the puddle
        /// and now comes back to the SCREENS base.
        /// </summary>
        public static readonly LevelDefinition[] Levels =
        {
            // ---- 1 · Набережная (была 4) ------------------------------------------------------------
            new LevelDefinition
            {
                Number = 1,
                Title = "Набережная",
                VesselWord = "ведёрко",
                BackgroundSprite = "L1/background",
                VesselSprite = "L1/objects/bucket",
                VesselCentre = new Vector2(549f, 846f),
                VesselSize = new Vector2(240f, 389f),
                // A trapezoid in a box: its narrowest solid column is barely half the rectangle.
                VesselSolid = new Rect(0.239f, 0.208f, 0.522f, 0.712f),
                Details = new[]
                {
                    // The plane is drawn WITH its contrail, so its rectangle is 484 px of sky and its
                    // centre lands in the middle of the trail rather than on the fuselage — the same
                    // thing the vine does on level 5. The rectangle is what the preview draws, and the
                    // catalogue is not allowed to invent a tighter one — but the ICON is allowed to be
                    // the plane itself: the body ends at x 387 of 990, and the 600 px of trail behind it
                    // is what turned the slot into a dark smudge with three hairlines.
                    //
                    // …and it is also why this detail is the reason ArtDetail carries an alpha
                    // centroid at all: the ink's own middle is 0.2497 of the rectangle, i.e. design
                    // (760, 135) — on the fuselage — while the rectangle's centre (881) is trail.
                    Detail("самолёт в небе", "L1/objects/airplane", 881f, 134f, 484f, 84f,
                        new Rect(0f, 0f, 0.4040f, 1f), 0f, 0.2497f, 0.5103f),
                    Detail("чайка", "L1/objects/seagull-large", 1535f, 172f, 133f, 28f, 0.5002f, 0.5023f),
                    Detail("кораблик", "L1/objects/ship", 1592f, 695f, 139f, 64f, 0.4439f, 0.6381f),
                    Detail("акулий плавник", "L1/objects/shark-fin", 169f, 725f, 196f, 63f, 0.4728f, 0.6655f),
                    Detail("ракушка на асфальте", "L1/objects/seashell", 1285f, 1021f, 189f, 60f,
                        0.6433f, 0.6399f)
                },
                // Nothing on this plate wants the HUD's corners: the gull stops at x 1601 (the sun's
                // dial starts at 1740), the shark fin is 100 px above the indicator's halo, and the
                // slot row's 424 px end well left of the plane. All three widgets stay on the SCREENS
                // base — «HUD переехал» must not quietly become «HUD переезжает на каждом уровне».
                SunCentre = new Vector2(1800f, 100f),
                CrankIndicatorCentre = new Vector2(140f, 950f),
                SlotsOrigin = new Vector2(60f, 40f),
                ThoughtSprites = new[]
                {
                    // Order matters: the tutorial's shake beat spawns [0] — «первая мысль (кот)»
                    // (SCREENS §Обучение, walkthrough А1).
                    "L1/thoughts/cat", "L1/thoughts/guitar", "L1/thoughts/wine-bottle",
                    "L1/thoughts/butterfly", "L1/thoughts/umbrella"
                }
            },

            // ---- 2 · Офис (была 5) ------------------------------------------------------------------
            new LevelDefinition
            {
                Number = 2,
                Title = "Офис",
                VesselWord = "кружка",
                BackgroundSprite = "L2/background",
                VesselSprite = "L2/objects/mug",
                VesselCentre = new Vector2(1094f, 891f),
                VesselSize = new Vector2(292f, 280f),
                // The mug's rectangle is mostly not mug: the steam takes the top third and the handle
                // the right quarter. And the body is not CENTRED in it — the handle hangs off one side,
                // so the cup's own middle sits at 0.39 of the rectangle, not at 0.5. A box measured
                // symmetrically had to give up everything left of 0.279 to stay clear of the handle on
                // the right, and the haul it produced stood 34 px right of the cup: 84 px of empty
                // porcelain on the left, 15 on the right (design skeptic, 2026-08-01). Measured off the
                // alpha as the largest opaque box anywhere in the rectangle: x 38…419, y 179…543 of
                // 589×556 — the cup, handle excluded.
                VesselSolid = new Rect(0.065f, 0.322f, 0.649f, 0.656f),
                Details = new[]
                {
                    // Two more yellow sticky notes are PAINTED into this plate (the laptop upper-left
                    // and the right-hand monitor). They are decor, deliberately: the level is about
                    // looking twice. Only the one below is a detail — LevelCatalogTests counts them.
                    Detail("цветок в окне", "L2/objects/flower", 328f, 253f, 53f, 92f, 0.5081f, 0.6160f),
                    Detail("скрепка", "L2/objects/paperclip", 338f, 574f, 40f, 56f, 0.4955f, 0.4752f),
                    Detail("стикер на мониторе", "L2/objects/sticky-note", 1160f, 606f, 58f, 56f,
                        0.4617f, 0.4714f),
                    Detail("улитка-игрушка", "L2/objects/snail-toy", 1530f, 745f, 205f, 115f,
                        0.5263f, 0.5209f),
                    Detail("тапки", "L2/objects/slippers", 215f, 975f, 129f, 51f, 0.5007f, 0.5620f)
                },
                SunCentre = new Vector2(1800f, 100f),
                // The slippers (150…279 × 950…1001) lie exactly where the indicator's halo does, so the
                // indicator slides right along the floor. 375 is the first position that clears them
                // (279 + halo 86 + 10 px of air) and it stops well short of the mug at x 948.
                CrankIndicatorCentre = new Vector2(375f, 950f),
                SlotsOrigin = new Vector2(60f, 40f),
                ThoughtSprites = new[]
                {
                    "L2/thoughts/banknote", "L2/thoughts/cake", "L2/thoughts/glasses",
                    "L2/thoughts/palm", "L2/thoughts/paperplane"
                }
            },

            // ---- 3 · Метро (осталась третьей) -------------------------------------------------------
            new LevelDefinition
            {
                Number = 3,
                Title = "Метро",
                VesselWord = "сумка",
                BackgroundSprite = "L3/background",
                // The passenger and his bag are painted into the plate — there is no vessel sprite, so
                // the filling is drawn as an overlay indicator over the baked bag (SCREENS «Уровень 3»).
                VesselSprite = null,
                // The window used to be the bag's BODY (168×143 at (1256, 903)) and cut the handle's
                // arch off — on the finale and on the victory tableau the bag arrived with a flat top
                // and a stump of handle on either side. Measured back off the plate: the brown body
                // runs x 1170…1339, y 834…975, and the handle arch reaches up to y 812. The window is
                // the whole thing, handle included.
                VesselCentre = new Vector2(1255f, 894f),
                VesselSize = new Vector2(170f, 166f),
                // Everything inside a cut-out rectangle is opaque, so the box below is not about alpha
                // but about the bag: the haul belongs in the body, never in the handle's arch.
                VesselSolid = new Rect(0.030f, 0.145f, 0.940f, 0.855f),
                Details = new[]
                {
                    Detail("плакат «Пляжи Сызрани»", "L3/objects/ad-poster", 744f, 77f, 280f, 116f,
                        0.5010f, 0.4764f),
                    Detail("гусь из-за края", "L3/objects/goose", 89f, 447f, 238f, 204f, 0.5469f, 0.5135f),
                    Detail("газета в воздухе", "L3/objects/newspaper", 886f, 524f, 162f, 121f,
                        0.4988f, 0.4750f),
                    Detail("стикер на поручне", "L3/objects/sticker", 1570f, 373f, 53f, 68f,
                        0.4960f, 0.4647f),
                    Detail("перчатка на сиденье", "L3/objects/glove", 399f, 665f, 107f, 100f,
                        0.4773f, 0.5138f),
                    // The puddle on the floor was the seventh detail here. The drop of 2026-08-07 took
                    // its sprite away and painted the floor clean, so it is neither a detail nor decor.
                    Detail("рыба на полу", "L3/objects/fish", 1775f, 991f, 199f, 85f, 0.5288f, 0.5063f)
                },
                SunCentre = new Vector2(1800f, 100f),
                // Back on the SCREENS base: the shift to (420, 950) existed only to clear the puddle
                // (260 px of floor at (172, 942)), and the puddle left with the drop.
                CrankIndicatorCentre = new Vector2(140f, 950f),
                // The «Пляжи Сызрани» poster (604…884 × 19…135) reaches under the last slot of the row,
                // so the row drops below it — the wall there is bare down to the goose at y 345.
                SlotsOrigin = new Vector2(60f, 146f),
                ThoughtSprites = new[]
                {
                    "L3/thoughts/phone-call", "L3/thoughts/soccer-ball", "L3/thoughts/exclamations",
                    "L3/thoughts/beetle", "L3/thoughts/sock"
                }
            },

            // ---- 4 · Библиотека (была 2) ------------------------------------------------------------
            new LevelDefinition
            {
                Number = 4,
                Title = "Библиотека",
                VesselWord = "рюкзак",
                BackgroundSprite = "L4/background",
                VesselSprite = "L4/objects/backpack",
                VesselCentre = new Vector2(556f, 974f),
                VesselSize = new Vector2(182f, 230f),
                VesselSolid = new Rect(0.116f, 0.210f, 0.768f, 0.738f),
                Details = new[]
                {
                    Detail("солнце на постере", "L4/objects/sun-circle", 995f, 178f, 55f, 55f,
                        0.5000f, 0.5000f),
                    Detail("кот на подоконнике", "L4/objects/cat", 1572f, 620f, 146f, 75f,
                        0.4671f, 0.5870f),
                    Detail("бюст", "L4/objects/bust", 1200f, 660f, 88f, 191f, 0.4998f, 0.5861f),
                    Detail("раскрытая книга", "L4/objects/open-book", 780f, 800f, 92f, 68f,
                        0.4993f, 0.4999f),
                    // The lamp stands on the RIGHT BENCH, not on the little table beside the book:
                    // the table's lamp is painted into the plate, and putting the collectable copy
                    // on top of it meant collecting it changed nothing you could see. The preview
                    // is the authority on positions, and the preview↔plate difference isolates the
                    // free-standing lamp as its own blob — re-matched by compositing the sprite onto
                    // the plate, the error falls from 24.2 to 7.2 of 255 at (1330, 817), 57×99.
                    Detail("лампа на скамье", "L4/objects/lamp", 1330f, 817f, 57f, 99f, 0.5320f, 0.5193f),
                    Detail("очки на скамье", "L4/objects/glasses", 1617f, 862f, 175f, 51f,
                        0.5235f, 0.4961f),
                    Detail("валентинка", "L4/objects/valentine", 191f, 874f, 47f, 36f, 0.5024f, 0.5016f),
                    Detail("мышь с книжкой", "L4/objects/mouse-book", 52f, 1029f, 163f, 55f,
                        0.6503f, 0.5132f)
                },
                SunCentre = new Vector2(1800f, 100f),
                // The base corner is taken twice over: the valentine (167…214 × 856…892) and the mouse
                // with the book (…133 × 1001…1056) leave a gap only 110 px tall between them, and the
                // dial needs 172. Sliding right along the same floor line keeps the indicator in its
                // corner and lands it in the empty stretch between the mouse and the backpack (x 465).
                CrankIndicatorCentre = new Vector2(310f, 950f),
                SlotsOrigin = new Vector2(60f, 40f),
                ThoughtSprites = new[]
                {
                    "L4/thoughts/alarm-clock", "L4/thoughts/burger", "L4/thoughts/baby-head",
                    "L4/thoughts/mushroom", "L4/thoughts/speech-bubble"
                }
            },

            // ---- 5 · Город (была 1) -----------------------------------------------------------------
            new LevelDefinition
            {
                Number = 5,
                Title = "Город",
                // The level card calls it «дипломат», so that is the word the game uses for it.
                VesselWord = "дипломат",
                BackgroundSprite = "L5/background",
                VesselSprite = "L5/objects/briefcase",
                VesselCentre = new Vector2(1056f, 904f),
                VesselSize = new Vector2(343f, 211f),
                VesselSolid = new Rect(0.090f, 0.178f, 0.820f, 0.775f),
                Details = new[]
                {
                    Detail("одуванчик", "L5/objects/dandelion", 500f, 958f, 59f, 75f, 0.4626f, 0.4258f),
                    Detail("шторы в окне", "L5/objects/curtains", 824f, 390f, 70f, 100f, 0.5000f, 0.4476f),
                    Detail("воробей", "L5/objects/bird", 187f, 83f, 62f, 62f, 0.4830f, 0.4435f),
                    Detail("луна", "L5/objects/moon", 1680f, 112f, 176f, 176f, 0.5001f, 0.5000f),
                    Detail("облако у столба", "L5/objects/cloud-middle", 728f, 53f, 180f, 46f,
                        0.4968f, 0.5600f),
                    // The cloud «у провода» is NOT here on purpose. The drop ships it twice: once
                    // painted into background.png at (1373, 152) and once as cloud-2.png — so
                    // collecting it left its baked copy hanging in the sky and the level read as
                    // broken. Founder's decision of 2026-08-01, re-measured on the new plate
                    // 2026-08-07: until the designer takes it out of the plate it is decor, exactly
                    // like the office's yellow sticky notes. LevelCatalogTests holds the absence.
                    Detail("летучая мышь", "L5/objects/bat", 1504f, 211f, 89f, 92f, 0.5018f, 0.6284f),
                    Detail("горшок с цветком", "L5/objects/flowerpot", 1253f, 616f, 46f, 61f,
                        0.4819f, 0.5717f),
                    // «Лужа у двери» was the ninth detail here. Its sprite left with the drop of
                    // 2026-08-07 («отражение в луже» — интерактивным не делаем).
                    //
                    // The vine was the one rectangle taken from the table instead of the picture: at
                    // 802 px tall it ran off the bottom of the frame and lay across the road. Measured
                    // back off превью.png — the green of the vine occupies x 1564…1617, y 280…937,
                    // i.e. it ends ON the pavement line, exactly as the drop draws it.
                    //
                    // Its ICON is one bend of it: whole, 111×1603 fitted into a 66 px slot is a column
                    // four pixels wide carrying fourteen bends of stem, and it renders as interference.
                    // The window y 274…385 of the sprite is the one that holds a whole leaf, the stem
                    // running corner to corner and the three purple flowers (design skeptic, 2026-08-01).
                    // Silhouette radius 4: the leaf window is line art — a square box full of air —
                    // so the auto radius (2) left 6.4 % ink; 4 hardens it past the 8 % readability
                    // bar without turning the bend into a blob.
                    Detail("лоза на стене", "L5/objects/vine", 1591f, 608f, 54f, 658f,
                        new Rect(0f, 0.1709f, 1f, 0.0692f), 4f, 0.4632f, 0.4757f)
                },
                // The moon is 176 px and sits at (1680, 112): the base sun overlapped it by ~28 px and
                // read as a second moon beside it. The only free lane in the top-right corner is BELOW
                // the moon (the vine's column ends at x 1619), so the sun drops to y 270 — 10 px clear
                // of the moon's lower edge, still the right-top corner, and its caption clears the vine.
                SunCentre = new Vector2(1800f, 270f),
                CrankIndicatorCentre = new Vector2(140f, 950f),
                // The sparrow (156…218 × 52…114) sat UNDER the slot row — a collectable detail the
                // player could not see, let alone aim at. The sky below it is empty down to the
                // curtains at y 340, so the row drops just past the sparrow.
                SlotsOrigin = new Vector2(60f, 124f),
                ThoughtSprites = new[]
                {
                    "L5/thoughts/teddy-bear", "L5/thoughts/empty-bed", "L5/thoughts/cake",
                    "L5/thoughts/question-marks", "L5/thoughts/coin-purse"
                }
            }
        };

        public static int Count => Levels.Length;

        /// <summary>Level by 0-based index, clamped — the flow never asks for one that is not there.</summary>
        public static LevelDefinition At(int index) =>
            Levels[Mathf.Clamp(index, 0, Levels.Length - 1)];

        /// <summary>
        /// The run's vessels in order, for the tuning panel's readout. It used to be the finale's
        /// subtitle; that line is baked into <c>screens/finale</c> since 2026-08-07 and the game no
        /// longer renders any text of its own there.
        /// </summary>
        public static string VesselWords()
        {
            var words = new string[Levels.Length];
            for (int i = 0; i < Levels.Length; i++) words[i] = Levels[i].VesselWord;
            return string.Join(" · ", words);
        }

        // ---- geometry: one place both the view and the layout test read ------------------------------
        //
        // The HUD/art clearance is a property of the CATALOGUE, not of a drawn frame: it has to be
        // decidable before a scene exists, so the EditMode test can hold the founder's decision («HUD
        // двигается, арт — нет») without loading a level. Everything below is therefore plain
        // arithmetic on the numbers above, and LevelView draws from exactly these.

        /// <summary>The rectangle a detail occupies, design px.</summary>
        public static Rect RectOf(ArtDetail detail) => Centred(detail.Home, detail.Size);

        /// <summary>
        /// Where a detail actually IS, as opposed to where its rectangle is centred: its alpha centroid
        /// in design px (<see cref="ArtDetail.AlphaCentroid"/>).
        ///
        /// This is the point everything that attaches to a detail hangs off — the progress ring, the
        /// collection thread, the arrow of a teaching hint. Details whose centroid was never measured
        /// (the greybox stand's, and anything added without one) fall back to the rectangle's centre,
        /// which is what the whole game used before 2026-08-07.
        /// </summary>
        public static Vector2 AnchorOf(ArtDetail detail)
        {
            Vector2 centroid = detail.AlphaCentroid;
            if (centroid.sqrMagnitude < 1e-6f) return detail.Home;
            return detail.Home + new Vector2(
                (centroid.x - 0.5f) * detail.Size.x,
                (centroid.y - 0.5f) * detail.Size.y);
        }

        /// <summary>How far the anchor sits from the rectangle's centre — the offset a moving detail keeps.</summary>
        public static Vector2 AnchorOffsetOf(ArtDetail detail) => AnchorOf(detail) - detail.Home;

        /// <summary>The anchors of every detail of a level, in catalogue order.</summary>
        public static Vector2[] AnchorsOf(LevelDefinition level)
        {
            var anchors = new Vector2[level.DetailCount];
            for (int i = 0; i < anchors.Length; i++) anchors[i] = AnchorOf(level.Details[i]);
            return anchors;
        }

        /// <summary>
        /// The size the detail's ICON had in the scene — its own size, or the fragment's share of it
        /// when the icon is a crop. The haul caps every item at «не больше, чем в сцене», and for a
        /// cropped icon that ceiling belongs to the fragment: the vine's 658 px of height are the
        /// vine's, not one bend's.
        /// </summary>
        public static Vector2 IconSizeOf(ArtDetail detail) =>
            detail.HasIconCrop
                ? new Vector2(detail.Size.x * detail.IconCrop.width, detail.Size.y * detail.IconCrop.height)
                : detail.Size;

        /// <summary>The rectangle the vessel occupies, design px.</summary>
        public static Rect VesselRectOf(LevelDefinition level) => Centred(level.VesselCentre, level.VesselSize);

        /// <summary>Timer sun, the dial itself (r = 60).</summary>
        public static Rect SunRectOf(LevelDefinition level) =>
            Centred(level.SunCentre, new Vector2(LevelOneData.SunRadius * 2f, LevelOneData.SunRadius * 2f));

        /// <summary>The caption under the sun — its box, which is wider than the «0:42» inside it.</summary>
        public static Rect TimerLabelRectOf(LevelDefinition level) =>
            Centred(
                new Vector2(level.SunCentre.x,
                    level.SunCentre.y + LevelOneData.SunRadius + LevelOneData.TimerLabelGap),
                new Vector2(LevelOneData.TimerLabelWidth, LevelOneData.TimerLabelHeight));

        /// <summary>Crank indicator, the widest ring drawn around it (halo r = 86, not the dial's 70).</summary>
        public static Rect CrankRectOf(LevelDefinition level) =>
            Centred(level.CrankIndicatorCentre,
                new Vector2(LevelOneData.CrankHaloRadius * 2f, LevelOneData.CrankHaloRadius * 2f));

        /// <summary>The whole slot row of this level: N slots of 72 px with step 88 from the origin.</summary>
        public static Rect SlotsRectOf(LevelDefinition level) =>
            new Rect(level.SlotsOrigin.x, level.SlotsOrigin.y,
                (level.DetailCount - 1) * LevelOneData.SlotStep + LevelOneData.SlotSize,
                LevelOneData.SlotSize);

        /// <summary>Slot number <paramref name="index"/> of this level (0-based), design px.</summary>
        public static Rect SlotRectOf(LevelDefinition level, int index) =>
            new Rect(level.SlotsOrigin.x + index * LevelOneData.SlotStep, level.SlotsOrigin.y,
                LevelOneData.SlotSize, LevelOneData.SlotSize);

        /// <summary>
        /// Everything a teaching plate is not allowed to cover: every detail, the vessel, and each HUD
        /// widget (the sun and its caption, the crank indicator, the slot row).
        ///
        /// Приёмка п.3 of the drop names the HUD; the design gate of 2026-08-07 extended it to the
        /// tutorial's own plates, and it is the same list either way — the point is that the level stays
        /// readable while it is being explained. Composed here rather than in the view for the same
        /// reason the HUD clearance is: it has to be decidable without a scene, so a test can hold it.
        /// </summary>
        public static Rect[] HintObstaclesOf(LevelDefinition level)
        {
            var rects = new Rect[level.DetailCount + 5];
            int at = 0;
            for (int i = 0; i < level.DetailCount; i++) rects[at++] = RectOf(level.Details[i]);
            rects[at++] = VesselRectOf(level);
            rects[at++] = SunRectOf(level);
            rects[at++] = TimerLabelRectOf(level);
            rects[at++] = CrankRectOf(level);
            rects[at] = SlotsRectOf(level);
            return rects;
        }

        /// <summary>
        /// The subset a teaching ARROW should try not to cross: the art itself. The HUD is left out
        /// deliberately — an arrow that has to reach the crank indicator crosses HUD corners on the way,
        /// and forbidding that would push the plate into the far half of the frame.
        /// </summary>
        public static Rect[] ArtRectsOf(LevelDefinition level)
        {
            var rects = new Rect[level.DetailCount + 1];
            for (int i = 0; i < level.DetailCount; i++) rects[i] = RectOf(level.Details[i]);
            rects[level.DetailCount] = VesselRectOf(level);
            return rects;
        }

        // ---- the victory tableau (S4, mock 18) -------------------------------------------------------

        /// <summary>Width of the box mock 18 draws the winning vessel in (<c>rect 720,390 480×300</c>).</summary>
        public const float VictoryBoxWidth = 480f;

        /// <summary>…and its height. The caption stands at y 800, the row of filled slots below it.</summary>
        public const float VictoryBoxHeight = 300f;

        /// <summary>SCREENS S4 says «масштаб ×2» — that is the ceiling, not the rule.</summary>
        public const float VictoryMaxScale = 2f;

        /// <summary>
        /// How much the vessel grows on its victory screen.
        ///
        /// A flat ×2 is only the mock's number for the mock's vessel. The art drop's are all different
        /// shapes, and ×2 of the bucket is 480×778 — 72 % of the frame's height, standing on top of
        /// «Собрано: …» and of the slot row underneath it. So the tableau is normalised to the box mock
        /// 18 actually draws: same proportions, never bigger than 480×300, and never bigger than ×2.
        /// </summary>
        public static float VictoryScaleOf(LevelDefinition level)
        {
            Vector2 size = level.VesselSize;
            return Mathf.Min(VictoryMaxScale,
                VictoryBoxWidth / Mathf.Max(1f, size.x),
                VictoryBoxHeight / Mathf.Max(1f, size.y));
        }

        /// <summary>Where the winning vessel is drawn: centred on the frame, normalised to the box.</summary>
        public static Rect VictoryTableauRectOf(LevelDefinition level)
        {
            float scale = VictoryScaleOf(level);
            return Centred(new Vector2(960f, 540f), level.VesselSize * scale);
        }

        /// <summary>The vessel's solid box, or the whole rectangle when a level never measured one.</summary>
        public static Rect SolidOf(LevelDefinition level)
        {
            Rect solid = level.VesselSolid;
            return solid.width > 0.01f && solid.height > 0.01f ? solid : new Rect(0f, 0f, 1f, 1f);
        }

        private static Rect Centred(Vector2 centre, Vector2 size) =>
            new Rect(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f, size.x, size.y);

        /// <param name="cxAlpha">Alpha centroid, fraction of the width (see <see cref="ArtDetail.AlphaCentroid"/>).</param>
        /// <param name="cyAlpha">…and of the height, counted down from the top.</param>
        private static ArtDetail Detail(string name, string sprite, float cx, float cy, float w, float h,
            float cxAlpha = 0.5f, float cyAlpha = 0.5f)
        {
            return new ArtDetail
            {
                Name = name,
                Sprite = sprite,
                Home = new Vector2(cx, cy),
                Size = new Vector2(w, h),
                AlphaCentroid = new Vector2(cxAlpha, cyAlpha)
            };
        }

        /// <summary>…and the same detail whose ICON is a fragment (<see cref="ArtDetail.IconCrop"/>).</summary>
        private static ArtDetail Detail(string name, string sprite, float cx, float cy, float w, float h,
            Rect iconCrop, float silhouetteRadius = 0f, float cxAlpha = 0.5f, float cyAlpha = 0.5f)
        {
            ArtDetail detail = Detail(name, sprite, cx, cy, w, h, cxAlpha, cyAlpha);
            detail.IconCrop = iconCrop;
            detail.SilhouetteRadius = silhouetteRadius;
            return detail;
        }
    }
}
