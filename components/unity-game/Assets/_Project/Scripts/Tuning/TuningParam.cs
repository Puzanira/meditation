using System;

namespace Meditation.Tuning
{
    /// <summary>One [tune]/[toggle] row on the tuning panel, bound straight to <see cref="TuningConfig"/>.</summary>
    public abstract class TuningParam
    {
        public string Label;
    }

    /// <summary>A [tune] number with a slider and a live value readout.</summary>
    public sealed class FloatParam : TuningParam
    {
        public float Min;
        public float Max;
        public Func<float> Get;
        public Action<float> Set;
        public string Format = "0.##";
        public string Unit = "";
        public bool WholeNumbers;

        public static FloatParam Make(string label, float min, float max, Func<float> get, Action<float> set,
            string unit = "", string format = "0.##", bool whole = false)
        {
            return new FloatParam
            {
                Label = label, Min = min, Max = max, Get = get, Set = set,
                Unit = unit, Format = format, WholeNumbers = whole
            };
        }
    }

    /// <summary>A [toggle] on/off switch.</summary>
    public sealed class BoolParam : TuningParam
    {
        public Func<bool> Get;
        public Action<bool> Set;

        public static BoolParam Make(string label, Func<bool> get, Action<bool> set)
        {
            return new BoolParam { Label = label, Get = get, Set = set };
        }
    }

    /// <summary>A [toggle] with named variants (A / B / C).</summary>
    public sealed class ChoiceParam : TuningParam
    {
        public string[] Options;
        public Func<int> Get;
        public Action<int> Set;

        public static ChoiceParam Make(string label, string[] options, Func<int> get, Action<int> set)
        {
            return new ChoiceParam { Label = label, Options = options, Get = get, Set = set };
        }
    }
}
