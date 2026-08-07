using System.Collections.Generic;
using System.Linq;
using Meditation.Tuning;
using NUnit.Framework;
using UnityEngine;

namespace Meditation.Tests
{
    /// <summary>
    /// Done contract §7 with teeth: the panel of every scenette must expose EXACTLY the parameters
    /// MECHANICS.md §7 and SCREENS.md hand it — no missing row, no stray one. Counting rows was not
    /// enough: a whole group of §4 parameters went missing while the count still looked healthy.
    ///
    /// Each expected list below is the contract. If a parameter is added or renamed, this test is the
    /// place where that decision is recorded.
    /// </summary>
    public class TuningPanelCoverageTests
    {
        private static readonly string[] CrankCollectRows =
        {
            // MECHANICS §1 + §7.1
            "порог кручения",
            "grace-период",
            "время сбора детали",
            "при остановке",
            "скорость таяния (B)",
            "скорость кручения влияет",
            "эталон скорости (B)"
        };

        private static readonly string[] ShakeAwayRows =
        {
            // MECHANICS §3 + §7.2, plus the §4 wave knobs the thoughts-only scenette owns
            "порог удара: амплитуда",
            "порог удара: резкость",
            "прочность: слабые",
            "прочность: средние",
            "прочность: крепкие",
            "затухание счётчика ударов",
            "пауза затухания",
            "таргетинг ударов",
            "интервал волн",
            "в волне: слабых",
            "в волне: средних",
            "в волне: крепких"
        };

        private static readonly string[] TwoHandsRows =
        {
            // MECHANICS §2 (+ SCREENS gaze numbers), §4 in full, and the two collection knobs
            "выбор детали",
            "скорость взгляда",
            "удержание взгляда",
            "интервал волн",
            "в волне: слабых",
            "в волне: средних",
            "в волне: крепких",
            "рост давления в уровне",
            "сокращение интервала за волну",
            "дрейф мыслей к центру",
            "скорость дрейфа",
            "мысли закрывают сосуд и деталь",
            "порог поражения (перекрытие)",
            "порог кручения",
            "время сбора детали"
        };

        private static readonly string[] FullLevelRows =
        {
            // MECHANICS §5 + §4
            "длительность уровня",
            "передышка после детали",
            "длина передышки",
            "авто-ретрай после поражения",
            "интервал волн",
            "в волне: слабых",
            "в волне: средних",
            "в волне: крепких",
            "рост давления в уровне",
            "сокращение интервала за волну",
            "дрейф мыслей к центру",
            "скорость дрейфа",
            "мысли закрывают сосуд и деталь",
            "порог поражения (перекрытие)",
            "выбор детали",
            "скорость взгляда",
            "удержание взгляда"
        };

        /// <summary>
        /// The game's own panel (done contract §6–§7). Per-level rows are named «У{n} · …» so a number
        /// on the panel belongs to exactly one level and cannot be mistaken for a global; everything
        /// below is what stays the same wherever the player is.
        /// </summary>
        private static readonly string[] GameLevelRows =
        {
            "длительность уровня",
            "интервал волн",
            "в волне: слабых",
            "в волне: средних",
            "в волне: крепких",
            "прочность: слабые",
            "прочность: средние",
            "прочность: крепкие",
            "скорость дрейфа",
            "сокращение интервала за волну"
        };

        private static readonly string[] GameSharedRows =
        {
            "порог кручения",
            "grace-период",
            "время сбора детали",
            "при остановке",
            "выбор детали",
            "скорость взгляда",
            "удержание взгляда",
            "порог удара: амплитуда",
            "порог удара: резкость",
            "затухание счётчика ударов",
            "пауза затухания",
            "таргетинг ударов",
            "дрейф мыслей к центру",
            "мысли закрывают сосуд и деталь",
            "порог пика хаоса",
            "порог поражения (перекрытие)",
            "передышка после детали",
            "длина передышки",
            "авто-ретрай после поражения",
            "таймер в обучении стоит",

            // MECHANICS §8 «Звук» — заказ founder 2026-08-07: три слоя, все ручки на панель.
            "звук: громкость фона",
            "звук: кроссфейд в медитацию",
            "звук: кроссфейд из медитации",
            "звук: хвост медитации",
            "звук: медитация заменяет фон",
            "звук: потолок слоя мыслей",
            "звук: мыслей до максимума",
            "звук: сглаживание громкости",
            "звук: мысли по перекрытию, а не по числу",
            "звук: тишина вне уровня",

            // SCREENS «Детали в сцене» — луч-подсветка, тот же заказ.
            "луч: период",
            "луч: длительность прохода",
            "луч: ширина полосы",
            "луч: сила подсветки",
            "луч: только по незамеченным",
            "луч: реже на поздних уровнях"
        };

        /// <summary>
        /// Ranges of the two new groups, straight out of the spec that ordered them. Same lesson as
        /// the scenettes: a slider whose range nobody pinned is a slider that drifts, and these two
        /// groups are the ones the founder will be moving at the gate.
        /// </summary>
        private static readonly Dictionary<string, (float Min, float Max)> SharedRanges =
            new Dictionary<string, (float, float)>
            {
                { "звук: громкость фона", (0f, 1f) },                    // MECHANICS §8
                { "звук: кроссфейд в медитацию", (0.1f, 1.5f) },
                { "звук: кроссфейд из медитации", (0.1f, 1.5f) },
                { "звук: хвост медитации", (0f, 4f) },
                { "звук: потолок слоя мыслей", (0f, 1f) },
                { "звук: мыслей до максимума", (4f, 15f) },
                { "звук: сглаживание громкости", (0.1f, 2f) },
                { "луч: период", (4f, 20f) },                            // SCREENS «Детали в сцене»
                { "луч: длительность прохода", (0.6f, 2.5f) },
                { "луч: ширина полосы", (150f, 600f) },
                { "луч: сила подсветки", (0.1f, 1f) }
            };

        [Test]
        public void TheSoundAndSweepRows_KeepTheirSpecRanges()
        {
            IList<TuningParam> rows = TuningCatalog.GameShared();

            foreach (KeyValuePair<string, (float Min, float Max)> range in SharedRanges)
            {
                var param = rows.OfType<FloatParam>().FirstOrDefault(p => p.Label == range.Key);
                Assert.IsNotNull(param, "Нет строки «" + range.Key + "» на панели игры.");
                Assert.AreEqual(range.Value.Min, param.Min, 1e-3f,
                    range.Key + ": нижняя граница не по спеку.");
                Assert.AreEqual(range.Value.Max, param.Max, 1e-3f,
                    range.Key + ": верхняя граница не по спеку.");
            }
        }

        /// <summary>
        /// …and the shipped starting values are the ones the spec names, inside those ranges. §8 gives
        /// a start for every knob it declares, and a start outside its own slider is a value the
        /// founder cannot get back to after moving it.
        /// </summary>
        [Test]
        public void TheSoundAndSweepDefaults_AreTheSpecsStartingValues()
        {
            TuningConfig.ResetToDefaults();

            Assert.AreEqual(0.6f, TuningConfig.AudioBackgroundVolume, 1e-3f);
            Assert.AreEqual(0.4f, TuningConfig.AudioMeditationFadeInSeconds, 1e-3f);
            Assert.AreEqual(0.6f, TuningConfig.AudioMeditationFadeOutSeconds, 1e-3f);
            Assert.AreEqual(1.5f, TuningConfig.AudioMeditationTailSeconds, 1e-3f);
            Assert.IsTrue(TuningConfig.AudioMeditationReplacesBackground);
            Assert.AreEqual(0.8f, TuningConfig.AudioThoughtsMaxVolume, 1e-3f);
            Assert.AreEqual(8f, TuningConfig.AudioThoughtsAtCount, 1e-3f);
            Assert.AreEqual(0.5f, TuningConfig.AudioThoughtsSmoothingSeconds, 1e-3f);
            Assert.IsFalse(TuningConfig.AudioThoughtsByOverlap);
            Assert.IsTrue(TuningConfig.AudioSilentOffLevel);

            Assert.AreEqual(8f, TuningConfig.SweepPeriodSeconds, 1e-3f);
            Assert.AreEqual(1.2f, TuningConfig.SweepDurationSeconds, 1e-3f);
            Assert.AreEqual(320f, TuningConfig.SweepWidthPx, 1e-3f);
            Assert.AreEqual(0.45f, TuningConfig.SweepStrength, 1e-3f);
            Assert.IsTrue(TuningConfig.SweepOnlyUnnoticed);
            Assert.IsTrue(TuningConfig.SweepRarerOnLateLevels);
        }

        [Test]
        public void GamePanel_HasASectionPerLevel_PlusTheSharedValues()
        {
            var expected = new List<string>();
            for (int level = 1; level <= TuningConfig.LevelBands; level++)
                foreach (string row in GameLevelRows) expected.Add("У" + level + " · " + row);
            expected.AddRange(GameSharedRows);

            AssertRows("Игра", TuningCatalog.Game(), expected.ToArray());
        }

        [Test]
        public void GamePanel_KeepsTheSpecsRangesOnEveryLevelSection()
        {
            // Same lesson as the scenettes: the same parameter is declared once per level, so pinning
            // one section would leave the other four free to drift.
            var ranges = new Dictionary<string, (float Min, float Max)>
            {
                { "длительность уровня", (60f, 180f) },     // MECHANICS §5
                { "прочность: слабые", (2f, 12f) },         // MECHANICS §3
                { "прочность: средние", (2f, 12f) },
                { "прочность: крепкие", (2f, 12f) },
                { "скорость дрейфа", (20f, 60f) }           // SCREENS §Мысли
            };

            for (int level = 0; level < TuningConfig.LevelBands; level++)
            {
                IList<TuningParam> rows = TuningCatalog.GameLevel(level);
                foreach (KeyValuePair<string, (float Min, float Max)> range in ranges)
                {
                    string label = "У" + (level + 1) + " · " + range.Key;
                    var param = rows.OfType<FloatParam>().FirstOrDefault(p => p.Label == label);
                    Assert.IsNotNull(param, "Нет строки «" + label + "».");
                    Assert.AreEqual(range.Value.Min, param.Min, 1e-3f, label + ": нижняя граница не по спеку.");
                    Assert.AreEqual(range.Value.Max, param.Max, 1e-3f, label + ": верхняя граница не по спеку.");
                }
            }
        }

        [Test]
        public void EveryGamePanelRow_ReadsAndWritesTheLiveConfig()
        {
            TuningConfig.ResetToDefaults();
            try
            {
                foreach (TuningParam param in TuningCatalog.Game()) RoundTrip(param);
            }
            finally
            {
                TuningConfig.ResetToDefaults();
            }
        }

        private static void AssertRows(string scenette, IList<TuningParam> actual, string[] expected)
        {
            List<string> labels = actual.Select(p => p.Label).ToList();

            var missing = expected.Where(e => !labels.Contains(e)).ToList();
            var extra = labels.Where(l => !expected.Contains(l)).ToList();

            Assert.IsEmpty(missing, scenette + ": параметры спека не выведены на панель: " +
                                    string.Join(", ", missing));
            Assert.IsEmpty(extra, scenette + ": на панели лишние строки: " + string.Join(", ", extra));
            Assert.AreEqual(expected.Length, labels.Count, scenette + ": дубли строк на панели.");
        }

        [Test]
        public void CrankCollectPanel_MatchesTheSpec() =>
            AssertRows("Сценка 1", TuningCatalog.CrankCollect(), CrankCollectRows);

        [Test]
        public void ShakeAwayPanel_MatchesTheSpec() =>
            AssertRows("Сценка 2", TuningCatalog.ShakeAway(), ShakeAwayRows);

        [Test]
        public void TwoHandsPanel_MatchesTheSpec() =>
            AssertRows("Сценка 3", TuningCatalog.TwoHands(), TwoHandsRows);

        [Test]
        public void FullLevelPanel_MatchesTheSpec() =>
            AssertRows("Сценка 4", TuningCatalog.FullLevel(), FullLevelRows);

        [Test]
        public void EveryPanelRow_ReadsAndWritesTheLiveConfig()
        {
            TuningConfig.ResetToDefaults();
            try
            {
                foreach (TuningParam param in TuningCatalog.CrankCollect()
                             .Concat(TuningCatalog.ShakeAway())
                             .Concat(TuningCatalog.TwoHands())
                             .Concat(TuningCatalog.FullLevel()))
                {
                    RoundTrip(param);
                }
            }
            finally
            {
                TuningConfig.ResetToDefaults();
            }
        }

        /// <summary>A row is only a getter/setter pair — prove it really reaches the config and back.</summary>
        private static void RoundTrip(TuningParam param)
        {
            switch (param)
            {
                case FloatParam f:
                    Assert.Less(f.Min, f.Max, f.Label + ": пустой диапазон слайдера.");
                    float before = f.Get();
                    float target = Mathf.Approximately(before, f.Max) ? f.Min : f.Max;
                    f.Set(target);
                    Assert.AreEqual(target, f.Get(), 1e-3f, f.Label + ": сеттер не пишет в конфиг.");
                    f.Set(before);
                    break;

                case BoolParam b:
                    bool wasOn = b.Get();
                    b.Set(!wasOn);
                    Assert.AreEqual(!wasOn, b.Get(), b.Label + ": тогглер не пишет в конфиг.");
                    b.Set(wasOn);
                    break;

                case ChoiceParam c:
                    Assert.GreaterOrEqual(c.Options.Length, 2, c.Label + ": вариантов меньше двух.");
                    int wasIndex = c.Get();
                    for (int i = 0; i < c.Options.Length; i++)
                    {
                        c.Set(i);
                        Assert.AreEqual(i, c.Get(), c.Label + ": вариант не выбирается.");
                    }
                    c.Set(wasIndex);
                    break;
            }
        }

        /// <summary>Every panel that carries the key, by name — a range lives in more than one place.</summary>
        private static readonly (string Scenette, IList<TuningParam> Rows)[] AllPanels =
        {
            ("Сценка 1", TuningCatalog.CrankCollect()),
            ("Сценка 2", TuningCatalog.ShakeAway()),
            ("Сценка 3", TuningCatalog.TwoHands()),
            ("Сценка 4", TuningCatalog.FullLevel())
        };

        [Test]
        public void SliderRanges_StayInsideTheSpecsRanges()
        {
            // Checked on EVERY panel that exposes the key: the same parameter is declared per
            // scenette, so pinning one panel leaves the copies free to drift (that is exactly how
            // the 10–120 drift range survived review on scenette 4).
            AssertRangeEverywhere("grace-период", 0f, 800f);
            AssertRangeEverywhere("время сбора детали", 3f, 15f);
            AssertRangeEverywhere("прочность: слабые", 2f, 12f);
            AssertRangeEverywhere("прочность: средние", 2f, 12f);
            AssertRangeEverywhere("прочность: крепкие", 2f, 12f);
            AssertRangeEverywhere("пауза затухания", 400f, 1500f);
            AssertRangeEverywhere("скорость дрейфа", 20f, 60f);              // SCREENS §Мысли
            AssertRangeEverywhere("порог поражения (перекрытие)", 85f, 100f);
            AssertRangeEverywhere("длительность уровня", 60f, 180f);
            AssertRangeEverywhere("длина передышки", 0f, 5f);
        }

        /// <summary>Assert the range on every panel that has the row, and that at least one does.</summary>
        private static void AssertRangeEverywhere(string label, float min, float max)
        {
            int found = 0;
            foreach ((string scenette, IList<TuningParam> rows) in AllPanels)
            {
                var param = rows.OfType<FloatParam>().FirstOrDefault(p => p.Label == label);
                if (param == null) continue;

                found++;
                Assert.AreEqual(min, param.Min, 1e-3f,
                    scenette + " · " + label + ": нижняя граница не по спеку.");
                Assert.AreEqual(max, param.Max, 1e-3f,
                    scenette + " · " + label + ": верхняя граница не по спеку.");
            }

            Assert.Greater(found, 0, "Строки «" + label + "» нет ни на одной панели.");
        }

        // ---- panel layout bands ------------------------------------------------------------------

        /// <summary>
        /// The arithmetic behind «строки не наезжают на показания»: the four bands must tile the panel
        /// exactly, at every window height, with nothing left over and nothing negative. The founder's
        /// playtest died on a 636 px-tall Game view, where the rows ran from the top while the readings
        /// hung off the bottom and the two simply met in the middle.
        /// </summary>
        [Test]
        public void PanelBands_TileTheHeightExactly_AtEveryWindowSize()
        {
            for (float height = 0f; height <= 1400f; height += 7f)
            {
                TuningPanel.ResolveZones(height, out float header, out float rows, out float readout,
                    out float chrome);

                Assert.GreaterOrEqual(header, 0f, height + ": отрицательная шапка.");
                Assert.GreaterOrEqual(rows, 0f, height + ": отрицательная зона строк.");
                Assert.GreaterOrEqual(readout, 0f, height + ": отрицательная зона показаний.");
                Assert.GreaterOrEqual(chrome, 0f, height + ": отрицательная нижняя полоса.");
                Assert.AreEqual(height, header + rows + readout + chrome, 1e-3f,
                    height + ": полосы панели не покрывают её высоту ровно — значит, они пересекаются " +
                    "или между ними дыра.");
            }
        }

        [Test]
        public void PanelBands_KeepTheReadingsReadable_OnTheFoundersViewport()
        {
            // Her Game view at the playtest, and a smaller one still.
            foreach (float height in new[] { 636f, 576f })
            {
                TuningPanel.ResolveZones(height, out float header, out float rows, out float readout,
                    out _);

                Assert.AreEqual(TuningPanel.HeaderHeight, header, 1e-3f,
                    height + ": шапка должна остаться полной.");
                Assert.AreEqual(TuningPanel.ReadoutZoneHeight, readout, 1e-3f,
                    height + ": показания должны остаться в полный рост.");
                Assert.Greater(rows, 100f,
                    height + ": строкам не осталось окна — панель нечитаема.");
            }
        }

        [Test]
        public void PanelBands_ShrinkTheReadings_RatherThanLettingRowsRunIntoThem()
        {
            // Squeezed past the point where everything fits, the readings give ground — but the rows
            // still get a window of their own instead of drawing through the readings.
            TuningPanel.ResolveZones(240f, out float header, out float rows, out float readout, out _);

            Assert.Less(readout, TuningPanel.ReadoutZoneHeight, "Показания обязаны ужиматься.");
            Assert.AreEqual(240f, header + rows + readout + TuningPanel.ChromeBottomHeight, 1e-3f);
            Assert.GreaterOrEqual(rows, 0f);
        }
    }
}
