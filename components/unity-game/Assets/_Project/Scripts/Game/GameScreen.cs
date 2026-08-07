using Meditation.View;
using UnityEngine;

namespace Meditation.Game
{
    /// <summary>Both hands as the game reads them this frame — nothing here comes from a keyboard.</summary>
    public readonly struct Hands
    {
        /// <summary>Smoothed crank speed, deg/s.</summary>
        public readonly float CrankSpeed;

        /// <summary>Is the crank above the [tune] threshold this frame?</summary>
        public readonly bool CrankSpinning;

        /// <summary>Degrees the crank turned this frame (sign kept).</summary>
        public readonly float CrankDelta;

        /// <summary>Total degrees since the game started — the crank dial's needle.</summary>
        public readonly float CrankTotal;

        public readonly Vector2 Stick;

        /// <summary>Shake hits registered this frame.</summary>
        public readonly int Hits;

        public Hands(float crankSpeed, bool crankSpinning, float crankDelta, float crankTotal,
            Vector2 stick, int hits)
        {
            CrankSpeed = crankSpeed;
            CrankSpinning = crankSpinning;
            CrankDelta = crankDelta;
            CrankTotal = crankTotal;
            Stick = stick;
            Hits = hits;
        }

        /// <summary>True when either hand is doing something — the finale leaves on any input.</summary>
        public bool AnyInput => Mathf.Abs(CrankDelta) > 0.5f || Stick.sqrMagnitude > 0.09f || Hits > 0;
    }

    /// <summary>
    /// One screen of the flow (SCREENS.md S1–S6). Each owns a layer of the design frame and is
    /// destroyed whole when the flow moves on — that is the "no leftovers" half of the cabinet's
    /// lifecycle contract, and it means a screen can never leak a timer into the next one.
    /// </summary>
    public abstract class GameScreen
    {
        protected GameScreen(GameFlow flow, string name)
        {
            Flow = flow;
            Root = Ui.Layer(flow.Stage.Frame, name);
        }

        protected GameFlow Flow { get; }

        /// <summary>This screen's own layer; everything it draws lives under it.</summary>
        public RectTransform Root { get; private set; }

        /// <summary>Seconds this screen has been on.</summary>
        public float Age { get; private set; }

        public void Advance(float deltaTime, in Hands hands)
        {
            Age += deltaTime;
            Tick(deltaTime, hands);
        }

        protected abstract void Tick(float deltaTime, in Hands hands);

        /// <summary>What the tuning panel's readout block shows while this screen is up.</summary>
        public virtual string Readout() => string.Empty;

        /// <summary>
        /// What this screen sounds like (MECHANICS §8). The flow owns the three <c>AudioSource</c>s —
        /// they have to outlive a screen or every card would cut the music — so each screen only
        /// DESCRIBES its own state and the mix decides what that means. Silence off the level is the
        /// default because that is what the spec starts at.
        /// </summary>
        public virtual Mechanics.AudioScene Audio => Mechanics.AudioScene.Quiet(0);

        public virtual void Dispose()
        {
            if (Root != null) Object.Destroy(Root.gameObject);
            Root = null;
        }
    }
}
