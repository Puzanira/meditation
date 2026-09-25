using System.Collections.Generic;
using Meditation.Game;
using Meditation.Mechanics;
using Meditation.Tuning;
using Meditation.View;
using NUnit.Framework;
using UnityEngine;

namespace Meditation.Tests
{
    /// <summary>
    /// The art drop, checked against the increment's own numbers before a single pixel is drawn.
    ///
    /// Done contract §3 asks for three levels «с артом… детали на позициях превью (±20 px)», and the
    /// cheapest place to lose that is a typo in a resource key: a missing sprite is an invisible
    /// detail, and an invisible detail is an unwinnable level that still looks fine on a screenshot.
    /// So every key in the catalogue is resolved here, and the composition is checked for the things
    /// a picture cannot tell you — that details are on screen, that slots fit, that the ladder climbs.
    /// </summary>
    public class LevelCatalogTests
    {
        [SetUp]
        public void SetUp() => TuningConfig.ResetToDefaults();

        [TearDown]
        public void TearDown() => TuningConfig.ResetToDefaults();

        // ---- the drop is actually there ------------------------------------------------------------

        [Test]
        public void EveryArtKeyOfEveryLevel_ResolvesToASprite()
        {
            var missing = new List<string>();

            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Check(level.BackgroundSprite, missing);
                if (!level.VesselIsBaked) Check(level.VesselSprite, missing);

                foreach (ArtDetail detail in level.Details)
                {
                    Check(detail.Sprite, missing);
                    // …and both pictures a hideaway keeps behind the first one.
                    if (!string.IsNullOrEmpty(detail.CaptureSprite)) Check(detail.CaptureSprite, missing);
                    if (!string.IsNullOrEmpty(detail.BehindSprite)) Check(detail.BehindSprite, missing);
                }

                foreach (string thought in level.ThoughtSprites) Check(thought, missing);
            }

            Assert.IsEmpty(missing, "Нет спрайтов для ключей: " + string.Join(", ", missing));
        }

        private static void Check(string key, ICollection<string> missing)
        {
            if (Resources.Load<Sprite>(LevelCatalog.ArtRoot + key) == null) missing.Add(key);
        }

        [Test]
        public void EveryBackground_IsTheFullDesignFrame()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                var sprite = Resources.Load<Sprite>(LevelCatalog.ArtRoot + level.BackgroundSprite);
                Assert.IsNotNull(sprite, level.Title + ": нет фона.");
                Assert.AreEqual(DesignStage.DesignWidth, sprite.rect.width, 1f,
                    level.Title + ": фон не 1920 по ширине.");
                Assert.AreEqual(DesignStage.DesignHeight, sprite.rect.height, 1f,
                    level.Title + ": фон не 1080 по высоте.");
            }
        }

        // ---- the composition the increment describes ------------------------------------------------

        [Test]
        public void TheLevelOrder_IsTheOneTheDropRenumberedTo()
        {
            // SCREENS «Маппинг уровней», drop 2026-08-07: «вперёд вынесены сцены с меньшим числом
            // объектов». The order is a design decision, so it is named here rather than inferred —
            // and it is named by SETTING, because that is what moved.
            var expected = new[] { "Набережная", "Офис", "Метро", "Библиотека", "Город" };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], LevelCatalog.At(i).Title,
                    "Уровень " + (i + 1) + " — не тот сеттинг, что в дропе 2026-08-07.");
                Assert.AreEqual(i + 1, LevelCatalog.At(i).Number,
                    "Номер уровня разошёлся с его местом в каталоге.");
            }
        }

        [Test]
        public void DetailCounts_AreTheOnesTheDoneContractNames()
        {
            // «Победа = все N деталей», and N is the drop's own 5·5·6·8·8 (SCREENS «Маппинг уровней»).
            // Two details left the game with this drop: the metro's puddle and the city's «отражение в
            // луже» — their sprites are gone, so those levels are one shorter than they were.
            Assert.AreEqual(5, LevelCatalog.Count, "В игре пять уровней.");
            Assert.AreEqual(5, LevelCatalog.At(0).DetailCount, "Набережная — 5 деталей.");
            Assert.AreEqual(5, LevelCatalog.At(1).DetailCount, "Офис — 5 деталей.");
            Assert.AreEqual(6, LevelCatalog.At(2).DetailCount, "Метро — 6 деталей (лужа убрана).");
            Assert.AreEqual(8, LevelCatalog.At(3).DetailCount, "Библиотека — 8 деталей.");
            Assert.AreEqual(8, LevelCatalog.At(4).DetailCount, "Город — 8 деталей (лужа убрана).");

            // …and the run as a whole: 5+5+6+8+8.
            int total = 0;
            foreach (LevelDefinition level in LevelCatalog.Levels) total += level.DetailCount;
            Assert.AreEqual(32, total, "За прогон игрок собирает 32 детали.");
        }

        /// <summary>
        /// The two puddles the drop of 2026-08-07 took away («убраны две лужи как интерактивные
        /// объекты… спрайтов больше нет»). Unlike the baked cloud they are not decor — there is nothing
        /// left of them at all, and a catalogue entry for one would be a target with no picture.
        /// </summary>
        [Test]
        public void NeitherPuddle_IsADetailAnyMore()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            foreach (ArtDetail detail in level.Details)
                Assert.IsFalse(detail.Sprite.EndsWith("puddle"),
                    level.Title + ": лужа удалена из дропа 2026-08-07 — деталью она быть не может.");
        }

        /// <summary>
        /// A slot is a square, and two of the drop's sprites are ribbons: the vine is 111×1603 and the
        /// airplane is 990×168, most of it contrail. Fitted whole into the 66 px box the vine comes out
        /// four pixels wide carrying fourteen bends of its own stem — television interference, not a
        /// plant — and the plane's body becomes a smudge behind three hairlines (design skeptic,
        /// 2026-08-01). Their icons are fragments instead, and the fragment has to be square enough to
        /// FILL the slot: that is the whole point of taking one.
        ///
        /// The other long sprites deliberately have no crop — the gull is a chevron, the puddles are
        /// ellipses, the glasses two rings. They ARE their silhouette end to end, and a crop of one of
        /// those is a blob. So this is a registry of two decisions, not a rule on the aspect ratio.
        /// </summary>
        [Test]
        public void TheTwoRibbons_HaveAnIconCrop_AndItFillsTheSlot()
        {
            var cropped = new List<string>();

            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                foreach (ArtDetail detail in level.Details)
                {
                    if (!detail.HasIconCrop) continue;
                    cropped.Add(detail.Sprite);

                    string what = "«" + detail.Name + "»: кроп иконки ";
                    Rect crop = detail.IconCrop;
                    Assert.GreaterOrEqual(crop.xMin, -1e-3f, what + "вылезает за спрайт слева.");
                    Assert.GreaterOrEqual(crop.yMin, -1e-3f, what + "вылезает за спрайт сверху.");
                    Assert.LessOrEqual(crop.xMax, 1f + 1e-3f, what + "вылезает за спрайт справа.");
                    Assert.LessOrEqual(crop.yMax, 1f + 1e-3f, what + "вылезает за спрайт снизу.");

                    Sprite sprite = Resources.Load<Sprite>(LevelCatalog.ArtRoot + detail.Sprite);
                    Assert.IsNotNull(sprite, what + "снят с несуществующего спрайта.");

                    float w = sprite.rect.width * crop.width;
                    float h = sprite.rect.height * crop.height;
                    Assert.Greater(w, 8f, what + "уже восьми пикселей исходника.");
                    Assert.Greater(h, 8f, what + "ниже восьми пикселей исходника.");

                    float wholeAspect = Mathf.Max(sprite.rect.width, sprite.rect.height) /
                                        Mathf.Max(1f, Mathf.Min(sprite.rect.width, sprite.rect.height));
                    float iconAspect = Mathf.Max(w, h) / Mathf.Max(1f, Mathf.Min(w, h));
                    Assert.Greater(wholeAspect, 3f,
                        what + "снят со спрайта, который и целиком помещается в квадратный слот.");
                    Assert.LessOrEqual(iconAspect, 3f,
                        what + "сам остался лентой (" + iconAspect.ToString("0.0") +
                        ":1) — в квадратном слоте он снова схлопнется в ниточку.");
                }
            }

            CollectionAssert.AreEquivalent(
                new[] { "L5/objects/vine", "L1/objects/airplane" }, cropped,
                "Иконка-фрагмент — решение по каждой детали отдельно; список изменился молча.");
        }

        /// <summary>
        /// The office plate carries two yellow sticky notes as decor («это НЕ деталь», SCREENS
        /// «Уровень 5»), and the drop ships exactly one sticky-note sprite. A second one in the
        /// catalogue would be a target the player can chase but never collect.
        /// </summary>
        [Test]
        public void TheOffice_CountsExactlyOneStickyNote_TheBakedOnesAreDecor()
        {
            LevelDefinition office = LevelCatalog.At(1);

            int stickers = 0;
            foreach (ArtDetail detail in office.Details)
                if (detail.Sprite.EndsWith("sticky-note")) stickers++;

            Assert.AreEqual(1, stickers,
                "Стикер-деталь в офисе ровно один — жёлтые стикеры в фоне это обманки-декор.");
        }

        /// <summary>
        /// The street's second cloud is painted INTO background.png and shipped as a sprite as well, so
        /// collecting it took the sprite away and left the baked copy hanging over the wire — the level
        /// said «собрано» about something still visibly there. Founder's decision of 2026-08-01: until
        /// the designer removes it from the plate the cloud is decor, exactly like the office's baked
        /// sticky notes, and the street is nine details. This is the test that keeps the decision
        /// visible when somebody counts the drop's files and finds one more object than the catalogue.
        /// </summary>
        [Test]
        public void TheCity_LeavesTheBakedCloudOutOfItsDetails()
        {
            foreach (ArtDetail detail in LevelCatalog.At(4).Details)
                Assert.AreNotEqual("L5/objects/cloud-2", detail.Sprite,
                    "Облако «у провода» впечено в фон — деталью оно быть не может, пока фон не правлен.");
        }

        /// <summary>
        /// …and the same trap the other way round: a detail may not be parked on top of the place where
        /// a baked copy of it is painted. The library's lamp was, on the little table beside the book —
        /// the preview draws the free-standing one on the right bench, and the preview is the authority.
        /// </summary>
        [Test]
        public void TheLibrarysLamp_StandsOnTheBench_NotOnTheBakedTable()
        {
            ArtDetail lamp = System.Array.Find(LevelCatalog.At(3).Details,
                d => d.Sprite == "L4/objects/lamp");

            Assert.IsNotNull(lamp.Sprite, "Лампы нет в каталоге библиотеки.");
            Assert.AreEqual(1330f, lamp.Home.x, 20f, "Лампа не на правой скамье по X (превью: 1330).");
            Assert.AreEqual(817f, lamp.Home.y, 20f, "Лампа не на правой скамье по Y (превью: 817).");
        }

        [Test]
        public void EveryDetail_SitsOnScreen_AndHasARealSize()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                foreach (ArtDetail detail in level.Details)
                {
                    string what = level.Title + " · " + detail.Name;

                    // The CENTRE has to be reachable: it is what the gaze aims at and where the
                    // collection thread starts. A detail may hang over an edge (the goose does, and
                    // the mouse does — the author drew them that way), its anchor may not.
                    Assert.That(detail.Home.x, Is.InRange(0f, DesignStage.DesignWidth), what + ": X вне кадра.");
                    Assert.That(detail.Home.y, Is.InRange(0f, DesignStage.DesignHeight), what + ": Y вне кадра.");

                    Assert.Greater(detail.Size.x, 20f, what + ": ширина меньше 20 px — не заметить.");
                    Assert.Greater(detail.Size.y, 20f, what + ": высота меньше 20 px — не заметить.");
                    Assert.LessOrEqual(detail.Size.x, DesignStage.DesignWidth, what + ": шире экрана.");
                    Assert.LessOrEqual(detail.Size.y, DesignStage.DesignHeight, what + ": выше экрана.");

                    // …and a detail may hang over an edge, but it may not be CUT by one: the vine was
                    // taken from the table instead of the preview, came out 802 px tall and ran off the
                    // bottom of the frame, lying across the road and the puddle. A rectangle that ends
                    // at the frame's edge is a rectangle nobody measured against the picture.
                    Assert.Less(detail.Home.y + detail.Size.y * 0.5f, DesignStage.DesignHeight,
                        what + ": нижний край детали срезан кадром — размер не с превью.");
                }
            }
        }

        /// <summary>
        /// How far apart two details have to be for the gaze to be able to prefer one of them.
        ///
        /// Not the gaze RADIUS: the selector takes the nearest selectable target inside its circle and
        /// drops collected ones from the running, so two details 87 px apart (the library's open book
        /// and its desk lamp, which is how the designer drew that table) are both perfectly reachable —
        /// aim at one and it is nearer. What would really be unpickable is a shared position, and what
        /// would be miserable is a separation smaller than the aim wobble.
        /// </summary>
        private const float MinimumSeparation = 40f;

        [Test]
        public void DetailsOfALevel_AreFarEnoughApartToBeAimedAt()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                ArtDetail[] details = level.Details;
                for (int i = 0; i < details.Length; i++)
                for (int j = i + 1; j < details.Length; j++)
                {
                    float distance = Vector2.Distance(details[i].Home, details[j].Home);
                    Assert.Greater(distance, MinimumSeparation,
                        level.Title + ": «" + details[i].Name + "» и «" + details[j].Name +
                        "» слишком близко — взгляд не сможет предпочесть одну.");
                }
            }
        }

        // «TheHudSlots_FitOnScreen_EvenForTheLongestLevel» stood here until 2026-08-08. The row of
        // detail slots is out of the game (founder: «убрать ряд совсем»), so the longest level no
        // longer has a 8×88 px row to fit anywhere.

        /// <summary>
        /// The progress bar under the vessel — «на всех уровнях» since 2026-08-07 (founder).
        ///
        /// Two claims, and the second one is why this is a test rather than a line in the view. «Под
        /// сосудом» reads as arithmetic that cannot fail until you meet the library: its backpack is
        /// 230 px tall centred at y 974, so its own lower edge is NINE PIXELS PAST the frame, and an
        /// unclamped bar would have been drawn off screen on exactly the level with the most details
        /// to keep track of. The clamp is the fix; this is what holds it.
        /// </summary>
        [Test]
        public void TheVesselBar_IsOnScreenAndUnderItsVessel_OnEveryLevel()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Rect bar = LevelCatalog.VesselBarRectOf(level);
                Rect vessel = LevelCatalog.VesselRectOf(level);

                Assert.GreaterOrEqual(bar.xMin, 0f, level.Title + ": полоса уходит за левый край.");
                Assert.LessOrEqual(bar.xMax, DesignStage.DesignWidth,
                    level.Title + ": полоса уходит за правый край.");
                Assert.LessOrEqual(bar.yMax, DesignStage.DesignHeight,
                    level.Title + ": полоса уходит за нижний край кадра.");
                Assert.Greater(bar.yMin, vessel.center.y,
                    level.Title + ": полоса обязана быть ПОД серединой сосуда, а не над ним.");
                Assert.AreEqual(level.VesselCentre.x, bar.center.x, 1e-3f,
                    level.Title + ": полоса не по центру своего сосуда.");
                Assert.Greater(bar.width, 60f,
                    level.Title + ": полоса уже 60 px — заполнение на ней не прочитать.");
            }
        }

        // ---- HUD против арта (решение founder 2026-07-31: двигается HUD, арт — канон) ---------------

        /// <summary>
        /// The rule the founder settled on 2026-07-31: the art drop is canon, so where a HUD widget and
        /// a painted detail wanted the same pixels, the widget moved. This is the test that keeps the
        /// decision true, and it is deliberately about the CATALOGUE rather than about a drawn frame —
        /// the clash is decidable from the numbers alone, and a screenshot test would only notice it if
        /// somebody happened to look at the right corner of the right level.
        ///
        /// It matters beyond tidiness on two of the three levels: a detail under an opaque HUD widget is
        /// a detail the player cannot find, and every detail has to be collectable for the level to be
        /// winnable at all.
        ///
        /// Footprints are the ones actually drawn, not the nominal ones from SCREENS: the crank
        /// indicator carries an arc at r + 14 and a halo at 86. The sun and its caption left this list
        /// on 2026-08-07 with the timer; the bar under the vessel joined it the same day.
        /// </summary>
        [Test]
        public void NoHudWidget_OverlapsAnyDetail_OnAnyLevel()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            foreach (ArtDetail detail in level.Details)
            {
                Rect art = LevelCatalog.RectOf(detail);
                string what = level.Title + " · «" + detail.Name + "»";

                AssertClear(LevelCatalog.CrankRectOf(level), art, what, "индикатор динамо");
                AssertClear(LevelCatalog.VesselBarRectOf(level), art, what, "полоса наполнения сосуда");
            }
        }

        [Test]
        public void NoHudWidget_OverlapsTheVessel_OnAnyLevel()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Rect vessel = LevelCatalog.VesselRectOf(level);
                string what = level.Title + " · сосуд";

                AssertClear(LevelCatalog.CrankRectOf(level), vessel, what, "индикатор динамо");
            }
        }

        // ---- альфа-центроид: где деталь ДЕЙСТВИТЕЛЬНО находится --------------------------------------

        /// <summary>
        /// The measured centroid is a point ON the detail, and — everywhere except one sprite — it is
        /// within a fifth of the rectangle of its centre.
        ///
        /// Both halves matter. Without the first, a mis-typed fraction moves the progress ring and the
        /// thread somewhere off the picture; without the second, the number silently becomes a licence
        /// to redraw the composition, which is precisely what the drop's positions are not (SCREENS:
        /// «при расхождении авторитет — превью»). Compact sprites measure out at their own centre.
        /// </summary>
        [Test]
        public void EveryDetailsAlphaCentroid_LiesOnTheDetail_AndBarelyMovesACompactOne()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            foreach (ArtDetail detail in level.Details)
            {
                string what = level.Title + " · «" + detail.Name + "»";
                Vector2 anchor = LevelCatalog.AnchorOf(detail);
                Rect box = LevelCatalog.RectOf(detail);

                Assert.IsTrue(box.Contains(anchor),
                    what + ": альфа-центроид " + anchor + " лежит вне прямоугольника детали " + box + ".");

                // The plane of level 1 is the exception this whole mechanism exists for: it is drawn
                // WITH its contrail, so its ink's middle is a quarter of the rectangle to the left of
                // the rectangle's own middle.
                if (detail.Sprite == PlaneWithAContrail) continue;

                Vector2 shift = LevelCatalog.AnchorOffsetOf(detail);
                Assert.LessOrEqual(Mathf.Abs(shift.x), detail.Size.x * MaxCentroidShare,
                    what + ": центроид уехал по X на " + shift.x.ToString("0.0") + " px из " + detail.Size.x +
                    " — это уже другая позиция детали, а не её центр тяжести.");
                Assert.LessOrEqual(Mathf.Abs(shift.y), detail.Size.y * MaxCentroidShare,
                    what + ": центроид уехал по Y на " + shift.y.ToString("0.0") + " px из " + detail.Size.y + ".");
            }
        }

        /// <summary>
        /// …and the one sprite it exists for: the plane's centroid has to land on the FUSELAGE.
        ///
        /// SCREENS puts the body «около (738, 138)» and the icon crop says the aircraft ends at 0.404 of
        /// the rectangle; the rectangle's own centre (881) is trail. The ring drawn there circled empty
        /// sky and the thread left from 145 px off the plane (design gate, 2026-08-07).
        /// </summary>
        [Test]
        public void ThePlanesAlphaCentroid_LandsOnItsBody_NotInTheContrail()
        {
            ArtDetail plane = System.Array.Find(LevelCatalog.At(0).Details,
                d => d.Sprite == PlaneWithAContrail);
            Assert.AreNotEqual(default(ArtDetail).Sprite, plane.Sprite, "Самолёт пропал из уровня 1.");

            Rect box = LevelCatalog.RectOf(plane);
            Rect body = new Rect(box.xMin, box.yMin, box.width * plane.IconCrop.width, box.height);
            Vector2 anchor = LevelCatalog.AnchorOf(plane);

            Assert.IsTrue(body.Contains(anchor),
                "Центроид самолёта " + anchor + " не попал в корпус " + body + " — кольцо снова обводит след.");
            Assert.Less(Mathf.Abs(anchor.x - 738f), 40f,
                "Центроид самолёта далеко от корпуса, который SCREENS ставит около x 738: " + anchor.x);
        }

        private const string PlaneWithAContrail = "L1/objects/airplane";

        /// <summary>How far a centroid may sit from its rectangle's centre, as a share of the rectangle.</summary>
        private const float MaxCentroidShare = 0.2f;

        // ---- обучающие плашки: приёмка п.3 распространяется и на них ----------------------------------

        /// <summary>
        /// No teaching PLATE covers a detail, the vessel, or a HUD widget — приёмка п.3 of the drop,
        /// extended to the tutorial's own hints by the design gate of 2026-08-07.
        ///
        /// It was broken exactly the way an offset breaks: «КРУТИ РУЧКУ» sat 230 px above the dynamo
        /// indicator, was clamped back into the frame, and the 468×88 plate landed on the whole shark
        /// fin and the left edge of the bucket. Checked here, on the catalogue, because a placement is
        /// arithmetic and this way it is decided before there is a scene to be surprised by.
        ///
        /// It used to place the drop's three drawn BUTTONS — НАВОДИ, КРУТИ РУЧКУ and «ТАЩИ» sampled
        /// along the whole thread, because that one rode the travelling detail. All three left the
        /// teaching screens on 2026-09-22 (founder: «убрать стрелки все с экранов обучений… остальные
        /// элементы подсказок убрать»), so what is placed now is what is drawn now: one plate per beat,
        /// sized off the registry's own sentence.
        /// </summary>
        [Test]
        public void NoTeachingPlate_CoversADetail_TheVessel_OrTheHud()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Rect[] obstacles = LevelCatalog.HintObstaclesOf(level);

                foreach (TeachingPlate beat in TeachingPlates(level))
                {
                    Vector2 size = HintPlate.SizeFor(beat.Line, beat.HasBracket);
                    Vector2 spot = HintPlacement.Beside(size, beat.About, obstacles);
                    Rect plate = HintPlacement.Centred(spot, size);

                    Assert.IsTrue(HintPlacement.InsideFrame(plate),
                        level.Title + " · плашка «" + beat.Name + "» вылезла за кадр: " + plate);

                    for (int i = 0; i < obstacles.Length; i++)
                        Assert.IsFalse(plate.Overlaps(obstacles[i]),
                            level.Title + " · плашка «" + beat.Name + "» " + plate +
                            " накрывает " + obstacles[i] + ".");
                }
            }
        }

        /// <summary>
        /// …and it still stands NEXT to what it teaches. Without this the test above is satisfied by
        /// parking every plate in the emptiest corner of the frame, which is «не перекрывает» and
        /// «рядом с объектом» traded against each other rather than both honoured.
        /// </summary>
        [Test]
        public void EveryTeachingPlate_StaysWithinReachOfWhatItTeaches()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Rect[] obstacles = LevelCatalog.HintObstaclesOf(level);

                foreach (TeachingPlate beat in TeachingPlates(level))
                {
                    Vector2 size = HintPlate.SizeFor(beat.Line, beat.HasBracket);
                    Vector2 spot = HintPlacement.Beside(size, beat.About, obstacles);

                    Assert.Less(Vector2.Distance(spot, beat.About), MaxHintReach,
                        level.Title + " · плашка «" + beat.Name + "» уехала от своего объекта на " +
                        Vector2.Distance(spot, beat.About).ToString("0") + " px — это уже не «рядом».");
                }
            }
        }

        /// <summary>One beat's hint: the line it says and the thing it says it about.</summary>
        private readonly struct TeachingPlate
        {
            public TeachingPlate(string name, string line, bool hasBracket, Vector2 about)
            {
                Name = name;
                Line = line;
                HasBracket = hasBracket;
                About = about;
            }

            public string Name { get; }
            public string Line { get; }
            public bool HasBracket { get; }
            public Vector2 About { get; }
        }

        /// <summary>
        /// The four beats of the lesson, as the level places them (<c>LevelScreen</c>).
        ///
        /// Run on EVERY level and not only on level 1, although level 1 is the only one that teaches:
        /// the claim is about the placement RULE against a composition, and a rule that only works on
        /// one of the five plates is a rule that happens to work. It is also the guard that catches a
        /// level whose art leaves no clear 560 px anywhere.
        /// </summary>
        /// <remarks>
        /// All four are single-line plates since 2026-09-22: the PC bracket that used to add a row to
        /// the first three is withdrawn («убрать все подсказки про клавиатуру — она играется на
        /// автомате», founder). The flag stays on the struct because the PLATE still has a two-line
        /// form, and this list is what says the game does not use it.
        /// </remarks>
        private static IEnumerable<TeachingPlate> TeachingPlates(LevelDefinition level)
        {
            yield return new TeachingPlate("наводи", GameTexts.BeatAim, false,
                LevelCatalog.AnchorOf(level.Details[0]));
            yield return new TeachingPlate("крути крутилку и тащи", GameTexts.BeatCollect, false,
                LevelCatalog.AnchorOf(level.Details[0]));
            yield return new TeachingPlate("отгон", GameTexts.SwipeHint, false,
                LevelCatalog.AnchorOf(level.Details[level.DetailCount > 1 ? 1 : 0]));
            yield return new TeachingPlate("весь уровень", GameTexts.BeatWhole, false,
                level.VesselCentre);
        }

        /// <summary>
        /// …and none of the four teaching lines names a keyboard, a mouse or a PC.
        ///
        /// The EditMode half of the founder's order of 2026-09-22, made on the REGISTRY rather than on
        /// a screen: <c>GameFlowTests.NoKeyboardHint_IsOnAnyScreenOfTheGame</c> watches the pixels, and
        /// this watches the source they are set from — so a bracket added back to
        /// <see cref="GameTexts.Live"/> is red before anybody builds a scene.
        /// </summary>
        [Test]
        public void NoLiveLine_NamesAKeyboardAMouseOrAPc()
        {
            string[] words = { "компьютер", "клавиш", "клавиатур", "мыши", "мышь", "колесо", "esc" };

            foreach (string line in GameTexts.Live)
            {
                string lowered = line.ToLowerInvariant();
                foreach (string word in words)
                    Assert.IsFalse(lowered.Contains(word),
                        "Живая строка «" + line + "» называет «" + word +
                        "» — игра идёт на автомате, клавиатуры и мыши в комнате нет.");
            }
        }

        /// <summary>
        /// …and the five brackets that were taken out really are in the withdrawn list, so the guard
        /// that walks the screens has something to recognise them BY. Deleting a line from a screen and
        /// forgetting to register it is exactly how «Крути ручку» came back twice.
        /// </summary>
        [Test]
        public void TheWithdrawnList_KnowsThePcBrackets()
        {
            string[] brackets =
            {
                "(на компьютере — колесо мыши)",
                "(на компьютере — стрелки)",
                "(на компьютере — Q и A)",
                "(на компьютере — Esc)"
            };

            foreach (string bracket in brackets)
                Assert.Contains(bracket, GameTexts.Withdrawn,
                    "ПК-скобка «" + bracket + "» снята с экрана, но не занесена в реестр выведенных — " +
                    "значит сторож её не узнает, когда она вернётся.");
        }

        /// <summary>How far a hint may stand from what it points at, design px — a third of the frame.</summary>
        private const float MaxHintReach = 640f;

        /// <summary>
        /// …and the widgets stay in the corners SCREENS gives them. Without this the test above is
        /// satisfied by parking the indicator in the middle of the sky: «не пересекается» is only half
        /// the requirement, the other half is that the player finds a widget where it always is.
        /// </summary>
        [Test]
        public void EveryHudWidget_StaysInItsCorner()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Rect crank = LevelCatalog.CrankRectOf(level);
                Assert.Less(crank.center.x, DesignStage.DesignWidth * 0.33f,
                    level.Title + ": индикатор динамо ушёл из левого нижнего угла по X.");
                Assert.Greater(crank.center.y, DesignStage.DesignHeight * 0.75f,
                    level.Title + ": индикатор динамо ушёл из левого нижнего угла по Y.");
            }
        }

        /// <summary>
        /// Only the three clashes the founder ruled on may move. Everything else stays on the SCREENS
        /// base, so «HUD переехал» never quietly becomes «HUD переезжает на каждом уровне».
        /// </summary>
        [Test]
        public void HudWidgets_MoveOffTheScreensBase_OnlyWhereTheArtDemandedIt()
        {
            // The shifts of SCREENS §S3 travelled with their scenes when the levels were renumbered —
            // the same widget still yields to the same painted object — and two were cancelled: the
            // metro's crank indicator moved for a puddle the drop has removed, and the city's sun for
            // a moon it no longer has to share a corner with (the timer is gone, 2026-08-07).
            Assert.AreEqual(LevelOneData.CrankIndicatorCentre, LevelCatalog.At(0).CrankIndicatorCentre,
                "Набережная: индикатору нечего было уступать — он должен стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.CrankIndicatorCentre, LevelCatalog.At(4).CrankIndicatorCentre,
                "Город: индикатору нечего было уступать — он должен стоять на базе SCREENS.");
            // The three «ряд слотов стоит на базе SCREENS» claims left with the row itself on
            // 2026-08-08, together with the two shifts they were guarding (метро y 146, город y 124).

            // …and the ones that did move stayed as close to the base as the art allowed.
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Assert.LessOrEqual(
                    Vector2.Distance(level.CrankIndicatorCentre, LevelOneData.CrankIndicatorCentre), 300f,
                    level.Title + ": индикатор уехал от базы SCREENS дальше, чем нужно.");
            }
        }

        /// <summary>
        /// The one shift the drop CANCELLED. The metro's crank indicator was pushed right along the
        /// floor (140 → 420) to clear a puddle 260 px wide at (172, 942); the drop of 2026-08-07 took
        /// the puddle out and painted the floor clean, so the indicator belongs back in its corner.
        ///
        /// Worth its own test because «HUD подвинут» is a debt, not a feature: without something that
        /// says «this one had a reason and the reason is gone», a shift outlives its cause forever.
        /// </summary>
        [Test]
        public void TheMetrosCrankIndicator_CameBackToTheScreensBase_WithThePuddleGone()
        {
            Assert.AreEqual(LevelOneData.CrankIndicatorCentre, LevelCatalog.At(2).CrankIndicatorCentre,
                "Метро: лужи больше нет — индикатор динамо обязан вернуться на базу (140, 950).");
        }

        private static void AssertClear(Rect hud, Rect art, string what, string widget)
        {
            Assert.IsFalse(hud.Overlaps(art),
                what + ": " + widget + " перекрывает арт (HUD " + hud + " × арт " + art + ").");
        }

        [Test]
        public void EveryVessel_StandsInTheForegroundStrip()
        {
            // SCREENS «Зоны»: полоса переднего плана 810–1080, «на ней сосуд».
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Assert.Greater(level.VesselCentre.y, LevelCatalog.SceneHeight,
                    level.Title + ": сосуд не в полосе переднего плана.");
                Assert.Greater(level.VesselSize.x, 40f, level.Title + ": сосуд слишком мал.");
                Assert.Greater(level.VesselSize.y, 40f, level.Title + ": сосуд слишком мал.");
            }
        }

        /// <summary>
        /// The reward beat has a box, and every vessel of the run fits it.
        ///
        /// A flat ×2 gave the bucket 480×778 — 72 % of the frame. The caption and the row of slots that
        /// used to stand under it are gone with the drop (the drawn «Отлично!» screen replaced them),
        /// but the box did not become optional: the beat lasts a second and a half and it is the only
        /// look the player ever gets at what they collected, so the haul has to be readable rather than
        /// spread across the whole frame.
        /// </summary>
        [Test]
        public void TheVictoryTableau_FitsItsBox_OnEveryVesselOfTheRun()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Rect tableau = LevelCatalog.VictoryTableauRectOf(level);
                string what = level.Title + ": сосуд победы ";

                Assert.LessOrEqual(tableau.width, LevelCatalog.VictoryBoxWidth + 1f, what + "шире коробки макета.");
                Assert.LessOrEqual(tableau.height, LevelCatalog.VictoryBoxHeight + 1f, what + "выше коробки макета.");
                Assert.Greater(LevelCatalog.VictoryScaleOf(level), 0.5f, what + "усох до миниатюры.");
                Assert.LessOrEqual(LevelCatalog.VictoryScaleOf(level), LevelCatalog.VictoryMaxScale + 1e-3f,
                    what + "вырос больше ×2.");

                // …and its proportions are the vessel's own — normalising is not squashing.
                Assert.AreEqual(level.VesselSize.x / level.VesselSize.y, tableau.width / tableau.height,
                    1e-3f, what + "потерял пропорции.");

                // …and it stays in the middle third of the frame, where the eye already is.
                Assert.Less(tableau.yMax, 770f, what + "сполз в нижнюю треть кадра.");
            }
        }

        /// <summary>
        /// Every vessel's measured silhouette box is a real box inside its rectangle. Nothing here can
        /// prove the alpha was read correctly — <c>GamePictureTests</c> does that off the rendered
        /// frame — but a typo that lets the haul out of the vessel is caught before it is drawn.
        /// </summary>
        [Test]
        public void EveryVessel_KnowsTheBoxThatIsActuallyVessel()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Rect solid = LevelCatalog.SolidOf(level);
                string what = level.Title + ": силуэт сосуда ";

                Assert.Greater(solid.width, 0.2f, what + "уже пятой части габарита — измерение потерялось.");
                Assert.Greater(solid.height, 0.2f, what + "ниже пятой части габарита — измерение потерялось.");
                Assert.GreaterOrEqual(solid.xMin, -1e-3f, what + "вылезает за габарит слева.");
                Assert.GreaterOrEqual(solid.yMin, -1e-3f, what + "вылезает за габарит сверху.");
                Assert.LessOrEqual(solid.xMax, 1f + 1e-3f, what + "вылезает за габарит справа.");
                Assert.LessOrEqual(solid.yMax, 1f + 1e-3f, what + "вылезает за габарит снизу.");

                // …and the haul box it produces really is inside it.
                Rect haul = VesselHaul.InnerBox(level.VesselSize, solid);
                float left = level.VesselSize.x * (solid.xMin - 0.5f);
                float right = level.VesselSize.x * (solid.xMax - 0.5f);
                float top = level.VesselSize.y * (0.5f - solid.yMin);
                float bottom = level.VesselSize.y * (0.5f - solid.yMax);

                Assert.GreaterOrEqual(haul.xMin, left - 1e-2f, what + "добыча выходит за него слева.");
                Assert.LessOrEqual(haul.xMax, right + 1e-2f, what + "добыча выходит за него справа.");
                Assert.LessOrEqual(haul.yMax, top + 1e-2f, what + "добыча выходит за него сверху.");
                Assert.GreaterOrEqual(haul.yMin, bottom - 1e-2f, what + "добыча выходит за него снизу.");
            }
        }

        [Test]
        public void OnlyLevelThree_HasABakedVessel()
        {
            // SCREENS «Уровень 3»: пассажир и сумка запечены в фон — наполнение оверлеем.
            Assert.IsFalse(LevelCatalog.At(0).VesselIsBaked, "Ведёрко уровня 1 — отдельный спрайт.");
            Assert.IsFalse(LevelCatalog.At(1).VesselIsBaked, "Кружка уровня 2 — отдельный спрайт.");
            Assert.IsTrue(LevelCatalog.At(2).VesselIsBaked, "Сумка уровня 3 запечена в фон.");
            Assert.IsFalse(LevelCatalog.At(3).VesselIsBaked, "Рюкзак уровня 4 — отдельный спрайт.");
            Assert.IsFalse(LevelCatalog.At(4).VesselIsBaked, "Портфель уровня 5 — отдельный спрайт.");
        }

        /// <summary>
        /// The teaching level has to have somewhere to put its thought.
        ///
        /// The old form of this test named a detail by index («Details[1] — шторы»), and that only
        /// worked because level 1 happened to be a scene whose second object stood in the middle of
        /// the frame. The renumbering broke that, and rightly: which detail the cat lands on is the
        /// LEVEL's business, not the catalogue's. What the catalogue owes the tutorial is that at
        /// least one of its details is somewhere a weak blob fits whole — otherwise the beat is taught
        /// with half the thought off screen.
        /// </summary>
        [Test]
        public void TheTeachingLevel_HasADetailAWeakThoughtCanSitOnWhole()
        {
            LevelDefinition one = LevelCatalog.At(0);
            Vector2 blob = Thought.SizeOf(ThoughtStrength.Weak);

            int fits = 0;
            foreach (ArtDetail detail in one.Details)
            {
                Vector2 home = detail.Home;
                if (home.x < blob.x * 0.5f || home.x > DesignStage.DesignWidth - blob.x * 0.5f) continue;
                if (home.y < blob.y * 0.5f || home.y > DesignStage.DesignHeight - blob.y * 0.5f) continue;
                fits++;
            }

            Assert.Greater(fits, 1,
                one.Title + ": обучающей мысли не на что сесть — нужна хотя бы пара деталей, над " +
                "которыми слабая мысль помещается целиком.");
        }

        [Test]
        public void TheFirstLevel_IsTheSmallestOne()
        {
            // The whole point of the renumbering: «вперёд вынесены сцены, где меньше объектов». A
            // teaching level with eight things to find is a teaching level nobody finishes.
            for (int i = 1; i < LevelCatalog.Count; i++)
                Assert.LessOrEqual(LevelCatalog.At(0).DetailCount, LevelCatalog.At(i).DetailCount,
                    "Первый уровень обязан быть не больше любого следующего.");
        }

        [Test]
        public void TheVesselWords_FollowTheNewOrder_AndUseTheCardsOwnWord()
        {
            // No longer a rendered line — the finale's own render carries its text — but still the
            // readout the founder tunes against, and still the place the vessel of level 5 is named.
            // SCREENS: «на карточке уровня он назван ДИПЛОМАТОМ, используем это слово в текстах».
            Assert.AreEqual("ведёрко · кружка · сумка · рюкзак · дипломат", LevelCatalog.VesselWords());
        }

        [Test]
        public void EveryLevel_HasItsOwnThoughtSet()
        {
            var seen = new HashSet<string>();
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Assert.GreaterOrEqual(level.ThoughtSprites.Length, 3,
                    level.Title + ": слишком мало мыслей для волн.");

                foreach (string key in level.ThoughtSprites)
                    Assert.IsTrue(seen.Add(key), "Мысль «" + key + "» повторяется между уровнями.");
            }
        }

        /// <summary>
        /// «Мысли НИКОГДА не спавнить меньше класса» — checked on the size a thought is actually
        /// SPAWNED at, for every silhouette of every level in every class.
        ///
        /// The old version of this test asked <see cref="ArtLibrary.FitThought(string,ThoughtStrength)"/>
        /// to stay INSIDE the box, which is the same sentence read backwards, and it passed while the
        /// game spawned the wine bottle 42 px wide against a class of 180 (Codex review, 2026-08-08).
        /// It is asked of <see cref="Thought.SpawnSize"/> and not of the fit, because the fit is an
        /// implementation detail and the founder's rule is about the blob that appears on the screen:
        /// whatever the library does, the thing that spawns must not be smaller than its class.
        /// </summary>
        [Test]
        public void EveryThoughtSilhouette_SpawnsNoSmallerThanItsSizeClass()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            foreach (string key in level.ThoughtSprites)
            foreach (ThoughtStrength strength in new[]
                     { ThoughtStrength.Weak, ThoughtStrength.Medium, ThoughtStrength.Strong })
            {
                Vector2 box = Thought.SizeOf(strength);
                var thought = new Thought { Strength = strength, Label = key };
                ArtLibrary.FitThought(thought);
                Vector2 spawn = thought.SpawnSize;

                string what = key + " (" + strength + ", класс " + box.x + "×" + box.y + "): ";
                Assert.GreaterOrEqual(spawn.x, box.x - 0.5f,
                    what + "спавнится УЖЕ класса — " + spawn.x.ToString("0") + " px.");
                Assert.GreaterOrEqual(spawn.y, box.y - 0.5f,
                    what + "спавнится НИЖЕ класса — " + spawn.y.ToString("0") + " px.");

                // …and one side has to sit ON the box, or "cover" quietly became "blow up".
                bool touches = Mathf.Abs(spawn.x - box.x) < 1f || Mathf.Abs(spawn.y - box.y) < 1f;
                Assert.IsTrue(touches,
                    what + "силуэт не подогнан под класс, а просто раздут до " +
                    spawn.x.ToString("0") + "×" + spawn.y.ToString("0") + ".");

                // A cover-fit silhouette overhangs its box on the long side, and that is allowed —
                // but not to the point where one thought IS the screen.
                Assert.LessOrEqual(spawn.x, DesignStage.DesignWidth,
                    what + "силуэт шире кадра.");
                Assert.LessOrEqual(spawn.y, DesignStage.DesignHeight * 1.5f,
                    what + "силуэт выше полутора кадров — это уже не мысль, а занавес.");
            }
        }

        /// <summary>
        /// The baked ink shares are the PNGs' own numbers, re-measured here.
        ///
        /// They decide how much screen a thought hides (<see cref="ThoughtField.OverlapPercent"/>) and
        /// therefore when the level is lost, so a stale number after an art re-drop would move the
        /// defeat threshold silently. The drop imports unreadable, so the file is read off disk and
        /// decoded into a texture of this test's own.
        /// </summary>
        [Test]
        public void EveryThoughtsInkShare_MatchesItsPng()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            foreach (string key in level.ThoughtSprites)
            {
                Sprite sprite = ArtLibrary.Get(key);
                Assert.IsNotNull(sprite, key + ": спрайт мысли не найден.");

                string path = UnityEditor.AssetDatabase.GetAssetPath(sprite);
                Assert.IsNotEmpty(path, key + ": у спрайта нет файла — нечего перемерить.");

                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.IsTrue(decoded.LoadImage(System.IO.File.ReadAllBytes(path)),
                        key + ": PNG не читается — " + path);

                    Color32[] pixels = decoded.GetPixels32();
                    int ink = 0;
                    for (int i = 0; i < pixels.Length; i++)
                        if (pixels[i].a >= 128) ink++;

                    float measured = ink / (float)pixels.Length;
                    Assert.AreEqual(measured, ArtLibrary.InkShareOf(key), 0.02f,
                        key + ": записанная заполненность разошлась с PNG (" +
                        measured.ToString("0.000") + " по файлу).");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(decoded);
                }
            }
        }

        /// <summary>
        /// The baked stroke thicknesses are the PNGs' own numbers too, re-measured here.
        ///
        /// They cap the neon rim (<see cref="LevelView.RimRadiusPx"/>), so a stale one after a re-drop
        /// would quietly put the office paperclip back to being a turquoise blob. Same treatment as the
        /// ink shares and for the same reason: the drop imports unreadable, so the file is decoded here.
        ///
        /// Twice the ink area over the ink perimeter — the thickness of a stroke, and a quantity that
        /// does not care how much empty margin the export left around the drawing.
        /// </summary>
        [Test]
        public void EveryDetailsStrokeThickness_MatchesItsPng()
        {
            foreach (string key in ArtLibrary.StrokeKeys)
            {
                Sprite sprite = ArtLibrary.Get(key);
                Assert.IsNotNull(sprite, key + ": спрайт детали не найден.");

                string path = UnityEditor.AssetDatabase.GetAssetPath(sprite);
                Assert.IsNotEmpty(path, key + ": у спрайта нет файла — нечего перемерить.");

                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.IsTrue(decoded.LoadImage(System.IO.File.ReadAllBytes(path)),
                        key + ": PNG не читается — " + path);

                    float measured = StrokeThicknessOf(decoded);
                    Assert.AreEqual(measured, ArtLibrary.StrokeThicknessOf(key), 0.6f,
                        key + ": записанная толщина штриха разошлась с PNG (" +
                        measured.ToString("0.0") + " px по файлу).");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(decoded);
                }
            }
        }

        /// <summary>Twice the ink area over the ink perimeter, in the PNG's own pixels.</summary>
        private static float StrokeThicknessOf(Texture2D png)
        {
            Color32[] pixels = png.GetPixels32();
            int w = png.width;
            int h = png.height;

            bool Ink(int x, int y) =>
                x >= 0 && y >= 0 && x < w && y < h && pixels[y * w + x].a >= 128;

            int area = 0;
            int perimeter = 0;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!Ink(x, y)) continue;
                area++;
                if (!Ink(x - 1, y) || !Ink(x + 1, y) || !Ink(x, y - 1) || !Ink(x, y + 1)) perimeter++;
            }

            return perimeter > 0 ? 2f * area / perimeter : 0f;
        }

        /// <summary>
        /// No detail's neon rim is wider than its own drawing can carry.
        ///
        /// A rim is a dilation, and a dilation adds twice its radius to every stroke it goes around: at
        /// the shipped 6 px the office paperclip — a 10.7 px wire at the size it is drawn — came out a
        /// solid turquoise blob and the librarian's glasses lost both lenses (design gate, 2026-08-08).
        /// The rule is «the rim may add at most half the stroke's own width», i.e. a quarter of it, and
        /// it is checked here rather than on a frame because it is arithmetic about every detail of
        /// every level and a frame can only ever show one.
        /// </summary>
        [Test]
        public void NoDetailsNeonRim_IsWiderThanItsOwnStrokeCanCarry()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            foreach (ArtDetail detail in level.Details)
            {
                // Both skins of a hideaway: the rim is re-pointed at the captured drawing while the
                // detail is hauled (LevelView.ApplyNeonOutline), and «too wide for its own stroke» is a
                // question about whichever picture is on the frame.
                for (int skin = 0; skin < 2; skin++)
                {
                    bool captured = skin == 1;
                    if (captured && string.IsNullOrEmpty(detail.CaptureSprite)) continue;

                    string key = LevelCatalog.DrawnSpriteOf(detail, captured);
                    Vector2 drawn = LevelCatalog.DrawnSizeOf(detail, captured);
                    string skinned = detail.Name + (captured ? " (в захвате)" : string.Empty);

                    float rim = LevelView.RimRadiusPx(key, drawn, TuningConfig.Defaults.DetailOutlinePx);
                    Assert.LessOrEqual(rim, TuningConfig.Defaults.DetailOutlinePx,
                        skinned + ": обводка шире, чем ручка на панели.");

                    Sprite sprite = ArtLibrary.Get(key);
                    Assert.IsNotNull(sprite, skinned + ": спрайт не найден.");

                    float scale = Mathf.Min(drawn.x / sprite.rect.width, drawn.y / sprite.rect.height);
                    float stroke = ArtLibrary.StrokeThicknessOf(key) * scale;

                    Assert.LessOrEqual(rim, Mathf.Max(LevelView.MinRimPx,
                            stroke * LevelView.RimShareOfStroke) + 0.01f,
                        "Уровень " + level.Number + ", «" + skinned + "»: обводка " +
                        rim.ToString("0.0") + " px на штрихе шириной " + stroke.ToString("0.0") +
                        " px — ободок смыкается в заливку.");
                }
            }
        }

        /// <summary>
        /// A cached sprite that has been unloaded is RELOADED, not handed back as a corpse — the bug
        /// behind the founder's «розовый квадрат в ведре» (живая сессия 2026-09-22).
        ///
        /// <c>ArtLibrary</c> caches for the whole session and is never cleared in the shipped game. Until
        /// this round the cache was asked only whether a key was PRESENT, and a destroyed
        /// <c>UnityEngine.Object</c> is present and null at the same time — a live C# wrapper round a
        /// dead native pointer. So the library returned it, <c>LevelView.CollectDetail</c> compared it to
        /// null with Unity's overloaded operator, got true, and painted the haul cell
        /// <see cref="Color.magenta"/> — the pink square she found in the bucket.
        ///
        /// The ICON crops are the entries that die: <see cref="ArtLibrary.IconOf"/> builds them with
        /// <c>Sprite.Create</c>, so they belong to no scene and no asset file and nothing outside this
        /// dictionary keeps them. A single-mode <c>SceneManager.LoadScene</c> — which the stand's level
        /// launcher does on every run — drags <c>Resources.UnloadUnusedAssets</c> behind it, and the
        /// editor reimporting a texture under a playing editor does the same thing by another road.
        /// Neither can happen inside one batchmode shot, which is why every contract frame scanned clean
        /// while the live session did not.
        ///
        /// The crop is destroyed here rather than the loaded asset on purpose: it is a runtime object, so
        /// killing it needs no <c>allowDestroyingAssets</c> and cannot touch anything on disk.
        /// </summary>
        [Test]
        public void TheArtCache_HandsBackALiveSprite_AfterACachedOneIsUnloaded()
        {
            ArtDetail cropped = default;
            bool found = false;
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                foreach (ArtDetail detail in level.Details)
                {
                    if (!detail.HasIconCrop) continue;
                    cropped = detail;
                    found = true;
                    break;
                }

                if (found) break;
            }

            Assert.IsTrue(found, "Ни у одной детали нет IconCrop — проверять нечего.");

            Sprite first = ArtLibrary.IconOf(cropped);
            Assert.IsNotNull(first, cropped.Name + ": иконка не собралась с первого раза.");
            Assert.AreNotSame(ArtLibrary.Get(cropped.Sprite), first,
                cropped.Name + ": иконка — сам ассет, а не вырезка; тест бьёт не туда.");

            UnityEngine.Object.DestroyImmediate(first);
            Assert.IsTrue(first == null, "Вырезку не удалось выгрузить — дальше проверять нечего.");

            Sprite again = ArtLibrary.IconOf(cropped);
            Assert.IsTrue(again != null,
                cropped.Name + ": кэш вернул выгруженный спрайт — в сосуде будет розовый квадрат.");
            Assert.IsTrue(again.texture != null,
                cropped.Name + ": спрайт живой, а его текстура выгружена.");
        }

        /// <summary>
        /// There is no square around the moon — measured on the ASSET, which is where the fix now is.
        ///
        /// The founder's «квадрат вокруг луны» (плейтест 2026-09-22). Nothing ever drew a square: the
        /// moon's PNG is a disc inside a wide radial glow, and the old 352 px canvas cut that glow off
        /// while it was still worth alpha 5/255. Five units of alpha is invisible over most things and a
        /// hard step over a night sky, because the UI composites in LINEAR space — the plate under the
        /// moon is sRGB (25, 25, 49) and the glow's cream at α = 5/255 lifts it by twelve units, with a
        /// straight edge exactly on this detail's own quad.
        ///
        /// It was patched in the shader until 2026-09-25 (a per-detail UV fade, <c>EdgeFadeUv</c>) and is
        /// patched in the art since: Катя's re-export is the same 220 px disc on a 380 px canvas, and the
        /// last two units of glow left at the border were taken off on the way into Resources.
        ///
        /// So the guard changed with the fix, and it is the stronger one — it reads the PNG and does the
        /// compositing arithmetic instead of asserting that a workaround is switched on. Two claims:
        ///
        ///   1. the moon's own border cannot lift the darkest plate pixel under it by a visible amount;
        ///   2. NO detail carries an edge fade any more — the field is gone, so this is the assertion
        ///      that the stopgap cannot creep back as a copy of itself on a sprite that is trimmed to
        ///      its own alpha box (where it would eat the gull's wingtips and the paperclip's wire).
        /// </summary>
        [Test]
        public void NoSquareAroundTheMoon_AndNoSpriteNeedsAnEdgeFadeAnyMore()
        {
            Sprite moon = ArtLibrary.Get("L5/objects/moon");
            Assert.IsNotNull(moon, "Спрайт луны не найден.");
            Assert.AreEqual(moon.rect.width, moon.rect.height, 1f,
                "Холст луны перестал быть квадратным — сияние пересобрали, проверьте кромку заново.");

            string path = UnityEditor.AssetDatabase.GetAssetPath(moon);
            Assert.IsNotEmpty(path, "У луны нет файла — нечего мерить.");

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(decoded.LoadImage(System.IO.File.ReadAllBytes(path)),
                    "PNG луны не читается — " + path);

                Color32[] pixels = decoded.GetPixels32();
                int w = decoded.width;
                int h = decoded.height;

                // The worst pixel of the border, and the colour it would be composited with.
                Color32 worst = new Color32(0, 0, 0, 0);
                for (int x = 0; x < w; x++)
                {
                    worst = Louder(worst, pixels[x]);
                    worst = Louder(worst, pixels[(h - 1) * w + x]);
                }

                for (int y = 0; y < h; y++)
                {
                    worst = Louder(worst, pixels[y * w]);
                    worst = Louder(worst, pixels[y * w + w - 1]);
                }

                float step = SrgbStepOver(NightSkyUnderTheMoon, worst);
                Assert.LessOrEqual(step, MaxInvisibleStep,
                    "Кромка холста луны поднимает ночное небо на " + step.ToString("0.0") +
                    " из 255 при пороге " + MaxInvisibleStep + " (альфа на границе " + worst.a +
                    "/255) — это и есть «квадрат вокруг луны»: сияние снова обрезано холстом.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decoded);
            }

            // …and the workaround has not grown back. The field is gone from ArtDetail, and this is the
            // line that says so out loud rather than letting a re-added `EdgeFadeUv` quietly compile:
            // a fade on the other thirty-one details would eat their own ink, because they are trimmed
            // to their alpha boxes and their drawings DO reach the border.
            Assert.IsNull(typeof(ArtDetail).GetField("EdgeFadeUv"),
                "Поле EdgeFadeUv вернулось в ArtDetail. Костыль снят 2026-09-25 вместе с ассетом, " +
                "который его требовал; чинить кромку надо в PNG, а не в шейдере.");
        }

        /// <summary>The louder of two border pixels — the one that would show most over a dark plate.</summary>
        private static Color32 Louder(Color32 a, Color32 b) => b.a > a.a ? b : a;

        /// <summary>
        /// The darkest place on level 5's plate under the moon's rectangle, sRGB — measured off
        /// `L5/background.png` (the sky there runs (17, 20, 43)…(34, 32, 57)).
        /// </summary>
        private static readonly Color32 NightSkyUnderTheMoon = new Color32(17, 20, 43, 255);

        /// <summary>
        /// How far a straight edge may move an sRGB channel before a person finds it, 0…255.
        ///
        /// One level. The step the founder reported measured twelve; the number here is «no step»
        /// rather than «a smaller step», because the whole point of fixing the asset instead of fading
        /// it in the shader was to stop arguing about how faint a hard edge has to be.
        /// </summary>
        private const float MaxInvisibleStep = 1f;

        /// <summary>
        /// How much one sRGB channel moves when <paramref name="over"/> is composited on
        /// <paramref name="under"/> — in LINEAR space, the way this project renders
        /// (<c>m_ActiveColorSpace: 1</c>). The whole defect was that a step invisible in gamma is not.
        /// </summary>
        private static float SrgbStepOver(Color32 under, Color32 over)
        {
            float alpha = over.a / 255f;
            float worst = 0f;
            for (int channel = 0; channel < 3; channel++)
            {
                float plate = channel == 0 ? under.r : channel == 1 ? under.g : under.b;
                float ink = channel == 0 ? over.r : channel == 1 ? over.g : over.b;
                float mixed = Mathf.LinearToGammaSpace(
                    Mathf.GammaToLinearSpace(plate / 255f) * (1f - alpha) +
                    Mathf.GammaToLinearSpace(ink / 255f) * alpha) * 255f;
                worst = Mathf.Max(worst, Mathf.Abs(mixed - plate));
            }

            return worst;
        }

        // ---- прятки: «проявление при захвате» (контракт 2026-09-25) ----------------------------------

        /// <summary>
        /// The three hideaways of this drop, and nothing else pretending to be one.
        ///
        /// A hideaway is a detail with a second picture — the shark behind its fin, the goose behind its
        /// head, the woman behind the city's curtain (<see cref="ArtDetail.IsHideaway"/>). The registry
        /// is named here rather than counted, for the same reason the icon crops are: it is three
        /// decisions the founder made about three drawings, and a fourth appearing silently would be a
        /// mechanic nobody asked for.
        ///
        /// What is checked beyond the list is the arithmetic that keeps the reveal from being a jump:
        /// the captured picture has to be BIGGER than the fragment (otherwise there is nothing to
        /// reveal) and it has to contain the fragment's own rectangle (otherwise the fin moves out from
        /// under the player's aim at the moment they start pulling).
        /// </summary>
        [Test]
        public void TheHideaways_AreTheThreeTheDropNames_AndTheirRevealDoesNotJump()
        {
            var found = new List<string>();

            foreach (LevelDefinition level in LevelCatalog.Levels)
            foreach (ArtDetail detail in level.Details)
            {
                if (!detail.IsHideaway) continue;
                found.Add(detail.Sprite);

                string what = "У" + level.Number + " «" + detail.Name + "»: ";

                if (!string.IsNullOrEmpty(detail.CaptureSprite))
                {
                    Assert.IsNotNull(Resources.Load<Sprite>(LevelCatalog.ArtRoot + detail.CaptureSprite),
                        what + "нет спрайта захвата «" + detail.CaptureSprite + "».");

                    Rect capture = LevelCatalog.CaptureRectOf(detail);
                    Rect rest = LevelCatalog.RectOf(detail);

                    Assert.Greater(capture.width * capture.height, rest.width * rest.height,
                        what + "полный арт не больше фрагмента — проявлять нечего.");
                    Assert.IsTrue(capture.Contains(new Vector2(rest.xMin, rest.yMin)) &&
                                  capture.Contains(new Vector2(rest.xMax, rest.yMax)),
                        what + "прямоугольник захвата " + capture + " не накрывает покой " + rest +
                        " — при захвате фрагмент прыгнет из-под прицела.");

                    // A captured picture may hang off an edge (the goose's body does, deliberately —
                    // «тело за пределами экрана»), but it may not be somewhere else entirely.
                    Assert.IsTrue(capture.Overlaps(new Rect(0f, 0f, DesignStage.DesignWidth,
                            DesignStage.DesignHeight)),
                        what + "полный арт целиком за кадром.");
                }

                if (string.IsNullOrEmpty(detail.BehindSprite)) continue;

                Assert.IsNotNull(Resources.Load<Sprite>(LevelCatalog.ArtRoot + detail.BehindSprite),
                    what + "нет спрайта под деталью «" + detail.BehindSprite + "».");

                Rect behind = LevelCatalog.BehindRectOf(detail);
                Assert.Greater(behind.width, 20f, what + "то, что под деталью, уже 20 px.");
                Assert.Greater(behind.height, 20f, what + "то, что под деталью, ниже 20 px.");
                Assert.IsTrue(behind.Overlaps(LevelCatalog.RectOf(detail)),
                    what + "то, что «под шторкой», лежит не под ней: " + behind + " против " +
                    LevelCatalog.RectOf(detail) + ".");
            }

            CollectionAssert.AreEquivalent(
                new[] { "L1/objects/shark-fin", "L3/objects/goose", "L5/objects/curtains" }, found,
                "Прятки — решение по каждой детали отдельно (founder, 2026-09-25); список изменился молча.");
        }

        /// <summary>
        /// A hideaway's HAUL shows what it turned out to be, and its resting geometry is untouched.
        ///
        /// Both halves are easy to lose. The icon is the reward beat's only subject, and a fin arriving
        /// in the bucket after the player has just watched a shark go in is the riddle asked twice. The
        /// resting geometry is what the aim, the ring, the thread and the teaching plates all read, and
        /// «the detail got bigger» must not quietly mean «the detail is now 292 px wide for everything
        /// that measures it».
        /// </summary>
        [Test]
        public void AHideawaysIcon_IsTheWholeThing_AndItsRestingRectangleIsNot()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            foreach (ArtDetail detail in level.Details)
            {
                string what = "У" + level.Number + " «" + detail.Name + "»: ";

                if (string.IsNullOrEmpty(detail.CaptureSprite))
                {
                    // The two ribbons are the exception the icon crop exists for and have their own
                    // test; everything else is its own sprite, whole.
                    if (!detail.HasIconCrop)
                        Assert.AreSame(ArtLibrary.Get(detail.Sprite), ArtLibrary.IconOf(detail),
                            what + "иконка обычной детали — не её собственный спрайт.");
                    continue;
                }

                Assert.IsFalse(detail.HasIconCrop,
                    what + "у прятки есть и кроп иконки, и полный арт — это два разных ответа на " +
                    "один вопрос «что кладём в сосуд».");

                Assert.AreSame(ArtLibrary.Get(detail.CaptureSprite), ArtLibrary.IconOf(detail),
                    what + "в сосуд кладётся фрагмент, а игрок только что втащил туда целое.");
                Assert.AreEqual(detail.CaptureSize, LevelCatalog.IconSizeOf(detail),
                    what + "потолок ячейки добычи снят с фрагмента, а не с того, что в неё легло.");

                // …and the catalogue's own numbers are still the fragment's.
                Assert.AreEqual(detail.Size, LevelCatalog.DrawnSizeOf(detail, false),
                    what + "покой перестал быть покоем.");
                Assert.AreEqual(detail.Sprite, LevelCatalog.DrawnSpriteOf(detail, false),
                    what + "в покое рисуется не фрагмент.");
            }
        }

        /// <summary>
        /// «Захват» is progress on the thread, not the crank flag — and a slip takes it back.
        ///
        /// The rule the contract states is «замечена и реально тянется… обратно — если бросили», and the
        /// tempting reading of «тянется» is <c>CrankCollector.IsSpinning</c>. It is the wrong one: that
        /// flag drops on every frame the player's hand crosses over on the dynamo, so a shark keyed to
        /// it flickers back into a fin twice a second while it is visibly travelling. This holds the
        /// reading that shipped (<see cref="LevelView.IsCaptured"/>), which is the same sentence read as
        /// a statement about the thread.
        /// </summary>
        [Test]
        public void TheCapture_FollowsTheThread_NotTheHandle()
        {
            Assert.IsFalse(LevelView.IsCaptured(0f, true, false),
                "Деталь замечена, но не сдвинулась — это ещё покой.");
            Assert.IsFalse(LevelView.IsCaptured(0.5f, false, false),
                "Деталь не замечена — показывать нечего.");
            Assert.IsTrue(LevelView.IsCaptured(0.02f, true, false),
                "Нить натянута, а прятка не раскрылась.");
            Assert.IsTrue(LevelView.IsCaptured(0.99f, true, false),
                "У самого сосуда прятка обязана быть раскрытой.");
            Assert.IsFalse(LevelView.IsCaptured(0.6f, true, true),
                "Бросили — прятка обязана закрыться, даже пока летит домой.");
        }

        /// <summary>
        /// «Где сейчас деталь» has ONE answer, and it is the drawing that is on the frame
        /// (<see cref="LevelCatalog.DrawnRectAt"/>).
        ///
        /// A hideaway has three plausible rectangles — the catalogue's, the fragment's, and the animal's,
        /// which stands half a body to the side of both — and every consumer that picks a different one
        /// produces the same class of bug: the game draws a shark and the logic reasons about a fin. Two
        /// consumers were caught doing exactly that on 2026-09-25 (the beat's obstacle track and the
        /// ring's «скрыта мыслью»), so the seam is held here rather than at each of them.
        /// </summary>
        [Test]
        public void TheDrawnRectangle_IsTheFragmentAtRest_AndTheWholeAnimalWithItsOffsetInTheHaul()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            foreach (ArtDetail detail in level.Details)
            {
                string what = "У" + level.Number + " «" + detail.Name + "»: ";

                Rect rest = LevelCatalog.DrawnRectAt(detail, detail.Home, false);
                AssertSameRect(LevelCatalog.RectOf(detail), rest,
                    what + "покой перестал быть прямоугольником каталога.");

                Rect hauled = LevelCatalog.DrawnRectAt(detail, detail.Home, true);
                Rect span = LevelCatalog.DrawnRectSpanAt(detail, detail.Home);

                if (string.IsNullOrEmpty(detail.CaptureSprite))
                {
                    // Обычная деталь — без изменений: у неё один рисунок, и захват его не меняет.
                    AssertSameRect(rest, hauled, what + "у обычной детали появился второй габарит.");
                    AssertSameRect(rest, span, what + "у обычной детали разъехались покой и охват.");
                    continue;
                }

                AssertSameRect(LevelCatalog.CaptureRectOf(detail), hauled,
                    what + "в захвате рисуется не полный арт со своим офсетом.");
                Assert.AreEqual(detail.CaptureSize.x, hauled.width, 0.001f,
                    what + "ширина в захвате — не CaptureSize.");
                Assert.AreEqual(detail.CaptureSize.y, hauled.height, 0.001f,
                    what + "высота в захвате — не CaptureSize.");
                Assert.AreEqual(detail.Home + detail.CaptureOffset, hauled.center,
                    what + "полный арт встал не по CaptureOffset.");

                Assert.IsTrue(span.xMin <= rest.xMin + 0.001f && span.yMin <= rest.yMin + 0.001f &&
                              span.xMax >= rest.xMax - 0.001f && span.yMax >= rest.yMax - 0.001f,
                    what + "охват " + span + " не накрывает покой " + rest + ".");
                Assert.IsTrue(span.xMin <= hauled.xMin + 0.001f && span.yMin <= hauled.yMin + 0.001f &&
                              span.xMax >= hauled.xMax - 0.001f && span.yMax >= hauled.yMax - 0.001f,
                    what + "охват " + span + " не накрывает захват " + hauled + ".");

                // …и то же самое в пути: прямоугольник едет вместе с деталью, офсет не «прилипает» к дому.
                Vector2 midway = Vector2.Lerp(detail.Home, level.VesselCentre, 0.37f);
                Rect moved = LevelCatalog.DrawnRectAt(detail, midway, true);
                Assert.AreEqual(midway - detail.Home, moved.center - hauled.center,
                    what + "полный арт не поехал вместе с деталью.");

                // Это и есть блокер 2 в чистой арифметике: мысль, накрывшая фрагмент целиком, не
                // накрывает то, чем деталь оказалась — то есть два прямоугольника дают РАЗНЫЙ ответ на
                // вопрос «деталь скрыта мыслью?», и спрашивать его про покой раскрытой прятки нельзя.
                Rect blob = HintPlacement.Centred(detail.Home, Thought.SizeOf(ThoughtStrength.Medium));
                Assert.IsTrue(Covers(blob, rest),
                    what + "средняя мысль не накрывает фрагмент — премисса блокера 2 не воспроизводится.");
                Assert.IsFalse(Covers(blob, hauled),
                    what + "средняя мысль накрыла и полный арт — блокер 2 нечем показать.");
            }
        }

        /// <summary>Does <paramref name="over"/> cover <paramref name="what"/> whole?</summary>
        private static bool Covers(Rect over, Rect what) =>
            over.xMin <= what.xMin && over.yMin <= what.yMin &&
            over.xMax >= what.xMax && over.yMax >= what.yMax;

        private static void AssertSameRect(Rect expected, Rect actual, string message)
        {
            Assert.AreEqual(expected.xMin, actual.xMin, 0.001f, message + " (x)");
            Assert.AreEqual(expected.yMin, actual.yMin, 0.001f, message + " (y)");
            Assert.AreEqual(expected.width, actual.width, 0.001f, message + " (ширина)");
            Assert.AreEqual(expected.height, actual.height, 0.001f, message + " (высота)");
        }

        /// <summary>
        /// The сбор beat's obstacle is everything the travelling detail will be — including the body a
        /// hideaway grows on the way (блокер 1, ревизия Codex 2026-09-25).
        ///
        /// <c>LevelScreen.AddDetailTrack</c> laid its chain out for <c>spec.Size</c>, i.e. for the 196×63
        /// fin, and the plate was placed clear of THAT. Two frames into the haul the fin is a 292×180
        /// shark standing 58 px lower, and the plate — placed once for the length of the beat and never
        /// moved — has no idea. The chain is now the union of both drawings
        /// (<see cref="LevelCatalog.DrawnRectSpanAt"/>).
        ///
        /// Held on the CHAIN and not only on where the plate ended up, because «где оказалась плашка» is
        /// a fact about today's five compositions: on level 1 the сбор sentence happens to land far
        /// enough from the water that the old arithmetic got away with it, and a guard that only watches
        /// the outcome would have stayed green through the whole bug and gone red the first time somebody
        /// moved the bucket. What is wrong is the obstacle, so the obstacle is what is measured — and the
        /// placement claim is kept underneath it as the end-to-end half.
        /// </summary>
        [Test]
        public void TheSborBeatsObstacle_IsTheWholeAnimal_NotTheFragmentItRestsAs()
        {
            var track = new List<Rect>();
            var blocked = new List<Rect>();

            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Rect[] standing = LevelCatalog.HintObstaclesOf(level);

                for (int i = 0; i < level.DetailCount; i++)
                {
                    ArtDetail spec = level.Details[i];
                    Vector2 about = LevelCatalog.AnchorOf(spec);
                    string what = level.Title + " · «" + spec.Name + "»: ";

                    track.Clear();
                    LevelScreen.AddDetailTrack(track, spec, about, level.VesselCentre);
                    Assert.Greater(track.Count, 1, what + "цепочка препятствия пуста.");

                    // 1. Every link is big enough for the drawing the detail can be showing under it.
                    Rect widest = ByHand(spec, Vector2.zero, true);
                    Rect resting = ByHand(spec, Vector2.zero, false);
                    float wide = Mathf.Max(widest.width, resting.width);
                    float tall = Mathf.Max(widest.height, resting.height);

                    for (int k = 0; k < track.Count; k++)
                    {
                        Assert.GreaterOrEqual(track[k].width, wide,
                            what + "звено цепочки " + track[k] + " уже, чем деталь бывает (" +
                            wide.ToString("0") + " px) — плашку поставят рядом с фрагментом, а ляжет " +
                            "она на то, чем деталь окажется (блокер 1).");
                        Assert.GreaterOrEqual(track[k].height, tall,
                            what + "звено цепочки " + track[k] + " ниже, чем деталь бывает (" +
                            tall.ToString("0") + " px) — блокер 1.");
                    }

                    // 2. …and the two ends of the run are covered by the links that stand there: the
                    // detail at home and the detail arriving at the vessel, in BOTH of its skins.
                    AssertLinkCovers(track[0], spec, spec.Home, what + "в начале пути ");
                    AssertLinkCovers(track[track.Count - 1], spec, level.VesselCentre,
                        what + "у самого сосуда ");

                    // 3. The end-to-end half: the plate the beat actually places touches neither picture
                    // at any point of the travel.
                    blocked.Clear();
                    blocked.AddRange(standing);
                    blocked.AddRange(track);

                    Vector2 size = HintPlate.SizeFor(GameTexts.BeatCollect);
                    Rect plate = HintPlacement.Centred(
                        HintPlacement.Beside(size, about, blocked), size);

                    Assert.IsTrue(HintPlacement.InsideFrame(plate),
                        what + "плашка сбора вылезла за кадр: " + plate);

                    for (int s = 0; s <= TrackProbes; s++)
                    {
                        float share = s / (float)TrackProbes;
                        Vector2 at = Vector2.Lerp(spec.Home, level.VesselCentre, share);

                        Assert.IsFalse(plate.Overlaps(ByHand(spec, at, false)),
                            what + "плашка сбора " + plate + " легла на деталь в покое " +
                            ByHand(spec, at, false) + " на доле пути " + share.ToString("0.00") + ".");

                        Assert.IsFalse(plate.Overlaps(ByHand(spec, at, true)),
                            what + "плашка сбора " + plate + " легла на проявившуюся деталь " +
                            ByHand(spec, at, true) + " на доле пути " + share.ToString("0.00") +
                            " (блокер 1, ревизия Codex 2026-09-25).");
                    }
                }
            }
        }

        /// <summary>One link of the chain has to cover both skins of the detail standing at that point.</summary>
        private static void AssertLinkCovers(Rect link, ArtDetail spec, Vector2 at, string what)
        {
            Assert.IsTrue(Covers(link, ByHand(spec, at, false)),
                what + "звено " + link + " не накрывает деталь в покое " + ByHand(spec, at, false) + ".");
            Assert.IsTrue(Covers(link, ByHand(spec, at, true)),
                what + "звено " + link + " не накрывает проявившуюся деталь " + ByHand(spec, at, true) +
                " (блокер 1, ревизия Codex 2026-09-25).");
        }

        /// <summary>How finely the travel is re-walked when the placed plate is checked against it.</summary>
        private const int TrackProbes = 96;

        /// <summary>
        /// The detail's rectangle spelled out from the catalogue's raw numbers, on purpose.
        ///
        /// The guard above must not be able to agree with the bug: if it asked
        /// <see cref="LevelCatalog.DrawnRectAt"/> what the detail's rectangle is, then a seam reverted to
        /// <c>spec.Size</c> would move the obstacle and the probe together and the test would stay green
        /// on a plate lying across a shark. So the probe re-states the arithmetic instead of calling it.
        /// </summary>
        private static Rect ByHand(ArtDetail spec, Vector2 at, bool captured)
        {
            bool whole = captured && !string.IsNullOrEmpty(spec.CaptureSprite);
            Vector2 centre = whole ? at + spec.CaptureOffset : at;
            Vector2 size = whole ? spec.CaptureSize : spec.Size;
            return new Rect(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f, size.x, size.y);
        }

        // ---- MECHANICS §6, the progression ---------------------------------------------------------

        [Test]
        public void TheShippedLadder_GetsHarderEveryLevel()
        {
            // «С каждым уровнем больше мыслей, выше прочность, быстрее наплыв» — and since 2026-08-07
            // the ladder is ONE quantity, not a column per knob.
            //
            // It had to become one. The founder's order for this round is «на первом мыслей больше, на
            // втором их МЕНЬШЕ, но крупнее», and a rung-by-rung «не меньше мыслей в волне» forbids
            // exactly that — it can only read «меньше блобов» as a step down, when the step is sideways
            // and the pressure still climbs. So what is measured is the screen the waves buy per
            // second: the AREA of one wave divided by the interval between two. Sending fewer bigger
            // blobs and sending more smaller ones are two ways of spending the same budget, and the
            // budget is what the player feels.
            //
            // That area is the one the thoughts are really SPAWNED at since 2026-08-08 (see
            // WavePressureOf), not the class box. In class boxes the shipped ladder read
            // 10 080 → 12 320 → 49 156 → 70 700 → 88 000 px²/с and was green; in the sizes the game
            // actually draws, the same numbers read 23 695 → 17 318 → …, i.e. the very first step went
            // DOWN — the ladder was being proved on arithmetic about a rectangle nothing is drawn at.
            //
            // The level's LENGTH used to be a column here too, and went out with the timer.
            for (int i = 1; i < LevelCatalog.Count; i++)
            {
                string step = "У" + i + " → У" + (i + 1) + ": ";

                Assert.Greater(WavePressureOf(i), WavePressureOf(i - 1),
                    step + "давление волн обязано расти (" + WavePressureOf(i - 1).ToString("0") +
                    " → " + WavePressureOf(i).ToString("0") + " px²/с).");
                Assert.LessOrEqual(TuningConfig.WaveIntervalOf(i), TuningConfig.WaveIntervalOf(i - 1),
                    step + "волны должны приходить не реже.");
                Assert.GreaterOrEqual(TuningConfig.DurabilityStrongOf(i), TuningConfig.DurabilityStrongOf(i - 1),
                    step + "крепкие мысли должны быть не слабее.");
                Assert.GreaterOrEqual(TuningConfig.DriftOf(i), TuningConfig.DriftOf(i - 1),
                    step + "наплыв должен быть не медленнее.");
                Assert.GreaterOrEqual(TuningConfig.ThoughtGrowthPercentPerSecOf(i),
                    TuningConfig.ThoughtGrowthPercentPerSecOf(i - 1),
                    step + "мысли должны разрастаться не медленнее.");
                Assert.GreaterOrEqual(TuningConfig.ThoughtGrowthCapOf(i),
                    TuningConfig.ThoughtGrowthCapOf(i - 1),
                    step + "потолок роста мыслей не должен опускаться.");
            }

            int last = LevelCatalog.Count - 1;
            Assert.Greater(TuningConfig.DurabilityStrongOf(last), TuningConfig.DurabilityStrongOf(0),
                "Крепкие мысли последнего уровня не крепче первого — прогрессии нет.");
            Assert.Greater(WavePressureOf(last), WavePressureOf(0) * 2f,
                "Давление последнего уровня не выросло даже вдвое против первого — прогрессии нет.");
        }

        /// <summary>
        /// The founder's wave ladder, as a test rather than as a comment — «волны», плейтест
        /// 2026-09-22, dictated level by level.
        ///
        /// **This supersedes her order of 2026-08-07** («на первом мыслей больше, на втором их МЕНЬШЕ,
        /// но крупнее»), which this same test used to hold. That shape was about two levels differing
        /// in TEXTURE while the tap ran forever; the new one is about a level being a finite amount of
        /// interference — «у уровня есть общий запас мыслей: на первом 5, на втором 10, на третьем 15,
        /// раз в 10 секунд» — and under it level 2 sends two per wave against level 1's one, which is
        /// the exact opposite of the sentence this test was written to protect. Superseded by the
        /// author of the thing it was protecting, so: rewritten, not deleted.
        ///
        /// Written as its own case because the ladder test cannot say it: that one only knows that
        /// pressure climbs, and it would stay green on any composition that happened to climb.
        /// </summary>
        [Test]
        public void TheWaveLadder_IsTheFoundersOwnNumbers()
        {
            int[] budgets = { 5, 10, 15, 20, 25 };
            for (int i = 0; i < LevelCatalog.Count; i++)
            {
                string level = "У" + (i + 1) + ": ";

                Assert.AreEqual(budgets[i], TuningConfig.ThoughtBudgetOf(i),
                    level + "запас мыслей не тот, который продиктовала основательница 2026-09-22.");
                Assert.AreEqual(10f, TuningConfig.WaveIntervalOf(i), 1e-3f,
                    level + "волны приходят не раз в 10 секунд.");

                // Five waves per level, on every rung — that is what makes the budgets a ladder rather
                // than five unrelated numbers.
                Assert.AreEqual(5, TuningConfig.WavesInBudgetOf(i),
                    level + "запас перестал делиться на пять волн — лестница сломана.");
                Assert.AreEqual(i + 1, TuningConfig.ThoughtsPerWaveOf(i),
                    level + "в волне не " + (i + 1) + " мыслей.");

                // …and the ramp is off, because «раз в 10 секунд» is a statement about the interval.
                Assert.AreEqual(0f, TuningConfig.PressureRampPercentOf(i), 1e-3f,
                    level + "интервал волн сокращается — это уже не «раз в 10 секунд».");
            }

            // The two rungs the founder spelled out by composition, not only by count.
            Assert.AreEqual(1, TuningConfig.WaveWeakOf(0), "У1: волна — одна СЛАБАЯ мысль.");
            Assert.AreEqual(0, TuningConfig.WaveMediumOf(0) + TuningConfig.WaveStrongOf(0),
                "У1: «все слабые» — ничего крупнее в волне быть не должно.");
            Assert.AreEqual(1, TuningConfig.WaveWeakOf(1), "У2: волна — пара «слабая + средняя».");
            Assert.AreEqual(1, TuningConfig.WaveMediumOf(1), "У2: волна — пара «слабая + средняя».");
            Assert.AreEqual(0, TuningConfig.WaveStrongOf(1), "У2: крепких в волне ещё нет.");

            // …and the class ceiling still climbs, which is the half of the old order that survived.
            Assert.Greater(HeaviestClassOf(2), HeaviestClassOf(1),
                "У3 обязан впервые прислать крепкую мысль.");
        }

        /// <summary>
        /// The budget is what the field actually spends: five thoughts on level 1 and then nothing,
        /// however long the level runs.
        ///
        /// The other half of «волны» — the founder's own «когда запас исчерпан и всё отогнано, мысли
        /// кончились». Without this the budget would be a number in a config that nothing reads.
        /// </summary>
        [Test]
        public void ALevel_SendsItsBudgetAndThenStops()
        {
            for (int i = 0; i < LevelCatalog.Count; i++)
            {
                TuningConfig.ResetToDefaults();
                TuningConfig.ApplyLevel(i);

                var field = new ThoughtField { Labels = LevelCatalog.At(i).ThoughtSprites };
                field.ArtFitter = ArtLibrary.FitThought;

                // Ten minutes of a level nobody touches — six times the whole budget's worth of waves.
                for (int step = 0; step < 2400; step++) field.Tick(0.25f, Vector2.zero, 0, true);

                Assert.AreEqual(TuningConfig.ThoughtBudgetOf(i), field.SpentFromBudget,
                    "У" + (i + 1) + ": уровень потратил не свой запас мыслей.");
                Assert.AreEqual(0, field.BudgetLeft, "У" + (i + 1) + ": запас не исчерпан за 10 минут.");
                Assert.AreEqual(TuningConfig.ThoughtBudgetOf(i), field.Thoughts.Count,
                    "У" + (i + 1) + ": на экране не весь запас — кто-то лопнул без удара.");

                // …and «мысли кончились» is only true once the screen is clear of them as well.
                Assert.IsFalse(field.ThoughtsAreOver,
                    "У" + (i + 1) + ": «мысли кончились» при полном экране мыслей.");
                field.ApplyHits(400, Vector2.zero);
                Assert.IsTrue(field.ThoughtsAreOver,
                    "У" + (i + 1) + ": запас потрачен и экран чист, а «мысли кончились» не наступило.");
            }

            TuningConfig.ResetToDefaults();
        }

        /// <summary>
        /// A thought that drifts never leaves the frame — the rule the budget made load-bearing.
        ///
        /// «Дрейф к центру» was a direction and nothing else: a blob crossed the centre and sailed out
        /// the far side. With waves for ever that was invisible; with a budget of five it meant level 1
        /// sat at 0 % coverage after three minutes because everything it owned had left the screen.
        /// </summary>
        [Test]
        public void ADriftingThought_NeverLeavesTheFrame()
        {
            TuningConfig.ResetToDefaults();
            TuningConfig.ApplyLevel(0);

            var field = new ThoughtField { Labels = LevelCatalog.At(0).ThoughtSprites };
            field.ArtFitter = ArtLibrary.FitThought;

            for (int step = 0; step < 1200; step++)
            {
                field.Tick(0.25f, Vector2.zero, 0, true);
                foreach (Thought t in field.Thoughts)
                {
                    Assert.That(t.Position.x, Is.InRange(-1f, ThoughtField.ScreenWidth + 1f),
                        "Мысль уехала за кадр по X: " + t.Position.x.ToString("0"));
                    Assert.That(t.Position.y, Is.InRange(-1f, ThoughtField.ScreenHeight + 1f),
                        "Мысль уехала за кадр по Y: " + t.Position.y.ToString("0"));
                }
            }

            TuningConfig.ResetToDefaults();
        }

        /// <summary>
        /// Area one wave of level <paramref name="i"/> puts on screen, per second — measured on the
        /// thoughts that level ACTUALLY sends.
        ///
        /// It used to be measured in class boxes, and that made the whole ladder arithmetic about a
        /// rectangle nothing is drawn at (Codex review, 2026-08-08): a level's five silhouettes are
        /// cover-fitted to the class, so how much screen a wave buys depends on how elongated that
        /// level's drawings are. Measured properly, level 1 — whose set holds a 454×1521 bottle and a
        /// 645×1688 guitar — was sending MORE screen per second than level 2, i.e. the shipped ladder
        /// stepped down where the test said it stepped up.
        ///
        /// Averaged over the level's set because which silhouette a wave draws is the field's own
        /// round-robin, not a per-level choice: what the ladder can state is the expected wave.
        /// </summary>
        private static float WavePressureOf(int i)
        {
            float area =
                TuningConfig.WaveWeakOf(i) * SpawnAreaOf(i, ThoughtStrength.Weak) +
                TuningConfig.WaveMediumOf(i) * SpawnAreaOf(i, ThoughtStrength.Medium) +
                TuningConfig.WaveStrongOf(i) * SpawnAreaOf(i, ThoughtStrength.Strong);
            return area / Mathf.Max(0.5f, TuningConfig.WaveIntervalOf(i));
        }

        /// <summary>The area an average thought of level <paramref name="i"/> spawns at, in class <paramref name="strength"/>.</summary>
        private static float SpawnAreaOf(int i, ThoughtStrength strength)
        {
            string[] set = LevelCatalog.At(i).ThoughtSprites;
            float total = 0f;
            foreach (string key in set)
            {
                var thought = new Thought { Strength = strength, Label = key };
                ArtLibrary.FitThought(thought);
                Vector2 spawn = thought.SpawnSize;
                total += spawn.x * spawn.y;
            }
            return total / Mathf.Max(1, set.Length);
        }

        /// <summary>The biggest class this level's wave contains, 0 = S, 1 = M, 2 = L.</summary>
        private static int HeaviestClassOf(int i)
        {
            if (TuningConfig.WaveStrongOf(i) > 0) return 2;
            if (TuningConfig.WaveMediumOf(i) > 0) return 1;
            return 0;
        }

        /// <summary>
        /// «Разрастаются на весь экран» (founder, 2026-09-22), as arithmetic: the SMALLEST class a
        /// level sends, grown to its shipped ceiling, is bigger than the frame.
        ///
        /// The smallest class, because that is the hard case — a strong thought starts at 420×320 and
        /// gets there easily, while a weak one starts at 180 wide and needs the ceiling to be worth
        /// more than ten. And on the real silhouettes, cover-fitted, not on the class box: what the
        /// player sees is the sprite.
        ///
        /// Kept here rather than on frame 34 because frame 34 cannot show it: a thought at the shipped
        /// ceiling has no edges inside the frame, and the picture that compares an old thought with a
        /// fresh one needs both of them whole.
        /// </summary>
        [Test]
        public void TheShippedGrowthCeiling_TakesAWeakThoughtPastTheWholeFrame()
        {
            for (int i = 0; i < LevelCatalog.Count; i++)
            {
                TuningConfig.ResetToDefaults();
                TuningConfig.ApplyLevel(i);

                float cap = TuningConfig.ThoughtGrowthCapOf(i);
                foreach (string key in LevelCatalog.At(i).ThoughtSprites)
                {
                    var thought = new Thought { Strength = ThoughtStrength.Weak, Label = key };
                    ArtLibrary.FitThought(thought);
                    Vector2 grown = thought.SpawnSize * cap;

                    Assert.GreaterOrEqual(grown.x, ThoughtField.ScreenWidth,
                        "У" + (i + 1) + " «" + key + "»: слабая мысль на потолке роста ×" +
                        cap.ToString("0.0") + " — " + grown.x.ToString("0") +
                        " px по ширине при кадре " + ThoughtField.ScreenWidth +
                        ". «Разрастаются на весь экран» не выполняется (founder 2026-09-22).");
                    Assert.GreaterOrEqual(grown.y, ThoughtField.ScreenHeight,
                        "У" + (i + 1) + " «" + key + "»: слабая мысль на потолке роста не закрывает " +
                        "кадр по высоте (" + grown.y.ToString("0") + " px).");
                }
            }

            TuningConfig.ResetToDefaults();
        }

        /// <summary>
        /// A level nobody plays still ends: with the sensors never touched, every level's screen fills
        /// up and the level is LOST, inside <see cref="DefeatBudgetSeconds"/>.
        ///
        /// This is the other half of «поражение = мысли заполнили экран», and until 2026-08-08 the
        /// first two levels did not have it: level 1's coverage settled around half the frame and
        /// stayed there — its wave clock never shortened and the thoughts drift straight across — so
        /// the only failure condition the game has was, on the level that teaches it, unreachable.
        /// The ladder test could not see that; it compares levels to each other, and a ladder of
        /// unreachable rungs is monotonic too.
        ///
        /// Run on the model rather than in PlayMode: this is three simulated minutes per level, and
        /// what is being asked about is the rules — waves, drift, growth, coverage — none of which
        /// needs a renderer. The field is driven at 4 Hz, which the same simulation says is within a
        /// few seconds of the frame-rate answer (the thoughts move 6–15 px per step).
        /// </summary>
        [Test]
        public void EveryLevel_FillsUpAndIsLost_WhenNobodyBeatsAThoughtOff()
        {
            for (int i = 0; i < LevelCatalog.Count; i++)
            {
                TuningConfig.ResetToDefaults();
                TuningConfig.ApplyLevel(i);

                var field = new ThoughtField { Labels = LevelCatalog.At(i).ThoughtSprites };
                field.ArtFitter = ArtLibrary.FitThought;
                var rules = new LevelRules();
                rules.Restart(LevelCatalog.At(i).DetailCount);

                float elapsed = 0f;
                while (elapsed < DefeatBudgetSeconds && rules.Outcome == LevelOutcome.Playing)
                {
                    // No hits: the отгон is exactly what is being left out.
                    field.Tick(DefeatStepSeconds, Vector2.zero, 0, rules.SpawningAllowed);
                    rules.Tick(DefeatStepSeconds, field.OverlapPercent);
                    elapsed += DefeatStepSeconds;
                }

                Assert.AreEqual(LevelOutcome.Lose, rules.Outcome,
                    LevelCatalog.At(i).Title + ": за " + DefeatBudgetSeconds +
                    " с бездействия экран так и не заполнился (дошёл до " +
                    field.OverlapPercent.ToString("0") + " % при пороге " +
                    TuningConfig.LossOverlapPercent.ToString("0") + " %) — поражения на этом уровне нет.");
                Assert.AreEqual(LevelRules.LoseByThoughts, rules.LoseReason,
                    LevelCatalog.At(i).Title + ": уровень проигран не мыслями.");

                // Written out rather than only asserted: the budget is loose on purpose, so the number
                // that actually matters — how long a level survives an idle player — has to be visible
                // somewhere the founder's tuning round can read it off.
                TestContext.WriteLine("У" + (i + 1) + " «" + LevelCatalog.At(i).Title +
                                      "»: экран заполнен за " + elapsed.ToString("0.0") +
                                      " с бездействия, мыслей на экране " + field.Thoughts.Count + ".");
            }
        }

        /// <summary>
        /// How long a player may do nothing before the screen has to have closed over them. Three
        /// minutes is deliberately generous — the shipped ladder loses in 124 s on level 1 and 18 s on
        /// level 5 — so that this guards the RULE (a level that cannot be lost) and not a balance
        /// number the founder is free to move.
        /// </summary>
        private const float DefeatBudgetSeconds = 180f;

        /// <summary>Simulation step — coverage is recomputed on every one of these.</summary>
        private const float DefeatStepSeconds = 0.25f;

        /// <summary>
        /// The clock is gone, and the ladder must not have quietly kept it: no per-level band may
        /// still carry a duration, and nothing may reintroduce «поражение по времени».
        ///
        /// Stated as a test rather than left to the compiler because the removal is a DESIGN decision
        /// (founder, 2026-08-07) — the field could come back as a knob nobody reads, which is exactly
        /// how a superseded rule survives a supersede.
        /// </summary>
        [Test]
        public void TheLevelBands_CarryNoDuration_TheTimerIsGone()
        {
            string[] gone = System.Linq.Enumerable.ToArray(
                System.Linq.Enumerable.Select(
                    System.Linq.Enumerable.Where(
                        typeof(TuningConfig).GetFields(
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.Static),
                        f => f.Name.Contains("LevelSeconds") || f.Name.Contains("TutorialTimer")),
                    f => f.Name));

            Assert.IsEmpty(gone,
                "В TuningConfig остались таймерные ручки: " + string.Join(", ", gone));

            var rules = new LevelRules();
            rules.Restart(5);
            for (int frame = 0; frame < 60 * 200; frame++) rules.Tick(1f / 60f, 0f);
            Assert.AreEqual(LevelOutcome.Playing, rules.Outcome,
                "Уровень всё ещё умеет кончаться по времени.");
        }

        [Test]
        public void ApplyLevel_PutsThatLevelsBandIntoTheLiveValues()
        {
            for (int i = 0; i < LevelCatalog.Count; i++)
            {
                TuningConfig.ApplyLevel(i);

                Assert.AreEqual(TuningConfig.WaveIntervalOf(i), TuningConfig.WaveIntervalSeconds, 1e-3f);
                Assert.AreEqual(TuningConfig.ThoughtGrowthPercentPerSecOf(i),
                    TuningConfig.ThoughtGrowthPercentPerSec, 1e-3f);
                Assert.AreEqual(TuningConfig.ThoughtGrowthCapOf(i), TuningConfig.ThoughtGrowthCap, 1e-3f);
                Assert.AreEqual(TuningConfig.DurabilityWeakOf(i), TuningConfig.DurabilityWeak);
                Assert.AreEqual(TuningConfig.DurabilityMediumOf(i), TuningConfig.DurabilityMedium);
                Assert.AreEqual(TuningConfig.DurabilityStrongOf(i), TuningConfig.DurabilityStrong);
                Assert.AreEqual(TuningConfig.DriftOf(i), TuningConfig.DriftPxPerSec, 1e-3f);
                Assert.AreEqual(TuningConfig.ThoughtsPerWaveOf(i), TuningConfig.ThoughtsPerWave);
            }
        }

        /// <summary>
        /// The way back OUT of a level band — the bug Codex found on 2026-09-22.
        ///
        /// «Уровень 5» off the stand menu copies its band onto the shared values, `ThoughtBudget`
        /// among them; the stand menu and the scenettes used to read them as they were left. So the
        /// founder came back from level 5 into «Отгон взмахами» — a rig whose one job is to send
        /// blobs for as long as she watches it — and it stopped sending them after the twenty-fifth.
        /// The budget is the value that BREAKS a rig rather than merely retunes it, because zero
        /// means «без ограничения» and no stand slider shows it.
        ///
        /// Checked as the general claim, not as the one value: the stand gets back everything the
        /// band took, and gets back what it HAD rather than the defaults (the founder's own sliders
        /// live on those same shared values, and the panel persists them).
        /// </summary>
        [Test]
        public void LeavingALevelBand_GivesTheStandBackTheValuesItWasRunningOn()
        {
            // The stand as the founder left it: a budget of nought (endless waves) and a couple of
            // sliders dragged away from the shipped numbers.
            TuningConfig.WaveIntervalSeconds = 3.5f;
            TuningConfig.DurabilityWeak = 7;
            Assert.AreEqual(0, TuningConfig.ThoughtBudget, "Стенд обязан стоять на безлимите.");
            Assert.IsFalse(TuningConfig.ALevelBandIsApplied);

            TuningConfig.ApplyLevel(4);
            Assert.AreEqual(TuningConfig.ThoughtBudgetOf(4), TuningConfig.ThoughtBudget,
                "Полоса уровня 5 не применилась — тест ни о чём.");
            Assert.IsTrue(TuningConfig.ALevelBandIsApplied);

            // …and back to the stand (its menu and every scenette call this as they boot).
            TuningConfig.LeaveLevelBand();

            Assert.AreEqual(0, TuningConfig.ThoughtBudget,
                "Запас мыслей уровня утёк на стенд: сценка кончится после " +
                TuningConfig.ThoughtBudgetOf(4) + "-й мысли, хотя обязана быть безлимитной.");
            Assert.AreEqual(3.5f, TuningConfig.WaveIntervalSeconds, 1e-3f,
                "Стенду вернули не его значение, а дефолт — настройка основательницы потеряна.");
            Assert.AreEqual(7, TuningConfig.DurabilityWeak,
                "Стенду вернули не его значение, а дефолт — настройка основательницы потеряна.");
            Assert.IsFalse(TuningConfig.ALevelBandIsApplied);

            // A second call has nothing to give back and must not undo anything.
            TuningConfig.LeaveLevelBand();
            Assert.AreEqual(3.5f, TuningConfig.WaveIntervalSeconds, 1e-3f);
        }

        /// <summary>
        /// …and the line holds for the NEXT value someone gives a level band.
        ///
        /// Every shared static ApplyLevel touches must be a row of the band table, otherwise the
        /// stand's way home has a hole in it and the hole is invisible until a scenette dies of it.
        /// Read by reflection over the whole of <c>TuningConfig</c> rather than as a list of names:
        /// a list of names is the very thing that falls behind.
        /// </summary>
        [Test]
        public void ALevelBand_GivesBackEveryValueItTakes()
        {
            System.Reflection.FieldInfo[] fields = System.Linq.Enumerable.ToArray(
                System.Linq.Enumerable.Where(
                    typeof(TuningConfig).GetFields(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static),
                    f => !f.IsLiteral && !f.IsInitOnly));

            var before = new Dictionary<string, object>();
            foreach (System.Reflection.FieldInfo field in fields) before[field.Name] = field.GetValue(null);

            // Level 5 is the far end of every ladder, so anything a band carries differs there.
            TuningConfig.ApplyLevel(4);

            var taken = new List<string>();
            foreach (System.Reflection.FieldInfo field in fields)
                if (!Equals(field.GetValue(null), before[field.Name])) taken.Add(field.Name);

            Assert.IsNotEmpty(taken, "ApplyLevel не изменил ни одного значения — тест ни о чём.");
            CollectionAssert.Contains(taken, "ThoughtBudget",
                "Запас мыслей перестал быть частью полосы уровня — этот тест надо переписать.");

            TuningConfig.LeaveLevelBand();

            var kept = new List<string>();
            foreach (System.Reflection.FieldInfo field in fields)
                if (!Equals(field.GetValue(null), before[field.Name])) kept.Add(field.Name);

            Assert.IsEmpty(kept,
                "Полоса уровня осталась на общих значениях после выхода на стенд: " +
                string.Join(", ", kept) + ". Новое значение полосы обязано быть строкой таблицы " +
                "BandValues в TuningConfig — иначе оно утечёт в грейбокс-сценки.");
        }

        [Test]
        public void APanelRowOfTheActiveLevel_ChangesTheLiveValueImmediately()
        {
            // Done contract §7: «правки действуют сразу». A row of the level being played has to reach
            // the rules in the same frame; a row of another level must not.
            TuningConfig.ActiveLevelIndex = 1;
            TuningConfig.ApplyLevel(1);

            TuningConfig.SetWaveInterval(1, 3.25f);
            Assert.AreEqual(3.25f, TuningConfig.WaveIntervalSeconds, 1e-3f,
                "Правка активного уровня не дошла до живых значений.");

            TuningConfig.SetWaveInterval(2, 9.5f);
            Assert.AreEqual(3.25f, TuningConfig.WaveIntervalSeconds, 1e-3f,
                "Правка ЧУЖОГО уровня переписала живые значения.");
            Assert.AreEqual(9.5f, TuningConfig.WaveIntervalOf(2), 1e-3f, "Правка не записалась в свой уровень.");

            TuningConfig.ActiveLevelIndex = 0;
        }
    }
}
