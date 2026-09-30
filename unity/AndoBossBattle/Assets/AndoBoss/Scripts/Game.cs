using UnityEngine;

namespace AndoBoss
{
    // ゲーム全体の進行：タイトル → 登場演出 → 戦闘 → 決着 → 結果
    public partial class Game : MonoBehaviour
    {
        public static Game I;
        public enum Mode { Title, Select, Intro, Battle, Ending, Result, Settings }
        public enum HitKind { Normal, Skill, Burst }

        public Mode State { get; private set; }
        public Player[] Party;
        int active;
        public int Active => active;
        public Player Player => Party[active];
        // ボスは2人ぶん作っておき、選んだほうだけ出す（ダブルのときは両方）
        public Boss[] Bosses;
        Boss target;
        // いま狙っている先生（ダブルのときはプレイヤーに近いほう）
        public Boss Boss => target;
        public CameraRig Cam;
        public Hud Hud;
        public Music Music;

        public const float TimeLimit = 210f;
        public float TimeLeft;
        public int Dealt, Combo, MaxCombo, Perfects, Reactions;
        // 菅原先生のカウント（5つで単位消滅）
        public const int MaxCount = 5;
        public int Counts;
        public static int BossKind; // 0 = 安東先生, 1 = 菅原先生, 2 = ダブル（超ハード）
        public static bool IsDouble => BossKind == 2;
        public const float DoubleTimeLimit = 270f;
        int KillBonus => IsDouble ? 3000 : 1000;
        int selectStep;             // 0 = ボス選択, 1 = キャラ選択
        // パーティ共通のHP・スタミナ・デバフ
        public const float MaxStam = 150f;
        public float MaxStamina => MaxStam * Player.Def.StamMul; // キャラによって多い（ともき）
        public float PartyHp = 1000f, PartyStam = MaxStam, StamDelay, SlowT;
        public int StartChar;
        public bool ReviveUsed;
        float drinkT;
        // 難しさの調整：ボスの攻撃の強さ・こちらの攻撃の強さ
        public float EnemyDmgMul = 1.4f; // 難易度で変わる（ResetRound で設定）
        public const float PlayerDmgMul = 1.1f; // 全員の攻撃力（前は 0.85）
        float comboT, stateT, titleOrbit, swapCd;
        bool paused;
        public bool Cinematic;
        bool win; string loseReason;
        bool energyAnnounced;
        bool raitoLow; float raitoCd;
        string BestKey => BossKind == 2 ? "double_boss_best" : BossKind == 1 ? "suga_boss_best" : "ando_boss_best";

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
            Difficulty = Mathf.Clamp(PlayerPrefs.GetInt("ando_diff", 1), 0, DiffNames.Length - 1);
            Binds.EnsureLoaded();
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
            Bosses = new Boss[2];
            for (int i = 0; i < 2; i++)
            {
                var b = new GameObject(i == 0 ? "Boss_Ando" : "Boss_Suga").AddComponent<Boss>();
                b.Kind = i;
                b.Build();
                Bosses[i] = b;
            }
            target = Bosses[0];

            ResetRound();
            GoTitle();
            ShotTour.StartIfRequested(this);
        }

        // ================= 場面の切り替え =================
        void ResetRound()
        {
            Fx.ClearAll();
            foreach (var o in FindObjectsByType<Orb>(FindObjectsSortMode.None)) Destroy(o.gameObject);
            EnemyDmgMul = DiffDmg[Difficulty];
            PartyHp = Player.MaxHp; PartyStam = MaxStam; StamDelay = 0; SlowT = 0; ReviveUsed = false; drinkT = Random.Range(12f, 17f);
            foreach (var d in FindObjectsByType<Drink>(FindObjectsSortMode.None)) Destroy(d.gameObject);
            foreach (var p in Party) { p.ResetState(); p.gameObject.SetActive(false); }
            active = StartChar;
            Party[active].gameObject.SetActive(true);
            PartyHp = Player.MaxHp;
            PartyStam = MaxStamina;
            swapCd = 0;
            ApplyBossSetup();
            TimeLeft = IsDouble ? DoubleTimeLimit : TimeLimit;
            Dealt = Combo = MaxCombo = Perfects = Reactions = 0;
            comboT = 0;
            energyAnnounced = false;
            raitoLow = false; raitoCd = 0;
            Cinematic = false;
            Sky.SetStorm(StormFor(BossKind));
            Counts = 0;
            if (PostFX.I) { PostFX.I.Desaturate(0); PostFX.I.SetTint(Color.white); PostFX.I.LowHp = 0; }
            Music.Duck(false);
            Music.SetMuffled(false);
            Music.SetPitch(PitchFor(BossKind));
        }

        static float StormFor(int k) => k == 2 ? 0.6f : k == 1 ? 0.35f : 0;
        static float PitchFor(int k) => k == 2 ? 0.97f : k == 1 ? 0.94f : 1f;

        // 選んだ先生だけ出す。ダブルのときは左右に並べる
        void ApplyBossSetup()
        {
            for (int i = 0; i < Bosses.Length; i++)
            {
                var b = Bosses[i];
                b.gameObject.SetActive(IsDouble || i == BossKind);
                b.HomePos = IsDouble ? new Vector3(i == 0 ? -5.5f : 5.5f, 0, 8) : new Vector3(0, 0, 7);
                b.ResetState();
            }
            target = IsDouble ? Bosses[0] : Bosses[BossKind];
        }

        public Boss OtherBoss(Boss b)
        {
            if (!IsDouble) return null;
            return b == Bosses[0] ? Bosses[1] : Bosses[0];
        }

        // 画面の中心にしたい位置（ダブルのときは2人の真ん中）
        Vector3 BossFocus => IsDouble ? (Bosses[0].Pos + Bosses[1].Pos) * 0.5f : Boss.Pos;

        void TickBosses(float dt)
        {
            foreach (var b in Bosses) if (b.gameObject.activeSelf) b.Tick(dt);
            if (!IsDouble) return;
            // 2人が重ならないように押しのけ合う
            Boss a = Bosses[0], c = Bosses[1];
            if (a.Alive && c.Alive)
            {
                var d = Player.Flat(c.Pos - a.Pos);
                float m = d.magnitude;
                if (m < 4.5f)
                {
                    var push = (m > 0.01f ? d / m : Vector3.right) * (4.5f - m) * 0.5f;
                    a.Pos -= push; c.Pos += push;
                }
            }
            if (State == Mode.Battle) PickTarget();
        }

        // プレイヤーに近いほうを狙う（ちらつかないよう、今の相手を少しひいきする）
        void PickTarget()
        {
            Boss best = null; float bd = float.MaxValue;
            foreach (var b in Bosses)
            {
                if (!b.gameObject.activeSelf || !b.Alive) continue;
                float d = Player.Flat(b.Pos - Player.Pos).magnitude - (b == target ? 2.5f : 0);
                if (d < bd) { bd = d; best = b; }
            }
            if (best != null) target = best;
        }

        internal void GoTitle()
        {
            ResetRound();
            State = Mode.Title; stateT = 0;
            Hud.ShowTitle(PlayerPrefs.GetInt(BestKey, 0));
            Music.SetTitle();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            foreach (var b in Bosses) b.Face = Mathf.PI;
        }

        // ---- キャラ選択 ----
        internal void GoSelect()
        {
            State = Mode.Select; stateT = 0;
            selectStep = 0;
            Sfx.Play("confirm", 0.8f);
            Hud.ShowBossSelect(BossKind);
        }

        // ボスを選んだあと、キャラ選択へ
        internal void GoCharSelect()
        {
            selectStep = 1; stateT = 0;
            Sfx.Play("confirm", 0.8f);
            for (int i = 0; i < Party.Length; i++)
            {
                var p = Party[i];
                p.gameObject.SetActive(true);
                p.ResetState();
                p.Pos = new Vector3((i - (Party.Length - 1) / 2f) * 2.2f, 0, -9);
                p.Face = Mathf.PI;
            }
            Hud.ShowSelect(StartChar);
        }

        void UpdateSelect()
        {
            if (selectStep == 0) { UpdateBossSelect(); return; }
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
            Cam.Cinematic(new Vector3(sel.Pos.x * 0.6f, 0, sel.Pos.z) + new Vector3(0.6f, 1.25f, -5.6f), new Vector3(sel.Pos.x * 0.6f, 0, sel.Pos.z) + Vector3.up * 0.35f, 42, stateT < 0.05f); // カードに隠れないよう、キャラを画面の上半分に
            for (int i = 0; i < Party.Length; i++)
            {
                var p = Party[i];
                p.Face = Player.TurnTo(p.Face, i == StartChar ? Mathf.PI + 0.15f : Mathf.PI, Time.deltaTime * 6);
                p.Tick(Time.deltaTime);
            }
            TickBosses(Time.deltaTime);
            if (stateT > 0.4f && GameInput.Down(GameInput.K.Confirm)) GoIntro();
            if (GameInput.Down(GameInput.K.Title) || GameInput.Down(GameInput.K.Back)) GoSelect();
        }

        internal void SelectBoss(int k)
        {
            BossKind = k;
            ApplyBossSetup();
            foreach (var b in Bosses) b.Face = Mathf.PI;
            Sky.SetStorm(StormFor(BossKind));
            Music.SetPitch(PitchFor(BossKind));
            Hud.ShowBossSelect(BossKind);
        }

        void UpdateBossSelect()
        {
            int before = BossKind;
            if (GameInput.Down(GameInput.K.Left)) BossKind = (BossKind + 2) % 3;
            if (GameInput.Down(GameInput.K.Right)) BossKind = (BossKind + 1) % 3;
            if (GameInput.Down(GameInput.K.Char1)) BossKind = 0;
            if (GameInput.Down(GameInput.K.Char2)) BossKind = 1;
            if (GameInput.Down(GameInput.K.Char3)) BossKind = 2;
            // ↑↓ で難易度
            if (GameInput.Down(GameInput.K.Up) || GameInput.Down(GameInput.K.Down))
            {
                SetDifficulty(Difficulty + (GameInput.Down(GameInput.K.Up) ? 1 : -1));
                Sfx.Play("tick", 0.7f, 1f + Difficulty * 0.1f);
            }
            if (before != BossKind)
            {
                Sfx.Play("swap", 0.6f, 0.9f);
                SelectBoss(BossKind);
            }
            var bp = BossFocus;
            float back = IsDouble ? 1.45f : 1f;
            Cam.Cinematic(bp + new Vector3(2.6f, 3.4f, -13.5f) * back, bp + Vector3.up * 3.3f, 40, stateT < 0.05f);
            TickBosses(Time.deltaTime);
            TickParty(Time.deltaTime);
            if (stateT > 0.4f && GameInput.Down(GameInput.K.Confirm)) GoCharSelect();
            if (GameInput.Down(GameInput.K.Title) || GameInput.Down(GameInput.K.Back)) GoTitle();
        }

        internal void GoIntro()
        {
            ResetRound();
            State = Mode.Intro; stateT = 0;
            Hud.ShowBattle();
            Hud.ShowIntro();
            Sfx.Play("confirm", 0.8f);
            Sfx.Play("roar", 0.6f, 1.2f);
            Music.Mix(0, 0.9f, 0.9f, 0, 0.6f);
            if (IsDouble)
            {
                Say("……今日は菅原先生と二人がかりだど。", 1.6f, "安東先生");
                Fx.Later(1.6f, () => { if (State == Mode.Intro) Say("カウント、たっぷりあげるよ。", 1.8f, "菅原先生"); });
            }
            else Say(Boss.Line("intro"), 2.8f);
            LockCursor();
        }

        internal void GoBattle()
        {
            State = Mode.Battle; stateT = 0;
            Cam.EndCinematic();
            Cam.SnapBehindPlayer();
            if (Player.Def.Weapon == Weapon.Spear) Say("ぼくそんなんじゃないよ〜", 2f, Player.Def.Name);
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
            GameInput.UpdateDevice();
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
                case Mode.Settings: UpdateSettings(); break;
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
            if (StamDelay > 0) StamDelay -= dt; else PartyStam = Mathf.Min(MaxStamina, PartyStam + dt * 55 * Player.Def.StamMul);
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
            Cam.Cinematic(c, BossFocus + Vector3.up * 3.2f + new Vector3(Mathf.Cos(titleOrbit), 0, -Mathf.Sin(titleOrbit)) * 4, 50, stateT < 0.1f);
            TickParty(Time.deltaTime);
            TickBosses(Time.deltaTime);
            if (stateT > 0.5f && GameInput.Down(GameInput.K.Settings)) { GoSettings(); return; }
            if (stateT > 0.5f && GameInput.Down(GameInput.K.Confirm)) GoSelect();
        }

        void UpdateIntro()
        {
            float t = stateT;
            var F = BossFocus; var P = Player;
            float far = IsDouble ? 2f : 1f;
            if (t < 1.8f)
            {
                float u = t / 1.8f;
                var from = F + new Vector3(2.5f, 1.2f, -5.5f) * far;
                var to = F + new Vector3(1.2f, 3.8f, -4.2f) * far;
                Cam.Cinematic(Vector3.Lerp(from, to, u), F + Vector3.up * (3.6f + u * 0.6f), 42, t < 0.05f);
                if (t > 0.9f && t - Time.unscaledDeltaTime <= 0.9f) { Sfx.Play("thunder", 0.6f); Fx.Bolt(F + new Vector3(4, 0, 2) * far, Mat.Electro); Cam.Shake(0.3f); }
            }
            else
            {
                var behind = P.Pos + new Vector3(0, 2.6f, -6) * (IsDouble ? 1.3f : 1f);
                Cam.Cinematic(behind, (P.Pos + F) * 0.5f + Vector3.up * 2f, 55);
            }
            TickParty(Time.deltaTime);
            TickBosses(Time.deltaTime);
            if (t > 3.3f || (t > 0.8f && GameInput.Down(GameInput.K.Confirm))) GoBattle();
        }

        void UpdateBattle(float dt)
        {
            if (Cursor.lockState != CursorLockMode.Locked && GameInput.Down(GameInput.K.Attack)) LockCursor();
            TimeLeft -= dt;
            swapCd -= dt;
            // キャラ交代はなし（最初に選んだキャラで最後まで戦う）
            TickParty(dt);
            TickBosses(dt);

            comboT -= dt;
            if (comboT <= 0 && Combo > 0) Combo = 0;

            if (PostFX.I) PostFX.I.LowHp = PartyHp < Player.MaxHp * 0.3f ? 1 - PartyHp / (Player.MaxHp * 0.3f) * 0.6f : 0;
            if (Player.Energy >= 100 && !energyAnnounced)
            {
                energyAnnounced = true;
                Sfx.Play("ready", 0.7f);
                Hud.Toast($"{Player.Def.Name}の奥義 準備完了！　{GameInput.ShortLabel(GameInput.K.Burst)} で発動");
            }
            if (Player.Energy < 100) energyAnnounced = false;

            // エナジードリンクがときどき空から落ちてくる
            drinkT -= dt;
            if (drinkT <= 0)
            {
                drinkT = Random.Range(14f, 20f);
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
            TickBosses(dt);
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
        public void Say(string text, float sec = 2.6f, string speaker = null)
        {
            speaker ??= Boss.Name;
            bool raspy = speaker == "菅原先生";
            Hud.Say(text, sec, speaker, raspy);
            // 菅原先生の声はかすかす（ささやくような声の効果音）
            if (raspy) Sfx.Play("whisper" + Random.Range(0, 3), 0.9f, 1f, 0.06f);
        }

        // 菅原先生の攻撃に当たるとカウントがたまる。5つで単位消滅
        public void AddCount(int n)
        {
            if (State != Mode.Battle || BossKind == 0) return;
            Counts = Mathf.Min(MaxCount, Counts + n);
            Hud.CountPop();
            Sfx.Play("stamp", 1f, 0.7f);
            Sfx.Play("count", 0.9f);
            Cam.Shake(0.4f);
            PostFX.I?.Flash(new Color(0.8f, 0.1f, 0.2f), 0.3f);
            if (Counts >= MaxCount)
            {
                Hud.Banner("カウント5", "単位消滅", new Color(1f, 0.25f, 0.3f), 2.4f);
                Say(Bosses[1].Line("count5"), 3.5f, "菅原先生");
                Lose("単位消滅…");
                return;
            }
            Hud.Banner($"カウント {Counts}", Counts == MaxCount - 1 ? "リーチ！あと1つで単位消滅" : $"あと{MaxCount - Counts}つで単位消滅", new Color(1f, 0.3f, 0.35f), 1.1f);
            string[] says = { "", "はい、カウント1。", "カウント2。", "あと2つで単位なくなるよ？", "……リーチだね。" };
            Say(says[Counts], 2.2f, "菅原先生");
        }

        public void OnHakai()
        {
            Hud.Banner("必殺「北の破壊神」", "「正」の字の壁が落ちてくる！ 画のすき間にもぐりこめ！", new Color(0.7f, 0.4f, 1f), 1.8f);
            Sky.SetStorm(1);
            Sky.Flash(1f);
            Cam.Shake(0.6f);
            Sfx.Play("roar", 0.9f, 0.7f);
        }

        // light = 多段ヒットの細かい1発（ヒットストップと効果音を軽くする）
        public void DamageBoss(float baseDmg, HitKind kind, Vector3 hitPos, Elem elem = Elem.None, bool light = false)
        {
            var B = Boss; var P = Player;
            if (State != Mode.Battle || !B.Alive) return;
            float critRate = (P.BuffT > 0 ? 0.6f : 0.18f) + (P.Def.Weapon == Weapon.Spear ? 0.15f : 0f); // らいとの能力「天才」
            bool crit = Random.value < critRate;
            float mul = PlayerDmgMul * (crit ? 1.7f : 1f) * (B.Broken ? 1.3f : 1f) * (B.DefDownT > 0 ? 1.3f : 1f) * (B.ProvenT > 0 ? 1.5f : 1f) * Random.Range(0.9f, 1.1f);

            // 属性コンボ
            var react = Elements.React(B.Aura, elem);
            if (react.name != null)
            {
                mul *= react.mul;
                B.Aura = Elem.None; B.AuraT = 0;
                Reactions++;
                Hud.WorldText(hitPos + Vector3.up * 1.2f, react.name, react.color, 1.1f);
                Sfx.Play(react.name == "過電流" ? "explode" : react.name == "放電嵐" || react.name == "超電導" ? "thunder" : react.name == "吹雪" || react.name == "融解" ? "ice" : "fire", 0.9f);
                if (react.name == "過電流") { Fx.Explosion(hitPos, react.color, 1.5f); Cam.Shake(0.4f); }
                else if (react.name == "放電嵐") { Fx.Ring(B.Pos, 6, react.color, 0.4f, 3f); Fx.Sparks(hitPos, react.color, 30, 1.4f); Fx.Bolt(B.Pos, react.color, 0.3f, 10f); }
                else if (react.name == "超電導") { Fx.Ring(B.Pos, 7, react.color, 0.45f, 3f); Fx.Bolt(B.Pos, react.color, 0.3f, 12f); Fx.Stars(hitPos, react.color, 10); }
                else if (react.name == "吹雪") { Fx.Ring(B.Pos, 6, Color.white, 0.4f, 3f); Fx.Sparks(hitPos, react.color, 40, 1.6f); Fx.Stars(hitPos, Color.white, 8); }
                else if (react.name == "融解") { Fx.Glow(hitPos, react.color, 2.4f); Fx.Sparks(hitPos, Color.white, 24, 1.2f); Cam.Shake(0.3f); }
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
                P.Energy = Mathf.Min(100, P.Energy + 1.5f);
                if (Random.value < 0.2f) SpawnOrbs(hitPos, 1);
            }
            if (B.Hp <= 0)
            {
                var O = OtherBoss(B);
                if (O != null && O.Alive) BossDown(B, O); else Win();
            }
        }

        // ダブルで1人目を倒したとき。残った先生は本気モードになる
        void BossDown(Boss b, Boss rest)
        {
            b.Die();
            Hud.Banner($"{b.Name} 撃破！", $"残るは{rest.Name}！", Mat.Gold, 2.2f);
            Say(b.IsSuga ? "……ノーカウントには、ならないか。" : "……あどは頼むど、菅原先生。", 2.4f, b.Name);
            Fx.Later(2.4f, () => { if (State == Mode.Battle) Say(rest.IsSuga ? "……よくも。カウント、倍にしてあげる。" : "菅原先生の分まで、やっでやる！", 2.4f, rest.Name); });
            // 残った先生は、少し間をおいて本気モードになる
            Fx.Later(3.0f, () => { if (State == Mode.Battle && rest.Alive && rest.Phase == 1) rest.PendingPhase = true; });
            Sfx.Play("break", 1f);
            Sfx.Play("boom", 0.8f);
            Fx.Slow(0.2f, 0.8f);
            Fx.HitStop(0.15f);
            PostFX.I?.Flash(Color.white, 0.5f);
            PostFX.I?.Radial(0.6f);
            Cam.Shake(0.8f);
            Fx.Ring(b.Pos, 8, Mat.Gold, 0.5f, 3f);
            Fx.Sparks(b.Pos + Vector3.up * 2.5f, Mat.Gold, 40, 1.6f);
            Fx.Later(0.5f, () => Fx.Confetti(b.Pos + Vector3.up, 120));
            PickTarget();
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
            // らいとは、やられるとキレる。HPが減ると友だちを呼ぶ
            if (Player.Def.Weapon == Weapon.Spear && State == Mode.Battle)
            {
                if (!raitoLow && PartyHp < Player.MaxHp * 0.3f && PartyHp > 0) { raitoLow = true; Say("えいじぃ〜", 2.2f, Player.Def.Name); return; }
                if (Time.time > raitoCd && Random.value < 0.5f) { raitoCd = Time.time + 9f; Say("おまえふざけんなよぉぉぉ", 2f, Player.Def.Name); return; }
            }
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
            bool canceled = BossKind != 0 && Counts > 0;
            if (canceled)
            {
                // ジャスト回避でカウントを1つ取り消せる
                Counts--;
                Hud.CountPop();
                Fx.Later(0.6f, () => Hud.Banner("カウント取り消し！", $"残りカウント {Counts}", new Color(0.6f, 0.95f, 1f), 1.0f));
                Say("……今のは、ノーカウント。", 1.8f, "菅原先生");
            }
            if (Player.Def.Weapon == Weapon.Spear && !canceled) Fx.Later(0.5f, () => Say("ぼくてんさいだから！", 1.6f, Player.Def.Name));
            Player.BuffT = 5f;
            Player.Energy = Mathf.Min(100, Player.Energy + 20);
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

        public void OnBreak(Boss b)
        {
            Hud.Banner("BREAK!!", "理論武装 崩壊！　5秒間ダメージ1.3倍", Mat.Gold, 1.6f);
            Sfx.Play("break", 1f);
            Fx.Slow(0.2f, 0.6f);
            PostFX.I?.Radial(0.6f);
            PostFX.I?.Flash(new Color(1, 0.9f, 0.5f), 0.4f);
            Cam.Shake(0.5f);
            Fx.Stars(b.HeadPos, Mat.Gold, 30);
            Fx.Sparks(b.Pos + Vector3.up * 2.5f, Mat.Gold, 40, 1.6f);
            Fx.Ring(b.Pos, 8, Mat.Gold, 0.5f, 3f);
            Say(b.Line("break"), 2f, b.Name);
        }

        public void OnPhase2(Boss b)
        {
            Hud.Banner("本気モード", b.IsSuga ? "菅原先生の攻撃が激しくなった！「北の破壊神」に注意" : "安東先生の攻撃が激しくなった！", new Color(1f, 0.5f, 0.85f), 2f);
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
                    Fx.Bolt(b.Pos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 6, new Color(1f, 0.5f, 0.85f), 0.3f);
                });
            }
        }

        public void OnSansou()
        {
            Hud.Banner("必殺「三相交流」", "3枚の波の壁のすき間にもぐりこめ！（ジャンプでは越えられない）", new Color(1f, 0.4f, 0.35f), 1.6f);
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
                case 3: Skills.RaitoBurst(p); break;
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
                Party[i].Energy = Mathf.Min(100, Party[i].Energy + (i == active ? 6 : 3));
            Sfx.Play("orb", 0.5f, 0.9f + Player.Energy / 300f);
            Fx.Sparks(Player.Pos + Vector3.up, Player.Def.ElemColor, 6, 0.5f);
        }

        // ================= 決着 =================
        internal void Win()
        {
            if (State != Mode.Battle) return;
            State = Mode.Ending; stateT = 0; win = true;
            Boss.Die();
            Player.Victory = true;
            Cinematic = true;
            Hud.Letterbox(true);
            Say(Boss.Line("win"), 4.5f);
            int bonus = KillBonus + Mathf.CeilToInt(TimeLeft) * 20 + Mathf.RoundToInt(PartyHp);
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
            if (reason != "単位消滅…") Say(reason == "時間切れ…" ? Boss.Line("timeup") : Boss.Line("lose"), 3.5f);
            Hud.Banner(reason, "", new Color(0.75f, 0.8f, 1f), 2.2f);
            Fx.Slow(0.3f, 1.2f);
            PostFX.I?.Desaturate(0.75f);
            Music.StopAll();
        }

        // やられたとき。やましょうのパッシブ「留年」が残っていれば1回だけ生き返る
        public void OnPlayerDown()
        {
            if (!ReviveUsed && Player.Def.Passive.StartsWith("留年")) { Revive(); return; }
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

        internal void ShowResult()
        {
            State = Mode.Result; stateT = 0;
            Hud.Letterbox(false);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var d = new Hud.ResultData
            {
                win = win,
                reason = loseReason,
                dealt = Dealt,
                killBonus = win ? KillBonus : 0,
                timeBonus = win ? Mathf.CeilToInt(Mathf.Max(0, TimeLeft)) * 20 : 0,
                hpBonus = win ? Mathf.RoundToInt(PartyHp) : 0,
                perfects = Perfects,
                perfectBonus = Perfects * 100,
                maxCombo = MaxCombo,
                comboBonus = MaxCombo * 5,
                reactions = Reactions,
                charName = Player.Def.Name,
            };
            d.total = d.dealt + d.killBonus + d.timeBonus + d.hpBonus + d.perfectBonus + d.comboBonus + d.reactions * 20;
            // 難易度で点数に倍率がかかる（やさしい ×0.7 〜 鬼 ×1.5）
            d.total = Mathf.RoundToInt(d.total * DiffScore[Difficulty]);
            d.charName = $"{Player.Def.Name}（{DiffNames[Difficulty]}）";
            // ダブルは与ダメージの上限が 22000（+6000）、撃破ボーナスが +2000 なので基準も上げる
            int off = IsDouble ? 8000 : 0;
            d.grade = !win ? "不可" : d.total >= 19500 + off ? "秀" : d.total >= 18700 + off ? "優" : d.total >= 18000 + off ? "良" : "可";
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
        public const float Heal = 400f;
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
                G.PartyHp = Mathf.Min(G.Player.MaxHp, G.PartyHp + Heal);
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
