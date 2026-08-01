using System.Collections.Generic;
using UnityEngine;

namespace Meditation.Mechanics
{
    /// <summary>
    /// What the «two hands» loop needs to be able to say to a screen. Two screens answer it: the
    /// preview stand's greybox <c>StageView</c> and the game's <c>LevelView</c> with the art drop
    /// behind it. Keeping the loop itself blind to which one it is talking to is what lets the levels
    /// reuse the rules the stand's tests already pin, instead of growing a second copy of them.
    /// </summary>
    public interface ICollectionView
    {
        /// <summary>Back to the start of a level: nothing collected, every detail home.</summary>
        void ResetCollected();

        /// <summary>Place a detail along its home → vessel path and drive its progress ring.</summary>
        void SetDetailProgress(int index, float progress01, bool spinning, bool active, bool slipped);

        /// <summary>The detail landed in the vessel: bounce, fill its HUD slot, show it inside.</summary>
        void CollectDetail(int index);

        /// <summary>The dashed collection thread between a point and the vessel.</summary>
        void SetThread(Vector2 from, Vector2 to, bool visible);

        /// <summary>The gaze circle and its dwell arc (notice variant B).</summary>
        void SetGaze(Vector2 position, float dwell01, bool visible);

        /// <summary>Peak-of-chaos veil, on once coverage passes the threshold.</summary>
        void SetPeak(bool peak);

        /// <summary>Mirror the live thought list into views.</summary>
        void SyncThoughts(IReadOnlyList<Thought> thoughts, float deltaTime);

        /// <summary>Idle pulse of the details nobody has noticed yet.</summary>
        void TickPulse(float deltaTime, IReadOnlyList<bool> collected, int noticedIndex);
    }
}
