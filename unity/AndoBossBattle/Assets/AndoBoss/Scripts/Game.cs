using UnityEngine;

namespace AndoBoss
{
    // ゲーム全体の進行：タイトル → 登場演出 → 戦闘 → 決着 → 結果
    public class Game : MonoBehaviour
    {
        public static Game I;
        public enum Mode { Title, Select, Intro, Battle, Ending, Result }
        public enum HitKind { Normal, Skill, Burst }

        public Mode State { get; private set; }
        public Player[] Party;
        int active;
        public int Active => active;
        public Player Player => Party[active];
        public Boss Boss;
        public CameraRig Cam;
        public Hud Hud;
        public Music Music;

        public const float TimeLimit = 210f;
        public float TimeLeft;
        public int Dealt, Combo, MaxCombo, Perfects, Reactions;
        // パーティ共通のHP・スタミナ・デバフ
        public const float MaxStam = 150f;
        public float PartyHp = Player.MaxHp, PartyStam = MaxStam, StamDelay, SlowT;
        public int StartChar;
        public bool ReviveUsed;
        float drinkT;
        // 難しさの調整：ボスの攻撃の強さ・こちらの攻撃の強さ
        public float EnemyDmgMul = 1.4f;
        public const float PlayerDmgMul = 0.85f;
        float comboT, stateT, titleOrbit, swapCd;
        bool paused;
        public bool Cinematic;
        bool win; string loseReason;
        bool energyAnnounced;
        const string BestKey = "ando_boss_best";

        // 画風：0 = アニメ調, 1 = リアル調（F3 で切り替え）
        public static int Style = 0;
        float realism = 0;

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

            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) l.gameObject.SetActive(false);
            foreach (var a in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) a.enabled = false;

            Style = PlayerPrefs.GetInt("ando_style2", 0);
            PostFX.Level = PlayerPrefs.GetInt("ando_light", 1);
            realism = Style;
            Shader.SetGlobalFloat("_AndoRealism", realism);

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

            Party = new Player[CharDef.All.Length];
            for (int i = 0; i < Party.Length; i++)
            {
                Party[i] = new GameObject("Player_" + CharDef.All[i].Name).AddComponent<Player>();
                Party[i].Build(CharDef.All[i]);
            }
            Boss = new GameObject("Boss").AddComponent<Boss>();
            Boss.Build();

            ResetRound();
            GoTitle();
        }

        // ================= 場面の切り替え =================
        void ResetRound()
        {
            Fx.ClearAll();
            foreach (var o in FindObjectsByType<Orb>(FindObjectsSortMode.None)) Destroy(o.gameObject);
            PartyHp = Player.MaxHp; PartyStam = MaxStam; StamDelay = 0; SlowT = 0; ReviveUsed = false; drinkT = Random.Range(15f, 22f);
            foreach (var d in FindObjectsByType<Drink>(FindObjectsSortMode.None)) Destroy(d.gameObject);
            foreach (var p in Party) { p.ResetState(); p.gameObject.SetActive(false); }
            active = StartChar;
            Party[active].gameObject.SetActive(true);
            swapCd = 0;
            Boss.ResetState();
            TimeLeft = TimeLimit;
            Dealt = Combo = MaxCombo = Perfects = Reactions = 0;
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

        // ---- キャラ選択 ----
        void GoSelect()
        {
            State = Mode.Select; stateT = 0;
            Sfx.Play("confirm", 0.8f);
            for (int i = 0; i < Party.Length; i++)
            {
                var p = Party[i];
                p.gameObject.SetActive(true);
                p.ResetState();
                p.Pos = new Vector3((i - 1) * 2.4f, 0, -9);
                p.Face = Mathf.PI;
            }
            Hud.ShowSelect(StartChar);
        }

        void UpdateSelect()
        {
            int before = StartChar;
            if (GameInput.Down(GameInput.K.Left)) StartChar = (StartChar + Party.Length - 1) % Party.Length;
            if (GameInput.Down(GameInput.K.Right)) StartChar = (StartChar + 1) % Party.Length;
            for (int i = 0; i < Party.Length; i++) if (GameInput.Down(GameInput.K.Char1 + i)) StartChar = i;
            if (before != StartChar)
            {
                Sfx.Play("swap", 0.6f, 1.2f);
                Party[StartChar].SwapInT = 0.35f;
                Hud.ShowSelect(StartChar);
            }
            var sel = Party[StartChar];
            Cam.Cinematic(sel.Pos + new Vector3(0.8f, 1.9f, -4.2f), sel.Pos + Vector3.up * 1.3f, 40, stateT < 0.05f);
            for (int i = 0; i < Party.Length; i++)
            {
                var p = Party[i];
                p.Face = Player.TurnTo(p.Face, i == StartChar ? Mathf.PI + 0.15f : Mathf.PI, Time.deltaTime * 6);
                p.Tick(Time.deltaTime);
            }
            Boss.Tick(Time.deltaTime);
            if (stateT > 0.4f && GameInput.Down(GameInput.K.Confirm)) GoIntro();
            if (GameInput.Down(GameInput.K.Title)) GoTitle();
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
            Say("……おめ、おれがら単位取るつもりだが？", 2.8f);
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
            PostFX.I?.Flash(Color.white, 0.4f);
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

            realism = Mathf.MoveTowards(realism, Style, Time.unscaledDeltaTime * 3);
            Shader.SetGlobalFloat("_AndoRealism", realism);

            if (GameInput.Down(GameInput.K.Mute))
            {
                Music.Muted = !Music.Muted;
                Sfx.Volume = Music.Muted ? 0 : 0.9f;
                Hud.Toast(Music.Muted ? "音：オフ" : "音：オン");
            }
            if (GameInput.Down(GameInput.K.Light))
            {
                PostFX.Level = (PostFX.Level + 1) % 3;
                PlayerPrefs.SetInt("ando_light", PostFX.Level);
                Hud.Toast(PostFX.LevelNames[PostFX.Level]);
            }
            if (GameInput.Down(GameInput.K.Style))
            {
                Style = 1 - Style;
                PlayerPrefs.SetInt("ando_style2", Style);
                Hud.Toast(Style == 1 ? "画風：リアル調" : "画風：アニメ調");
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
                case Mode.Select: UpdateSelect(); break;
                case Mode.Intro: UpdateIntro(); break;
                case Mode.Battle: UpdateBattle(dt); break;
                case Mode.Ending: UpdateEnding(dt); break;
                case Mode.Result: UpdateResult(); break;
            }
        }

        void LateUpdate() => Cam.Tick();

        void SetPause(bool on)
        {
            paused = on;
            Fx.I.Paused = on;
            Hud.Pause(on);
            Music.SetMuffled(on);
            if (on) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        void TickParty(float dt)
        {
            if (StamDelay > 0) StamDelay -= dt; else PartyStam = Mathf.Min(MaxStam, PartyStam + dt * 40);
            SlowT = Mathf.Max(0, SlowT - dt);
            for (int i = 0; i < Party.Length; i++)
            {
                if (i == active) Party[i].Tick(dt);
                else Party[i].TickOffField(dt);
            }
        }

        void UpdateTitle()
        {
            titleOrbit += Time.unscaledDeltaTime * 0.12f;
            var c = new Vector3(Mathf.Sin(titleOrbit) * 15, 5 + Mathf.Sin(titleOrbit * 0.7f) * 1.5f, Mathf.Cos(titleOrbit) * 15);
            Cam.Cinematic(c, Boss.Pos + Vector3.up * 3.2f + new Vector3(Mathf.Cos(titleOrbit), 0, -Mathf.Sin(titleOrbit)) * 4, 50, stateT < 0.1f);
            TickParty(Time.deltaTime);
            Boss.Tick(Time.deltaTime);
            if (stateT > 0.5f && GameInput.Down(GameInput.K.Confirm)) GoSelect();
        }

        void UpdateIntro()
        {
            float t = stateT;
            var B = Boss; var P = Player;
            if (t < 1.8f)
            {
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
            TickParty(Time.deltaTime);
            Boss.Tick(Time.deltaTime);
            if (t > 3.3f || (t > 0.8f && GameInput.Down(GameInput.K.Confirm))) GoBattle();
        }

        void UpdateBattle(float dt)
        {
            if (Cursor.lockState != CursorLockMode.Locked && GameInput.Down(GameInput.K.Attack)) LockCursor();
            TimeLeft -= dt;
            swapCd -= dt;
            for (int i = 0; i < Party.Length; i++)
                if (GameInput.Down(GameInput.K.Char1 + i)) SwapTo(i);
            TickParty(dt);
            Boss.Tick(dt);

            comboT -= dt;
            if (comboT <= 0 && Combo > 0) Combo = 0;

            if (PostFX.I) PostFX.I.LowHp = PartyHp < Player.MaxHp * 0.3f ? 1 - PartyHp / (Player.MaxHp * 0.3f) * 0.6f : 0;
            if (Player.Energy >= 100 && !energyAnnounced)
            {
                energyAnnounced = true;
                Sfx.Play("ready", 0.7f);
                Hud.Toast($"{Player.Def.Name}の奥義 準備完了！　Q で発動");
            }
            if (Player.Energy < 100) energyAnnounced = false;

            // エナジードリンクがときどき空から落ちてくる
            drinkT -= dt;
            if (drinkT <= 0)
            {
                drinkT = Random.Range(18f, 26f);
                if (FindObjectsByType<Drink>(FindObjectsSortMode.None).Length < 2) Drink.Spawn();
            }

            if (TimeLeft <= 0 && State == Mode.Battle) Lose("時間切れ…");
        }

        // キャラ交代（原神と同じく 1・2・3 キー）
        void SwapTo(int i)
        {
            if (i == active || swapCd > 0 || Player.BurstT > 0 || Player.Dead) return;
            var from = Player;
            var to = Party[i];
            swapCd = 1.0f;
            from.gameObject.SetActive(false);
            active = i;
            to.gameObject.SetActive(true);
            to.EnterField(from.Pos, from.Face);
            energyAnnounced = to.Energy >= 100;
            var ec = to.Def.ElemColor;
            Sfx.Play("swap", 0.9f);
            Fx.Ring(to.Pos, 3.5f, ec, 0.35f);
            Fx.Sparks(to.Pos + Vector3.up, ec, 20, 1f);
            Fx.Glow(to.Pos + Vector3.up, ec, 1.2f);
            Hud.OnSwap(to.Def);
        }

        void UpdateEnding(float dt)
        {
            TickParty(dt);
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
        public void Say(string text, float sec = 2.6f, string speaker = "安東先生") => Hud.Say(text, sec, speaker);

        // light = 多段ヒットの細かい1発（ヒットストップと効果音を軽くする）
        public void DamageBoss(float baseDmg, HitKind kind, Vector3 hitPos, Elem elem = Elem.None, bool light = false)
        {
            var B = Boss; var P = Player;
            if (State != Mode.Battle || !B.Alive) return;
            float critRate = P.BuffT > 0 ? 0.6f : 0.18f;
            bool crit = Random.value < critRate;
            float mul = PlayerDmgMul * (crit ? 1.7f : 1f) * (B.Broken ? 1.3f : 1f) * Random.Range(0.9f, 1.1f);

            // 属性コンボ
            var react = Elements.React(B.Aura, elem);
            if (react.name != null)
            {
                mul *= react.mul;
                B.Aura = Elem.None; B.AuraT = 0;
                Reactions++;
                Hud.WorldText(hitPos + Vector3.up * 1.2f, react.name, react.color, 1.1f);
                Sfx.Play(react.name == "過電流" ? "explode" : react.name == "放電嵐" ? "thunder" : "fire", 0.9f);
                if (react.name == "過電流") { Fx.Explosion(hitPos, react.color, 1.5f); Cam.Shake(0.4f); }
                else if (react.name == "放電嵐") { Fx.Ring(B.Pos, 6, react.color, 0.4f, 3f); Fx.Sparks(hitPos, react.color, 30, 1.4f); Fx.Bolt(B.Pos, react.color, 0.3f, 10f); }
                else { Fx.Glow(hitPos, react.color, 2f); Fx.Embers(hitPos, react.color, 20); }
                PostFX.I?.Chroma(0.15f);
            }
            else if (elem != Elem.None) { B.Aura = elem; B.AuraT = 7f; }

            int dmg = Mathf.Max(1, Mathf.RoundToInt(baseDmg * mul));
            if (react.name != null && react.extra > 0) dmg += Mathf.RoundToInt(baseDmg * react.extra * (1 + Random.value * 0.2f));
            dmg = Mathf.Min(dmg, Mathf.CeilToInt(B.Hp));
            B.Hp -= dmg;
            Dealt += dmg;
            float tough = dmg * (kind == HitKind.Skill ? 1.4f : kind == HitKind.Burst ? 0.8f : 0.9f) * (react.name != null ? react.tough : 1);
            B.OnDamaged(dmg, tough);

            var ec = elem != Elem.None ? Elements.Color(elem) : P.Def.ElemColor;
            var numKind = kind == HitKind.Burst ? Hud.NumKind.Burst : crit ? Hud.NumKind.Crit : B.Broken ? Hud.NumKind.Break : Hud.NumKind.Normal;
            Hud.Number(hitPos + Vector3.up * 0.5f, dmg, numKind, react.name != null ? react.color : ec);
            Hud.BossBarShake();
            if (!light)
            {
                Fx.Sparks(hitPos, crit ? Mat.Gold : Color.Lerp(ec, Color.white, 0.4f), crit ? 22 : 12, crit ? 1.4f : 1f);
                Fx.Glow(hitPos, crit ? Mat.Gold : ec, crit ? 1.1f : 0.7f);
                if (crit) { Fx.AirRing(hitPos, 2.2f, Mat.Gold, 0.25f); Fx.Stars(hitPos, Mat.Gold, 4); }
                Sfx.Play(crit ? "crit" : "hit", crit ? 0.9f : 0.75f, crit ? 1f : Random.Range(0.92f, 1.08f));
                if (P.Def.Elem == Elem.Electro) Sfx.Play("zap", 0.2f);
                Fx.HitStop(kind == HitKind.Burst ? 0.05f : crit ? 0.075f : 0.04f);
                Cam.Shake(crit ? 0.22f : 0.1f);
                if (crit) PostFX.I?.Chroma(0.12f);
            }
            else Fx.HitStop(0.012f);

            Combo++; comboT = 2.6f;
            MaxCombo = Mathf.Max(MaxCombo, Combo);
            Hud.Combo(Combo);

            if (kind == HitKind.Normal)
            {
                P.Energy = Mathf.Min(100, P.Energy + 1);
                if (Random.value < 0.2f) SpawnOrbs(hitPos, 1);
            }
            if (B.Hp <= 0) Win();
        }

        public void OnPlayerHurt(float amount)
        {
            Hud.Number(Player.Pos + Vector3.up * 2.2f, Mathf.RoundToInt(amount), Hud.NumKind.Player, Color.red);
            Hud.Hurt(0.8f);
            Cam.Shake(0.4f);
            Sfx.Play("hurt", 0.9f);
            PostFX.I?.Chroma(0.3f);
            PostFX.I?.Flash(new Color(1, 0.2f, 0.2f), 0.15f);
            Fx.HitStop(0.06f);
            Fx.Sparks(Player.Pos + Vector3.up, new Color(1, 0.4f, 0.4f), 12);
            if (Combo >= 5) Hud.Toast($"{Combo} コンボ終了");
            Combo = 0;
            Boss.OnHitPlayer();
        }

        public void ApplySlow(float sec)
        {
            bool was = SlowT > 0;
            SlowT = Mathf.Max(SlowT, sec);
            if (!was)
            {
                Sfx.Play("slowdown", 0.8f);
                Hud.Toast("鈍足！ 移動速度が下がった");
                Fx.Ring(Player.Pos, 2.5f, new Color(0.4f, 1f, 0.6f), 0.4f);
            }
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
            Hud.Banner("BREAK!!", "理論武装 崩壊！　5秒間ダメージ1.3倍", Mat.Gold, 1.6f);
            Sfx.Play("break", 1f);
            Fx.Slow(0.2f, 0.6f);
            PostFX.I?.Radial(0.6f);
            PostFX.I?.Flash(new Color(1, 0.9f, 0.5f), 0.4f);
            Cam.Shake(0.5f);
            Fx.Stars(Boss.HeadPos, Mat.Gold, 30);
            Fx.Sparks(Boss.Pos + Vector3.up * 2.5f, Mat.Gold, 40, 1.6f);
            Fx.Ring(Boss.Pos, 8, Mat.Gold, 0.5f, 3f);
            Say("な……おれの理論が……！", 2f);
        }

        public void OnPhase2()
        {
            Hud.Banner("本気モード", "安東先生の攻撃が激しくなった！", new Color(1f, 0.5f, 0.85f), 2f);
            Sky.SetStorm(1);
            Music.SetBattle(true);
            Sfx.Play("roar", 1f);
            Sfx.Play("thunder", 0.8f);
            Cam.Shake(0.7f);
            PostFX.I?.Flash(new Color(0.8f, 0.5f, 1f), 0.5f, 2f);
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

        public void OnSansou()
        {
            Hud.Banner("必殺「三相交流」", "3本の波のすき間をぬってよけろ！", new Color(1f, 0.4f, 0.35f), 1.6f);
            Cam.Shake(0.4f);
            PostFX.I?.Radial(0.3f);
        }

        // ---- 奥義 ----
        public void StartBurst(Player p)
        {
            Cinematic = true;
            Hud.CutIn(p.Def);
            Say(p.Def.BurstShout, 1.8f, p.Def.Name);
            Sfx.Play("cutin", 1f);
            Sfx.Play("charge", 0.8f);
            Music.Duck(true);
            Fx.Slow(0.35f, 1.0f);
            PostFX.I?.Radial(0.5f);
            var fwd = p.Forward;
            var right = new Vector3(fwd.z, 0, -fwd.x);
            Cam.Cinematic(p.Pos + fwd * 3.2f + right * 1.1f + Vector3.up * 0.9f, p.Pos + Vector3.up * 1.8f, 38);
            Fx.Ring(p.Pos, 5, p.Def.ElemColor, 0.8f, 3f);
            Fx.Sparks(p.Pos + Vector3.up, p.Def.ElemColor, 30, 1.2f);
        }

        public void FireBurst(Player p)
        {
            Cinematic = false;
            Cam.EndCinematic();
            Music.Duck(false);
            Hud.Letterbox(false);
            Sfx.Play("burstHit", 0.6f);
            PostFX.I?.Flash(Color.white, 0.4f);
            Sky.Flash(0.6f);
            switch (p.Def.Id)
            {
                case 0: Skills.TomokiBurst(p); break;
                case 1: Skills.SugiyamaBurst(p); break;
                default: Skills.YamashouBurst(p); break;
            }
        }

        // ---- やる気の玉 ----
        public void SpawnOrbs(Vector3 from, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("orb");
                go.transform.position = from;
                go.AddComponent<Orb>().Init(Random.onUnitSphere * Random.Range(5f, 9f) + Vector3.up * 5, Player.Def.ElemColor);
            }
        }

        public void CollectOrb()
        {
            // 出ているキャラは多め、控えのキャラも少したまる
            for (int i = 0; i < Party.Length; i++)
                Party[i].Energy = Mathf.Min(100, Party[i].Energy + (i == active ? 4 : 2));
            Sfx.Play("orb", 0.5f, 0.9f + Player.Energy / 300f);
            Fx.Sparks(Player.Pos + Vector3.up, Player.Def.ElemColor, 6, 0.5f);
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
            Say("……しかたねな。単位、認めるべ。", 4.5f);
            int bonus = 1000 + Mathf.CeilToInt(TimeLeft) * 20 + Mathf.RoundToInt(PartyHp);
            Hud.Banner("撃破！", $"撃破ボーナス +{bonus}", Mat.Gold, 2.6f);
            Fx.Slow(0.15f, 1.4f);
            Fx.HitStop(0.2f);
            PostFX.I?.Flash(Color.white, 0.8f, 1.5f);
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
            Say(reason == "時間切れ…" ? "時間だ。答案、回収するど。" : "へば、また来年な。", 3.5f);
            Hud.Banner(reason, "", new Color(0.75f, 0.8f, 1f), 2.2f);
            Fx.Slow(0.3f, 1.2f);
            PostFX.I?.Desaturate(0.75f);
            Music.StopAll();
        }

        // やられたとき。やましょうのパッシブ「留年」が残っていれば1回だけ生き返る
        public void OnPlayerDown()
        {
            if (!ReviveUsed) { Revive(); return; }
            Lose("力尽きた…");
        }

        void Revive()
        {
            ReviveUsed = true;
            var from = Player;
            const int yama = 2;
            foreach (var p in Party) { p.Dead = false; p.DeadT = 0; }
            PartyHp = Player.MaxHp * 0.5f;
            if (active != yama)
            {
                from.gameObject.SetActive(false);
                active = yama;
                Party[yama].gameObject.SetActive(true);
                Party[yama].EnterField(new Vector3(from.Pos.x, 0, from.Pos.z), from.Face);
                Hud.OnSwap(Party[yama].Def);
            }
            var y = Player;
            y.Inv = 3f;
            y.LockT = 0.8f;
            Hud.Banner("留年！", "やましょうの能力：1回だけ生き返る（もう1年がんばる）", Mat.Gold, 2f);
            Say("……留年したから、もう1年いける。", 2.2f, y.Def.Name);
            Fx.Slow(0.25f, 1.2f);
            PostFX.I?.Flash(new Color(0.7f, 1f, 0.8f), 0.6f, 2f);
            PostFX.I?.Radial(0.6f);
            Fx.Ring(y.Pos, 8, y.Def.ElemColor, 0.8f, 3f);
            Fx.Stars(y.Pos + Vector3.up, Mat.Gold, 20);
            Fx.Sparks(y.Pos + Vector3.up, y.Def.ElemColor, 40, 1.5f);
            Sfx.Play("perfect", 1f);
            Sfx.Play("ready", 0.8f);
            Cam.Shake(0.5f);
            // 生き返った勢いで、極太の矢を2本撃ち返す
            Fx.Later(0.9f, () => { if (State == Mode.Battle) Skills.RyunenShot(Player); });
        }

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
                hpBonus = win ? Mathf.RoundToInt(PartyHp) : 0,
                perfects = Perfects,
                perfectBonus = Perfects * 100,
                maxCombo = MaxCombo,
                comboBonus = MaxCombo * 5,
                reactions = Reactions,
            };
            d.total = d.dealt + d.killBonus + d.timeBonus + d.hpBonus + d.perfectBonus + d.comboBonus + d.reactions * 20;
            d.grade = !win ? "不可" : d.total >= 19500 ? "秀" : d.total >= 18700 ? "優" : d.total >= 18000 ? "良" : "可";
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

    // 空から落ちてくるエナジードリンク。拾うと体力回復
    public class Drink : MonoBehaviour
    {
        public const float Heal = 250f;
        float t, y = 14;
        Vector3 pos;
        Transform can;
        GameObject marker;

        public static void Spawn()
        {
            var G = Game.I;
            Vector3 p = Vector3.zero;
            for (int tries = 0; tries < 20; tries++)
            {
                float a = Random.value * Mathf.PI * 2, r = Random.Range(3f, World.ArenaR - 3);
                p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                if (Player.Flat(p - G.Boss.Pos).magnitude > 5) break;
            }
            var go = new GameObject("drink");
            go.AddComponent<Drink>().Init(p);
            Sfx.Play("whoosh", 0.5f, 0.6f);
            G.Hud.Toast("エナジードリンクが落ちてきた！（拾うと回復）");
        }

        void Init(Vector3 p)
        {
            pos = p;
            can = new GameObject("can").transform;
            can.SetParent(transform, false);
            // 缶：本体・上下のふち・ラベルの帯・光
            Mat.Part(can, Mat.Frustum(0.28f, 0.28f, 0.8f, 16), Mat.ToonShared(new Color(0.15f, 0.2f, 0.25f), 0.02f), Vector3.zero, Vector3.one);
            Mat.Part(can, Mat.Frustum(0.285f, 0.285f, 0.3f, 16), Mat.ToonShared(new Color(0.3f, 1f, 0.4f), 0.02f), Vector3.zero, Vector3.one);
            Mat.Part(can, Mat.Frustum(0.25f, 0.22f, 0.08f, 16), Mat.ToonShared(new Color(0.8f, 0.82f, 0.85f)), new Vector3(0, 0.44f, 0), Vector3.one);
            Mat.Part(can, Mat.Frustum(0.22f, 0.25f, 0.08f, 16), Mat.ToonShared(new Color(0.8f, 0.82f, 0.85f)), new Vector3(0, -0.44f, 0), Vector3.one);
            var glow = Mat.Part(can, Mat.Quad, Mat.FxShared(new Color(0.4f, 1f, 0.5f, 0.6f), Mat.Glow, true, 2f), Vector3.zero, Vector3.one * 2.2f, default, false);
            glow.AddComponent<Billboard>();
            marker = Mat.Part(transform, Mat.Disc(32), Mat.FxShared(new Color(0.4f, 1f, 0.5f, 0.6f), Mat.RingTex, true, 2f), new Vector3(p.x, 0.06f, p.z), Vector3.one * 1.6f, default, false);
            transform.position = Vector3.zero;
            can.position = new Vector3(p.x, y, p.z);
        }

        void Update()
        {
            var G = Game.I;
            if (G == null || G.State != Game.Mode.Battle) { if (G != null && G.State != Game.Mode.Ending) Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            t += dt;
            if (y > 0.7f) y = Mathf.Max(0.7f, y - dt * 12);
            float bob = y <= 0.7f ? Mathf.Sin(t * 3) * 0.15f : 0;
            can.position = new Vector3(pos.x, y + bob, pos.z);
            can.rotation = Quaternion.Euler(0, t * 120, y > 0.7f ? t * 400 : 0);
            marker.transform.localScale = Vector3.one * (1.6f + Mathf.Sin(t * 4) * 0.15f);
            var P = G.Player;
            if (y <= 0.8f && Player.Flat(P.Pos - pos).magnitude < 1.4f && P.Pos.y < 1.5f && !P.Dead)
            {
                float before = G.PartyHp;
                G.PartyHp = Mathf.Min(Player.MaxHp, G.PartyHp + Heal);
                int healed = Mathf.RoundToInt(G.PartyHp - before);
                G.Hud.Number(P.Pos + Vector3.up * 2.2f, healed, Hud.NumKind.Heal, new Color(0.4f, 1f, 0.5f));
                G.Hud.Toast("エナドリで回復！　元気100倍");
                Sfx.Play("drink", 0.9f);
                Fx.Sparks(P.Pos + Vector3.up, new Color(0.4f, 1f, 0.5f), 20, 1f);
                Fx.Ring(P.Pos, 3f, new Color(0.4f, 1f, 0.5f), 0.4f);
                Destroy(gameObject);
                return;
            }
            if (t > 14f) Destroy(gameObject);
        }
    }

    // 敵から出てきて主人公に吸い込まれるやる気の玉
    public class Orb : MonoBehaviour
    {
        Vector3 vel; float t;
        public void Init(Vector3 v, Color c)
        {
            vel = v;
            var q = Mat.Part(transform, Mat.Quad, Mat.FxShared(new Color(c.r, c.g, c.b, 1f), Mat.Glow, true, 3f), Vector3.zero, Vector3.one * 0.7f, default, false);
            q.AddComponent<Billboard>();
            var tr = gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.25f; tr.widthMultiplier = 0.25f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(c, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
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
