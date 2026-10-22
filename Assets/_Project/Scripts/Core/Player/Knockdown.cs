using System;

namespace BooTrayal.Core.Player
{
    /// <summary>"Ragdoll-lite" fall timer: input is ignored while down.</summary>
    public sealed class Knockdown
    {
        public float Remaining { get; private set; }
        public bool IsDown => Remaining > 0f;

        /// <summary>A new knockdown never shortens one already in progress.</summary>
        public void Start(float seconds) => Remaining = Math.Max(Remaining, seconds);

        public void Tick(float dt) => Remaining = Math.Max(0f, Remaining - dt);
    }
}
