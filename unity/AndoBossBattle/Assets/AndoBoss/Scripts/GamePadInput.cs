using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AndoBoss
{
    // コントローラー（Xbox / PlayStation / Switch プロコン）の入力。新 Input System の Gamepad で読むので、
    // Xbox と PS でボタンの位置は同じ扱いになる（A＝×、B＝○、X＝□、Y＝△）
    //
    //   左スティック/十字キー：移動   右スティック：視点   R3：ロックオン切替
    //   X/□：通常攻撃（押しっぱなしで連続）   A/×：ジャンプ・決定   B/○：回避・戻る
    //   RB/R1：特技   Y/△ または LB/L1：奥義   RT/R2：回避
    //   START/OPTIONS：一時停止   View/SHARE（Create）：タイトルへ
    public static partial class GameInput
    {
#if ENABLE_INPUT_SYSTEM
        static Gamepad Pad => Gamepad.current;
        public static bool PadConnected => Gamepad.current != null;

        // Switch プロコンかどうか。ボタンは位置で読むので、ゲーム中の操作は Xbox と同じ位置になる
        // （Switch の Y＝攻撃、B＝ジャンプ、A＝回避）。メニューだけは Switch の決まりに合わせて A で決定・B で戻る
        public static bool PadIsSwitch
        {
            get
            {
                var p = Pad;
                if (p == null) return false;
                var d = p.description;
                return p.layout.Contains("Switch") || (d.product != null && d.product.Contains("Pro Controller")) || (d.manufacturer != null && d.manufacturer.Contains("Nintendo"));
            }
        }

        // スティックを倒した瞬間を「押した」として扱う（メニューの左右選択用）
        static int stickFrame = -1;
        static float stickPrevX, stickNowX;
        static void UpdateStick()
        {
            if (stickFrame == Time.frameCount || Pad == null) return;
            stickFrame = Time.frameCount;
            stickPrevX = stickNowX;
            stickNowX = Pad.leftStick.ReadValue().x;
        }

        static bool PadHeld(K k)
        {
            var p = Pad;
            if (p == null) return false;
            switch (k)
            {
                case K.Attack: return p.buttonWest.isPressed;
                case K.Up: return p.dpad.up.isPressed;
                case K.Down: return p.dpad.down.isPressed;
                case K.Left: return p.dpad.left.isPressed;
                case K.Right: return p.dpad.right.isPressed;
            }
            return false;
        }

        static bool PadDown(K k)
        {
            var p = Pad;
            if (p == null) return false;
            UpdateStick();
            switch (k)
            {
                case K.Attack: return p.buttonWest.wasPressedThisFrame;
                case K.Jump: return p.buttonSouth.wasPressedThisFrame;
                case K.Confirm: return (PadIsSwitch ? p.buttonEast : p.buttonSouth).wasPressedThisFrame || p.startButton.wasPressedThisFrame;
                case K.Dodge: return p.buttonEast.wasPressedThisFrame || p.rightTrigger.wasPressedThisFrame;
                case K.Back: return (PadIsSwitch ? p.buttonSouth : p.buttonEast).wasPressedThisFrame;
                case K.Skill: return p.rightShoulder.wasPressedThisFrame;
                case K.Burst: return p.buttonNorth.wasPressedThisFrame || p.leftShoulder.wasPressedThisFrame;
                case K.Retry: return p.buttonNorth.wasPressedThisFrame;
                case K.Pause: return p.startButton.wasPressedThisFrame;
                case K.Title: return p.selectButton.wasPressedThisFrame;
                case K.LockOn: return p.rightStickButton.wasPressedThisFrame;
                case K.Left: return p.dpad.left.wasPressedThisFrame || (stickNowX < -0.6f && stickPrevX >= -0.6f);
                case K.Right: return p.dpad.right.wasPressedThisFrame || (stickNowX > 0.6f && stickPrevX <= 0.6f);
            }
            return false;
        }

        // 移動用の左スティック（遊びを取ってから 0〜1 に）
        static Vector2 PadMove()
        {
            var p = Pad;
            if (p == null) return Vector2.zero;
            var v = p.leftStick.ReadValue();
            if (p.dpad.ReadValue().sqrMagnitude > 0.1f) v = p.dpad.ReadValue();
            float m = v.magnitude;
            if (m < 0.2f) return Vector2.zero;
            return v / m * Mathf.Clamp01((m - 0.2f) / 0.75f);
        }

        // 視点用の右スティック
        public static Vector2 LookStick()
        {
            var p = Pad;
            if (p == null) return Vector2.zero;
            var v = p.rightStick.ReadValue();
            float m = v.magnitude;
            if (m < 0.15f) return Vector2.zero;
            return v / m * Mathf.Clamp01((m - 0.15f) / 0.8f);
        }
#else
        public static bool PadConnected => false;
        public static bool PadIsSwitch => false;
        static bool PadHeld(K k) => false;
        static bool PadDown(K k) => false;
        static Vector2 PadMove() => Vector2.zero;
        public static Vector2 LookStick() => Vector2.zero;
#endif
    }
}
