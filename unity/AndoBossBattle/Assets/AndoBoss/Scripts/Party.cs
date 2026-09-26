using UnityEngine;

namespace AndoBoss
{
    // 属性（ゲーム内の呼び名は「属性」）
    public enum Elem { None, Electro, Pyro, Wind }
    public enum Weapon { Sword, Fist, Bow }

    // 操作キャラ3人の設定（3人とも架空の人物）
    // E＝特技、Q＝奥義、ゲージ＝やる気
    public class CharDef
    {
        public int Id;
        public string Name, Title;
        public Elem Elem;
        public Weapon Weapon;
        public float AtkMul = 1f, Height = 1f;
        public float[] SwingDmg, SwingDur;
        public float SkillCd;
        public string SkillName, BurstName, BurstShout;
        public Color Hair, Jacket, Pants, Accent, Eye, Skin;
        public bool Glasses, Spiky, Sleepy;
        public string Passive = "";

        public Color ElemColor => Elements.Color(Elem);

        public static readonly CharDef[] All =
        {
            new CharDef
            {
                Id = 0, Name = "ともき", Title = "雷の受講生", Elem = Elem.Electro, Weapon = Weapon.Sword,
                SwingDmg = new[] { 40f, 48, 60, 105 }, SwingDur = new[] { 0.3f, 0.3f, 0.4f, 0.58f },
                SkillCd = 8f, SkillName = "レポート提出", BurstName = "一夜漬け・雷光乱舞", BurstShout = "徹夜の力、見せでやる！",
                Hair = new Color(0.3f, 0.22f, 0.45f), Jacket = new Color(0.16f, 0.18f, 0.32f), Pants = new Color(0.12f, 0.12f, 0.2f),
                Accent = new Color(0.71f, 0.49f, 1f), Eye = new Color(0.55f, 0.3f, 0.95f), Skin = new Color(1f, 0.87f, 0.77f),
            },
            new CharDef
            {
                Id = 1, Name = "杉山くん", Title = "炎のラグビー部", Elem = Elem.Pyro, Weapon = Weapon.Fist, Height = 1.08f,
                SwingDmg = new[] { 60f, 72, 130 }, SwingDur = new[] { 0.38f, 0.38f, 0.62f },
                SkillCd = 8f, SkillName = "炎のロングパス", BurstName = "ラグビー部タックル", BurstShout = "どけどけぇ！ラグビー部だ！",
                Hair = new Color(0.2f, 0.14f, 0.1f), Jacket = new Color(0.72f, 0.16f, 0.12f), Pants = new Color(0.95f, 0.95f, 0.95f),
                Accent = new Color(1f, 0.55f, 0.2f), Eye = new Color(0.8f, 0.35f, 0.15f), Skin = new Color(0.93f, 0.78f, 0.64f), Spiky = true,
            },
            new CharDef
            {
                Id = 2, Name = "やましょう", Title = "最強の弓使い（ねむい）", Elem = Elem.Wind, Weapon = Weapon.Bow, AtkMul = 1.25f, Height = 1.04f,
                SwingDmg = new[] { 38f, 38, 45, 90 }, SwingDur = new[] { 0.34f, 0.34f, 0.36f, 0.55f },
                SkillCd = 9f, SkillName = "二度寝アロー", BurstName = "寝ぼけ乱れ撃ち", BurstShout = "ねみぃ……全部撃っとくか。",
                Passive = "留年：1回やられても生き返り、極太の矢を2本撃ち返す（1戦に1回）",
                Hair = new Color(0.25f, 0.35f, 0.3f), Jacket = new Color(0.3f, 0.5f, 0.42f), Pants = new Color(0.2f, 0.22f, 0.26f),
                Accent = new Color(0.55f, 1f, 0.75f), Eye = new Color(0.3f, 0.75f, 0.55f), Skin = new Color(0.98f, 0.88f, 0.8f), Glasses = true, Sleepy = true,
            },
        };
    }

    public static class Elements
    {
        public static Color Color(Elem e)
        {
            switch (e)
            {
                case Elem.Electro: return new Color(0.71f, 0.49f, 1f);
                case Elem.Pyro: return new Color(1f, 0.5f, 0.2f);
                case Elem.Wind: return new Color(0.55f, 1f, 0.75f);
                default: return UnityEngine.Color.white;
            }
        }
        public static string Kanji(Elem e) => e == Elem.Electro ? "雷" : e == Elem.Pyro ? "炎" : e == Elem.Wind ? "風" : "";

        // 属性コンボ（ちがう属性を続けて当てたとき）。name が null ならなし
        public static (string name, float mul, float extra, float tough, Color color) React(Elem aura, Elem hit)
        {
            if (aura == Elem.None || hit == Elem.None || aura == hit) return (null, 1, 0, 1, UnityEngine.Color.white);
            bool Has(Elem a, Elem b) => (aura == a && hit == b) || (aura == b && hit == a);
            if (Has(Elem.Electro, Elem.Pyro)) return ("過電流", 1f, 0.8f, 1.3f, new Color(1f, 0.45f, 0.6f));
            if (Has(Elem.Electro, Elem.Wind)) return ("放電嵐", 1f, 0.5f, 1.8f, new Color(0.7f, 0.8f, 1f));
            return ("火炎旋風", 1.6f, 0, 1.2f, new Color(1f, 0.8f, 0.4f));
        }
    }
}
