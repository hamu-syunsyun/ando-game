using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace AndoBoss
{
    // コントローラー（Xbox / PlayStation / Switch プロコン）の入力。新 Input System の Gamepad で読む。
    // ボタンは「位置」で扱う（下＝Xbox の A・PS の ×・Switch の B）。
    // 戦闘の操作は Binds の割り当て、メニューの操作は固定：
    //   左スティック/十字キー：移動・選択   右スティック：視点
    //   決定：下のボタン（Switch は右の A）  戻る：右のボタン（Switch は下の B）
    //   START/OPTIONS/＋：一時停止   VIEW/SHARE/−：タイトルへ   上のボタン：もう一度・設定
    public static partial class GameInput
    {
#if ENABLE_INPUT_SYSTEM
        static Gamepad Pad => Gamepad.current;
        public static bool PadConnected => Gamepad.current != null;

        // Switch プロコンかどうか。メニューだけは Switch の決まりに合わせて A で決定・B で戻る
        public static bool PadIsSwitch => Style == Binds.PadStyle.Switch;

        // ボタンの表記（Xbox / PS / Switch）
        public static Binds.PadStyle Style
        {
            get
            {
                var p = Pad;
                if (p == null) return Binds.PadStyle.Xbox;
                var d = p.description;
                string lay = p.layout ?? "", prod = d.product ?? "", man = d.manufacturer ?? "";
                if (lay.Contains("Switch") || prod.Contains("Pro Controller") || man.Contains("Nintendo")) return Binds.PadStyle.Switch;
                if (lay.Contains("DualShock") || lay.Contains("DualSense") || man.Contains("Sony") || prod.Contains("Wireless Controller") || prod.Contains("DualSense")) return Binds.PadStyle.PS;
                return Binds.PadStyle.Xbox;
            }
        }

        static ButtonControl Ctrl(Gamepad p, PadBtn b)
        {
            switch (b)
            {
                case PadBtn.South: return p.buttonSouth;
                case PadBtn.East: return p.buttonEast;
                case PadBtn.West: return p.buttonWest;
                case PadBtn.North: return p.buttonNorth;
                case PadBtn.LB: return p.leftShoulder;
                case PadBtn.RB: return p.rightShoulder;
                case PadBtn.LT: return p.leftTrigger;
                case PadBtn.RT: return p.rightTrigger;
                case PadBtn.LS: return p.leftStickButton;
                case PadBtn.RS: return p.rightStickButton;
                case PadBtn.Start: return p.startButton;
                case PadBtn.Select: return p.selectButton;
                case PadBtn.DUp: return p.dpad.up;
                case PadBtn.DDown: return p.dpad.down;
                case PadBtn.DLeft: return p.dpad.left;
                case PadBtn.DRight: return p.dpad.right;
            }
            return null;
        }

        // 設定画面用：今押されたボタン（なければ None）
        public static PadBtn PadPressedNow()
        {
            var p = Pad;
            if (p == null) return PadBtn.None;
            for (var b = PadBtn.South; b <= PadBtn.DRight; b++)
            {
                var c = Ctrl(p, b);
                if (c != null && c.wasPressedThisFrame) return b;
            }
            return PadBtn.None;
        }

        static bool PadActivity()
        {
            var p = Pad;
            if (p == null) return false;
            if (PadPressedNow() != PadBtn.None) return true;
            return p.leftStick.ReadValue().sqrMagnitude > 0.25f || p.rightStick.ReadValue().sqrMagnitude > 0.25f;
        }

        // スティックを倒した瞬間を「押した」として扱う（メニューの選択用）
        static int stickFrame = -1;
        static Vector2 stickPrev, stickNow;
        static void UpdateStick()
        {
            if (stickFrame == Time.frameCount || Pad == null) return;
            stickFrame = Time.frameCount;
            stickPrev = stickNow;
            stickNow = Pad.leftStick.ReadValue();
        }
        static bool Flick(Vector2 dir)
        {
            float now = Vector2.Dot(stickNow, dir), prev = Vector2.Dot(stickPrev, dir);
            return now > 0.6f && prev <= 0.6f;
        }

        static bool PadHeld(K k)
        {
            var p = Pad;
            if (p == null) return false;
            int a = Binds.IndexOf(k);
            if (a >= 0)
            {
                Binds.EnsureLoaded();
                for (int s = 0; s < Binds.Slots; s++) { var c = Ctrl(p, Binds.Pads[a, s]); if (c != null && c.isPressed) return true; }
                return false;
            }
            switch (k)
            {
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
            int a = Binds.IndexOf(k);
            if (a >= 0)
            {
                Binds.EnsureLoaded();
                for (int s = 0; s < Binds.Slots; s++) { var c = Ctrl(p, Binds.Pads[a, s]); if (c != null && c.wasPressedThisFrame) return true; }
                return false;
            }
            bool sw = PadIsSwitch;
            switch (k)
            {
                case K.Confirm: return (sw ? p.buttonEast : p.buttonSouth).wasPressedThisFrame || p.startButton.wasPressedThisFrame;
                case K.Back: return (sw ? p.buttonSouth : p.buttonEast).wasPressedThisFrame;
                case K.Retry: return p.buttonNorth.wasPressedThisFrame;
                case K.Settings: return p.buttonNorth.wasPressedThisFrame;
                case K.Pause: return p.startButton.wasPressedThisFrame;
                case K.Title: return p.selectButton.wasPressedThisFrame;
                case K.Up: return p.dpad.up.wasPressedThisFrame || Flick(Vector2.up);
                case K.Down: return p.dpad.down.wasPressedThisFrame || Flick(Vector2.down);
                case K.Left: return p.dpad.left.wasPressedThisFrame || Flick(Vector2.left);
                case K.Right: return p.dpad.right.wasPressedThisFrame || Flick(Vector2.right);
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
        public static Binds.PadStyle Style => Binds.PadStyle.Xbox;
        public static PadBtn PadPressedNow() => PadBtn.None;
        static bool PadActivity() => false;
        static bool PadHeld(K k) => false;
        static bool PadDown(K k) => false;
        static Vector2 PadMove() => Vector2.zero;
        public static Vector2 LookStick() => Vector2.zero;
#endif
    }
}
