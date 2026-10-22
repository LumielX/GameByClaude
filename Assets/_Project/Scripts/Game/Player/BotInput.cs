using UnityEngine;

namespace BooTrayal.Game.Player
{
    public enum BotButton { Jump, Interact, Throw, Drop, Push, Flashlight }

    /// <summary>Scripted input for smoke tests. Disabled unless a SmokeProbe turns it on.</summary>
    public class BotInput : MonoBehaviour, IPlayerInput
    {
        bool _jump, _interact, _throw, _drop, _push, _flashlight;

        public Vector2 Move { get; set; }
        public Vector2 Look { get; set; }
        public bool Run { get; set; }
        public bool Crouch { get; set; }
        public bool JumpPressed => _jump;
        public bool InteractPressed => _interact;
        public bool ThrowPressed => _throw;
        public bool DropPressed => _drop;
        public bool PushPressed => _push;
        public bool FlashlightPressed => _flashlight;

        /// <summary>Presses a button for exactly one frame.</summary>
        public void Press(BotButton button)
        {
            switch (button)
            {
                case BotButton.Jump: _jump = true; break;
                case BotButton.Interact: _interact = true; break;
                case BotButton.Throw: _throw = true; break;
                case BotButton.Drop: _drop = true; break;
                case BotButton.Push: _push = true; break;
                case BotButton.Flashlight: _flashlight = true; break;
            }
        }

        void LateUpdate()
        {
            _jump = _interact = _throw = _drop = _push = _flashlight = false;
            Look = Vector2.zero;
        }
    }
}
