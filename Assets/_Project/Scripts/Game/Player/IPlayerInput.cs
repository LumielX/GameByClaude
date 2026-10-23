using UnityEngine;

namespace BooTrayal.Game.Player
{
    /// <summary>One frame of player intent. "Pressed" values are true for a single frame only.</summary>
    public interface IPlayerInput
    {
        Vector2 Move { get; }
        /// <summary>Look delta in degrees this frame (x = yaw, y = pitch up).</summary>
        Vector2 Look { get; }
        bool Run { get; }
        bool Crouch { get; }
        bool JumpPressed { get; }
        /// <summary>Jump held (mini ghosts fly up).</summary>
        bool JumpHeld { get; }
        bool InteractPressed { get; }
        bool ThrowPressed { get; }
        bool DropPressed { get; }
        bool PushPressed { get; }
        bool FlashlightPressed { get; }
        bool ScreamPressed { get; }
        /// <summary>Spy power keys until the M6 power wheel: Q sabotage, X blackout.</summary>
        bool PowerSabotagePressed { get; }
        bool PowerBlackoutPressed { get; }
        /// <summary>Push-to-talk held (V).</summary>
        bool TalkHeld { get; }
    }

    public static class PlayerInputSource
    {
        /// <summary>An enabled BotInput (smoke tests) wins over the keyboard/mouse reader.</summary>
        public static IPlayerInput Get(GameObject player)
        {
            var bot = player.GetComponent<BotInput>();
            if (bot != null && bot.enabled) return bot;
            return player.GetComponent<PlayerInputReader>();
        }
    }
}
