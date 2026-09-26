using UnityEngine;

namespace AndoBoss
{
    // ゲーム全体の進行：タイトル → 登場演出 → 戦闘 → 決着 → 結果
    public class Game : MonoBehaviour
    {
        public static Game I;
        public enum Mode { Title, Intro, Battle, Ending, Result }
        public enum HitKind { Normal, Skill, Burst }

        public Mode State { get; private set; }
        public Player Player;
        public Boss Boss;
        public CameraRig Cam;
        public Hud Hud;
        public Music Music;

        public const float TimeLimit = 180f;
        public float TimeLeft;
        public int Dealt, Combo, MaxCombo, Perfects;
        float comboT, stateT, titleOrbit;
        bool paused;
        public bool Cinematic;
        bool win; string loseReason;
        bool energyAnnounced;
        const string BestKey = "ando_boss_best";

        // どのシーンで再生しても、これが無ければ自動で作る
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<Game>() == null) new GameObject("AndoBossGame").AddComponent<Game>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // シーンに最初からあるカメラ・ライト・リスナーは止める（全部こちらで用意する）
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) l.gameObject.SetActive(false);
            foreach (var a in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) a.enabled = false;

            Mat.Init();
            var worldRoot = new GameObject("World").transform;
            Random.InitState(20240901);
            World.Build(worldRoot);
            Random.InitState(System.Environment.TickCount);

            var camGo = new GameObject("Camera");
            Cam = camGo.AddComponent<CameraRig>();
            Cam.Init();

            gameObject.AddComponent<Fx>();
            gameObject.AddComponent<Sfx>();
            Music = gameObject.AddComponent<Music>();
            Hud = gameObject.AddComponent<Hud>();
            Hud.Build();

            Player = new GameObject("Player").AddComponent<Player>();
            Player.Build();
            Boss = new GameObject("Boss").AddComponent<Boss>();
            Boss.Build();

            ResetRound();
            GoTitle();
        }

        // ================= 場面の切り替え =================
        void ResetRound()
        {
            Fx.ClearAll();
            foreach (var o in GameObject.FindObjectsByType<Orb>(FindObjectsSortMode.None)) Destroy(o.gameObject);
            Player.ResetState();
            Boss.ResetState();
            TimeLeft = TimeLimit;
            Dealt = Combo = MaxCombo = Perfects = 0;
            comboT = 0;
            energyAnnounced = false;
            Cinematic = false;
            Sky.SetStorm(0);
            if (PostFX.I) { PostFX.I.Desaturate(0); PostFX.I.SetTint(Color.white); PostFX.I.LowHp = 0; }
            Music.Duck(false);
            Music.SetMuffled(false);
        }

        void GoTitle()
        {
            ResetRound();
            State = Mode.Title; stateT = 0;
            Hud.ShowTitle(PlayerPrefs.GetInt(BestKey, 0));
            Music.SetTitle();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Boss.Face = Mathf.PI;
        }

        void GoIntro()
        {
            ResetRound();
            State = Mode.Intro; stateT = 0;
            Hud.ShowBattle();
            Hud.ShowIntro();
            Sfx.Play("confirm", 0.8f);
            Sfx.Play("roar", 0.6f, 1.2f);
            Music.Mix(0, 0.9f, 0.9f, 0, 0.6f);
            Say("……私から単位を取るつもりですか？", 2.8f);
            LockCursor();
        }

        void GoBattle()
        {
            State = Mode.Battle; stateT = 0;
            Cam.EndCinematic();
            Cam.SnapBehindPlayer();
            Hud.EndIntro();
            Hud.Banner("単位争奪戦", "START!", Color.white, 1.2f);
            Sfx.Play("burstHit", 0.6f);
            PostFX.I?.Flash(Color.white, 0.5f);
            Cam.Shake(0.3f);
            Music.Restart();
            Music.SetBattle(false);
            if (Cursor.lockState != CursorLockMode.Locked) Hud.Toast("画面をクリックするとマウスで視点を動かせます");
        }

        void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ================= 毎フレーム =================
        void Update()
        {
            float dt = Time.deltaTime;
            stateT += Time.unscaledDeltaTime;
            Sky.Update(Time.unscaledDeltaTime);
            World.Update(dt, Boss.Phase == 2 ? 1 : 0);

            if (GameInput.Down(GameInput.K.Mute))
            {
                Music.Muted = !Music.Muted;
                Sfx.Volume = Music.Muted ? 0 : 0.9f;
                Hud.Toast(Music.Muted ? "音：オフ" : "音：オン");
            }

            if (State == Mode.Battle && GameInput.Down(GameInput.K.Pause) && !paused) { SetPause(true); return; }
            if (paused)
            {
                if (GameInput.Down(GameInput.K.Confirm)) { SetPause(false); LockCursor(); }
                else if (GameInput.Down(GameInput.K.Title)) { SetPause(false); GoTitle(); }
                return;
            }

            switch (State)
            {
                case Mode.Title: UpdateTitle(); break;
                case Mode.Intro: UpdateIntro(); break;
                case Mode.Battle: UpdateBattle(dt); break;
                case Mode.Ending: UpdateEnding(dt); break;
                case Mode.Result: UpdateResult(); break;
            }
        }

        void LateUpdate()
        {
            Cam.Tick();
        }

        void SetPause(bool on)
        {
            paused = on;
            Fx.I.Paused = on;
            Hud.Pause(on);
            Music.SetMuffled(on);
            if (on) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        void UpdateTitle()
        {
            titleOrbit += Time.unscaledDeltaTime * 0.12f;
            var c = new Vector3(Mathf.Sin(titleOrbit) * 15, 5 + Mathf.Sin(titleOrbit * 0.7f) * 1.5f, Mathf.Cos(titleOrbit) * 15);
            Cam.Cinematic(c, Boss.Pos + Vector3.up * 3.2f + new Vector3(Mathf.Cos(titleOrbit), 0, -Mathf.Sin(titleOrbit)) * 4, 50, stateT < 0.1f);
            Player.Tick(Time.deltaTime);
            Boss.Tick(Time.deltaTime);
            if (stateT > 0.5f && GameInput.Down(GameInput.K.Confirm)) GoIntro();
        }

        void UpdateIntro()
        {
            float t = stateT;
            var B = Boss; var P = Player;
            if (t < 1.8f)
            {
                // ボスの顔にぐっと寄る
                float u = t / 1.8f;
                var from = B.Pos + new Vector3(2.5f, 1.2f, -5.5f);
                var to = B.Pos + new Vector3(1.2f, 3.8f, -4.2f);
                Cam.Cinematic(Vector3.Lerp(from, to, u), B.Pos + Vector3.up * (3.6f + u * 0.6f), 42, t < 0.05f);
                if (t > 0.9f && t - Time.unscaledDeltaTime <= 0.9f) { Sfx.Play("thunder", 0.6f); Fx.Bolt(B.Pos + new Vector3(4, 0, 2), Mat.Electro); Cam.Shake(0.3f); }
            }
            else
            {
                var behind = P.Pos + new Vector3(0, 2.6f, -6);
                Cam.Cinematic(behind, (P.Pos + B.Pos) * 0.5f + Vector3.up * 2f, 55);
            }
            Player.Tick(Time.deltaTime);
            Boss.Tick(Time.deltaTime);
            if (t > 3.3f || (t > 0.8f && GameInput.Down(GameInput.K.Confirm))) GoBattle();
        }

        void UpdateBattle(float dt)
        {
            if (Cursor.lockState != CursorLockMode.Locked && GameInput.Down(GameInput.K.Attack)) LockCursor();
            TimeLeft -= dt;
            Player.Tick(dt);
            Boss.Tick(dt);

            comboT -= dt;
            if (comboT <= 0 && Combo > 0) Combo = 0;

            if (PostFX.I) PostFX.I.LowHp = Player.Hp < Player.MaxHp * 0.3f ? 1 - Player.Hp / (Player.MaxHp * 0.3f) * 0.6f : 0;
            if (Player.Energy >= 100 && !energyAnnounced)
            {
                energyAnnounced = true;
                Sfx.Play("ready", 0.8f);
                Hud.Toast("元素爆発 準備完了！　Q で発動");
            }
            if (Player.Energy < 100) energyAnnounced = false;

            if (TimeLeft <= 0 && State == Mode.Battle) Lose("時間切れ…");
        }

        void UpdateEnding(float dt)
        {
            Player.Tick(dt);
            Boss.Tick(dt);
            if (win)
            {
                var B = Boss;
                float t = stateT;
                var side = Player.Flat(Player.Pos - B.Pos).normalized;
                if (side.sqrMagnitude < 0.01f) side = Vector3.back;
                var right = new Vector3(side.z, 0, -side.x);
                Cam.Cinematic(B.Pos + side * (8 - t * 0.6f) + right * 3 + Vector3.up * (3 + t * 0.3f), B.Pos + Vector3.up * 2.4f, 48);
                if (t > 2.4f)
                {
                    var pf = new Vector3(Mathf.Sin(Player.Face), 0, Mathf.Cos(Player.Face));
                    Cam.Cinematic(Player.Pos + pf * 4.2f + Vector3.up * 1.6f + new Vector3(pf.z, 0, -pf.x) * 1.2f, Player.Pos + Vector3.up * 1.5f, 45);
                    Player.Face = Player.TurnTo(Player.Face, Mathf.Atan2(Cam.transform.position.x - Player.Pos.x, Cam.transform.position.z - Player.Pos.z), dt * 5);
                }
                if (t > 5.2f) ShowResult();
            }
            else
            {
                Cam.Cinematic(Player.Pos + new Vector3(0, 6, -4), Player.Pos + Vector3.up * 0.5f, 50);
                if (stateT > 3.6f) ShowResult();
            }
        }

        void UpdateResult()
        {
            if (stateT < 1.0f) return;
            if (GameInput.Down(GameInput.K.Retry)) { GoIntro(); return; }
            if (GameInput.Down(GameInput.K.Title) || (stateT > 5f && GameInput.Down(GameInput.K.Confirm)) || stateT > 60f) GoTitle();
        }

        // ================= 戦闘のできごと =================
        public void Say(string text, float sec = 2.6f) => Hud.Say(text, sec);

        public void DamageBoss(float baseDmg, HitKind kind, Vector3 hitPos)
        {
            var B = Boss; var P = Player;
            if (State != Mode.Battle || !B.Alive) return;
            float critRate = P.BuffT > 0 ? 0.7f : 0.22f;
            bool crit = Random.value < critRate;
            float mul = (crit ? 1.8f : 1f) * (B.Broken ? 1.5f : 1f) * Random.Range(0.9f, 1.1f);
            int dmg = Mathf.Max(1, Mathf.RoundToInt(baseDmg * mul));
            dmg = Mathf.Min(dmg, Mathf.CeilToInt(B.Hp));
            B.Hp -= dmg;
            Dealt += dmg;
            float tough = dmg * (kind == HitKind.Skill ? 1.6f : kind == HitKind.Burst ? 1.0f : 0.9f);
            B.OnDamaged(dmg, tough);

            var numKind = kind == HitKind.Burst ? Hud.NumKind.Burst : crit ? Hud.NumKind.Crit : B.Broken ? Hud.NumKind.Break : Hud.NumKind.Normal;
            Hud.Number(hitPos + Vector3.up * 0.5f, dmg, numKind);
            Hud.BossBarShake();
            Fx.Sparks(hitPos, crit ? Mat.Gold : Mat.ElectroLight, crit ? 22 : 12, crit ? 1.4f : 1f);
            Fx.Glow(hitPos, crit ? Mat.Gold : Mat.Electro, crit ? 1.3f : 0.8f);
            if (crit) { Fx.AirRing(hitPos, 2.2f, Mat.Gold, 0.25f); Fx.Stars(hitPos, Mat.Gold, 4); }
            Sfx.Play(crit ? "crit" : "hit", crit ? 0.9f : 0.75f, crit ? 1f : Random.Range(0.95f, 1.1f));
            Sfx.Play("zap", 0.25f, 1.4f);
            Fx.HitStop(kind == HitKind.Burst ? 0.05f : crit ? 0.075f : 0.04f);
            Cam.Shake(crit ? 0.22f : 0.1f);
            if (crit) PostFX.I?.Chroma(0.12f);

            Combo++; comboT = 2.6f;
            MaxCombo = Mathf.Max(MaxCombo, Combo);
            Hud.Combo(Combo);
            if (Combo % 10 == 0) Sfx.Play("tick", 0.6f, 1 + Mathf.Min(Combo, 60) / 60f);

            if (kind == HitKind.Normal)
            {
                P.Energy = Mathf.Min(100, P.Energy + 2);
                if (Random.value < 0.3f) SpawnOrbs(hitPos, 1);
            }
            if (B.Hp <= 0) Win();
        }

        public void OnPlayerHurt(float amount)
        {
            Hud.Number(Player.Pos + Vector3.up * 2.2f, Mathf.RoundToInt(amount), Hud.NumKind.Player);
            Hud.Hurt(0.8f);
            Cam.Shake(0.4f);
            Sfx.Play("hurt", 0.9f);
            PostFX.I?.Chroma(0.35f);
            PostFX.I?.Flash(new Color(1, 0.2f, 0.2f), 0.18f);
            Fx.HitStop(0.06f);
            Fx.Sparks(Player.Pos + Vector3.up, new Color(1, 0.4f, 0.4f), 12);
            if (Combo >= 5) Hud.Toast($"{Combo} コンボ終了");
            Combo = 0;
        }

        public void PerfectDodge()
        {
            Perfects++;
            Player.BuffT = 5f;
            Player.Energy = Mathf.Min(100, Player.Energy + 15);
            Fx.Slow(0.22f, 0.9f);
            Hud.Perfect();
            Hud.Banner("ジャスト回避！", "5秒間 会心率アップ", new Color(0.6f, 0.95f, 1f), 1.0f);
            Sfx.Play("perfect", 1f);
            PostFX.I?.Chroma(0.25f);
            PostFX.I?.Radial(0.25f);
            Fx.AirRing(Player.Pos + Vector3.up, 3, new Color(0.6f, 0.95f, 1f), 0.5f);
            Fx.Stars(Player.Pos + Vector3.up, new Color(0.6f, 0.95f, 1f), 12);
            Cam.FovPunch(-6);
        }

        public void OnBreak()
        {
            Hud.Banner("BREAK!!", "理論武装 崩壊！　5秒間ダメージ1.5倍", Mat.Gold, 1.6f);
            Sfx.Play("break", 1f);
            Fx.Slow(0.2f, 0.6f);
            PostFX.I?.Radial(0.6f);
            PostFX.I?.Flash(new Color(1, 0.9f, 0.5f), 0.4f);
            Cam.Shake(0.5f);
            Fx.Stars(Boss.HeadPos, Mat.Gold, 30);
            Fx.Sparks(Boss.Pos + Vector3.up * 2.5f, Mat.Gold, 40, 1.6f);
            Fx.Ring(Boss.Pos, 8, Mat.Gold, 0.5f, 3f);
            Say("な……私の理論が……！", 2f);
        }

        public void OnPhase2()
        {
            Hud.Banner("本気モード", "安東先生の攻撃が激しくなった！", new Color(1f, 0.5f, 0.85f), 2f);
            Sky.SetStorm(1);
            Music.SetBattle(true);
            Sfx.Play("roar", 1f);
            Sfx.Play("thunder", 0.8f);
            Cam.Shake(0.7f);
            PostFX.I?.Flash(new Color(0.8f, 0.5f, 1f), 0.6f, 2f);
            PostFX.I?.Radial(0.5f);
            for (int i = 0; i < 6; i++)
            {
                int k = i;
                Fx.Later(0.15f * i, () =>
                {
                    float a = k / 6f * Mathf.PI * 2;
                    Fx.Bolt(Boss.Pos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 6, new Color(1f, 0.5f, 0.85f), 0.3f);
                });
            }
        }

        // ---- 元素爆発 ----
        public void StartBurst()
        {
            Cinematic = true;
            Hud.CutIn();
            Sfx.Play("cutin", 1f);
            Sfx.Play("charge", 0.8f);
            Music.Duck(true);
            Fx.Slow(0.35f, 1.0f);
            PostFX.I?.Radial(0.5f);
            PostFX.I?.Bloom(0.8f);
            var P = Player;
            var fwd = new Vector3(Mathf.Sin(P.Face), 0, Mathf.Cos(P.Face));
            var right = new Vector3(fwd.z, 0, -fwd.x);
            Cam.Cinematic(P.Pos + fwd * 3.2f + right * 1.1f + Vector3.up * 0.9f, P.Pos + Vector3.up * 1.9f, 38);
            Fx.Ring(P.Pos, 5, Mat.Electro, 0.8f, 3f);
            Fx.Sparks(P.Pos + Vector3.up, Mat.ElectroLight, 30, 1.2f);
        }

        public void FireBurst()
        {
            Cinematic = false;
            Cam.EndCinematic();
            Music.Duck(false);
            Hud.Letterbox(false);
            Sfx.Play("burstHit", 0.7f);
            PostFX.I?.Flash(Color.white, 0.6f);
            Sky.Flash(1f);
            for (int i = 0; i < 6; i++)
            {
                Fx.Later(0.1f + i * 0.16f, () =>
                {
                    if (!Boss.Alive || State != Mode.Battle) return;
                    var p = Boss.Pos + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
                    Fx.Bolt(p, Mat.Electro, 0.55f);
                    Fx.Ring(Boss.Pos, 4.5f, Mat.Electro, 0.3f);
                    Cam.Shake(0.3f);
                    Sfx.Play("thunder", 0.7f, 1.1f);
                    PostFX.I?.Chroma(0.15f);
                    DamageBoss(120, HitKind.Burst, Boss.Pos + Vector3.up * 3f);
                });
            }
            // とどめの一撃
            Fx.Later(1.25f, () =>
            {
                if (!Boss.Alive || State != Mode.Battle) return;
                for (int k = 0; k < 4; k++) Fx.Bolt(Boss.Pos + Random.insideUnitSphere * 1.5f, k % 2 == 0 ? Color.white : Mat.Electro, 0.9f, 34);
                Fx.Ring(Boss.Pos, 11, Mat.Electro, 0.6f, 3f);
                Fx.Ring(Boss.Pos, 7, Color.white, 0.4f, 3f);
                Fx.Sparks(Boss.Pos + Vector3.up * 2, Mat.ElectroLight, 60, 2f);
                Fx.Glow(Boss.Pos + Vector3.up * 2, Color.white, 4f);
                Sfx.Play("burstHit", 1f, 0.85f);
                PostFX.I?.Flash(Color.white, 0.7f);
                PostFX.I?.Radial(0.8f);
                Cam.Shake(0.8f);
                Cam.FovPunch(8);
                Fx.HitStop(0.12f);
                DamageBoss(320, HitKind.Burst, Boss.Pos + Vector3.up * 3.6f);
            });
        }

        // ---- 元素エネルギーの玉 ----
        public void SpawnOrbs(Vector3 from, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("orb");
                go.transform.position = from;
                var o = go.AddComponent<Orb>();
                o.Init(Random.onUnitSphere * Random.Range(5f, 9f) + Vector3.up * 5);
            }
        }

        public void CollectOrb()
        {
            bool wasReady = Player.Energy >= 100;
            Player.Energy = Mathf.Min(100, Player.Energy + 6);
            Sfx.Play("orb", 0.6f, 1 + Player.Energy / 200f);
            Fx.Sparks(Player.Pos + Vector3.up, Mat.ElectroLight, 6, 0.5f);
            _ = wasReady;
        }

        // ================= 決着 =================
        void Win()
        {
            if (State != Mode.Battle) return;
            State = Mode.Ending; stateT = 0; win = true;
            Boss.Die();
            Player.Victory = true;
            Cinematic = true;
            Hud.Letterbox(true);
            Say("……いいでしょう。単位を認めます。", 4.5f);
            int bonus = 1000 + Mathf.CeilToInt(TimeLeft) * 20 + Mathf.RoundToInt(Player.Hp);
            Hud.Banner("撃破！", $"撃破ボーナス +{bonus}", Mat.Gold, 2.6f);
            Fx.Slow(0.15f, 1.4f);
            Fx.HitStop(0.2f);
            PostFX.I?.Flash(Color.white, 1f, 1.5f);
            PostFX.I?.Radial(1f);
            Cam.Shake(1f);
            Music.StopAll();
            Music.CutNow();
            Sfx.Play("burstHit", 1f, 0.7f);
            Sfx.Play("fanfare", 0.9f);
            Sky.SetStorm(0);
            for (int i = 0; i < 6; i++)
            {
                int k = i;
                Fx.Later(0.2f + k * 0.28f, () =>
                {
                    Fx.Sparks(Boss.Pos + Vector3.up * Random.Range(1f, 4f), k % 2 == 0 ? Mat.Gold : Mat.Electro, 30, 1.5f);
                    Fx.Glow(Boss.Pos + Vector3.up * 2.5f + Random.insideUnitSphere, k % 2 == 0 ? Mat.Gold : Mat.Electro, 2f);
                    Fx.Ring(Boss.Pos, 6 + k, k % 2 == 0 ? Mat.Gold : Mat.Electro, 0.5f);
                    Sfx.Play("boom", 0.5f, 1 + k * 0.05f);
                });
            }
            Fx.Later(1.2f, () => { Fx.Confetti(Boss.Pos + Vector3.up * 1, 250); Fx.Confetti(Player.Pos, 150); });
            Fx.Later(2.5f, () => Fx.Confetti(Player.Pos + Vector3.up, 200));
        }

        void Lose(string reason)
        {
            if (State != Mode.Battle) return;
            State = Mode.Ending; stateT = 0; win = false; loseReason = reason;
            Cinematic = true;
            Hud.Letterbox(true);
            Say(reason == "時間切れ…" ? "時間です。答案を回収します。" : "来年また会いましょう。", 3.5f);
            Hud.Banner(reason, "", new Color(0.75f, 0.8f, 1f), 2.2f);
            Fx.Slow(0.3f, 1.2f);
            PostFX.I?.Desaturate(0.75f);
            Music.StopAll();
            if (reason == "時間切れ…") Player.Victory = false;
        }

        public void OnPlayerDown() => Lose("力尽きた…");

        void ShowResult()
        {
            State = Mode.Result; stateT = 0;
            Hud.Letterbox(false);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var d = new Hud.ResultData
            {
                win = win,
                reason = loseReason,
                dealt = Dealt,
                killBonus = win ? 1000 : 0,
                timeBonus = win ? Mathf.CeilToInt(Mathf.Max(0, TimeLeft)) * 20 : 0,
                hpBonus = win ? Mathf.RoundToInt(Player.Hp) : 0,
                perfects = Perfects,
                perfectBonus = Perfects * 100,
                maxCombo = MaxCombo,
                comboBonus = MaxCombo * 5,
            };
            d.total = d.dealt + d.killBonus + d.timeBonus + d.hpBonus + d.perfectBonus + d.comboBonus;
            d.grade = !win ? "不可" : d.total >= 7000 ? "秀" : d.total >= 6200 ? "優" : d.total >= 5400 ? "良" : "可";
            int best = PlayerPrefs.GetInt(BestKey, 0);
            d.record = d.total > best;
            if (d.record) { PlayerPrefs.SetInt(BestKey, d.total); PlayerPrefs.Save(); }
            d.best = Mathf.Max(best, d.total);
            Hud.ShowResult(d);
            Music.Mix(0, 0, win ? 0.6f : 0.35f, 0, 0);
            if (!win) Music.SetMuffled(true);
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && State == Mode.Battle && !paused) SetPause(true);
        }
    }

    // 敵から出てきて主人公に吸い込まれる元素エネルギーの玉
    public class Orb : MonoBehaviour
    {
        Vector3 vel; float t;
        public void Init(Vector3 v)
        {
            vel = v;
            var q = Mat.Part(transform, Mat.Quad, Mat.FxShared(new Color(0.8f, 0.6f, 1f, 1f), Mat.Glow, true, 3f), Vector3.zero, Vector3.one * 0.7f, default, false);
            q.AddComponent<Billboard>();
            var tr = gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.25f; tr.widthMultiplier = 0.25f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Mat.Electro, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            tr.colorGradient = g;
            tr.sharedMaterial = Mat.FxShared(Color.white, Mat.White, true, 2f);
        }

        void Update()
        {
            var G = Game.I;
            if (G == null) return;
            float dt = Time.deltaTime;
            t += dt;
            var target = G.Player.Pos + Vector3.up * 1.2f;
            if (t < 0.35f) vel *= 1 - dt * 4;
            else
            {
                var to = target - transform.position;
                vel = Vector3.Lerp(vel, to.normalized * (14 + t * 20), dt * 6);
            }
            transform.position += vel * dt;
            if ((target - transform.position).magnitude < 0.7f || t > 3f)
            {
                if (G.State == Game.Mode.Battle) G.CollectOrb();
                Destroy(gameObject);
            }
        }
    }
}
