using UnityEngine;
using UnityEngine.InputSystem;

namespace BooTrayal.Game.Player
{
    /// <summary>Keyboard/mouse/gamepad bindings built in code (GDD 5 controls). Rebinding comes with Settings in M15.</summary>
    public class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        public static float MouseSensitivity = 0.12f;
        public const float GamepadLookDegreesPerSecond = 160f;

        /// <summary>True while a menu needs the mouse (Esc toggles). Gameplay input reads as idle.</summary>
        public static bool CursorFree { get; private set; }

        InputAction _move, _look, _lookPad, _run, _crouch, _jump, _interact, _throw, _drop, _push, _flashlight, _scream, _powerSabotage, _powerBlackout, _talk, _escape;
        bool _active;

        void Awake()
        {
            _move = new InputAction("Move");
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");
            _look = new InputAction("Look", binding: "<Mouse>/delta");
            _lookPad = new InputAction("LookPad", binding: "<Gamepad>/rightStick");
            _run = Button("Run", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            _crouch = Button("Crouch", "<Keyboard>/leftCtrl", "<Keyboard>/c", "<Gamepad>/buttonEast");
            _jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            _interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
            _throw = Button("Throw", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            _drop = Button("Drop", "<Keyboard>/g", "<Gamepad>/buttonNorth");
            _push = Button("Push", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            _flashlight = Button("Flashlight", "<Keyboard>/f", "<Gamepad>/dpad/up");
            _scream = Button("Scream", "<Keyboard>/z", "<Gamepad>/rightShoulder");
            _powerSabotage = Button("PowerSabotage", "<Keyboard>/q", "<Gamepad>/dpad/left");
            _powerBlackout = Button("PowerBlackout", "<Keyboard>/x", "<Gamepad>/dpad/right");
            _talk = Button("Talk", "<Keyboard>/v", "<Gamepad>/rightStickPress");
            _escape = Button("Escape", "<Keyboard>/escape", "<Gamepad>/start");
        }

        static InputAction Button(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) action.AddBinding(b);
            return action;
        }

        /// <summary>Called by the owning player only.</summary>
        public void Activate()
        {
            if (_active) return;
            _active = true;
            foreach (var a in All()) a.Enable();
            SetCursorFree(false);
        }

        void OnDisable()
        {
            if (!_active) return;
            foreach (var a in All()) a.Disable();
            _active = false;
        }

        void OnDestroy()
        {
            foreach (var a in All()) a?.Dispose();
        }

        void Update()
        {
            // Esc is handled by the UI (pause menu), which owns the cursor.
        }

        public static void SetCursorFree(bool free)
        {
            CursorFree = free;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        InputAction[] All() => new[] { _move, _look, _lookPad, _run, _crouch, _jump, _interact, _throw, _drop, _push, _flashlight, _scream, _powerSabotage, _powerBlackout, _talk, _escape };

        bool Live => _active && !CursorFree;

        public Vector2 Move => Live ? _move.ReadValue<Vector2>() : Vector2.zero;

        public Vector2 Look
        {
            get
            {
                if (!Live) return Vector2.zero;
                var mouse = _look.ReadValue<Vector2>() * MouseSensitivity;
                var pad = _lookPad.ReadValue<Vector2>() * (GamepadLookDegreesPerSecond * Time.deltaTime);
                return mouse + pad;
            }
        }

        public bool Run => Live && _run.IsPressed();
        public bool Crouch => Live && _crouch.IsPressed();
        public bool JumpPressed => Live && _jump.WasPressedThisFrame();
        public bool JumpHeld => Live && _jump.IsPressed();
        public bool InteractPressed => Live && _interact.WasPressedThisFrame();
        public bool ThrowPressed => Live && _throw.WasPressedThisFrame();
        public bool DropPressed => Live && _drop.WasPressedThisFrame();
        public bool PushPressed => Live && _push.WasPressedThisFrame();
        public bool FlashlightPressed => Live && _flashlight.WasPressedThisFrame();
        public bool ScreamPressed => Live && _scream.WasPressedThisFrame();
        public bool PowerSabotagePressed => Live && _powerSabotage.WasPressedThisFrame();
        public bool PowerBlackoutPressed => Live && _powerBlackout.WasPressedThisFrame();
        public bool TalkHeld => Live && _talk.IsPressed();
    }
}
