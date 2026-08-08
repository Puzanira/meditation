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
                float rim = LevelView.RimRadiusPx(detail, TuningConfig.Defaults.DetailOutlinePx);
                Assert.LessOrEqual(rim, TuningConfig.Defaults.DetailOutlinePx,
                    detail.Name + ": обводка шире, чем ручка на панели.");

                Sprite sprite = ArtLibrary.Get(detail.Sprite);
                Assert.IsNotNull(sprite, detail.Name + ": спрайт не найден.");

                float scale = Mathf.Min(detail.Size.x / sprite.rect.width,
                    detail.Size.y / sprite.rect.height);
                float stroke = ArtLibrary.StrokeThicknessOf(detail.Sprite) * scale;

                Assert.LessOrEqual(rim, Mathf.Max(LevelView.MinRimPx,
                        stroke * LevelView.RimShareOfStroke) + 0.01f,
                    "Уровень " + level.Number + ", «" + detail.Name + "»: обводка " +
                    rim.ToString("0.0") + " px на штрихе шириной " + stroke.ToString("0.0") +
                    " px — ободок смыкается в заливку.");
            }
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
        /// The founder's own order of 2026-08-07, as a test rather than as a comment: level 1 sends
        /// MORE thoughts than level 2, and level 2 sends BIGGER ones («чуть больше первого»).
        ///
        /// Written as its own case because the ladder above cannot say it — the ladder only knows that
        /// pressure climbs, and it would stay green if somebody « fixed » the dip in blob count by
        /// making level 2 a swarm again, which is the shape the founder asked us to move away from.
        /// </summary>
        [Test]
        public void TheFirstTwoLevels_SwapCountForSize()
        {
            Assert.Greater(TuningConfig.ThoughtsPerWaveOf(0), TuningConfig.ThoughtsPerWaveOf(1),
                "У1 обязан слать БОЛЬШЕ мыслей за волну, чем У2 (решение founder 2026-08-07).");
            Assert.Greater(HeaviestClassOf(1), HeaviestClassOf(0),
                "Класс размера У2 обязан быть выше, чем у У1 — «меньше, но крупнее».");
            Assert.Greater(WavePressureOf(1), WavePressureOf(0),
                "…и при этом давление У2 обязано остаться выше первого.");
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
