namespace AndoBoss
{
    // 実名なし版：画面に出る名前を、実在しない名前に置きかえる。
    // 「実名なし版のexeを作る」でビルドしたとき（ANDO_ANON）と、起動引数 -andoAnon のときに有効
    public static class Names
    {
#if ANDO_ANON
        public static readonly bool Anon = true;
#else
        public static readonly bool Anon = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-andoAnon") >= 0;
#endif

        // 置きかえ表（左 → 右）。名前を変えたいときはここを書きかえる
        static readonly string[,] Map =
        {
            { "安東", "ボルト" },
            { "菅原", "グリム" },
            { "杉山", "ほむら" },
            { "やましょう", "ねむお" },
            { "ともき", "らいと" },
        };

        public static string Fix(string s)
        {
            if (!Anon || string.IsNullOrEmpty(s)) return s;
            for (int i = 0; i < Map.GetLength(0); i++)
                if (s.Contains(Map[i, 0])) s = s.Replace(Map[i, 0], Map[i, 1]);
            return s;
        }
    }
}
