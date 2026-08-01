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
        public void DetailCounts_AreTheOnesTheDoneContractNames()
        {
            // Done contract §3 and the scope extension: «победа = все N деталей». The street lost one
            // to the founder's decision of 2026-08-01 — see the test below.
            Assert.AreEqual(5, LevelCatalog.Count, "В игре пять уровней (арт-дроп L4–L5).");
            Assert.AreEqual(9, LevelCatalog.At(0).DetailCount, "Уровень 1 — 9 деталей.");
            Assert.AreEqual(8, LevelCatalog.At(1).DetailCount, "Уровень 2 — 8 деталей.");
            Assert.AreEqual(7, LevelCatalog.At(2).DetailCount, "Уровень 3 — 7 деталей.");
            Assert.AreEqual(5, LevelCatalog.At(3).DetailCount, "Уровень 4 — 5 деталей.");
            Assert.AreEqual(5, LevelCatalog.At(4).DetailCount, "Уровень 5 — 5 деталей.");

            // …and the run as a whole: 9+8+7+5+5. The number is written down in the docs beside every
            // «замер по всем деталям», and it was 39 there for a while — a count of the art drop's
            // files, not of the catalogue's details.
            int total = 0;
            foreach (LevelDefinition level in LevelCatalog.Levels) total += level.DetailCount;
            Assert.AreEqual(34, total, "За прогон игрок собирает 34 детали.");
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
                new[] { "L1/objects/vine", "L4/objects/airplane" }, cropped,
                "Иконка-фрагмент — решение по каждой детали отдельно; список изменился молча.");
        }

        /// <summary>
        /// The office plate carries two yellow sticky notes as decor («это НЕ деталь», SCREENS
        /// «Уровень 5»), and the drop ships exactly one sticky-note sprite. A second one in the
        /// catalogue would be a target the player can chase but never collect.
        /// </summary>
        [Test]
        public void LevelFive_CountsExactlyOneStickyNote_TheBakedOnesAreDecor()
        {
            LevelDefinition office = LevelCatalog.At(4);

            int stickers = 0;
            foreach (ArtDetail detail in office.Details)
                if (detail.Sprite.EndsWith("sticky-note")) stickers++;

            Assert.AreEqual(1, stickers,
                "Стикер-деталь на уровне 5 ровно один — жёлтые стикеры в фоне это обманки-декор.");
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
        public void TheStreet_LeavesTheBakedCloudOutOfItsDetails()
        {
            foreach (ArtDetail detail in LevelCatalog.At(0).Details)
                Assert.AreNotEqual("L1/objects/cloud-2", detail.Sprite,
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
            ArtDetail lamp = System.Array.Find(LevelCatalog.At(1).Details,
                d => d.Sprite == "L2/objects/lamp");

            Assert.IsNotNull(lamp.Sprite, "Лампы нет в каталоге уровня 2.");
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
            Assert.AreEqual(LevelOneData.SunCentre, LevelCatalog.At(1).SunCentre,
                "Библиотека: солнцу нечего было уступать — оно должно стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.SunCentre, LevelCatalog.At(2).SunCentre,
                "Метро: солнцу нечего было уступать — оно должно стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.SunCentre, LevelCatalog.At(3).SunCentre,
                "Набережная: солнцу нечего было уступать — оно должно стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.SunCentre, LevelCatalog.At(4).SunCentre,
                "Офис: солнцу нечего было уступать — оно должно стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.CrankIndicatorCentre, LevelCatalog.At(0).CrankIndicatorCentre,
                "Улица: индикатору нечего было уступать — он должен стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.CrankIndicatorCentre, LevelCatalog.At(3).CrankIndicatorCentre,
                "Набережная: индикатору нечего было уступать — он должен стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.SlotsOrigin, LevelCatalog.At(1).SlotsOrigin,
                "Библиотека: ряду слотов нечего было уступать — он должен стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.SlotsOrigin, LevelCatalog.At(3).SlotsOrigin,
                "Набережная: ряду слотов нечего было уступать — он должен стоять на базе SCREENS.");
            Assert.AreEqual(LevelOneData.SlotsOrigin, LevelCatalog.At(4).SlotsOrigin,
                "Офис: ряду слотов нечего было уступать — он должен стоять на базе SCREENS.");

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
        /// The victory tableau has a box, and everything else on that screen stands below it.
        ///
        /// A flat ×2 gave the bucket 480×778 — 72 % of the frame — so «Собрано: Набережная» was printed
        /// across the bucket and the row of filled slots was buried in it. Mock 18 draws the vessel in
        /// a 480×300 rectangle with the caption at y 800 and the slots under that, and this is the
        /// arithmetic that says the drop's five vessels all fit it.
        /// </summary>
        [Test]
        public void TheVictoryTableau_FitsItsBox_AndClearsTheCaptionAndTheSlots()
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

                // The caption stands at y 800 and the row of slots at 925 (LevelView).
                Assert.Less(tableau.yMax, 800f - 30f, what + "наезжает на подпись «Собрано: …».");
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
            Assert.IsFalse(LevelCatalog.At(0).VesselIsBaked, "Портфель уровня 1 — отдельный спрайт.");
            Assert.IsFalse(LevelCatalog.At(1).VesselIsBaked, "Рюкзак уровня 2 — отдельный спрайт.");
            Assert.IsTrue(LevelCatalog.At(2).VesselIsBaked, "Сумка уровня 3 запечена в фон.");
            Assert.IsFalse(LevelCatalog.At(3).VesselIsBaked, "Ведёрко уровня 4 — отдельный спрайт.");
            Assert.IsFalse(LevelCatalog.At(4).VesselIsBaked, "Кружка уровня 5 — отдельный спрайт.");
        }

        [Test]
        public void TheTutorialsFirstDetail_IsTheDandelion_AndTheSecondIsInTheOpen()
        {
            // SCREENS §Обучение names the first one; the second is the detail the first thought lands
            // on top of, so it has to be somewhere a blob can sit whole.
            LevelDefinition one = LevelCatalog.At(0);
            Assert.AreEqual("одуванчик", one.Details[0].Name);

            Vector2 second = one.Details[1].Home;
            Vector2 blob = Thought.SizeOf(ThoughtStrength.Weak);
            Assert.That(second.x, Is.InRange(blob.x * 0.5f, DesignStage.DesignWidth - blob.x * 0.5f),
                "Первая мысль обучения не помещается над второй деталью по горизонтали.");
            Assert.That(second.y, Is.InRange(blob.y * 0.5f, DesignStage.DesignHeight - blob.y * 0.5f),
                "Первая мысль обучения не помещается над второй деталью по вертикали.");
        }

        [Test]
        public void TheFinalesSubtitle_IsTheLineFromTheTextRegistry()
        {
            // Walkthrough frame 25 «Реестр текстов», row «Финал»: the second line names every vessel
            // of the run in order. A level added to the end has to appear in it, or the finale shows
            // five vessels under a caption for four.
            Assert.AreEqual("портфель · рюкзак · сумка · ведёрко · кружка", LevelCatalog.VesselWords());
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
            // dimension the panel exposes. Read off Defaults, not off the live values, so this is a
            // statement about what ships rather than about whatever the last test left behind.
            for (int i = 1; i < LevelCatalog.Count; i++)
            {
                string step = "У" + i + " → У" + (i + 1) + ": ";

                Assert.LessOrEqual(TuningConfig.LevelSecondsOf(i), TuningConfig.LevelSecondsOf(i - 1),
                    step + "времени должно оставаться не больше.");
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
            Assert.Less(TuningConfig.LevelSecondsOf(last), TuningConfig.LevelSecondsOf(0),
                "Последний уровень не стал короче первого — прогрессии нет.");
            Assert.Greater(TuningConfig.ThoughtsPerWaveOf(last), TuningConfig.ThoughtsPerWaveOf(0),
                "Последний уровень не шлёт больше мыслей, чем первый — прогрессии нет.");
            Assert.Greater(TuningConfig.DurabilityStrongOf(last), TuningConfig.DurabilityStrongOf(0),
                "Крепкие мысли последнего уровня не крепче первого — прогрессии нет.");
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
