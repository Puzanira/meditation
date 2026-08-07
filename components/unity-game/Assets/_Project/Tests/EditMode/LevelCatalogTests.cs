using System.Collections.Generic;
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

                foreach (ArtDetail detail in level.Details) Check(detail.Sprite, missing);
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

        [Test]
        public void TheHudSlots_FitOnScreen_EvenForTheLongestLevel()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Rect slots = LevelCatalog.SlotsRectOf(level);

                Assert.GreaterOrEqual(slots.xMin, 0f, level.Title + ": ряд слотов уходит за левый край.");
                Assert.GreaterOrEqual(slots.yMin, 0f, level.Title + ": ряд слотов уходит за верхний край.");
                Assert.LessOrEqual(slots.yMax, DesignStage.DesignHeight,
                    level.Title + ": ряд слотов уходит за нижний край.");
                Assert.Less(slots.xMax, LevelCatalog.SunRectOf(level).xMin,
                    level.Title + ": ряд слотов дотягивается до таймера-солнца.");
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
        /// Footprints are the ones actually drawn, not the nominal ones from SCREENS: the sun carries
        /// its caption underneath, and the crank indicator carries an arc at r + 14 and a halo at 86.
        /// </summary>
        [Test]
        public void NoHudWidget_OverlapsAnyDetail_OnAnyLevel()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            foreach (ArtDetail detail in level.Details)
            {
                Rect art = LevelCatalog.RectOf(detail);
                string what = level.Title + " · «" + detail.Name + "»";

                AssertClear(LevelCatalog.SunRectOf(level), art, what, "таймер-солнце");
                AssertClear(LevelCatalog.TimerLabelRectOf(level), art, what, "подпись таймера");
                AssertClear(LevelCatalog.CrankRectOf(level), art, what, "индикатор динамо");
                AssertClear(LevelCatalog.SlotsRectOf(level), art, what, "ряд слотов");
            }
        }

        [Test]
        public void NoHudWidget_OverlapsTheVessel_OnAnyLevel()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Rect vessel = LevelCatalog.VesselRectOf(level);
                string what = level.Title + " · сосуд";

                AssertClear(LevelCatalog.SunRectOf(level), vessel, what, "таймер-солнце");
                AssertClear(LevelCatalog.TimerLabelRectOf(level), vessel, what, "подпись таймера");
                AssertClear(LevelCatalog.CrankRectOf(level), vessel, what, "индикатор динамо");
                AssertClear(LevelCatalog.SlotsRectOf(level), vessel, what, "ряд слотов");
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
        /// No teaching button covers a detail, the vessel, or a HUD widget — приёмка п.3 of the drop,
        /// extended to the tutorial's own plates by the design gate of 2026-08-07.
        ///
        /// It was broken exactly the way an offset breaks: «КРУТИ РУЧКУ» sat 230 px above the dynamo
        /// indicator, was clamped back into the frame, and the 468×88 plate landed on the whole shark
        /// fin and the left edge of the bucket. Checked here, on the catalogue, because a placement is
        /// arithmetic and this way it is decided before there is a scene to be surprised by.
        ///
        /// Both hints of beat 2 are placed, in the order the level places them, so the second one has to
        /// clear the first as well.
        /// </summary>
        [Test]
        public void NoTeachingButton_CoversADetail_TheVessel_OrTheHud()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Rect[] obstacles = LevelCatalog.HintObstaclesOf(level);
                Rect[] art = LevelCatalog.ArtRectsOf(level);

                AssertPlateIsClear(level, ArtScreens.ButtonAim, LevelCatalog.AnchorOf(level.Details[0]),
                    obstacles, art, "НАВОДИ");
                Rect crank = AssertPlateIsClear(level, ArtScreens.ButtonCrank, level.CrankIndicatorCentre,
                    obstacles, art, "КРУТИ РУЧКУ");

                // «ТАЩИ» rides the detail, so it is checked along the whole thread rather than once.
                var withCrank = new List<Rect>(obstacles) { crank };
                Vector2 size = ButtonHint.SizeOf(ArtLibrary.Get(ArtScreens.ButtonDrag));
                float side = 90f;

                for (int step = 0; step <= 8; step++)
                {
                    float progress = step / 8f;
                    ArtDetail detail = level.Details[0];
                    Vector2 travelling = Vector2.Lerp(detail.Home, level.VesselCentre, progress) +
                                         LevelCatalog.AnchorOffsetOf(detail);
                    float ring = View.LevelView.RingRadius(detail.Size);

                    // The obstacle set the level itself builds: the travelling detail's WHOLE sprite,
                    // where it is now. The plane is 484 px of rectangle whose ink centre sits at a
                    // quarter of it, so a plate an arm's length from the centroid still lands on the
                    // contrail — frame 18 shipped with 1 px between them (design gate, 2026-08-07).
                    Rect sprite = HintPlacement.Centred(
                        travelling - LevelCatalog.AnchorOffsetOf(detail), detail.Size);
                    Rect keepOff = HintPlacement.Inflate(sprite, HintPlacement.DraggedSpriteMargin);

                    var blocked = new List<Rect>(withCrank) { keepOff };

                    Vector2 spot = HintPlacement.BesideTheThread(size, travelling, level.VesselCentre,
                        ring, blocked, ref side);
                    Rect plate = HintPlacement.Centred(spot, size);

                    Assert.IsFalse(plate.Overlaps(keepOff),
                        level.Title + " · «ТАЩИ» на " + (progress * 100f).ToString("0") +
                        " % пути легла на сам спрайт детали (" + detail.Name + "): плашка " + plate +
                        ", спрайт с полем " + keepOff + ".");

                    Assert.IsTrue(HintPlacement.InsideFrame(plate),
                        level.Title + " · «ТАЩИ» на " + (progress * 100f).ToString("0") +
                        " % пути вылезла за кадр: " + plate);
                    Assert.IsFalse(
                        HintPlacement.SegmentHits(travelling, level.VesselCentre, plate),
                        level.Title + " · «ТАЩИ» на " + (progress * 100f).ToString("0") +
                        " % пути легла на нить.");
                    Assert.Greater(Vector2.Distance(spot, travelling), ring,
                        level.Title + " · «ТАЩИ» на " + (progress * 100f).ToString("0") +
                        " % пути залезла в кольцо прогресса (r=" + ring.ToString("0") + ").");
                }
            }
        }

        private static Rect AssertPlateIsClear(LevelDefinition level, string buttonKey, Vector2 target,
            Rect[] obstacles, Rect[] art, string what)
        {
            Vector2 size = ButtonHint.SizeOf(ArtLibrary.Get(buttonKey));
            Vector2 spot = HintPlacement.Beside(size, target, obstacles, art);
            Rect plate = HintPlacement.Centred(spot, size);

            Assert.IsTrue(HintPlacement.InsideFrame(plate),
                level.Title + " · плашка «" + what + "» вылезла за кадр: " + plate);

            for (int i = 0; i < obstacles.Length; i++)
                Assert.IsFalse(plate.Overlaps(obstacles[i]),
                    level.Title + " · плашка «" + what + "» " + plate + " накрывает " + obstacles[i] + ".");

            return plate;
        }

        /// <summary>
        /// …and it still stands NEXT to what it teaches. Without this the test above is satisfied by
        /// parking every button in the emptiest corner of the frame, which is «не перекрывает» and
        /// «рядом с объектом» traded against each other rather than both honoured.
        /// </summary>
        [Test]
        public void EveryTeachingButton_StaysWithinReachOfWhatItTeaches()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Rect[] obstacles = LevelCatalog.HintObstaclesOf(level);
                Rect[] art = LevelCatalog.ArtRectsOf(level);

                AssertWithinReach(level, ArtScreens.ButtonAim, LevelCatalog.AnchorOf(level.Details[0]),
                    obstacles, art, "НАВОДИ");
                AssertWithinReach(level, ArtScreens.ButtonCrank, level.CrankIndicatorCentre,
                    obstacles, art, "КРУТИ РУЧКУ");
            }
        }

        private static void AssertWithinReach(LevelDefinition level, string buttonKey, Vector2 target,
            Rect[] obstacles, Rect[] art, string what)
        {
            Vector2 size = ButtonHint.SizeOf(ArtLibrary.Get(buttonKey));
            Vector2 spot = HintPlacement.Beside(size, target, obstacles, art);

            Assert.Less(Vector2.Distance(spot, target), MaxHintReach,
                level.Title + " · плашка «" + what + "» уехала от своего объекта на " +
                Vector2.Distance(spot, target).ToString("0") + " px — это уже не «рядом».");
        }

        /// <summary>How far a hint may stand from what it points at, design px — a third of the frame.</summary>
        private const float MaxHintReach = 640f;

        /// <summary>
        /// …and the widgets stay in the corners SCREENS gives them. Without this the test above is
        /// satisfied by parking the sun in the middle of the sky: «не пересекается» is only half the
        /// requirement, the other half is that the player finds the timer where the timer always is.
        /// </summary>
        [Test]
        public void EveryHudWidget_StaysInItsCorner()
        {
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Rect sun = LevelCatalog.SunRectOf(level);
                Assert.Greater(sun.center.x, DesignStage.DesignWidth * 0.75f,
                    level.Title + ": солнце ушло из правого верхнего угла по X.");
                Assert.Less(sun.center.y, DesignStage.DesignHeight * 0.33f,
                    level.Title + ": солнце ушло из правого верхнего угла по Y.");

                Rect crank = LevelCatalog.CrankRectOf(level);
                Assert.Less(crank.center.x, DesignStage.DesignWidth * 0.33f,
                    level.Title + ": индикатор динамо ушёл из левого нижнего угла по X.");
                Assert.Greater(crank.center.y, DesignStage.DesignHeight * 0.75f,
                    level.Title + ": индикатор динамо ушёл из левого нижнего угла по Y.");

                Assert.LessOrEqual(LevelCatalog.SlotsRectOf(level).yMax, LevelCatalog.SceneHeight * 0.5f,
                    level.Title + ": ряд слотов сполз из верхней полосы кадра.");
            }
        }

        /// <summary>
        /// Only the three clashes the founder ruled on may move. Everything else stays on the SCREENS
        /// base, so «HUD переехал» never quietly becomes «HUD переезжает на каждом уровне».
        /// </summary>
        [Test]
        public void HudWidgets_MoveOffTheScreensBase_OnlyWhereTheArtDemandedIt()
        {
            // The five shifts of SCREENS §S3 travelled with their scenes when the levels were
            // renumbered — the same widget still yields to the same painted object — and ONE was
            // cancelled: the metro's crank indicator moved for a puddle the drop has removed.
            Assert.AreEqual(LevelOneData.SunCentre, LevelCatalog.At(0).SunCentre,
                "Набережная: солнцу нечего было уступать — оно должно стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.SunCentre, LevelCatalog.At(1).SunCentre,
                "Офис: солнцу нечего было уступать — оно должно стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.SunCentre, LevelCatalog.At(2).SunCentre,
                "Метро: солнцу нечего было уступать — оно должно стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.SunCentre, LevelCatalog.At(3).SunCentre,
                "Библиотека: солнцу нечего было уступать — оно должно стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.CrankIndicatorCentre, LevelCatalog.At(0).CrankIndicatorCentre,
                "Набережная: индикатору нечего было уступать — он должен стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.CrankIndicatorCentre, LevelCatalog.At(4).CrankIndicatorCentre,
                "Город: индикатору нечего было уступать — он должен стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.SlotsOrigin, LevelCatalog.At(0).SlotsOrigin,
                "Набережная: ряду слотов нечего было уступать — он должен стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.SlotsOrigin, LevelCatalog.At(1).SlotsOrigin,
                "Офис: ряду слотов нечего было уступать — он должен стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.SlotsOrigin, LevelCatalog.At(3).SlotsOrigin,
                "Библиотека: ряду слотов нечего было уступать — он должен стоять на базе SCREENS.");

            // …and the ones that did move stayed as close to the base as the art allowed.
            foreach (LevelDefinition level in LevelCatalog.Levels)
            {
                Assert.LessOrEqual(Vector2.Distance(level.SunCentre, LevelOneData.SunCentre), 200f,
                    level.Title + ": солнце уехало от базы SCREENS дальше, чем нужно.");
                Assert.LessOrEqual(
                    Vector2.Distance(level.CrankIndicatorCentre, LevelOneData.CrankIndicatorCentre), 300f,
                    level.Title + ": индикатор уехал от базы SCREENS дальше, чем нужно.");
                Assert.LessOrEqual(Vector2.Distance(level.SlotsOrigin, LevelOneData.SlotsOrigin), 150f,
                    level.Title + ": ряд слотов уехал от базы SCREENS дальше, чем нужно.");
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

        [Test]
        public void EveryThoughtSilhouette_FitsInsideItsSizeClass()
        {
            // Thought.Size clamps to the class box, but the fit is what decides the picture: coverage
            // is meant to be won by the NUMBER of thoughts, never by one silhouette growing.
            foreach (LevelDefinition level in LevelCatalog.Levels)
            foreach (string key in level.ThoughtSprites)
            foreach (ThoughtStrength strength in new[]
                     { ThoughtStrength.Weak, ThoughtStrength.Medium, ThoughtStrength.Strong })
            {
                Vector2 box = Thought.SizeOf(strength);
                Vector2 fitted = ArtLibrary.FitThought(key, strength);

                Assert.LessOrEqual(fitted.x, box.x + 0.5f, key + " шире своего класса.");
                Assert.LessOrEqual(fitted.y, box.y + 0.5f, key + " выше своего класса.");
                Assert.Greater(fitted.x, 20f, key + ": силуэт выродился в точку.");
                Assert.Greater(fitted.y, 20f, key + ": силуэт выродился в точку.");

                // …and one dimension has to actually touch the box, or "contain" silently shrank it.
                bool touches = Mathf.Abs(fitted.x - box.x) < 1f || Mathf.Abs(fitted.y - box.y) < 1f;
                Assert.IsTrue(touches, key + ": силуэт не вписан в класс, а просто уменьшен.");
            }
        }

        // ---- MECHANICS §6, the progression ---------------------------------------------------------

        [Test]
        public void TheShippedLadder_GetsHarderEveryLevel()
        {
            // «С каждым уровнем больше мыслей, выше прочность, быстрее наплыв» — monotonic in every
            // dimension of PRESSURE. Read off the live values after a reset, so this is a statement
            // about what ships rather than about whatever the last test left behind.
            //
            // The level's LENGTH is deliberately not in this list any more. It used to be, and it could
            // be, only while the levels also got smaller as they got harder (9 details down to 5). The
            // drop of 2026-08-07 turned that around — 5·5·6·8·8 — so the last levels are the ones with
            // the most to find, and a monotonically shrinking clock would make them unwinnable. What
            // stayed true is the thing the founder actually tuned: less time PER DETAIL as it goes on.
            for (int i = 1; i < LevelCatalog.Count; i++)
            {
                string step = "У" + i + " → У" + (i + 1) + ": ";

                Assert.LessOrEqual(TuningConfig.WaveIntervalOf(i), TuningConfig.WaveIntervalOf(i - 1),
                    step + "волны должны приходить не реже.");
                Assert.GreaterOrEqual(TuningConfig.ThoughtsPerWaveOf(i), TuningConfig.ThoughtsPerWaveOf(i - 1),
                    step + "мыслей в волне должно быть не меньше.");
                Assert.GreaterOrEqual(TuningConfig.DurabilityStrongOf(i), TuningConfig.DurabilityStrongOf(i - 1),
                    step + "крепкие мысли должны быть не слабее.");
                Assert.GreaterOrEqual(TuningConfig.DriftOf(i), TuningConfig.DriftOf(i - 1),
                    step + "наплыв должен быть не медленнее.");
            }

            // And the ladder must really climb somewhere, not merely fail to descend — measured across
            // the whole run, so adding a level to the end cannot leave the last steps flat.
            int last = LevelCatalog.Count - 1;
            Assert.Greater(TuningConfig.ThoughtsPerWaveOf(last), TuningConfig.ThoughtsPerWaveOf(0),
                "Последний уровень не шлёт больше мыслей, чем первый — прогрессии нет.");
            Assert.Greater(TuningConfig.DurabilityStrongOf(last), TuningConfig.DurabilityStrongOf(0),
                "Крепкие мысли последнего уровня не крепче первого — прогрессии нет.");
        }

        /// <summary>
        /// The timers, which are the one column the renumbering had to recompute (MECHANICS §6).
        ///
        /// Stated as «seconds per detail», not as «seconds»: the shipped 80·75·84·104·96 is not a
        /// falling curve and must not be — the last two levels hold eight details each and the first
        /// holds five. What has to fall is the room the player is given per detail, and it is the
        /// number the founder will actually be moving on the panel at the gate. Bands rather than
        /// exact values, so a tuning pass is not a red suite.
        /// </summary>
        [Test]
        public void TheTimers_LeaveLessRoomPerDetail_AsTheRunGoesOn()
        {
            int last = LevelCatalog.Count - 1;
            float first = TuningConfig.LevelSecondsOf(0) / LevelCatalog.At(0).DetailCount;
            float final = TuningConfig.LevelSecondsOf(last) / LevelCatalog.At(last).DetailCount;

            Assert.Less(final, first,
                "На деталь в конце прогона должно оставаться меньше времени, чем в начале (" +
                final.ToString("0.0") + " с против " + first.ToString("0.0") + " с).");

            // …and every one of them stays inside the slider the panel declares, so the founder can
            // tune in both directions without the value snapping.
            for (int i = 0; i < LevelCatalog.Count; i++)
            {
                float seconds = TuningConfig.LevelSecondsOf(i);
                Assert.That(seconds, Is.InRange(60f, 180f),
                    "У" + (i + 1) + ": таймер вне диапазона слайдера 60–180 с.");
                float perDetail = seconds / LevelCatalog.At(i).DetailCount;
                Assert.Greater(perDetail, 6f,
                    "У" + (i + 1) + ": на деталь остаётся " + perDetail.ToString("0.0") +
                    " с — при времени сбора 6 с уровень непроходим.");
            }
        }

        [Test]
        public void ApplyLevel_PutsThatLevelsBandIntoTheLiveValues()
        {
            for (int i = 0; i < LevelCatalog.Count; i++)
            {
                TuningConfig.ApplyLevel(i);

                Assert.AreEqual(TuningConfig.LevelSecondsOf(i), TuningConfig.LevelSeconds, 1e-3f);
                Assert.AreEqual(TuningConfig.WaveIntervalOf(i), TuningConfig.WaveIntervalSeconds, 1e-3f);
                Assert.AreEqual(TuningConfig.DurabilityWeakOf(i), TuningConfig.DurabilityWeak);
                Assert.AreEqual(TuningConfig.DurabilityMediumOf(i), TuningConfig.DurabilityMedium);
                Assert.AreEqual(TuningConfig.DurabilityStrongOf(i), TuningConfig.DurabilityStrong);
                Assert.AreEqual(TuningConfig.DriftOf(i), TuningConfig.DriftPxPerSec, 1e-3f);
                Assert.AreEqual(TuningConfig.ThoughtsPerWaveOf(i), TuningConfig.ThoughtsPerWave);
            }
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
