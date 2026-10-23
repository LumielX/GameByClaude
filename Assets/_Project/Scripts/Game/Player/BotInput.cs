using UnityEngine;

namespace BooTrayal.Game.Player
{
    public enum BotButton { Jump, Interact, Throw, Drop, Push, Flashlight, Scream, PowerSabotage, PowerBlackout }

    /// <summary>Scripted input for smoke tests. Disabled unless a SmokeProbe turns it on.</summary>
    [DefaultExecutionOrder(-100)]
    public class BotInput : MonoBehaviour, IPlayerInput
    {
        const int ButtonCount = 9;

        // Presses requested by coroutines (which run after Update) become visible on the next frame's Update.
        readonly bool[] _pending = new bool[ButtonCount];
        readonly bool[] _active = new bool[ButtonCount];

        public Vector2 Move { get; set; }
        public Vector2 Look { get; set; }
        public bool Run { get; set; }
        public bool Crouch { get; set; }
        public bool JumpHeld { get; set; }
        public bool JumpPressed => _active[(int)BotButton.Jump];
        public bool InteractPressed => _active[(int)BotButton.Interact];
        public bool ThrowPressed => _active[(int)BotButton.Throw];
        public bool DropPressed => _active[(int)BotButton.Drop];
        public bool PushPressed => _active[(int)BotButton.Push];
        public bool FlashlightPressed => _active[(int)BotButton.Flashlight];
        public bool ScreamPressed => _active[(int)BotButton.Scream];
        public bool PowerSabotagePressed => _active[(int)BotButton.PowerSabotage];
        public bool PowerBlackoutPressed => _active[(int)BotButton.PowerBlackout];

        /// <summary>Presses a button for exactly one frame (the next one).</summary>
        public void Press(BotButton button) => _pending[(int)button] = true;

        void Update()
        {
            for (int i = 0; i < ButtonCount; i++)
            {
                _active[i] = _pending[i];
                _pending[i] = false;
            }
        }

        void LateUpdate()
        {
            for (int i = 0; i < ButtonCount; i++) _active[i] = false;
            Look = Vector2.zero;
        }
    }
}
