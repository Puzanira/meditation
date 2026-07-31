using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Meditation.Tuning
{
    /// <summary>
    /// Makes the tuning panel's values outlive the editor. <see cref="TuningConfig"/> is static state,
    /// which is enough to survive jumping between scenettes — but not enough to survive closing the
    /// editor: the founder tuned a whole session's worth of numbers, quit, and got the defaults back.
    ///
    /// So every change made through the panel writes the WHOLE config to one small JSON file, and the
    /// stand reads it back before the first scenette (<see cref="LoadOnStart"/>).
    ///
    /// Where the file lives:
    /// <list type="bullet">
    /// <item>editor — <c>&lt;projectPath&gt;/UserSettings/tuning.json</c>: inside the project (easy to
    /// find and to delete), outside <c>Assets/</c> (no import, no .meta), and already gitignored, so a
    /// tuning session can never turn into a commit;</item>
    /// <item>player — <c>Application.persistentDataPath/tuning.json</c>, the only writable place a
    /// build has.</item>
    /// </list>
    ///
    /// The format is deliberately dumb: one flat object, key = the name of the
    /// <see cref="TuningConfig"/> field, value = a number or a boolean. Fields are found by reflection,
    /// so a new [tune] parameter is persisted the moment it is declared — nobody has to remember to
    /// add it here. Renaming a field is safe in both directions: an unknown key in the file is ignored,
    /// a key missing from the file keeps its default.
    ///
    /// Nothing here is allowed to be noisy. A missing file is the normal first run, and a file someone
    /// hand-edited into garbage is a nuisance, not an incident: both simply mean "defaults".
    /// </summary>
    public static class TuningStore
    {
        public const string FileName = "tuning.json";

        /// <summary>Set only by tests, so a suite run can never write over the founder's real file.</summary>
        private static string _pathOverride;

        private static FieldInfo[] _fields;

        /// <summary>The file the panel reads and writes right now.</summary>
        public static string FilePath => _pathOverride ?? DefaultPath();

        /// <summary>True while a test has redirected the store away from the real file.</summary>
        public static bool IsRedirected => _pathOverride != null;

        /// <summary>Point the store at a scratch file (tests only).</summary>
        public static void UseFileForTests(string path)
        {
            _pathOverride = path;
        }

        /// <summary>Back to the real per-project file.</summary>
        public static void UseDefaultFile()
        {
            _pathOverride = null;
        }

        private static string DefaultPath()
        {
            if (Application.isEditor)
            {
                // Application.dataPath is <projectPath>/Assets — UserSettings is its sibling.
                DirectoryInfo project = Directory.GetParent(Application.dataPath);
                string root = project != null ? project.FullName : Application.dataPath;
                return Path.Combine(root, "UserSettings", FileName);
            }

            return Path.Combine(Application.persistentDataPath, FileName);
        }

        /// <summary>
        /// Every mutable static field of <see cref="TuningConfig"/> — that is the whole persisted set.
        /// Consts (the <c>Defaults</c> block) and computed properties are not fields and never appear.
        /// </summary>
        private static FieldInfo[] Fields
        {
            get
            {
                if (_fields != null) return _fields;

                FieldInfo[] all = typeof(TuningConfig)
                    .GetFields(BindingFlags.Public | BindingFlags.Static);
                var mutable = new List<FieldInfo>(all.Length);
                for (int i = 0; i < all.Length; i++)
                    if (!all[i].IsLiteral && !all[i].IsInitOnly) mutable.Add(all[i]);

                _fields = mutable.ToArray();
                return _fields;
            }
        }

        /// <summary>The names this store persists — the coverage guard in the tests reads it.</summary>
        public static IReadOnlyList<string> PersistedKeys
        {
            get
            {
                FieldInfo[] fields = Fields;
                var names = new string[fields.Length];
                for (int i = 0; i < fields.Length; i++) names[i] = fields[i].Name;
                return names;
            }
        }

        // ---- read / write ----------------------------------------------------------------------

        /// <summary>
        /// Write every current value. Called on each panel interaction — the config is a few dozen
        /// scalars, so chattiness costs nothing and there is no debounce to get wrong.
        /// The write is temp-file-then-replace, so a crash mid-save leaves the previous file intact
        /// rather than a half-written one.
        /// </summary>
        public static void Save()
        {
            string path = FilePath;
            string temp = path + ".tmp";
            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                File.WriteAllText(temp, Serialize(), new UTF8Encoding(false));

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temp, path, null);
                    }
                    catch (Exception)
                    {
                        // Not every filesystem supports Replace; fall back to the two-step move.
                        File.Delete(path);
                        File.Move(temp, path);
                    }
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            catch (Exception e)
            {
                // A warning, not an error: failing to persist must not break a tuning session, and the
                // PlayMode suite treats console errors as failures.
                Debug.LogWarning("Не удалось сохранить значения тюнера в " + path + ": " + e.Message);
                TryDelete(temp);
            }
        }

        /// <summary>
        /// Bring <see cref="TuningConfig"/> up from disk. Missing or unreadable file → defaults,
        /// silently. Returns true only when a file was actually parsed.
        /// </summary>
        public static bool Load()
        {
            string json = null;
            try
            {
                string path = FilePath;
                if (File.Exists(path)) json = File.ReadAllText(path);
            }
            catch (Exception)
            {
                json = null;
            }

            if (json == null)
            {
                TuningConfig.ResetToDefaults();
                return false;
            }

            return Apply(json);
        }

        /// <summary>
        /// Back to shipped defaults AND forget the file — the panel's «сброс к дефолтам». Deleting
        /// rather than rewriting means the next start goes through the plain first-run path.
        /// </summary>
        public static void Clear()
        {
            TuningConfig.ResetToDefaults();
            TryDelete(FilePath);
            TryDelete(FilePath + ".tmp");
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
                // Nothing to do and nothing worth saying: the values are already back to defaults.
            }
        }

        /// <summary>Loaded once per process, before the first scene of the stand comes up.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStart()
        {
            Load();
        }

        // ---- format ------------------------------------------------------------------------------

        /// <summary>The whole config as one flat JSON object.</summary>
        public static string Serialize()
        {
            FieldInfo[] fields = Fields;
            var sb = new StringBuilder(fields.Length * 32);
            sb.Append('{');

            bool first = true;
            for (int i = 0; i < fields.Length; i++)
            {
                string encoded = Encode(fields[i].FieldType, fields[i].GetValue(null));
                if (encoded == null) continue;   // not a scalar (or not finite) — skip, load gives default

                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(fields[i].Name).Append("\":").Append(encoded);
            }

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Defaults first, then whatever the text supplies. Unknown keys are never looked up, so they
        /// are ignored; a key whose value will not parse is treated exactly like a missing one.
        /// </summary>
        public static bool Apply(string json)
        {
            Dictionary<string, string> values = Parse(json);

            TuningConfig.ResetToDefaults();
            if (values == null) return false;

            FieldInfo[] fields = Fields;
            for (int i = 0; i < fields.Length; i++)
            {
                if (!values.TryGetValue(fields[i].Name, out string raw)) continue;

                object decoded = Decode(fields[i].FieldType, raw);
                if (decoded != null) fields[i].SetValue(null, decoded);
            }

            return true;
        }

        private static string Encode(Type type, object value)
        {
            if (type == typeof(bool)) return (bool)value ? "true" : "false";
            if (type == typeof(int)) return ((int)value).ToString(CultureInfo.InvariantCulture);
            if (type == typeof(float))
            {
                var number = (float)value;
                // NaN/∞ are not JSON and mean nothing to a slider — drop the key, load takes the default.
                if (float.IsNaN(number) || float.IsInfinity(number)) return null;
                // G9, not R: nine significant digits round-trip a float exactly on every runtime,
                // while "R" for float has a history of losing the last bit on Mono.
                return number.ToString("G9", CultureInfo.InvariantCulture);
            }
            if (type.IsEnum) return Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture);
            return null;
        }

        /// <summary>Boxed value for the field, or null when the text is not usable for that type.</summary>
        private static object Decode(Type type, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;

            if (type == typeof(bool))
            {
                if (raw.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (raw.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
                return null;
            }

            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
                return null;
            if (float.IsNaN(number) || float.IsInfinity(number)) return null;

            if (type == typeof(float)) return number;
            if (type == typeof(int)) return Mathf.RoundToInt(number);

            if (type.IsEnum)
            {
                int index = Mathf.RoundToInt(number);
                // A variant that no longer exists must not become an invalid enum value.
                return Enum.IsDefined(type, index) ? Enum.ToObject(type, index) : null;
            }

            return null;
        }

        // ---- a deliberately small JSON reader ----------------------------------------------------
        // The file is ours and holds one flat object of scalars, so a full parser would be dead weight.
        // Anything that is not that shape is "broken file" — the caller falls back to defaults.

        private static Dictionary<string, string> Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            var values = new Dictionary<string, string>();
            int i = 0;

            SkipWhitespace(json, ref i);
            if (i >= json.Length || json[i] != '{') return null;
            i++;

            SkipWhitespace(json, ref i);
            if (i < json.Length && json[i] == '}')
            {
                i++;
                SkipWhitespace(json, ref i);
                return i == json.Length ? values : null;
            }

            while (true)
            {
                SkipWhitespace(json, ref i);
                string key = ReadString(json, ref i);
                if (key == null) return null;

                SkipWhitespace(json, ref i);
                if (i >= json.Length || json[i] != ':') return null;
                i++;

                SkipWhitespace(json, ref i);
                string value = ReadScalar(json, ref i);
                if (value == null) return null;
                values[key] = value;

                SkipWhitespace(json, ref i);
                if (i >= json.Length) return null;
                if (json[i] == ',')
                {
                    i++;
                    continue;
                }
                if (json[i] == '}')
                {
                    i++;
                    break;
                }
                return null;
            }

            SkipWhitespace(json, ref i);
            return i == json.Length ? values : null;
        }

        private static void SkipWhitespace(string json, ref int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
        }

        private static string ReadString(string json, ref int i)
        {
            if (i >= json.Length || json[i] != '"') return null;
            i++;

            var sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    if (i >= json.Length) return null;
                    char escaped = json[i++];
                    switch (escaped)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'u':
                            if (i + 4 > json.Length) return null;
                            if (!int.TryParse(json.Substring(i, 4), NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture, out int code)) return null;
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default: sb.Append(escaped); break;
                    }
                    continue;
                }
                sb.Append(c);
            }

            return null;   // unterminated
        }

        /// <summary>
        /// One value: a number, a boolean, null, or a quoted string. Objects and arrays are not part of
        /// the format — meeting one means the file is not ours, and the whole read fails.
        /// A string value is read (so the file stays parseable) but will not decode into any field,
        /// which is the same as the key being absent.
        /// </summary>
        private static string ReadScalar(string json, ref int i)
        {
            if (i >= json.Length) return null;
            if (json[i] == '{' || json[i] == '[') return null;
            if (json[i] == '"') return ReadString(json, ref i);

            int start = i;
            while (i < json.Length && json[i] != ',' && json[i] != '}' && !char.IsWhiteSpace(json[i]))
            {
                if (json[i] == '{' || json[i] == '[' || json[i] == '"' || json[i] == ':') return null;
                i++;
            }

            return i > start ? json.Substring(start, i - start) : null;
        }
    }
}
