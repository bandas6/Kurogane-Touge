using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Kurogane.Controls
{
    /// <summary>
    /// Lectura de controles: teclado y mando. Funciona con el Input System nuevo o con el antiguo,
    /// según lo que esté activo en Project Settings > Player > Active Input Handling.
    /// Los controles táctiles llegan en la Fase 5 escribiendo en <see cref="Touch"/>.
    /// </summary>
    public static class DriverInput
    {
        /// <summary>Valores que escriben los botones táctiles en pantalla (Fase 5).</summary>
        public static class Touch
        {
            public static float gas, brake, steer;
            public static bool hand;
        }

        public static float Gas
        {
            get
            {
                float v = Touch.gas;
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k != null && (k.upArrowKey.isPressed || k.wKey.isPressed)) v = 1;
                var g = Gamepad.current;
                if (g != null) v = Mathf.Max(v, g.rightTrigger.ReadValue());
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (UnityEngine.Input.GetKey(KeyCode.UpArrow) || UnityEngine.Input.GetKey(KeyCode.W)) v = 1;
#endif
                return v;
            }
        }

        public static float Brake
        {
            get
            {
                float v = Touch.brake;
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k != null && (k.downArrowKey.isPressed || k.sKey.isPressed)) v = 1;
                var g = Gamepad.current;
                if (g != null) v = Mathf.Max(v, g.leftTrigger.ReadValue());
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (UnityEngine.Input.GetKey(KeyCode.DownArrow) || UnityEngine.Input.GetKey(KeyCode.S)) v = 1;
#endif
                return v;
            }
        }

        /// <summary>-1 izquierda, +1 derecha.</summary>
        public static float Steer
        {
            get
            {
                float v = Touch.steer;
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.leftArrowKey.isPressed || k.aKey.isPressed) v -= 1;
                    if (k.rightArrowKey.isPressed || k.dKey.isPressed) v += 1;
                }
                var g = Gamepad.current;
                if (g != null)
                {
                    float x = g.leftStick.ReadValue().x;
                    if (Mathf.Abs(x) > 0.12f) v += x;
                }
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (UnityEngine.Input.GetKey(KeyCode.LeftArrow) || UnityEngine.Input.GetKey(KeyCode.A)) v -= 1;
                if (UnityEngine.Input.GetKey(KeyCode.RightArrow) || UnityEngine.Input.GetKey(KeyCode.D)) v += 1;
#endif
                return Mathf.Clamp(v, -1, 1);
            }
        }

        public static bool Handbrake
        {
            get
            {
                bool v = Touch.hand;
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k != null && k.spaceKey.isPressed) v = true;
                var g = Gamepad.current;
                if (g != null && g.buttonSouth.isPressed) v = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (UnityEngine.Input.GetKey(KeyCode.Space)) v = true;
#endif
                return v;
            }
        }

#if ENABLE_INPUT_SYSTEM
        static bool Pressed(Key key, System.Func<Gamepad, UnityEngine.InputSystem.Controls.ButtonControl> pad = null)
        {
            var k = Keyboard.current;
            if (k != null && k[key].wasPressedThisFrame) return true;
            var g = Gamepad.current;
            return g != null && pad != null && pad(g).wasPressedThisFrame;
        }
        public static bool ShiftUp => Pressed(Key.E, g => g.rightShoulder);
        public static bool ShiftDown => Pressed(Key.Q, g => g.leftShoulder);
        public static bool Respawn => Pressed(Key.R, g => g.buttonNorth);
        public static bool CycleCamera => Pressed(Key.C, g => g.buttonWest);
        public static bool Pause => Pressed(Key.Escape, g => g.startButton) || Pressed(Key.P);
        public static bool Confirm => Pressed(Key.Enter, g => g.buttonSouth);
        public static bool NextCar => Pressed(Key.Tab, g => g.selectButton);
        public static bool MenuUp => Pressed(Key.UpArrow, g => g.dpad.up) || Pressed(Key.W);
        public static bool MenuDown => Pressed(Key.DownArrow, g => g.dpad.down) || Pressed(Key.S);
        public static bool MenuLeft => Pressed(Key.LeftArrow, g => g.dpad.left) || Pressed(Key.A);
        public static bool MenuRight => Pressed(Key.RightArrow, g => g.dpad.right) || Pressed(Key.D);
#elif ENABLE_LEGACY_INPUT_MANAGER
        public static bool ShiftUp => UnityEngine.Input.GetKeyDown(KeyCode.E);
        public static bool ShiftDown => UnityEngine.Input.GetKeyDown(KeyCode.Q);
        public static bool Respawn => UnityEngine.Input.GetKeyDown(KeyCode.R);
        public static bool CycleCamera => UnityEngine.Input.GetKeyDown(KeyCode.C);
        public static bool Pause => UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.P);
        public static bool Confirm => UnityEngine.Input.GetKeyDown(KeyCode.Return);
        public static bool NextCar => UnityEngine.Input.GetKeyDown(KeyCode.Tab);
        public static bool MenuUp => UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) || UnityEngine.Input.GetKeyDown(KeyCode.W);
        public static bool MenuDown => UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) || UnityEngine.Input.GetKeyDown(KeyCode.S);
        public static bool MenuLeft => UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.A);
        public static bool MenuRight => UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) || UnityEngine.Input.GetKeyDown(KeyCode.D);
#else
        public static bool ShiftUp => false;
        public static bool ShiftDown => false;
        public static bool Respawn => false;
        public static bool CycleCamera => false;
        public static bool Pause => false;
        public static bool Confirm => false;
        public static bool NextCar => false;
        public static bool MenuUp => false;
        public static bool MenuDown => false;
        public static bool MenuLeft => false;
        public static bool MenuRight => false;
#endif
    }
}
