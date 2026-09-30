using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AndoBoss
{
    // キーボード・マウス・コントローラー（Xbox / PS）の入力をまとめる。
    // キーボードとマウスは旧 Input Manager、コントローラーは新 Input System で読む（両方有効の設定）
    public static partial class GameInput
    {
        public enum K { Up, Down, Left, Right, Attack, Skill, Burst, Dodge, Jump, Confirm, Pause, Retry, Title, Mute, LockOn, Char1, Char2, Char3, Light, Style, Back }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        static bool Any(params Key[] ks)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            foreach (var k in ks) if (kb[k].isPressed) return true;
            return false;
        }
        static bool AnyDown(params Key[] ks)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            foreach (var k in ks) if (kb[k].wasPressedThisFrame) return true;
            return false;
        }
        static Mouse M => Mouse.current;

        static bool KbHeld(K k)
        {
            switch (k)
            {
                case K.Up: return Any(Key.W, Key.UpArrow);
                case K.Down: return Any(Key.S, Key.DownArrow);
                case K.Left: return Any(Key.A, Key.LeftArrow);
                case K.Right: return Any(Key.D, Key.RightArrow);
                case K.Attack: return Any(Key.J) || (M != null && M.leftButton.isPressed);
            }
            return false;
        }

        static bool KbDown(K k)
        {
            switch (k)
            {
                case K.Attack: return AnyDown(Key.J) || (M != null && M.leftButton.wasPressedThisFrame);
                case K.Left: return AnyDown(Key.A, Key.LeftArrow);
                case K.Right: return AnyDown(Key.D, Key.RightArrow);
                case K.Skill: return AnyDown(Key.E);
                case K.Burst: return AnyDown(Key.Q);
                case K.Dodge: return AnyDown(Key.LeftShift, Key.RightShift, Key.K) || (M != null && M.rightButton.wasPressedThisFrame);
                case K.Jump: return AnyDown(Key.Space);
                case K.Confirm: return AnyDown(Key.Enter, Key.NumpadEnter, Key.Space) || (M != null && M.leftButton.wasPressedThisFrame);
                case K.Pause: return AnyDown(Key.Escape, Key.P);
                case K.Retry: return AnyDown(Key.R);
                case K.Title: return AnyDown(Key.T);
                case K.Mute: return AnyDown(Key.M);
                case K.LockOn: return AnyDown(Key.Tab) || (M != null && M.middleButton.wasPressedThisFrame);
                case K.Char1: return AnyDown(Key.Digit1, Key.Numpad1);
                case K.Char2: return AnyDown(Key.Digit2, Key.Numpad2);
                case K.Char3: return AnyDown(Key.Digit3, Key.Numpad3);
                case K.Light: return AnyDown(Key.F2);
                case K.Style: return AnyDown(Key.F3);
                case K.Back: return AnyDown(Key.Backspace);
            }
            return false;
        }

        static Vector2 KbMouseDelta()
        {
            if (M == null) return Vector2.zero;
            return M.delta.ReadValue() * 0.05f;
        }
#else
        static bool Any(params KeyCode[] ks)
        {
            foreach (var k in ks) if (Input.GetKey(k)) return true;
            return false;
        }
        static bool AnyDown(params KeyCode[] ks)
        {
            foreach (var k in ks) if (Input.GetKeyDown(k)) return true;
            return false;
        }

        static bool KbHeld(K k)
        {
            switch (k)
            {
                case K.Up: return Any(KeyCode.W, KeyCode.UpArrow);
                case K.Down: return Any(KeyCode.S, KeyCode.DownArrow);
                case K.Left: return Any(KeyCode.A, KeyCode.LeftArrow);
                case K.Right: return Any(KeyCode.D, KeyCode.RightArrow);
                case K.Attack: return Any(KeyCode.J) || Input.GetMouseButton(0);
            }
            return false;
        }

        static bool KbDown(K k)
        {
            switch (k)
            {
                case K.Attack: return AnyDown(KeyCode.J) || Input.GetMouseButtonDown(0);
                case K.Left: return AnyDown(KeyCode.A, KeyCode.LeftArrow);
                case K.Right: return AnyDown(KeyCode.D, KeyCode.RightArrow);
                case K.Skill: return AnyDown(KeyCode.E);
                case K.Burst: return AnyDown(KeyCode.Q);
                case K.Dodge: return AnyDown(KeyCode.LeftShift, KeyCode.RightShift, KeyCode.K) || Input.GetMouseButtonDown(1);
                case K.Jump: return AnyDown(KeyCode.Space);
                case K.Confirm: return AnyDown(KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space) || Input.GetMouseButtonDown(0);
                case K.Pause: return AnyDown(KeyCode.Escape, KeyCode.P);
                case K.Retry: return AnyDown(KeyCode.R);
                case K.Title: return AnyDown(KeyCode.T);
                case K.Mute: return AnyDown(KeyCode.M);
                case K.LockOn: return AnyDown(KeyCode.Tab) || Input.GetMouseButtonDown(2);
                case K.Char1: return AnyDown(KeyCode.Alpha1, KeyCode.Keypad1);
                case K.Char2: return AnyDown(KeyCode.Alpha2, KeyCode.Keypad2);
                case K.Char3: return AnyDown(KeyCode.Alpha3, KeyCode.Keypad3);
                case K.Light: return AnyDown(KeyCode.F2);
                case K.Style: return AnyDown(KeyCode.F3);
                case K.Back: return AnyDown(KeyCode.Backspace);
            }
            return false;
        }

        static Vector2 KbMouseDelta()
        {
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        }
#endif

        public static bool Held(K k) => KbHeld(k) || PadHeld(k);
        public static bool Down(K k) => KbDown(k) || PadDown(k);
        public static Vector2 MouseDelta() => KbMouseDelta();

        // カメラ基準の移動方向（XZ 平面、長さ 0〜1）。スティックを少し倒すとゆっくり歩く
        public static Vector3 MoveVector(float camYaw)
        {
            float fz = (KbHeld(K.Up) ? 1 : 0) - (KbHeld(K.Down) ? 1 : 0);
            float rx = (KbHeld(K.Right) ? 1 : 0) - (KbHeld(K.Left) ? 1 : 0);
            var stick = PadMove();
            if (stick.sqrMagnitude > 0) { rx = stick.x; fz = stick.y; }
            var fwd = new Vector3(Mathf.Sin(camYaw), 0, Mathf.Cos(camYaw));
            var right = new Vector3(fwd.z, 0, -fwd.x);
            var v = fwd * fz + right * rx;
            if (v.sqrMagnitude > 1) v.Normalize();
            if (stick.sqrMagnitude == 0 && v.sqrMagnitude > 0) v.Normalize();
            return v;
        }
    }
}
