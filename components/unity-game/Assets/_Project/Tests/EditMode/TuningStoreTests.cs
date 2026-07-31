using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Meditation.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Meditation.Tests
{
    /// <summary>
    /// The incident this guards: the founder tuned a whole session on the preview stand, closed the
    /// editor, and every number was gone — the values only ever lived in static fields.
    ///
    /// Everything here writes to a scratch file in the system temp folder, never to the real
    /// <c>UserSettings/tuning.json</c>: a test run must not be able to overwrite her tuning.
    /// </summary>
    public class TuningStoreTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "meditation-tuning-" + Guid.NewGuid().ToString("N"));
            TuningStore.UseFileForTests(Path.Combine(_folder, "tuning.json"));
            TuningConfig.ResetToDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            TuningStore.UseDefaultFile();
            TuningConfig.ResetToDefaults();

            try
            {
                if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A stray temp folder is not worth failing a test over.
            }
        }

        // ---- the field set, read straight off TuningConfig ----------------------------------------

        private static FieldInfo[] ConfigFields()
        {
            FieldInfo[] all = typeof(TuningConfig).GetFields(BindingFlags.Public | BindingFlags.Static);
            var mutable = new List<FieldInfo>();
            foreach (FieldInfo field in all)
                if (!field.IsLiteral && !field.IsInitOnly) mutable.Add(field);

            Assert.Greater(mutable.Count, 20, "TuningConfig потерял поля — тест смотрит не туда.");
            return mutable.ToArray();
        }

        /// <summary>Give every field a value that is NOT its default, so a lost value cannot pass.</summary>
        private static Dictionary<string, object> ScrambleEveryField()
        {
            var expected = new Dictionary<string, object>();
            foreach (FieldInfo field in ConfigFields())
            {
                object before = field.GetValue(null);
                object after;

                if (field.FieldType == typeof(bool)) after = !(bool)before;
                else if (field.FieldType == typeof(int)) after = (int)before + 3;
                else if (field.FieldType == typeof(float)) after = (float)before + 7.25f;
                else if (field.FieldType.IsEnum)
                {
                    Array variants = Enum.GetValues(field.FieldType);
                    object candidate = variants.GetValue(variants.Length - 1);
                    if (candidate.Equals(before)) candidate = variants.GetValue(0);
                    after = candidate;
                }
                else
                {
                    Assert.Fail(field.Name + ": тип " + field.FieldType.Name +
                                " не умеет сохраняться — добавь его в TuningStore.");
                    return null;
                }

                Assert.AreNotEqual(before, after, field.Name + ": подмена не изменила значение.");
                field.SetValue(null, after);
                expected[field.Name] = after;
            }

            return expected;
        }

        private static void AssertFieldsAre(Dictionary<string, object> expected)
        {
            foreach (FieldInfo field in ConfigFields())
            {
                Assert.IsTrue(expected.ContainsKey(field.Name),
                    field.Name + ": поле не покрыто ожиданиями теста.");
                Assert.AreEqual(expected[field.Name], field.GetValue(null),
                    field.Name + ": значение не восстановилось из файла.");
            }
        }

        // ---- round trip ---------------------------------------------------------------------------

        [Test]
        public void SaveThenLoad_RestoresEverySingleField()
        {
            Dictionary<string, object> tuned = ScrambleEveryField();
            TuningStore.Save();

            Assert.IsTrue(File.Exists(TuningStore.FilePath), "Сохранение не создало файл.");

            TuningConfig.ResetToDefaults();
            Assert.IsTrue(TuningStore.Load(), "Загрузка не прочитала только что записанный файл.");

            AssertFieldsAre(tuned);
        }

        [Test]
        public void Save_LeavesNoTemporaryFileBehind()
        {
            TuningStore.Save();
            TuningStore.Save();   // the second write goes over an existing file — the replace path

            Assert.IsFalse(File.Exists(TuningStore.FilePath + ".tmp"),
                "Временный файл записи остался на диске.");
        }

        [Test]
        public void EveryPersistedKey_IsAFieldOfTheConfig()
        {
            var names = new List<string>();
            foreach (FieldInfo field in ConfigFields()) names.Add(field.Name);

            CollectionAssert.AreEquivalent(names, TuningStore.PersistedKeys,
                "Набор сохраняемых ключей разошёлся с полями TuningConfig.");
        }

        // ---- broken and missing files -------------------------------------------------------------

        [Test]
        public void BrokenJson_FallsBackToDefaults_WithoutErrorsOrExceptions()
        {
            TuningConfig.CollectSeconds = 11f;
            TuningStore.Save();

            Directory.CreateDirectory(_folder);
            File.WriteAllText(TuningStore.FilePath, "{\"CollectSeconds\": 11, \"GraceMs\"");

            Assert.IsFalse(TuningStore.Load(), "Битый файл не должен считаться прочитанным.");
            Assert.AreEqual(TuningConfig.Defaults.CollectSeconds, TuningConfig.CollectSeconds, 1e-3f);
            Assert.AreEqual(TuningConfig.Defaults.GraceMs, TuningConfig.GraceMs, 1e-3f);

            // Nothing in the console: a hand-mangled settings file is a nuisance, not an incident.
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GarbageThatIsNotJsonAtAll_FallsBackToDefaults()
        {
            Directory.CreateDirectory(_folder);
            foreach (string junk in new[] { "", "   ", "не json", "[1,2,3]", "{", "{\"a\":{\"b\":1}}" })
            {
                TuningConfig.CollectSeconds = 11f;
                File.WriteAllText(TuningStore.FilePath, junk);

                Assert.IsFalse(TuningStore.Load(), "«" + junk + "» не должен считаться прочитанным.");
                Assert.AreEqual(TuningConfig.Defaults.CollectSeconds, TuningConfig.CollectSeconds, 1e-3f,
                    "«" + junk + "»: значения обязаны стать дефолтными.");
            }

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void MissingFile_IsTheNormalFirstRun_AndGivesDefaults()
        {
            TuningConfig.CollectSeconds = 11f;

            Assert.IsFalse(File.Exists(TuningStore.FilePath));
            Assert.IsFalse(TuningStore.Load());
            Assert.AreEqual(TuningConfig.Defaults.CollectSeconds, TuningConfig.CollectSeconds, 1e-3f);

            LogAssert.NoUnexpectedReceived();
        }

        // ---- resilience to renames ----------------------------------------------------------------

        [Test]
        public void UnknownKeys_AreIgnored_AndMissingOnesKeepTheirDefault()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(TuningStore.FilePath,
                "{\"CollectSeconds\":9.5,\"ParameterWeRenamedLastWeek\":42,\"AlsoGone\":false," +
                "\"StillHereButAString\":\"1200\"}");

            Assert.IsTrue(TuningStore.Load(), "Файл с лишними ключами обязан читаться.");

            Assert.AreEqual(9.5f, TuningConfig.CollectSeconds, 1e-3f, "Знакомый ключ не применился.");
            Assert.AreEqual(TuningConfig.Defaults.GraceMs, TuningConfig.GraceMs, 1e-3f,
                "Отсутствующий в файле ключ должен взять дефолт.");
            Assert.AreEqual(TuningConfig.Defaults.Targeting, TuningConfig.Targeting);

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AnEnumVariantThatNoLongerExists_KeepsTheDefault()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(TuningStore.FilePath, "{\"Targeting\":99,\"Notice\":2}");

            Assert.IsTrue(TuningStore.Load());
            Assert.AreEqual(TuningConfig.Defaults.Targeting, TuningConfig.Targeting,
                "Несуществующий вариант обязан откатиться к дефолту, а не стать мусорным enum'ом.");
            Assert.AreEqual(NoticeMode.AutoNearest, TuningConfig.Notice, "Валидный вариант не применился.");
        }

        // ---- reset --------------------------------------------------------------------------------

        [Test]
        public void Reset_RestoresDefaults_AndDeletesTheFile()
        {
            TuningConfig.CollectSeconds = 12f;
            TuningConfig.HitDecayEnabled = !TuningConfig.Defaults.HitDecayEnabled;
            TuningStore.Save();
            Assert.IsTrue(File.Exists(TuningStore.FilePath), "Нечего сбрасывать — файла нет.");

            TuningStore.Clear();

            Assert.IsFalse(File.Exists(TuningStore.FilePath),
                "«сброс к дефолтам» обязан убрать файл, иначе значения вернутся при следующем старте.");
            Assert.AreEqual(TuningConfig.Defaults.CollectSeconds, TuningConfig.CollectSeconds, 1e-3f);
            Assert.AreEqual(TuningConfig.Defaults.HitDecayEnabled, TuningConfig.HitDecayEnabled);

            // And a start after the reset really does come up on defaults.
            TuningConfig.CollectSeconds = 12f;
            Assert.IsFalse(TuningStore.Load());
            Assert.AreEqual(TuningConfig.Defaults.CollectSeconds, TuningConfig.CollectSeconds, 1e-3f);
        }

        // ---- where the file lives -----------------------------------------------------------------

        [Test]
        public void TheRealFile_LivesInUserSettings_OutsideAssets()
        {
            TuningStore.UseDefaultFile();
            string path = TuningStore.FilePath.Replace('\\', '/');

            Assert.IsTrue(path.EndsWith("/UserSettings/" + TuningStore.FileName),
                "Файл значений должен лежать в UserSettings (он в .gitignore и переживает редактор): " + path);

            string assets = Application.dataPath.Replace('\\', '/');
            Assert.IsFalse(path.StartsWith(assets + "/"),
                "Файл значений не должен лежать внутри Assets — иначе Unity его импортирует: " + path);

            string project = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            Assert.IsTrue(path.StartsWith(project + "/"),
                "Файл значений должен лежать внутри проекта: " + path);
        }

        // ---- the values the panel actually writes --------------------------------------------------

        [Test]
        public void AValueWrittenByAPanelRow_ComesBackFromTheFile()
        {
            // A row is only a getter/setter pair, so what it writes has to be exactly what the store
            // persists. That the widget itself calls Save is the PlayMode half of the check —
            // StandBootTests drags the real on-screen slider and looks for the file.
            foreach (TuningParam param in TuningCatalog.CrankCollect())
            {
                if (!(param is FloatParam f) || f.Label != "время сбора детали") continue;

                f.Set(13f);
                TuningStore.Save();
                break;
            }

            TuningConfig.ResetToDefaults();
            Assert.IsTrue(TuningStore.Load());
            Assert.AreEqual(13f, TuningConfig.CollectSeconds, 1e-3f);
        }
    }
}
