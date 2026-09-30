using UnityEngine;
using K = AndoBoss.GameInput.K;

namespace AndoBoss
{
    // 難易度と、操作設定の画面（タイトルで O キー／コントローラーの上のボタン）
    public partial class Game
    {
        // ---- 難易度 ----
        public static int Difficulty = 1;
        public static readonly string[] DiffNames = { "やさしい", "ふつう", "むずかしい", "鬼" };
        static readonly string[] DiffBase =
        {
            "受けるダメージ少なめ・ボスのHP 7割・攻撃の間隔ゆっくり",
            "ふつうの強さ",
            "受けるダメージ多め・ボスのHP 1.15倍",
            "受けるダメージ大・ボスのHP 1.3倍・休みなく攻撃してくる",
        };
        // 難易度の説明（自分の体力はダブルかどうかで変わるので、その場で作る）
        public static string DiffNote(int d)
        {
            float hp = (IsDouble ? DiffPlayerHpDouble : DiffPlayerHp)[d];
            string h = hp > 1.001f ? $"・自分の体力 {hp:0.##}倍" : "";
            return $"{DiffBase[d]}{h}（点数×{DiffScore[d]:0.0}）";
        }
        static readonly float[] DiffDmg = { 0.8f, 1.4f, 1.8f, 2.3f };
        static readonly float[] DiffHp = { 0.7f, 1f, 1.15f, 1.3f };
        // 自分（プレイヤー）の体力：難易度を上げるごとに増える（攻撃が激しくなるぶん、耐えられるように）
        static readonly float[] DiffPlayerHp = { 1f, 1.1f, 1.25f, 1.4f };
        // ダブル（安東＋菅原）のときは2人ぶんの攻撃を受けるので、体力の増え方を多めにする
        static readonly float[] DiffPlayerHpDouble = { 1f, 1.25f, 1.5f, 1.8f };
        public static float PlayerHpMul => (IsDouble ? DiffPlayerHpDouble : DiffPlayerHp)[Difficulty];
        static readonly float[] DiffRest = { 1.8f, 1f, 0.65f, 0.4f };
        static readonly float[] DiffScore = { 0.7f, 1f, 1.2f, 1.5f };
        public static float BossHpMul => DiffHp[Difficulty];
        public static float BossRestMul => DiffRest[Difficulty];

        public void SetDifficulty(int d)
        {
            Difficulty = Mathf.Clamp(d, 0, DiffNames.Length - 1);
            PlayerPrefs.SetInt("ando_diff", Difficulty);
            PlayerPrefs.Save();
            // ボスのHPなどを作り直す
            ResetRound();
        }

        // ---- 設定画面 ----
        public const int RowDiff = 0, RowPreset = 1, RowAct0 = 2;
        public static int RowRegister => RowAct0 + Binds.Actions.Length;
        public static int RowReset => RowRegister + 1;
        public static int RowBack => RowRegister + 2;
        public int SetRow, SetCol, SetPreset, SetSlot;
        public bool SetWaiting;
        int setWaitFrame;

        internal void GoSettings()
        {

            State = Mode.Settings; stateT = 0;
            SetRow = 0; SetCol = 0; SetWaiting = false; SetSlot = 0;
            Sfx.Play("confirm", 0.8f);
            Hud.ShowSettings();
        }

        void LeaveSettings()
        {
            SetWaiting = false;
            Sfx.Play("swap", 0.6f, 0.9f);
            GoTitle();
        }

        void UpdateSettings()
        {
            TickBosses(Time.deltaTime);
            TickParty(Time.deltaTime);
            titleOrbit += Time.unscaledDeltaTime * 0.12f;
            var c = new Vector3(Mathf.Sin(titleOrbit) * 15, 5, Mathf.Cos(titleOrbit) * 15);
            Cam.Cinematic(c, BossFocus + Vector3.up * 3f, 50, false);

            if (SetWaiting) { UpdateWaiting(); return; }
            if (stateT < 0.2f) return;

            if (GameInput.Down(K.Up)) { SetRow = (SetRow + RowBack) % (RowBack + 1); Sfx.Play("tick", 0.5f); }
            if (GameInput.Down(K.Down)) { SetRow = (SetRow + 1) % (RowBack + 1); Sfx.Play("tick", 0.5f); }
            int lr = (GameInput.Down(K.Right) ? 1 : 0) - (GameInput.Down(K.Left) ? 1 : 0);
            if (lr != 0)
            {
                Sfx.Play("tick", 0.5f, 1.2f);
                if (SetRow == RowDiff) SetDifficulty(Difficulty + lr);
                else if (SetRow == RowPreset) SetPreset = (SetPreset + lr + Binds.PresetCount) % Binds.PresetCount;
                else if (SetRow == RowRegister) SetSlot = (SetSlot + lr + Binds.CustomSlots) % Binds.CustomSlots;
                else if (SetRow >= RowAct0 && SetRow < RowRegister) SetCol = (SetCol + lr + 4) % 4;
            }
            if (GameInput.Down(K.Back)) { LeaveSettings(); return; }
            if (!GameInput.Down(K.Confirm)) return;

            if (SetRow == RowPreset)
            {
                if (Binds.Load(SetPreset)) { Sfx.Play("confirm", 0.8f); Hud.Toast($"「{Binds.PresetName(SetPreset)}」にしました"); }
                else { Sfx.Play("hurt", 0.4f, 1.4f); Hud.Toast("このマイ設定はまだ空です（下の「マイ設定に登録」で保存できます）"); }
            }
            else if (SetRow == RowRegister)
            {
                Binds.SaveCustom(SetSlot);
                SetPreset = Binds.BuiltIn.Length + SetSlot;
                Sfx.Play("stamp", 0.8f);
                Hud.Toast($"今の操作を「マイ設定{SetSlot + 1}」に登録しました");
            }
            else if (SetRow == RowReset)
            {
                Binds.Load(0);
                SetPreset = 0;
                Sfx.Play("confirm", 0.8f);
                Hud.Toast("操作を初期設定にもどしました");
            }
            else if (SetRow == RowBack) LeaveSettings();
            else if (SetRow >= RowAct0 && SetRow < RowRegister)
            {
                SetWaiting = true;
                setWaitFrame = Time.frameCount;
                Sfx.Play("pop", 0.6f);
            }
        }

        // 割り当てるキー（ボタン）が押されるのを待つ
        void UpdateWaiting()
        {
            if (Time.frameCount <= setWaitFrame + 1) return;
            int a = SetRow - RowAct0;
            bool keyCol = SetCol < 2;
            int slot = SetCol % 2;
            if (keyCol)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) { SetWaiting = false; return; }
                if (Input.GetKeyDown(KeyCode.Delete)) { Binds.SetKey(a, slot, KeyCode.None); SetWaiting = false; return; }
                foreach (var k in Binds.Bindable)
                    if (Input.GetKeyDown(k)) { Binds.SetKey(a, slot, k); SetWaiting = false; Sfx.Play("confirm", 0.7f, 1.2f); return; }
            }
            else
            {
                var b = GameInput.PadPressedNow();
                if (b == PadBtn.Start || Input.GetKeyDown(KeyCode.Escape)) { SetWaiting = false; return; }
                if (b == PadBtn.Select || Input.GetKeyDown(KeyCode.Delete)) { Binds.SetPad(a, slot, PadBtn.None); SetWaiting = false; return; }
                if (b != PadBtn.None) { Binds.SetPad(a, slot, b); SetWaiting = false; Sfx.Play("confirm", 0.7f, 1.2f); }
            }
        }
    }
}
