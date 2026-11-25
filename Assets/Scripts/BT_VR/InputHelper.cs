using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BT_VR
{
    /// <summary>
    /// Lop ho tro doc ban phim & chuot tuong thich ca New Input System va Legacy Input Manager.
    /// </summary>
    public static class InputHelper
    {
        public static bool GetKey(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                return key switch
                {
                    KeyCode.LeftArrow => kb.leftArrowKey.isPressed,
                    KeyCode.RightArrow => kb.rightArrowKey.isPressed,
                    KeyCode.UpArrow => kb.upArrowKey.isPressed,
                    KeyCode.DownArrow => kb.downArrowKey.isPressed,
                    KeyCode.A => kb.aKey.isPressed,
                    KeyCode.D => kb.dKey.isPressed,
                    KeyCode.W => kb.wKey.isPressed,
                    KeyCode.S => kb.sKey.isPressed,
                    KeyCode.Space => kb.spaceKey.isPressed,
                    _ => false
                };
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#else
            return false;
#endif
        }

        public static bool GetKeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                return key switch
                {
                    KeyCode.Alpha1 => kb.digit1Key.wasPressedThisFrame,
                    KeyCode.Alpha2 => kb.digit2Key.wasPressedThisFrame,
                    KeyCode.Alpha3 => kb.digit3Key.wasPressedThisFrame,
                    KeyCode.Alpha4 => kb.digit4Key.wasPressedThisFrame,
                    KeyCode.R => kb.rKey.wasPressedThisFrame,
                    KeyCode.M => kb.mKey.wasPressedThisFrame,
                    KeyCode.L => kb.lKey.wasPressedThisFrame,
                    KeyCode.K => kb.kKey.wasPressedThisFrame,
                    KeyCode.Space => kb.spaceKey.wasPressedThisFrame,
                    _ => false
                };
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

        public static float GetHorizontalAxis()
        {
            float h = 0f;
            if (GetKey(KeyCode.LeftArrow) || GetKey(KeyCode.A)) h -= 1f;
            if (GetKey(KeyCode.RightArrow) || GetKey(KeyCode.D)) h += 1f;
            return h;
        }

        public static float GetVerticalAxis()
        {
            float v = 0f;
            if (GetKey(KeyCode.DownArrow) || GetKey(KeyCode.S)) v -= 1f;
            if (GetKey(KeyCode.UpArrow) || GetKey(KeyCode.W)) v += 1f;
            return v;
        }

        public static float GetMouseDeltaX()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.delta.x.ReadValue() * 0.05f;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxis("Mouse X");
#else
            return 0f;
#endif
        }
    }
}
