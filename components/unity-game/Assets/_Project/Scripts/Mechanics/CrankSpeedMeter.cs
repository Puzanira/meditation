using UnityEngine;

namespace Meditation.Mechanics
{
    /// <summary>
    /// Turns per-frame crank deltas into a stable "is the hand still turning?" speed.
    ///
    /// Raw <c>ArcadeInput.Crank.DeltaDegrees</c> arrives in bursts (a mouse wheel emits nothing on most
    /// frames; a real dynamo will jitter too), so a naive delta/dt reads zero between pulses and would
    /// fake a stall every few frames. This averages the degrees turned over a short sliding window,
    /// which is what "continuous cranking" actually means for the player.
    /// </summary>
    public sealed class CrankSpeedMeter
    {
        private const int Slots = 32;

        private readonly float[] _degrees = new float[Slots];
        private readonly float[] _seconds = new float[Slots];
        private int _head;

        /// <summary>Length of the averaging window, seconds.</summary>
        public float WindowSeconds { get; set; } = 0.25f;

        /// <summary>Smoothed crank speed, deg/s.</summary>
        public float SpeedDegPerSec { get; private set; }

        public void Reset()
        {
            for (int i = 0; i < Slots; i++)
            {
                _degrees[i] = 0f;
                _seconds[i] = 0f;
            }
            _head = 0;
            SpeedDegPerSec = 0f;
        }

        /// <summary>Feed one frame of crank movement; returns the smoothed speed in deg/s.</summary>
        public float Tick(float deltaDegrees, float deltaTime)
        {
            if (deltaTime <= 0f) return SpeedDegPerSec;

            _head = (_head + 1) % Slots;
            _degrees[_head] = Mathf.Abs(deltaDegrees);
            _seconds[_head] = deltaTime;

            float sumDeg = 0f;
            float sumSec = 0f;
            for (int i = 0; i < Slots && sumSec < WindowSeconds; i++)
            {
                int idx = (_head - i + Slots) % Slots;
                if (_seconds[idx] <= 0f) break;
                sumDeg += _degrees[idx];
                sumSec += _seconds[idx];
            }

            // Always divide by the full window: a burst that only covers part of it must read as a
            // partial speed, otherwise one big wheel tick would look like sustained fast cranking.
            float window = Mathf.Max(WindowSeconds, sumSec);
            SpeedDegPerSec = window > 0f ? sumDeg / window : 0f;
            return SpeedDegPerSec;
        }
    }
}
