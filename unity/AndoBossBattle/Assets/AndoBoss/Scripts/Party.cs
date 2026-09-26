using UnityEngine;

namespace AndoBoss
{
    public enum Elem { None, Electro, Pyro, Cryo }
    public enum Weapon { Sword, Iron, Ruler }

    // 操作キャラ3人の設定（3人とも架空の人物）
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
        public bool Glasses, Spiky;

        public Color ElemColor => Elements.Color(Elem);

        public static readonly CharDef[] All =
        {
            new CharDef
            {
                Id = 0, Name = "ともき", Title = "雷の受講生", Elem = Elem.Electro, Weapon = Weapon.Sword,
                SwingDmg = new[] { 45f, 55, 70, 125 }, SwingDur = new[] { 0.3f, 0.3f, 0.4f, 0.58f },
                SkillCd = 7f, SkillName = "レポート提出", BurstName = "一夜漬け・雷光乱舞", BurstShout = "徹夜の力、見せでやる！",
                Hair = new Color(0.3f, 0.22f, 0.45f), Jacket = new Color(0.16f, 0.18f, 0.32f), Pants = new Color(0.12f, 0.12f, 0.2f),
                Accent = new Color(0.71f, 0.49f, 1f), Eye = new Color(0.55f, 0.3f, 0.95f), Skin = new Color(1f, 0.87f, 0.77f),
            },
            new CharDef
            {
                Id = 1, Name = "杉山くん", Title = "炎のはんだ職人", Elem = Elem.Pyro, Weapon = Weapon.Iron, Height = 1.06f,
                SwingDmg = new[] { 75f, 90, 160 }, SwingDur = new[] { 0.46f, 0.46f, 0.72f },
                SkillCd = 8f, SkillName = "はんだ付け", BurstName = "ショート回路", BurstShout = "回路ごと燃やすど！",
                Hair = new Color(0.2f, 0.14f, 0.1f), Jacket = new Color(0.55f, 0.2f, 0.12f), Pants = new Color(0.2f, 0.18f, 0.2f),
                Accent = new Color(1f, 0.55f, 0.2f), Eye = new Color(0.8f, 0.35f, 0.15f), Skin = new Color(0.96f, 0.82f, 0.7f), Spiky = true,
            },
            new CharDef
            {
                Id = 2, Name = "やましょう", Title = "最強の氷使い", Elem = Elem.Cryo, Weapon = Weapon.Ruler, AtkMul = 1.35f, Height = 1.04f,
                SwingDmg = new[] { 42f, 42, 58, 58, 120 }, SwingDur = new[] { 0.22f, 0.22f, 0.26f, 0.26f, 0.46f },
                SkillCd = 7f, SkillName = "液体窒素", BurstName = "絶対零度", BurstShout = "凍れ。……終わりだ。",
                Hair = new Color(0.82f, 0.9f, 1f), Jacket = new Color(0.9f, 0.93f, 0.97f), Pants = new Color(0.2f, 0.26f, 0.38f),
                Accent = new Color(0.45f, 0.85f, 1f), Eye = new Color(0.3f, 0.75f, 1f), Skin = new Color(0.98f, 0.88f, 0.8f), Glasses = true,
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
                case Elem.Cryo: return new Color(0.55f, 0.88f, 1f);
                default: return UnityEngine.Color.white;
            }
        }
        public static string Kanji(Elem e) => e == Elem.Electro ? "雷" : e == Elem.Pyro ? "炎" : e == Elem.Cryo ? "氷" : "";

        // 元素反応。name が null なら反応なし
        public static (string name, float mul, float extra, float tough, Color color) React(Elem aura, Elem hit)
        {
            if (aura == Elem.None || hit == Elem.None || aura == hit) return (null, 1, 0, 1, UnityEngine.Color.white);
            bool Has(Elem a, Elem b) => (aura == a && hit == b) || (aura == b && hit == a);
            if (Has(Elem.Electro, Elem.Pyro)) return ("過負荷", 1f, 1.2f, 2f, new Color(1f, 0.45f, 0.6f));
            if (Has(Elem.Electro, Elem.Cryo)) return ("超電導", 1f, 0.6f, 3f, new Color(0.7f, 0.7f, 1f));
            return ("溶解", 2f, 0, 1.5f, new Color(1f, 0.75f, 0.35f));
        }
    }
}
