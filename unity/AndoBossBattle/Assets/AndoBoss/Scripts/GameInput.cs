using UnityEngine;

namespace AndoBoss
{
    // キーボード・マウス・コントローラー（Xbox / PS / Switch プロコン）の入力をまとめる。
    // キーボードとマウスは旧 Input Manager、コントローラーは新 Input System で読む（両方有効の設定）。
    // 戦闘の操作（攻撃・特技・奥義・回避・ジャンプ・ロックオン）は Binds で割り当てを変えられる
    public static partial class GameInput
    {
        public enum K { Up, Down, Left, Right, Attack, Skill, Burst, Dodge, Jump, Confirm, Pause, Retry, Title, Mute, LockOn, Char1, Char2, Char3, Char4, Light, Style, Back, Settings }

        static bool IsBound(K k) => Binds.IndexOf(k) >= 0;

        // 最後に使ったのがコントローラーなら true（画面のボタン表記を切りかえる）
        public static bool UsingPad;
        static int deviceFrame = -1;

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
            if (IsBound(k)) return Binds.KeyHeld(k);
            switch (k)
            {
                case K.Up: return Any(KeyCode.W, KeyCode.UpArrow);
                case K.Down: return Any(KeyCode.S, KeyCode.DownArrow);
                case K.Left: return Any(KeyCode.A, KeyCode.LeftArrow);
                case K.Right: return Any(KeyCode.D, KeyCode.RightArrow);
            }
            return false;
        }

        static bool KbDown(K k)
        {
            if (IsBound(k)) return Binds.KeyDown(k);
            switch (k)
            {
                case K.Up: return AnyDown(KeyCode.W, KeyCode.UpArrow);
                case K.Down: return AnyDown(KeyCode.S, KeyCode.DownArrow);
                case K.Left: return AnyDown(KeyCode.A, KeyCode.LeftArrow);
                case K.Right: return AnyDown(KeyCode.D, KeyCode.RightArrow);
                case K.Confirm: return AnyDown(KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space) || Input.GetMouseButtonDown(0);
                case K.Pause: return AnyDown(KeyCode.Escape, KeyCode.P);
                case K.Retry: return AnyDown(KeyCode.R);
                case K.Title: return AnyDown(KeyCode.T);
                case K.Mute: return AnyDown(KeyCode.M);
                case K.Char1: return AnyDown(KeyCode.Alpha1, KeyCode.Keypad1);
                case K.Char2: return AnyDown(KeyCode.Alpha2, KeyCode.Keypad2);
                case K.Char3: return AnyDown(KeyCode.Alpha3, KeyCode.Keypad3);
                case K.Char4: return AnyDown(KeyCode.Alpha4, KeyCode.Keypad4);
                case K.Light: return AnyDown(KeyCode.F2);
                case K.Style: return AnyDown(KeyCode.F3);
                case K.Back: return AnyDown(KeyCode.Backspace, KeyCode.Escape);
                case K.Settings: return AnyDown(KeyCode.O);
            }
            return false;
        }

        static Vector2 KbMouseDelta()
        {
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        }

        public static bool Held(K k) => KbHeld(k) || PadHeld(k);
        public static bool Down(K k) => KbDown(k) || PadDown(k);
        public static Vector2 MouseDelta() => KbMouseDelta();

        // 毎フレーム1回：キーボード・マウスとコントローラーのどちらを最後に触ったかを調べる
        public static void UpdateDevice()
        {
            if (deviceFrame == Time.frameCount) return;
            deviceFrame = Time.frameCount;
            if (PadActivity()) { UsingPad = true; return; }
            if (!PadConnected) { UsingPad = false; return; }
            // コントローラーのボタンは旧 Input Manager では JoystickButton として anyKeyDown に入るので、
            // キーボード・マウスだけを見る
            bool kb = false;
            if (Input.anyKeyDown)
            {
                for (var c = KeyCode.Backspace; c <= KeyCode.Mouse6; c++)
                    if (Input.GetKeyDown(c)) { kb = true; break; }
            }
            if (!kb && KbMouseDelta().sqrMagnitude > 4f) kb = true;
            if (kb) UsingPad = false;
        }

        // 画面に出すボタン名（今使っているほうの機器で）
        public static string Label(K k)
        {
            UpdateDevice();
            int a = Binds.IndexOf(k);
            if (UsingPad && PadConnected)
            {
                if (a >= 0) return JoinPad(a);
                switch (k)
                {
                    case K.Confirm: return Binds.PadName(PadIsSwitch ? PadBtn.East : PadBtn.South, Style);
                    case K.Back: return Binds.PadName(PadIsSwitch ? PadBtn.South : PadBtn.East, Style);
                    case K.Pause: return Binds.PadName(PadBtn.Start, Style);
                    case K.Title: return Binds.PadName(PadBtn.Select, Style);
                    case K.Retry: case K.Settings: return Binds.PadName(PadBtn.North, Style);
                    case K.Left: case K.Right: return "十字キー";
                }
                return "";
            }
            if (a >= 0) return JoinKeys(a);
            switch (k)
            {
                case K.Confirm: return "Enter";
                case K.Back: return "BS";
                case K.Pause: return "Esc";
                case K.Title: return "T";
                case K.Retry: return "R";
                case K.Settings: return "O";
                case K.Left: case K.Right: return "← →";
            }
            return "";
        }

        // 1つ目の割り当てだけ（キーの小さい札に出す用）
        public static string ShortLabel(K k)
        {
            UpdateDevice();
            int a = Binds.IndexOf(k);
            if (a < 0) return Label(k);
            Binds.EnsureLoaded();
            if (UsingPad && PadConnected)
            {
                for (int s = 0; s < Binds.Slots; s++) if (Binds.Pads[a, s] != PadBtn.None) return Binds.PadName(Binds.Pads[a, s], Style);
                return "−";
            }
            for (int s = 0; s < Binds.Slots; s++) if (Binds.Keys[a, s] != KeyCode.None) return Binds.KeyName(Binds.Keys[a, s]);
            return "−";
        }

        static string JoinKeys(int a)
        {
            Binds.EnsureLoaded();
            string r = "";
            for (int s = 0; s < Binds.Slots; s++)
                if (Binds.Keys[a, s] != KeyCode.None) r += (r == "" ? "" : " / ") + Binds.KeyName(Binds.Keys[a, s]);
            return r == "" ? "−" : r;
        }

        static string JoinPad(int a)
        {
            Binds.EnsureLoaded();
            string r = "";
            for (int s = 0; s < Binds.Slots; s++)
                if (Binds.Pads[a, s] != PadBtn.None) r += (r == "" ? "" : " / ") + Binds.PadName(Binds.Pads[a, s], Style);
            return r == "" ? "−" : r;
        }

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
