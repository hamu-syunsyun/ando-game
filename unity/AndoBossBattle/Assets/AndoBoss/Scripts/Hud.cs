using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AndoBoss
{
    // 画面のUI。uGUI をコードだけで組み立てる
    public class Hud : MonoBehaviour
    {
        Font uiFont, bigFont;
        Canvas canvas;
        RectTransform root, battle, title, result, intro, pause, cutin;

        // 戦闘UI
        RectTransform bossFill, bossLag, toughFill, playerFill, playerLag;
        Image bossFillImg, toughFillImg, playerFillImg;
        Text bossName, toughLabel, timerText, dmgText, playerHpText, hintText;
        Image stamRing, stamBg; CanvasGroup stamGroup;
        Image skillCdImg, burstRing, burstGlow, burstIcon, skillIcon; Text skillCdText, burstLabel;
        RectTransform burstRt, skillRt;
        Text comboNum, comboLabel, comboRate; RectTransform comboRt; CanvasGroup comboGroup;
        float comboPop, comboFade;
        Text subName, subText; CanvasGroup subGroup; float subT;
        Text bannerText, bannerSub; RectTransform bannerRt; CanvasGroup bannerGroup; float bannerT = 99, bannerDur;
        Image bannerFlash;
        Text toastText; CanvasGroup toastGroup; float toastT = 99;
        Image hurtEdge, perfectEdge; float hurtA, perfectA;
        Image letterTop, letterBot; float letter, letterTarget;
        float bossBarShake;
        Text playerName, skillLabel, burstLabelName, auraText, slowText;
        Image auraDot;
        readonly List<(Text name, Image dot, Image ready, RectTransform rt)> partyRows = new List<(Text, Image, Image, RectTransform)>();
        float swapFlash;
        CanvasGroup battleGroup;

        // タイトル
        Text titleSmall, titleBig, titleSub, titlePrompt, titleBest; RectTransform titleBlock; float titleT;
        // 登場カード
        Text introSmall, introBig, introSub; RectTransform introLine; CanvasGroup introGroup; float introT = 99;
        // カットイン
        RectTransform cutBand; Text cutSmall, cutBig; CanvasGroup cutGroup; float cutT = 99;
        // リザルト
        Text resTitle, resTotal, resGrade, resStamp, resPrompt, resRecord; RectTransform resGradeRt, resStampRt; Image resGradeRing;
        readonly List<Text> resRows = new List<Text>();
        CanvasGroup resGroup;
        public struct ResultData { public bool win; public string reason; public int dealt, killBonus, timeBonus, hpBonus, perfectBonus, comboBonus, total, best, maxCombo, perfects, reactions; public string grade; public bool record; }
        ResultData res; float resT; bool resActive; int shownTotal;

        // ダメージ数字
        class Num { public Text t; public Vector3 world; public float age, life; public Vector2 drift; public float scale; public bool active; }
        readonly List<Num> nums = new List<Num>();

        public void Build()
        {
            uiFont = Resources.Load<Font>("Fonts/MPLUS1p-ExtraBold");
            bigFont = Resources.Load<Font>("Fonts/DelaGothicOne-Regular");
            if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (bigFont == null) bigFont = uiFont;

            var cgo = new GameObject("HUD");
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root = cgo.GetComponent<RectTransform>();

            // 画面ふちの赤（被弾）・水色（ジャスト回避）
            var edgeTex = Mat.MakeTex(128, (x, y) =>
            {
                float dx = Mathf.Abs(x * 2 - 1), dy = Mathf.Abs(y * 2 - 1);
                float d = Mathf.Max(Mathf.Pow(dx, 4), Mathf.Pow(dy, 4));
                return new Color(1, 1, 1, Mathf.Clamp01(d * 1.3f));
            });
            var edgeSprite = Sprite.Create(edgeTex, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
            hurtEdge = Img(Full(root, "hurt"), new Color(1, 0.1f, 0.15f, 0), edgeSprite);
            perfectEdge = Img(Full(root, "perfect"), new Color(0.5f, 0.9f, 1, 0), edgeSprite);
            hurtEdge.raycastTarget = perfectEdge.raycastTarget = false;

            battle = Full(root, "battle");
            battleGroup = battle.gameObject.AddComponent<CanvasGroup>();
            BuildBattle();
            cutin = Full(root, "cutin");
            BuildCutin();
            intro = Full(root, "intro");
            BuildIntro();
            title = Full(root, "title");
            BuildTitle();
            result = Full(root, "result");
            BuildResult();
            pause = Full(root, "pause");
            Img(pause, new Color(0, 0, 0, 0.55f));
            Label(pause, "一時停止中", bigFont, 90, Color.white, new Vector2(0, 60), TextAnchor.MiddleCenter, 1000);
            Label(pause, "クリックで再開　／　T：タイトルへ　／　M：音のオン・オフ", uiFont, 34, new Color(1, 1, 1, 0.9f), new Vector2(0, -50), TextAnchor.MiddleCenter, 1400);
            pause.gameObject.SetActive(false);

            // バナー・テロップ（いちばん上）
            var bgo = New(root, "banner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1600, 300));
            bannerRt = bgo;
            bannerGroup = bgo.gameObject.AddComponent<CanvasGroup>();
            bannerFlash = Img(New(bgo, "flash", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 260)), new Color(1, 1, 1, 0), Mat.GlowSprite);
            bannerText = Label(bgo, "", bigFont, 130, Color.white, new Vector2(0, 20), TextAnchor.MiddleCenter, 1600, 200);
            Shadowed(bannerText, new Color(0.15f, 0.05f, 0.3f), 4);
            bannerSub = Label(bgo, "", uiFont, 40, Mat.Gold, new Vector2(0, -85), TextAnchor.MiddleCenter, 1600);
            Shadowed(bannerSub, new Color(0, 0, 0, 0.8f), 2);
            bannerGroup.alpha = 0;
            var tgo = New(root, "toast", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(800, 60));
            toastGroup = tgo.gameObject.AddComponent<CanvasGroup>();
            Img(tgo, new Color(0, 0, 0, 0.5f), Mat.RoundSprite).type = Image.Type.Sliced;
            toastText = Label(tgo, "", uiFont, 30, Color.white, Vector2.zero, TextAnchor.MiddleCenter, 780);
            toastGroup.alpha = 0;

            ShowOnly(title);
        }

        // ---------------- 部品づくり ----------------
        static RectTransform New(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }
        static RectTransform Full(Transform parent, string name)
        {
            var rt = New(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return rt;
        }
        static Image Img(RectTransform rt, Color c, Sprite s = null)
        {
            var i = rt.gameObject.AddComponent<Image>();
            i.sprite = s; i.color = c; i.raycastTarget = false;
            return i;
        }
        Text Label(RectTransform parent, string s, Font f, int size, Color c, Vector2 pos, TextAnchor align, float w = 800, float h = 0)
        {
            var rt = New(parent, "txt", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(w, h > 0 ? h : size * 1.6f));
            var t = rt.gameObject.AddComponent<Text>();
            t.font = f; t.fontSize = size; t.color = c; t.text = s; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
        static void Shadowed(Text t, Color c, float d)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = c; o.effectDistance = new Vector2(d, -d);
        }
        static RectTransform Bar(RectTransform parent, Vector2 pos, Vector2 size, Color bg, out RectTransform lag, Color lagC, out RectTransform fill, out Image fillImg, Color fillC, Vector2? anchor = null)
        {
            var a = anchor ?? new Vector2(0.5f, 0.5f);
            var b = New(parent, "bar", a, a, pos, size);
            Img(b, new Color(0, 0, 0, 0.6f), Mat.RoundSprite).type = Image.Type.Sliced;
            var inner = New(b, "inner", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6, -6));
            Img(inner, bg);
            lag = New(inner, "lag", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
            Img(lag, lagC);
            fill = New(inner, "fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
            fillImg = Img(fill, fillC);
            // 光沢
            var gloss = New(fill, "gloss", new Vector2(0, 0.55f), Vector2.one, Vector2.zero, Vector2.zero);
            Img(gloss, new Color(1, 1, 1, 0.22f));
            return b;
        }
        static void SetFill(RectTransform r, float v)
        {
            r.anchorMax = new Vector2(Mathf.Clamp01(v), 1);
            r.offsetMax = Vector2.zero; r.offsetMin = Vector2.zero;
        }

        // ---------------- 戦闘UI ----------------
        void BuildBattle()
        {
            var top = new Vector2(0.5f, 1);
            bossName = Label(battle, "電気回路担当・安東先生", uiFont, 34, Color.white, Vector2.zero, TextAnchor.MiddleCenter, 900);
            Anchor(bossName.rectTransform, top, new Vector2(0, -42));
            Shadowed(bossName, new Color(0, 0, 0, 0.8f), 2);
            var lv = Label(battle, "Lv.90", uiFont, 22, Mat.Gold, Vector2.zero, TextAnchor.MiddleLeft, 200);
            Anchor(lv.rectTransform, top, new Vector2(-520, -46));
            var bb = Bar(battle, new Vector2(0, -80), new Vector2(960, 26), new Color(0.15f, 0.1f, 0.15f, 0.9f), out bossLag, new Color(1, 0.95f, 0.85f), out bossFill, out bossFillImg, new Color(0.93f, 0.25f, 0.3f), top);
            // 50% の目印
            Img(New(bb, "mark", new Vector2(0.5f, 0), new Vector2(0.5f, 1), Vector2.zero, new Vector2(3, 6)), new Color(1, 1, 1, 0.8f));
            Bar(battle, new Vector2(0, -102), new Vector2(760, 12), new Color(0.1f, 0.08f, 0.15f, 0.9f), out var tl, new Color(0, 0, 0, 0), out toughFill, out toughFillImg, new Color(0.75f, 0.6f, 1f), top);
            tl.gameObject.SetActive(false);
            toughLabel = Label(battle, "理論武装", uiFont, 18, new Color(0.85f, 0.8f, 1f), Vector2.zero, TextAnchor.MiddleRight, 200);
            Anchor(toughLabel.rectTransform, top, new Vector2(-490, -102));

            var tr = new Vector2(1, 1);
            var tlabel = Label(battle, "残り時間", uiFont, 22, new Color(1, 1, 1, 0.8f), Vector2.zero, TextAnchor.MiddleRight, 300);
            Anchor(tlabel.rectTransform, tr, new Vector2(-190, -40));
            timerText = Label(battle, "3:00", bigFont, 56, Color.white, Vector2.zero, TextAnchor.MiddleRight, 300);
            Anchor(timerText.rectTransform, tr, new Vector2(-190, -85));
            Shadowed(timerText, new Color(0, 0, 0, 0.7f), 3);
            dmgText = Label(battle, "与ダメージ 0", uiFont, 26, Mat.Gold, Vector2.zero, TextAnchor.MiddleRight, 400);
            Anchor(dmgText.rectTransform, tr, new Vector2(-190, -130));
            Shadowed(dmgText, new Color(0, 0, 0, 0.7f), 2);

            // プレイヤーHP
            var bc = new Vector2(0.5f, 0);
            var pn = Label(battle, "ともき　Lv.90", uiFont, 24, Color.white, Vector2.zero, TextAnchor.MiddleLeft, 400);
            Anchor(pn.rectTransform, bc, new Vector2(-110, 98));
            Shadowed(pn, new Color(0, 0, 0, 0.8f), 2);
            playerName = pn;
            slowText = Label(battle, "", uiFont, 24, new Color(0.5f, 1f, 0.65f), Vector2.zero, TextAnchor.MiddleCenter, 400);
            Anchor(slowText.rectTransform, bc, new Vector2(0, 130));
            Shadowed(slowText, new Color(0, 0, 0, 0.8f), 2);

            // パーティ（右側。1・2・3 キーで交代）
            for (int i = 0; i < CharDef.All.Length; i++)
            {
                var d = CharDef.All[i];
                var row = New(battle, "party" + i, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-150, -20 - i * 70), new Vector2(260, 60));
                Img(row, new Color(0.05f, 0.04f, 0.12f, 0.6f), Mat.RoundSprite).type = Image.Type.Sliced;
                var ready = Img(New(row, "ready", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(70, 70)), new Color(1, 1, 1, 0), Mat.GlowSprite);
                var dot = Img(New(row, "dot", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(36, 36)), d.ElemColor, Mat.CircleSprite);
                Label(dot.rectTransform, Elements.Kanji(d.Elem), uiFont, 20, new Color(0.1f, 0.05f, 0.15f), Vector2.zero, TextAnchor.MiddleCenter, 40);
                var nm = Label(row, d.Name, uiFont, 26, Color.white, new Vector2(40, 0), TextAnchor.MiddleLeft, 170);
                Shadowed(nm, new Color(0, 0, 0, 0.8f), 1);
                var key = New(row, "key", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(32, 30));
                Img(key, new Color(1, 1, 1, 0.9f), Mat.RoundSprite).type = Image.Type.Sliced;
                Label(key, (i + 1).ToString(), uiFont, 20, new Color(0.15f, 0.1f, 0.25f), Vector2.zero, TextAnchor.MiddleCenter, 32);
                partyRows.Add((nm, dot, ready, row));
            }
            playerHpText = Label(battle, "1000 / 1000", uiFont, 24, Color.white, Vector2.zero, TextAnchor.MiddleRight, 400);
            Anchor(playerHpText.rectTransform, bc, new Vector2(110, 98));
            Shadowed(playerHpText, new Color(0, 0, 0, 0.8f), 2);
            Bar(battle, new Vector2(0, 70), new Vector2(620, 22), new Color(0.12f, 0.12f, 0.12f, 0.9f), out playerLag, new Color(1, 0.4f, 0.35f), out playerFill, out playerFillImg, new Color(0.45f, 0.9f, 0.35f), bc);

            // スタミナ（キャラの横に出る輪）
            var st = New(battle, "stam", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(64, 64));
            stamGroup = st.gameObject.AddComponent<CanvasGroup>();
            stamBg = Img(st, new Color(0, 0, 0, 0.35f), Mat.RingSprite);
            stamRing = Img(New(st, "ring", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), new Color(0.95f, 0.85f, 0.3f), Mat.RingSprite);
            stamRing.type = Image.Type.Filled; stamRing.fillMethod = Image.FillMethod.Radial360; stamRing.fillOrigin = 2; stamRing.fillClockwise = false;

            // 特技 E
            var br = new Vector2(1, 0);
            skillRt = New(battle, "skill", br, br, new Vector2(-300, 110), new Vector2(118, 118));
            Img(skillRt, new Color(0.08f, 0.06f, 0.15f, 0.75f), Mat.CircleSprite);
            Img(New(skillRt, "rim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), new Color(1, 1, 1, 0.6f), Mat.RingSprite);
            skillIcon = Img(New(skillRt, "icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84)), Mat.Electro, Sprite.Create(Mat.Star, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f)));
            skillCdImg = Img(New(skillRt, "cd", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), new Color(0, 0, 0, 0.6f), Mat.CircleSprite);
            skillCdImg.type = Image.Type.Filled; skillCdImg.fillMethod = Image.FillMethod.Radial360; skillCdImg.fillOrigin = 2;
            skillCdText = Label(skillRt, "", bigFont, 40, Color.white, Vector2.zero, TextAnchor.MiddleCenter, 120);
            Shadowed(skillCdText, Color.black, 2);
            skillLabel = KeyCap(skillRt, "E", "レポート提出");

            // 奥義 Q
            burstRt = New(battle, "burst", br, br, new Vector2(-135, 135), new Vector2(160, 160));
            burstGlow = Img(New(burstRt, "glow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 300)), new Color(0.75f, 0.5f, 1f, 0), Mat.GlowSprite);
            Img(New(burstRt, "bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), new Color(0.08f, 0.06f, 0.15f, 0.8f), Mat.CircleSprite);
            Img(New(burstRt, "ringbg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), new Color(1, 1, 1, 0.15f), Mat.RingSprite);
            burstRing = Img(New(burstRt, "ring", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), Mat.Electro, Mat.RingSprite);
            burstRing.type = Image.Type.Filled; burstRing.fillMethod = Image.FillMethod.Radial360; burstRing.fillOrigin = 2; burstRing.fillClockwise = true;
            burstIcon = Img(New(burstRt, "icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110)), new Color(1, 1, 1, 0.5f), Sprite.Create(Mat.Star, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f)));
            burstLabel = Label(burstRt, "Q", bigFont, 40, Color.white, new Vector2(0, -2), TextAnchor.MiddleCenter, 160);
            Shadowed(burstLabel, new Color(0.2f, 0.05f, 0.35f), 2);
            burstLabelName = KeyCap(burstRt, "Q", "一夜漬け・雷光乱舞");

            // コンボ
            comboRt = New(battle, "combo", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-230, 140), new Vector2(400, 200));
            comboGroup = comboRt.gameObject.AddComponent<CanvasGroup>();
            comboNum = Label(comboRt, "0", bigFont, 100, Color.white, new Vector2(-40, 20), TextAnchor.MiddleRight, 360, 130);
            Shadowed(comboNum, new Color(0.35f, 0.1f, 0.5f), 4);
            comboLabel = Label(comboRt, "HIT", bigFont, 40, Mat.Gold, new Vector2(155, 5), TextAnchor.MiddleLeft, 200);
            Shadowed(comboLabel, new Color(0.3f, 0.15f, 0), 2);
            comboRate = Label(comboRt, "", uiFont, 34, Mat.ElectroLight, new Vector2(20, -60), TextAnchor.MiddleCenter, 400);
            Shadowed(comboRate, new Color(0.2f, 0.05f, 0.35f), 2);
            comboGroup.alpha = 0;

            // セリフ字幕
            var sg = New(root, "sub", bc, bc, new Vector2(0, 185), new Vector2(1400, 70));
            subGroup = sg.gameObject.AddComponent<CanvasGroup>();
            Img(New(sg, "bg", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 110)), new Color(0, 0, 0, 0.55f), Mat.GlowSprite);
            subName = Label(sg, "安東先生", uiFont, 26, Mat.Gold, new Vector2(0, 22), TextAnchor.MiddleCenter, 600);
            subText = Label(sg, "", uiFont, 34, Color.white, new Vector2(0, -14), TextAnchor.MiddleCenter, 1400);
            Shadowed(subText, new Color(0, 0, 0, 0.8f), 2);
            subGroup.alpha = 0;

            // ボスについている属性
            auraDot = Img(New(battle, "aura", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(510, -80), new Vector2(34, 34)), Color.white, Mat.CircleSprite);
            auraText = Label(auraDot.rectTransform, "", uiFont, 20, new Color(0.1f, 0.05f, 0.15f), Vector2.zero, TextAnchor.MiddleCenter, 40);

            hintText = Label(battle, "WASD 移動　クリック/J 攻撃　E 特技　Q 奥義　1・2・3 交代　Shift 回避　Space ジャンプ　Tab ロックオン　F2 光　F3 画風　Esc 一時停止",
                uiFont, 18, new Color(1, 1, 1, 0.75f), Vector2.zero, TextAnchor.MiddleLeft, 1400);
            Anchor(hintText.rectTransform, Vector2.zero, new Vector2(730, 24));
            Shadowed(hintText, new Color(0, 0, 0, 0.7f), 1);

            // 映画みたいな黒帯
            letterTop = Img(New(root, "lbTop", new Vector2(0, 1), Vector2.one, Vector2.zero, new Vector2(0, 0), new Vector2(0.5f, 1)), Color.black);
            letterBot = Img(New(root, "lbBot", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 0), new Vector2(0.5f, 0)), Color.black);
        }

        Text KeyCap(RectTransform parent, string key, string name)
        {
            var k = New(parent, "key", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -6), new Vector2(40, 34));
            Img(k, new Color(1, 1, 1, 0.92f), Mat.RoundSprite).type = Image.Type.Sliced;
            Label(k, key, uiFont, 22, new Color(0.15f, 0.1f, 0.25f), Vector2.zero, TextAnchor.MiddleCenter, 40);
            var n = Label(parent, name, uiFont, 20, Color.white, new Vector2(0, -parent.sizeDelta.y / 2 - 40), TextAnchor.MiddleCenter, 260);
            Shadowed(n, new Color(0, 0, 0, 0.8f), 1);
            return n;
        }

        static void Anchor(RectTransform rt, Vector2 a, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = a;
            rt.anchoredPosition = pos;
        }

        // ---------------- カットイン ----------------
        void BuildCutin()
        {
            cutGroup = cutin.gameObject.AddComponent<CanvasGroup>();
            cutBand = New(cutin, "band", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3000, 260));
            cutBand.localRotation = Quaternion.Euler(0, 0, 8);
            Img(cutBand, new Color(0.25f, 0.1f, 0.5f, 0.85f));
            Img(New(cutBand, "edge1", new Vector2(0, 1), Vector2.one, Vector2.zero, new Vector2(0, 8), new Vector2(0.5f, 1)), Mat.ElectroLight);
            Img(New(cutBand, "edge2", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 8), new Vector2(0.5f, 0)), Mat.ElectroLight);
            for (int i = 0; i < 14; i++)
                Img(New(cutBand, "speed", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Random.Range(-1400, 1400), Random.Range(-110, 110)), new Vector2(Random.Range(200, 600), 4)), new Color(1, 1, 1, 0.35f));
            cutSmall = Label(cutBand, "奥義", uiFont, 44, Mat.Gold, new Vector2(-260, 70), TextAnchor.MiddleCenter, 600);
            cutBig = Label(cutBand, "V ＝ I R", bigFont, 150, Color.white, new Vector2(80, -20), TextAnchor.MiddleCenter, 1400, 200);
            Shadowed(cutBig, new Color(0.45f, 0.2f, 0.9f), 6);
            cutGroup.alpha = 0;
        }

        // ---------------- 登場カード ----------------
        void BuildIntro()
        {
            introGroup = intro.gameObject.AddComponent<CanvasGroup>();
            var l = new Vector2(0, 0.5f);
            introSmall = Label(intro, "電気回路担当", uiFont, 44, new Color(1, 1, 1, 0.9f), Vector2.zero, TextAnchor.MiddleLeft, 900);
            Anchor(introSmall.rectTransform, l, new Vector2(620, 120));
            introBig = Label(intro, "安東 先生", bigFont, 170, Color.white, Vector2.zero, TextAnchor.MiddleLeft, 1400, 230);
            Anchor(introBig.rectTransform, l, new Vector2(860, 0));
            Shadowed(introBig, new Color(0.3f, 0.1f, 0.5f), 6);
            introLine = New(intro, "line", l, l, new Vector2(620, -110), new Vector2(900, 4));
            Img(introLine, Mat.Gold);
            introSub = Label(intro, "〜 単位の番人 〜", uiFont, 44, Mat.Gold, Vector2.zero, TextAnchor.MiddleLeft, 900);
            Anchor(introSub.rectTransform, l, new Vector2(620, -160));
            Shadowed(introSub, new Color(0, 0, 0, 0.6f), 2);
            introGroup.alpha = 0;
        }

        // ---------------- タイトル ----------------
        void BuildTitle()
        {
            Img(New(title, "shade", Vector2.zero, new Vector2(0.62f, 1), Vector2.zero, Vector2.zero, Vector2.zero), new Color(0.05f, 0.02f, 0.15f, 0.45f), Mat.GlowSprite);
            titleBlock = New(title, "block", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(620, 120), new Vector2(1100, 500));
            titleSmall = Label(titleBlock, "安東先生から", bigFont, 72, Color.white, new Vector2(-150, 150), TextAnchor.MiddleCenter, 1000, 110);
            Shadowed(titleSmall, new Color(0.3f, 0.1f, 0.55f), 4);
            titleBig = Label(titleBlock, "単位をもぎとれ！", bigFont, 118, Mat.Gold, new Vector2(0, 30), TextAnchor.MiddleCenter, 1300, 160);
            Shadowed(titleBig, new Color(0.45f, 0.12f, 0.1f), 6);
            titleSub = Label(titleBlock, "〜 電気回路 期末ボス決戦 〜", uiFont, 44, Mat.ElectroLight, new Vector2(0, -80), TextAnchor.MiddleCenter, 1100);
            Shadowed(titleSub, new Color(0.15f, 0.05f, 0.3f), 3);
            titlePrompt = Label(title, "クリック または Enter で開始", uiFont, 44, Color.white, new Vector2(-340, -250), TextAnchor.MiddleCenter, 1100);
            Shadowed(titlePrompt, new Color(0.2f, 0.05f, 0.4f), 3);
            titleBest = Label(title, "", uiFont, 30, Mat.Gold, new Vector2(-340, -320), TextAnchor.MiddleCenter, 1100);
            Shadowed(titleBest, new Color(0, 0, 0, 0.7f), 2);

            var panel = New(title, "howto", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-330, -40), new Vector2(560, 620));
            Img(panel, new Color(0.06f, 0.04f, 0.14f, 0.72f), Mat.RoundSprite).type = Image.Type.Sliced;
            Label(panel, "操作方法", uiFont, 36, Mat.Gold, new Vector2(0, 260), TextAnchor.MiddleCenter, 500);
            string[,] rows =
            {
                { "移動", "W A S D" }, { "視点", "マウス" }, { "通常攻撃", "左クリック / J" }, { "特技", "E" },
                { "奥義", "Q（やる気満タン）" }, { "キャラ交代", "1 / 2 / 3" }, { "回避", "Shift / 右クリック" }, { "ジャンプ", "Space" },
                { "光の強さ・画風", "F2 ・ F3" }, { "一時停止", "Esc" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                Label(panel, rows[i, 0], uiFont, 24, Color.white, new Vector2(-40, 205 - i * 42), TextAnchor.MiddleLeft, 400);
                Label(panel, rows[i, 1], uiFont, 24, Mat.ElectroLight, new Vector2(40, 205 - i * 42), TextAnchor.MiddleRight, 400);
            }
            Label(panel, "ともき（雷）・杉山くん（炎）・やましょう（風）\nちがう属性を続けて当てると「属性コンボ」！", uiFont, 21, new Color(1, 0.9f, 0.6f), new Vector2(0, -255), TextAnchor.MiddleCenter, 540, 70);
            var credit = Label(title, "音楽・効果音・グラフィックはすべてプログラムで生成しています。安東先生は架空の人物です。", uiFont, 20, new Color(1, 1, 1, 0.7f), Vector2.zero, TextAnchor.MiddleCenter, 1800);
            Anchor(credit.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 30));
        }

        // ---------------- リザルト ----------------
        void BuildResult()
        {
            resGroup = result.gameObject.AddComponent<CanvasGroup>();
            Img(result, new Color(0.03f, 0.02f, 0.08f, 0.7f));
            var panel = New(result, "panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230, 0), new Vector2(900, 820));
            Img(panel, new Color(0.08f, 0.06f, 0.16f, 0.85f), Mat.RoundSprite).type = Image.Type.Sliced;
            resTitle = Label(panel, "", bigFont, 90, Color.white, new Vector2(0, 320), TextAnchor.MiddleCenter, 880, 130);
            Shadowed(resTitle, new Color(0.35f, 0.1f, 0.55f), 5);
            for (int i = 0; i < 7; i++)
            {
                var t = Label(panel, "", uiFont, 32, Color.white, new Vector2(0, 200 - i * 58), TextAnchor.MiddleLeft, 760);
                resRows.Add(t);
            }
            Img(New(panel, "line", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -225), new Vector2(760, 3)), new Color(1, 1, 1, 0.5f));
            resTotal = Label(panel, "", bigFont, 64, Mat.Gold, new Vector2(0, -280), TextAnchor.MiddleCenter, 860, 90);
            Shadowed(resTotal, new Color(0.4f, 0.2f, 0), 3);
            resRecord = Label(panel, "NEW RECORD!", bigFont, 36, new Color(1, 0.45f, 0.5f), new Vector2(270, -330), TextAnchor.MiddleCenter, 500);
            resPrompt = Label(result, "R：もう一度　　T：タイトルへ", uiFont, 36, Color.white, new Vector2(0, -470), TextAnchor.MiddleCenter, 1200);
            Shadowed(resPrompt, new Color(0, 0, 0, 0.8f), 2);

            resGradeRt = New(result, "grade", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520, 110), new Vector2(360, 360));
            resGradeRing = Img(resGradeRt, new Color(0.9f, 0.15f, 0.15f, 0.95f), Mat.RingSprite);
            resGrade = Label(resGradeRt, "", bigFont, 230, new Color(0.9f, 0.15f, 0.15f), new Vector2(0, 10), TextAnchor.MiddleCenter, 360, 300);
            resStampRt = New(result, "stamp", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520, -200), new Vector2(460, 130));
            Img(resStampRt, new Color(0.9f, 0.15f, 0.15f, 0.12f), Mat.RoundSprite).type = Image.Type.Sliced;
            resStamp = Label(resStampRt, "", bigFont, 78, new Color(0.9f, 0.15f, 0.15f), Vector2.zero, TextAnchor.MiddleCenter, 460, 120);
            var so = resStamp.gameObject.AddComponent<Outline>(); so.effectColor = new Color(1, 1, 1, 0.8f); so.effectDistance = new Vector2(2, -2);
        }

        void ShowOnly(RectTransform which)
        {
            foreach (var r in new[] { battle, title, result })
                r.gameObject.SetActive(r == which);
        }

        // ================= 外から呼ぶ =================
        public void ShowTitle(int best)
        {
            ShowOnly(title);
            titleT = 0;
            titleBest.text = best > 0 ? $"ベストスコア　{best}" : "";
            letterTarget = 0;
        }
        public void ShowBattle() { ShowOnly(battle); comboGroup.alpha = 0; subGroup.alpha = 0; }
        public void ShowIntro() { introT = 0; letterTarget = 1; }
        public void EndIntro() { letterTarget = 0; }
        public void Pause(bool on) { pause.gameObject.SetActive(on); pause.SetAsLastSibling(); }
        public void Letterbox(bool on) => letterTarget = on ? 1 : 0;

        public void Say(string text, float sec = 2.6f, string speaker = "安東先生")
        {
            subName.text = speaker;
            subText.text = text;
            subT = sec;
        }

        public void Banner(string text, string sub, Color c, float dur = 1.6f)
        {
            bannerText.text = text; bannerText.color = c;
            bannerSub.text = sub ?? "";
            bannerT = 0; bannerDur = dur;
            bannerRt.SetAsLastSibling();
            Sfx.Play("pop", 0.7f);
        }

        public void Toast(string s) { toastText.text = s; toastT = 0; }
        public void NoStamina() { if (toastT > 1) Toast("スタミナが足りない！"); }
        public void Hurt(float strength) => hurtA = Mathf.Max(hurtA, strength);
        public void Perfect() => perfectA = 1;
        public void BossBarShake() => bossBarShake = 1;

        public void Combo(int n)
        {
            if (n < 2) return;
            comboNum.text = n.ToString();
            comboPop = 1; comboFade = 2.6f;
            string rate = n >= 60 ? "電撃的！！" : n >= 40 ? "EXCELLENT!" : n >= 25 ? "AMAZING!" : n >= 12 ? "GREAT!" : n >= 5 ? "GOOD!" : "";
            comboRate.text = rate;
            comboNum.color = n >= 40 ? new Color(1f, 0.55f, 0.9f) : n >= 25 ? Mat.Gold : n >= 12 ? new Color(0.7f, 0.85f, 1f) : Color.white;
        }

        public void CutIn(CharDef d)
        {
            cutT = 0; cutin.SetAsLastSibling(); letterTarget = 1;
            cutSmall.text = $"{d.Name}　奥義";
            cutBig.text = d.BurstName;
            cutBig.fontSize = d.BurstName.Length > 6 ? 110 : 150;
            cutBand.GetComponent<Image>().color = Color.Lerp(d.ElemColor, Color.black, 0.55f) * new Color(1, 1, 1, 0.88f);
            cutBig.GetComponent<Outline>().effectColor = Color.Lerp(d.ElemColor, Color.black, 0.3f);
        }

        public void OnSwap(CharDef d)
        {
            swapFlash = 1;
            Toast($"{d.Name}（{d.Title}）");
        }

        // スキル名をキャラの近くに出す
        public void SkillName(string name, Color c) => WorldText(Game.I.Player.Pos + Vector3.up * 2.6f, name, c, 0.8f);

        // 空中に文字を出す（反応名・「バババババッ！！」など）
        public void WorldText(Vector3 world, string text, Color c, float scale)
        {
            Number(world, 0, NumKind.Text, c);
            var n = nums[lastNum];
            n.t.text = text;
            n.t.fontSize = (int)(56 * scale);
            n.t.color = c;
            n.t.GetComponent<Outline>().effectColor = Color.Lerp(c, Color.black, 0.7f);
            n.life = 1.2f; n.scale = 1.5f;
            n.drift = new Vector2(0, 90);
        }

        public enum NumKind { Normal, Crit, Player, Break, Burst, Text }
        int lastNum;
        public void Number(Vector3 world, int value, NumKind kind, Color tint = default)
        {
            Num n = null;
            for (int i = 0; i < nums.Count; i++) if (!nums[i].active) { n = nums[i]; lastNum = i; break; }
            if (n == null)
            {
                var rt = New(battle, "num", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(400, 120));
                var t = rt.gameObject.AddComponent<Text>();
                t.font = bigFont; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                var o = rt.gameObject.AddComponent<Outline>(); o.effectDistance = new Vector2(3, -3);
                n = new Num { t = t };
                nums.Add(n);
                lastNum = nums.Count - 1;
            }
            n.active = true; n.age = 0; n.world = world + Random.insideUnitSphere * 0.5f;
            n.drift = new Vector2(Random.Range(-60f, 60f), Random.Range(60, 110));
            n.t.gameObject.SetActive(true);
            n.t.transform.SetAsLastSibling();
            var ol = n.t.GetComponent<Outline>();
            switch (kind)
            {
                case NumKind.Crit:
                    n.t.text = value.ToString(); n.t.fontSize = 76; n.t.color = new Color(1f, 0.86f, 0.3f); ol.effectColor = new Color(0.55f, 0.25f, 0); n.life = 1.1f; n.scale = 1.9f; break;
                case NumKind.Player:
                    n.t.text = "-" + value; n.t.fontSize = 48; n.t.color = new Color(1f, 0.35f, 0.35f); ol.effectColor = new Color(0.3f, 0, 0); n.life = 0.9f; n.scale = 1.3f; break;
                case NumKind.Break:
                    n.t.text = value.ToString(); n.t.fontSize = 64; n.t.color = new Color(1f, 0.6f, 0.25f); ol.effectColor = new Color(0.4f, 0.1f, 0); n.life = 1f; n.scale = 1.6f; break;
                case NumKind.Burst:
                    n.t.text = value.ToString(); n.t.fontSize = 70; n.t.color = new Color(0.95f, 0.75f, 1f); ol.effectColor = new Color(0.35f, 0.05f, 0.6f); n.life = 1.1f; n.scale = 1.8f; break;
                default:
                    var tc = tint.a > 0 ? Color.Lerp(tint, Color.white, 0.25f) : new Color(0.85f, 0.7f, 1f);
                    n.t.text = value.ToString(); n.t.fontSize = 50; n.t.color = tc; ol.effectColor = Color.Lerp(tc, Color.black, 0.75f); n.life = 0.85f; n.scale = 1.4f; break;
            }
        }

        public void ShowResult(ResultData d)
        {
            res = d; resT = 0; resActive = true; shownTotal = 0;
            ShowOnly(result);
            result.gameObject.SetActive(true);
            resTitle.text = d.win ? "撃破！" : d.reason;
            resTitle.color = d.win ? Mat.Gold : new Color(0.75f, 0.8f, 1f);
            string[] rows =
            {
                $"与えたダメージ　　　　{d.dealt}",
                d.win ? $"撃破ボーナス　　　　　+{d.killBonus}" : "撃破ボーナス　　　　　―",
                d.win ? $"残り時間ボーナス　　　+{d.timeBonus}" : "残り時間ボーナス　　　―",
                d.win ? $"残りHPボーナス　　　　+{d.hpBonus}" : "残りHPボーナス　　　　―",
                $"ジャスト回避 ×{d.perfects}　　 +{d.perfectBonus}",
                $"最大コンボ {d.maxCombo}　　　　+{d.comboBonus}",
                $"属性コンボ ×{d.reactions}　　　 +{d.reactions * 20}",
            };
            for (int i = 0; i < resRows.Count; i++) { resRows[i].text = rows[i]; resRows[i].color = new Color(1, 1, 1, 0); }
            resTotal.text = "";
            resGrade.text = d.grade;
            resStamp.text = d.win ? "単位認定" : "単位不認定";
            resGradeRt.localScale = Vector3.zero;
            resStampRt.localScale = Vector3.zero;
            resRecord.gameObject.SetActive(false);
            resPrompt.color = new Color(1, 1, 1, 0);
            var gc = d.win ? new Color(0.9f, 0.15f, 0.15f) : new Color(0.35f, 0.4f, 0.55f);
            resGrade.color = gc; resGradeRing.color = gc; resStamp.color = gc;
        }

        // ================= 毎フレーム =================
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var G = Game.I;
            if (G == null) return;

            letter = Mathf.MoveTowards(letter, letterTarget, dt * 3);
            float lh = 120 * letter * letter * (3 - 2 * letter);
            letterTop.rectTransform.sizeDelta = new Vector2(0, lh);
            letterBot.rectTransform.sizeDelta = new Vector2(0, lh);
            letterTop.transform.SetAsLastSibling(); letterBot.transform.SetAsLastSibling();

            hurtA = Mathf.MoveTowards(hurtA, 0, dt * 2.2f);
            perfectA = Mathf.MoveTowards(perfectA, 0, dt * 1.2f);
            hurtEdge.color = new Color(1, 0.1f, 0.15f, hurtA * 0.8f);
            perfectEdge.color = new Color(0.55f, 0.9f, 1f, perfectA * 0.7f);

            // バナー
            bannerT += dt;
            if (bannerT < bannerDur + 0.4f)
            {
                float u = bannerT;
                float s = u < 0.12f ? Mathf.Lerp(2.4f, 0.92f, u / 0.12f) : u < 0.22f ? Mathf.Lerp(0.92f, 1.05f, (u - 0.12f) / 0.1f) : 1.05f + (u - 0.22f) * 0.04f;
                bannerRt.localScale = Vector3.one * s;
                bannerGroup.alpha = u < bannerDur ? 1 : 1 - (u - bannerDur) / 0.4f;
                bannerFlash.color = new Color(1, 1, 1, Mathf.Clamp01(1 - u * 3) * 0.8f);
            }
            else bannerGroup.alpha = 0;

            toastT += dt;
            toastGroup.alpha = toastT < 1.4f ? Mathf.Clamp01((1.4f - toastT) * 3) : 0;

            // タイトル
            if (title.gameObject.activeSelf)
            {
                titleT += dt;
                float e = Mathf.Clamp01(titleT / 0.6f);
                titleBlock.anchoredPosition = new Vector2(620 - (1 - e) * (1 - e) * 400, 120 + Mathf.Sin(titleT * 1.5f) * 6);
                titleBig.transform.localScale = Vector3.one * (1 + Mathf.Max(0, Mathf.Sin(titleT * 3)) * 0.03f);
                titlePrompt.color = new Color(1, 1, 1, 0.55f + 0.45f * Mathf.Sin(titleT * 4));
            }

            // 登場カード
            introT += dt;
            if (introT < 3.2f)
            {
                introGroup.alpha = introT < 2.7f ? Mathf.Clamp01(introT * 4) : 1 - (introT - 2.7f) / 0.5f;
                float e = 1 - Mathf.Pow(1 - Mathf.Clamp01(introT / 0.5f), 3);
                introBig.rectTransform.anchoredPosition = new Vector2(860 + (1 - e) * 500 - introT * 12, 0);
                introSmall.rectTransform.anchoredPosition = new Vector2(620 - (1 - e) * 300, 120);
                introLine.sizeDelta = new Vector2(900 * Mathf.Clamp01((introT - 0.2f) / 0.5f), 4);
                introSub.color = new Color(Mat.Gold.r, Mat.Gold.g, Mat.Gold.b, Mathf.Clamp01((introT - 0.5f) * 3));
            }
            else introGroup.alpha = 0;

            // カットイン
            cutT += dt;
            if (cutT < 1.3f)
            {
                cutGroup.alpha = cutT < 1.05f ? 1 : 1 - (cutT - 1.05f) / 0.25f;
                float e = 1 - Mathf.Pow(1 - Mathf.Clamp01(cutT / 0.25f), 3);
                cutBand.anchoredPosition = new Vector2((1 - e) * -2400 + cutT * 60, 0);
                cutBig.transform.localScale = Vector3.one * (1.25f - 0.2f * e + cutT * 0.05f);
            }
            else if (cutGroup.alpha > 0) { cutGroup.alpha = 0; if (G.State == Game.Mode.Battle && !G.Cinematic) letterTarget = 0; }

            // 登場演出・奥義・決着の演出中はUIを隠して画面を広く見せる
            bool hideUi = G.State == Game.Mode.Intro || G.Cinematic;
            battleGroup.alpha = Mathf.MoveTowards(battleGroup.alpha, hideUi ? 0 : 1, dt * 4);
            if (battle.gameObject.activeSelf) UpdateBattle(dt, G);
            // 字幕（登場演出・決着の演出中も出す。結果画面では消す）
            subT -= dt;
            subGroup.alpha = Mathf.MoveTowards(subGroup.alpha, subT > 0 && !result.gameObject.activeSelf && !title.gameObject.activeSelf ? 1 : 0, dt * 5);
            UpdateNums(dt);
            if (resActive && result.gameObject.activeSelf) UpdateResult(dt);
        }

        void UpdateBattle(float dt, Game G)
        {
            var B = G.Boss; var P = G.Player;
            SetFill(bossFill, B.Hp / Boss.MaxHp);
            SetFill(bossLag, B.LagHp / Boss.MaxHp);
            bossFillImg.color = B.Phase == 2 ? Color.Lerp(new Color(0.85f, 0.2f, 0.7f), new Color(1f, 0.3f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5)) : new Color(0.93f, 0.25f, 0.3f);
            bossBarShake = Mathf.MoveTowards(bossBarShake, 0, dt * 5);
            bossFill.parent.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(Random.Range(-1f, 1f) * bossBarShake * 6, -80 + Random.Range(-1f, 1f) * bossBarShake * 4);
            if (B.Broken)
            {
                SetFill(toughFill, 1 - B.BreakT / 5f);
                toughFillImg.color = Color.Lerp(Mat.Gold, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12));
                toughLabel.text = "BREAK!";
                toughLabel.color = Mat.Gold;
            }
            else
            {
                SetFill(toughFill, B.Tough / (Boss.MaxTough * (B.Phase == 2 ? 1.2f : 1f)));
                toughFillImg.color = new Color(0.75f, 0.6f, 1f);
                toughLabel.text = "理論武装";
                toughLabel.color = new Color(0.85f, 0.8f, 1f);
            }
            bossName.text = B.Phase == 2 ? "電気回路担当・安東先生〔本気〕" : "電気回路担当・安東先生";

            float tl = Mathf.Max(0, G.TimeLeft);
            int sec = Mathf.CeilToInt(tl);
            timerText.text = $"{sec / 60}:{sec % 60:00}";
            timerText.color = tl < 30 ? Color.Lerp(Color.white, new Color(1, 0.35f, 0.35f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8)) : Color.white;
            dmgText.text = $"与ダメージ {G.Dealt}";

            float hpR = P.Hp / Player.MaxHp;
            SetFill(playerFill, hpR);
            float lagR = Mathf.Lerp(playerLag.anchorMax.x, hpR, dt * 2.5f);
            SetFill(playerLag, Mathf.Max(lagR, hpR));
            playerFillImg.color = hpR > 0.5f ? new Color(0.45f, 0.9f, 0.35f) : hpR > 0.25f ? new Color(1f, 0.8f, 0.25f) : new Color(1f, 0.3f, 0.3f);
            playerHpText.text = $"{Mathf.CeilToInt(P.Hp)} / {(int)Player.MaxHp}";

            // スタミナの輪をキャラの右上に
            var cam = G.Cam.Cam;
            var sp = cam.WorldToScreenPoint(P.Pos + Vector3.up * 1.8f);
            var stRt = stamRing.transform.parent as RectTransform;
            stRt.position = new Vector3(sp.x + 70 * canvas.scaleFactor, sp.y, 0);
            stamRing.fillAmount = P.Stam / 100f;
            stamRing.color = P.Stam < 25 ? new Color(1, 0.35f, 0.3f) : new Color(0.95f, 0.85f, 0.3f);
            stamGroup.alpha = Mathf.MoveTowards(stamGroup.alpha, P.Stam < 99.5f && sp.z > 0 ? 1 : 0, dt * 4);

            // スキル
            var ec = P.Def.ElemColor;
            skillCdImg.fillAmount = P.SkillCd / P.Def.SkillCd;
            skillCdText.text = P.SkillCd > 0 ? P.SkillCd.ToString("0.0") : "";
            skillIcon.color = P.SkillCd > 0 ? Color.Lerp(ec, Color.gray, 0.6f) : ec;
            skillRt.localScale = Vector3.one * (P.SkillCd > P.Def.SkillCd - 0.15f ? 0.9f : 1f);
            skillLabel.text = P.Def.SkillName;
            burstLabelName.text = P.Def.BurstName;
            bool ready = P.Energy >= 100;
            burstRing.fillAmount = P.Energy / 100f;
            burstRing.color = ready ? Color.Lerp(ec, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8)) : ec;
            burstGlow.color = new Color(ec.r, ec.g, ec.b, ready ? 0.5f + 0.25f * Mathf.Sin(Time.unscaledTime * 6) : 0);

            playerName.text = $"{P.Def.Name}　Lv.90";
            slowText.text = G.SlowT > 0 ? $"鈍足（トランス）　{G.SlowT:0.0}" : "";
            swapFlash = Mathf.MoveTowards(swapFlash, 0, dt * 3);
            for (int i = 0; i < partyRows.Count; i++)
            {
                var r = partyRows[i];
                bool on = i == G.Active;
                var pm = G.Party[i];
                r.rt.anchoredPosition = new Vector2(on ? -170 - swapFlash * 20 : -150, -20 - i * 70);
                r.rt.localScale = Vector3.one * (on ? 1.08f : 0.95f);
                r.name.color = on ? Color.white : new Color(1, 1, 1, 0.6f);
                bool rdy = pm.Energy >= 100;
                r.ready.color = new Color(pm.Def.ElemColor.r, pm.Def.ElemColor.g, pm.Def.ElemColor.b, rdy ? 0.6f + 0.3f * Mathf.Sin(Time.unscaledTime * 6) : 0);
            }
            if (B.Aura != Elem.None)
            {
                auraDot.enabled = true; auraText.enabled = true;
                auraDot.color = Elements.Color(B.Aura);
                auraText.text = Elements.Kanji(B.Aura);
            }
            else { auraDot.enabled = false; auraText.enabled = false; }
            burstIcon.color = ready ? Mat.Gold : new Color(1, 1, 1, 0.35f);
            burstIcon.transform.localRotation = Quaternion.Euler(0, 0, ready ? Time.unscaledTime * 90 : 0);
            burstRt.localScale = Vector3.one * (ready ? 1 + 0.05f * Mathf.Sin(Time.unscaledTime * 6) : 1);

            // コンボ
            comboPop = Mathf.MoveTowards(comboPop, 0, dt * 6);
            comboFade -= dt;
            comboGroup.alpha = Mathf.Clamp01(comboFade * 2);
            comboRt.localScale = Vector3.one * (1 + comboPop * 0.35f);
            comboRate.transform.localScale = Vector3.one * (1 + comboPop * 0.2f);

        }

        void UpdateNums(float dt)
        {
            var cam = Game.I.Cam.Cam;
            foreach (var n in nums)
            {
                if (!n.active) continue;
                n.age += dt;
                if (n.age > n.life || !battle.gameObject.activeSelf) { n.active = false; n.t.gameObject.SetActive(false); continue; }
                var sp = cam.WorldToScreenPoint(n.world);
                if (sp.z < 0) { n.t.enabled = false; continue; }
                n.t.enabled = true;
                float u = n.age / n.life;
                var off = n.drift * (1 - Mathf.Pow(1 - Mathf.Min(1, u * 2), 2)) * canvas.scaleFactor;
                n.t.rectTransform.position = new Vector3(sp.x + off.x, sp.y + off.y, 0);
                float s = n.age < 0.08f ? Mathf.Lerp(n.scale * 1.4f, n.scale, n.age / 0.08f) : Mathf.Lerp(n.scale, n.scale * 0.7f, (n.age - 0.08f) / n.life);
                n.t.rectTransform.localScale = Vector3.one * s * 0.6f;
                var c = n.t.color; c.a = u < 0.7f ? 1 : 1 - (u - 0.7f) / 0.3f; n.t.color = c;
            }
        }

        void UpdateResult(float dt)
        {
            resT += dt;
            for (int i = 0; i < resRows.Count; i++)
            {
                float a = Mathf.Clamp01((resT - 0.3f - i * 0.18f) * 4);
                var c = resRows[i].color; c.a = a; resRows[i].color = c;
                resRows[i].rectTransform.anchoredPosition = new Vector2(-40 + 40 * a, 200 - i * 58);
                if (a > 0 && a < 1 && resRows[i].text != "" && Mathf.Abs(a - 0.25f) < 0.07f) Sfx.Play("tick", 0.4f);
            }
            float countStart = 1.6f, countDur = 1.4f;
            if (resT > countStart)
            {
                float u = Mathf.Clamp01((resT - countStart) / countDur);
                int v = Mathf.RoundToInt(res.total * (1 - Mathf.Pow(1 - u, 3)));
                if (v != shownTotal && Time.frameCount % 3 == 0) Sfx.Play("tick", 0.3f, 1 + u);
                shownTotal = v;
                resTotal.text = $"合計　{v}";
                resTotal.transform.localScale = Vector3.one * (u < 1 ? 1.05f : 1);
            }
            float gT = countStart + countDur + 0.3f;
            if (resT > gT)
            {
                float u = resT - gT;
                if (u - dt <= 0)
                {
                    Sfx.Play("stamp", 1f);
                    Game.I.Cam.Shake(0.3f);
                    PostFX.I?.Flash(Color.white, 0.25f);
                }
                float s = u < 0.15f ? Mathf.Lerp(3f, 0.9f, u / 0.15f) : u < 0.25f ? Mathf.Lerp(0.9f, 1f, (u - 0.15f) / 0.1f) : 1;
                resGradeRt.localScale = Vector3.one * s;
                resGradeRt.localRotation = Quaternion.Euler(0, 0, -12);
            }
            float sT = gT + 0.6f;
            if (resT > sT)
            {
                float u = resT - sT;
                if (u - dt <= 0)
                {
                    Sfx.Play("stamp", 1f, 0.8f);
                    if (res.win) { Sfx.Play("ready", 0.9f); Fx.Confetti(Game.I.Cam.transform.position + Game.I.Cam.transform.forward * 6 + Vector3.down * 3, 150); }
                    else Sfx.Play("defeat", 0.8f);
                    if (res.record) resRecord.gameObject.SetActive(true);
                }
                float s = u < 0.15f ? Mathf.Lerp(3f, 0.9f, u / 0.15f) : u < 0.25f ? Mathf.Lerp(0.9f, 1f, (u - 0.15f) / 0.1f) : 1;
                resStampRt.localScale = Vector3.one * s;
                resStampRt.localRotation = Quaternion.Euler(0, 0, 8);
                resRecord.transform.localScale = Vector3.one * (1 + 0.08f * Mathf.Sin(resT * 8));
                resPrompt.color = new Color(1, 1, 1, Mathf.Clamp01(u - 0.5f) * (0.6f + 0.4f * Mathf.Sin(resT * 4)));
            }
        }
    }
}
