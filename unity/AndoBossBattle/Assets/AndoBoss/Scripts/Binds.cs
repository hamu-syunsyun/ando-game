using System.Collections.Generic;
using UnityEngine;
using K = AndoBoss.GameInput.K;

namespace AndoBoss
{
    // コントローラーのボタン（位置で表す。Xbox の A と PS の × と Switch の B は同じ「下」）
    public enum PadBtn { None, South, East, West, North, LB, RB, LT, RT, LS, RS, Start, Select, DUp, DDown, DLeft, DRight }

    // 操作の割り当て（キーボード・マウス2つ＋コントローラー2つ）と、そのプリセット。
    // 移動・視点・決定・一時停止などは固定で、ここでは戦闘の操作だけを変えられる
    public static class Binds
    {
        public static readonly K[] Actions = { K.Attack, K.Skill, K.Burst, K.Dodge, K.Jump, K.LockOn };
        public static readonly string[] ActionNames = { "通常攻撃", "特技", "奥義", "回避", "ジャンプ", "ロックオン" };
        public const int Slots = 2;

        public static KeyCode[,] Keys = new KeyCode[6, Slots];
        public static PadBtn[,] Pads = new PadBtn[6, Slots];

        public class Preset
        {
            public string Name;
            public KeyCode[,] Keys;
            public PadBtn[,] Pads;
        }

        const KeyCode N = KeyCode.None;
        public static readonly Preset[] BuiltIn =
        {
            new Preset
            {
                Name = "標準",
                Keys = new[,] { { KeyCode.Mouse0, KeyCode.J }, { KeyCode.E, N }, { KeyCode.Q, N }, { KeyCode.LeftShift, KeyCode.Mouse1 }, { KeyCode.Space, N }, { KeyCode.Tab, KeyCode.Mouse2 } },
                Pads = new[,] { { PadBtn.West, PadBtn.None }, { PadBtn.RB, PadBtn.None }, { PadBtn.North, PadBtn.LB }, { PadBtn.East, PadBtn.RT }, { PadBtn.South, PadBtn.None }, { PadBtn.RS, PadBtn.None } },
            },
            new Preset
            {
                // キーボードだけで遊ぶ人向け：左手で移動、右手で ZXCV
                Name = "キーボードだけ（ZXCV）",
                Keys = new[,] { { KeyCode.Z, KeyCode.J }, { KeyCode.X, KeyCode.K }, { KeyCode.C, KeyCode.L }, { KeyCode.V, KeyCode.LeftShift }, { KeyCode.Space, N }, { KeyCode.Tab, N } },
                Pads = new[,] { { PadBtn.West, PadBtn.None }, { PadBtn.RB, PadBtn.None }, { PadBtn.North, PadBtn.LB }, { PadBtn.East, PadBtn.RT }, { PadBtn.South, PadBtn.None }, { PadBtn.RS, PadBtn.None } },
            },
            new Preset
            {
                // 原神のコントローラー操作に近い配置：L で特技、R で奥義
                Name = "原神風（コントローラー）",
                Keys = new[,] { { KeyCode.Mouse0, KeyCode.J }, { KeyCode.E, N }, { KeyCode.Q, N }, { KeyCode.LeftShift, KeyCode.Mouse1 }, { KeyCode.Space, N }, { KeyCode.Tab, KeyCode.Mouse2 } },
                Pads = new[,] { { PadBtn.West, PadBtn.None }, { PadBtn.LB, PadBtn.None }, { PadBtn.RB, PadBtn.None }, { PadBtn.East, PadBtn.RT }, { PadBtn.South, PadBtn.None }, { PadBtn.RS, PadBtn.None } },
            },
            new Preset
            {
                // 肩ボタン中心：攻撃は RB、回避は B、特技と奥義はトリガー
                Name = "肩ボタン（コントローラー）",
                Keys = new[,] { { KeyCode.Mouse0, KeyCode.J }, { KeyCode.E, N }, { KeyCode.Q, N }, { KeyCode.LeftShift, KeyCode.Mouse1 }, { KeyCode.Space, N }, { KeyCode.Tab, KeyCode.Mouse2 } },
                Pads = new[,] { { PadBtn.RB, PadBtn.West }, { PadBtn.LT, PadBtn.None }, { PadBtn.RT, PadBtn.None }, { PadBtn.East, PadBtn.LB }, { PadBtn.South, PadBtn.None }, { PadBtn.RS, PadBtn.None } },
            },
        };
        public const int CustomSlots = 3;
        public static int PresetCount => BuiltIn.Length + CustomSlots;
        public static string PresetName(int i) => i < BuiltIn.Length ? BuiltIn[i].Name : $"マイ設定{i - BuiltIn.Length + 1}" + (HasCustom(i - BuiltIn.Length) ? "" : "（空き）");

        static bool loaded;
        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            Apply(BuiltIn[0]);
            try
            {
                var s = PlayerPrefs.GetString("ando_binds", "");
                if (s != "") Decode(s, Keys, Pads);
            }
            catch { Apply(BuiltIn[0]); }
        }

        public static void Apply(Preset p)
        {
            for (int a = 0; a < Actions.Length; a++)
                for (int s = 0; s < Slots; s++) { Keys[a, s] = p.Keys[a, s]; Pads[a, s] = p.Pads[a, s]; }
        }

        // プリセットを読む（組み込み、またはマイ設定）。空きのマイ設定なら false
        public static bool Load(int preset)
        {
            EnsureLoaded();
            if (preset < BuiltIn.Length) { Apply(BuiltIn[preset]); Save(); return true; }
            var s = PlayerPrefs.GetString("ando_preset_" + (preset - BuiltIn.Length), "");
            if (s == "") return false;
            Decode(s, Keys, Pads);
            Save();
            return true;
        }

        public static bool HasCustom(int slot) => PlayerPrefs.GetString("ando_preset_" + slot, "") != "";

        // 今の割り当てをマイ設定に登録する
        public static void SaveCustom(int slot)
        {
            PlayerPrefs.SetString("ando_preset_" + slot, Encode());
            PlayerPrefs.Save();
        }

        public static void Save()
        {
            PlayerPrefs.SetString("ando_binds", Encode());
            PlayerPrefs.Save();
        }

        static string Encode()
        {
            var parts = new List<string>();
            for (int a = 0; a < Actions.Length; a++)
                for (int s = 0; s < Slots; s++) { parts.Add(((int)Keys[a, s]).ToString()); parts.Add(((int)Pads[a, s]).ToString()); }
            return string.Join(",", parts);
        }

        static void Decode(string str, KeyCode[,] keys, PadBtn[,] pads)
        {
            var parts = str.Split(',');
            int i = 0;
            for (int a = 0; a < Actions.Length; a++)
                for (int s = 0; s < Slots; s++)
                {
                    keys[a, s] = (KeyCode)int.Parse(parts[i++]);
                    pads[a, s] = (PadBtn)int.Parse(parts[i++]);
                }
        }

        public static int IndexOf(K k)
        {
            for (int i = 0; i < Actions.Length; i++) if (Actions[i] == k) return i;
            return -1;
        }

        // 同じキーがほかの操作に入っていたら外す（1つのキーで2つの操作が動かないように）
        public static void SetKey(int action, int slot, KeyCode key)
        {
            for (int a = 0; a < Actions.Length; a++)
                for (int s = 0; s < Slots; s++)
                    if (Keys[a, s] == key) Keys[a, s] = KeyCode.None;
            Keys[action, slot] = key;
            Save();
        }

        public static void SetPad(int action, int slot, PadBtn b)
        {
            for (int a = 0; a < Actions.Length; a++)
                for (int s = 0; s < Slots; s++)
                    if (Pads[a, s] == b) Pads[a, s] = PadBtn.None;
            Pads[action, slot] = b;
            Save();
        }

        public static bool KeyHeld(K k)
        {
            EnsureLoaded();
            int a = IndexOf(k);
            for (int s = 0; s < Slots; s++) if (Keys[a, s] != KeyCode.None && Input.GetKey(Keys[a, s])) return true;
            return false;
        }

        public static bool KeyDown(K k)
        {
            EnsureLoaded();
            int a = IndexOf(k);
            for (int s = 0; s < Slots; s++) if (Keys[a, s] != KeyCode.None && Input.GetKeyDown(Keys[a, s])) return true;
            return false;
        }

        // 割り当てに使えるキー。移動の WASD・矢印キー、「やめる」の Esc・BS、「外す」の Delete は入れない
        public static readonly KeyCode[] Bindable = BuildBindable();
        static KeyCode[] BuildBindable()
        {
            var l = new List<KeyCode>();
            for (var c = KeyCode.A; c <= KeyCode.Z; c++) if (c != KeyCode.W && c != KeyCode.A && c != KeyCode.S && c != KeyCode.D) l.Add(c);
            for (var c = KeyCode.Alpha0; c <= KeyCode.Alpha9; c++) l.Add(c);
            for (var c = KeyCode.Keypad0; c <= KeyCode.Keypad9; c++) l.Add(c);
            l.AddRange(new[]
            {
                KeyCode.Space, KeyCode.Tab, KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl,
                KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.Return, KeyCode.CapsLock,
                KeyCode.Comma, KeyCode.Period, KeyCode.Slash, KeyCode.Semicolon, KeyCode.Quote, KeyCode.LeftBracket, KeyCode.RightBracket,
                KeyCode.Minus, KeyCode.Equals, KeyCode.BackQuote, KeyCode.Backslash,
                KeyCode.Insert, KeyCode.Home, KeyCode.End, KeyCode.PageUp, KeyCode.PageDown,
                KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2, KeyCode.Mouse3, KeyCode.Mouse4,
            });
            return l.ToArray();
        }

        public static string KeyName(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.None: return "−";
                case KeyCode.Mouse0: return "左クリック";
                case KeyCode.Mouse1: return "右クリック";
                case KeyCode.Mouse2: return "ホイール";
                case KeyCode.Mouse3: return "マウス4";
                case KeyCode.Mouse4: return "マウス5";
                case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
                case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
                case KeyCode.Return: return "Enter";
                case KeyCode.Backspace: return "BS";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Backslash: return "\\";
                case KeyCode.CapsLock: return "Caps";
            }
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((int)(k - KeyCode.Alpha0)).ToString();
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "テンキー" + (int)(k - KeyCode.Keypad0);
            return k.ToString();
        }

        public enum PadStyle { Xbox, PS, Switch }

        public static string PadName(PadBtn b, PadStyle st)
        {
            switch (b)
            {
                case PadBtn.None: return "−";
                case PadBtn.South: return st == PadStyle.PS ? "×" : st == PadStyle.Switch ? "B" : "A";
                case PadBtn.East: return st == PadStyle.PS ? "○" : st == PadStyle.Switch ? "A" : "B";
                case PadBtn.West: return st == PadStyle.PS ? "□" : st == PadStyle.Switch ? "Y" : "X";
                case PadBtn.North: return st == PadStyle.PS ? "△" : st == PadStyle.Switch ? "X" : "Y";
                case PadBtn.LB: return st == PadStyle.PS ? "L1" : st == PadStyle.Switch ? "L" : "LB";
                case PadBtn.RB: return st == PadStyle.PS ? "R1" : st == PadStyle.Switch ? "R" : "RB";
                case PadBtn.LT: return st == PadStyle.PS ? "L2" : st == PadStyle.Switch ? "ZL" : "LT";
                case PadBtn.RT: return st == PadStyle.PS ? "R2" : st == PadStyle.Switch ? "ZR" : "RT";
                case PadBtn.LS: return st == PadStyle.PS ? "L3" : st == PadStyle.Switch ? "Lスティック押し" : "LS";
                case PadBtn.RS: return st == PadStyle.PS ? "R3" : st == PadStyle.Switch ? "Rスティック押し" : "RS";
                case PadBtn.Start: return st == PadStyle.PS ? "OPTIONS" : st == PadStyle.Switch ? "＋" : "START";
                case PadBtn.Select: return st == PadStyle.PS ? "SHARE" : st == PadStyle.Switch ? "−" : "VIEW";
                case PadBtn.DUp: return "十字↑";
                case PadBtn.DDown: return "十字↓";
                case PadBtn.DLeft: return "十字←";
                case PadBtn.DRight: return "十字→";
            }
            return "?";
        }
    }
}
