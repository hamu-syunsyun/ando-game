using UnityEngine;

namespace AndoBoss
{
    // ビルトインレンダーパイプライン用の自作ポストエフェクト。
    // ブルーム・色収差・放射ブラー・ビネット・色調・画面フラッシュ
    [RequireComponent(typeof(Camera))]
    public class PostFX : MonoBehaviour
    {
        public static PostFX I;
        Material mat;
        const int MaxIter = 6;
        readonly RenderTexture[] chain = new RenderTexture[MaxIter];

        // 明るいところだけが光るように、しきい値は高め・強さは控えめにしてある
        public float BloomThreshold = 1.3f;
        public float BloomIntensity = 0.3f;
        public float BaseSaturation = 1.08f;
        public float BaseContrast = 1.04f;
        public float BaseVignette = 0.3f;
        public float Exposure = 1.15f;

        // 光の演出の強さ（F2 で切り替え）。0 = 光らない, 1 = 控えめ, 2 = 派手
        public static int Level = 1;
        public static readonly string[] LevelNames = { "光の演出：オフ", "光の演出：控えめ", "光の演出：派手" };

        // 演出で一時的に足すもの
        float flashA; Color flashC = Color.white; float flashDecay = 4f;
        float chroma, radial, desat, extraBloom;
        public Color Tint = Color.white;
        Color tintTarget = Color.white;
        public float LowHp;   // 0..1 体力が少ないときの赤いビネット
        public bool Enabled = true;

        void Awake()
        {
            I = this;
            var sh = Shader.Find("Hidden/AndoBoss/Post");
            if (sh != null && sh.isSupported) mat = new Material(sh);
        }

        public void Flash(Color c, float a, float decay = 4f)
        {
            // 画面が真っ白になって見えなくならないよう上限をつける
            a = Mathf.Min(a * (Level == 2 ? 0.7f : 0.4f), Level == 0 ? 0.15f : 0.45f);
            if (a >= flashA) { flashC = c; flashDecay = decay; }
            flashA = Mathf.Max(flashA, a);
        }
        public void Chroma(float a) => chroma = Mathf.Max(chroma, a);
        public void Radial(float a) => radial = Mathf.Max(radial, a);
        public void Bloom(float a) => extraBloom = Mathf.Max(extraBloom, a);
        public void Desaturate(float a) => desat = a;
        public void SetTint(Color c) => tintTarget = c;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            flashA = Mathf.MoveTowards(flashA, 0, dt * flashDecay);
            chroma = Mathf.MoveTowards(chroma, 0, dt * 3f);
            radial = Mathf.MoveTowards(radial, 0, dt * 2.5f);
            extraBloom = Mathf.MoveTowards(extraBloom, 0, dt * 1.5f);
            Tint = Color.Lerp(Tint, tintTarget, dt * 2f);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null || !Enabled) { Graphics.Blit(src, dst); return; }

            mat.SetFloat("_Threshold", BloomThreshold);
            mat.SetFloat("_Knee", 0.5f);
            int w = src.width / 2, h = src.height / 2;
            var fmt = src.format;
            RenderTexture cur = chain[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, cur, mat, 0);
            int n = 1;
            for (; n < MaxIter; n++)
            {
                w /= 2; h /= 2;
                if (w < 4 || h < 4) break;
                chain[n] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(cur, chain[n], mat, 1);
                cur = chain[n];
            }
            for (int i = n - 2; i >= 0; i--)
            {
                Graphics.Blit(cur, chain[i], mat, 2);
                cur = chain[i];
            }

            float pulse = LowHp > 0 ? (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6)) * LowHp : 0;
            float lv = Level == 0 ? 0 : Level == 1 ? 1 : 1.8f;
            mat.SetTexture("_BloomTex", cur);
            mat.SetFloat("_BloomIntensity", (BloomIntensity + extraBloom * 0.3f) * lv);
            mat.SetFloat("_Chroma", (0.1f + chroma * 3f) * Mathf.Min(lv, 1));
            mat.SetFloat("_RadialBlur", radial * Mathf.Min(lv, 1) * 0.7f);
            mat.SetFloat("_Vignette", BaseVignette + pulse * 0.6f);
            mat.SetFloat("_Saturation", BaseSaturation * (1 - desat));
            mat.SetFloat("_Contrast", BaseContrast);
            mat.SetFloat("_Exposure", Exposure);
            var tint = Tint;
            if (pulse > 0) tint = Color.Lerp(tint, new Color(1.15f, 0.75f, 0.75f), pulse * 0.5f);
            mat.SetColor("_Tint", tint);
            mat.SetColor("_Flash", new Color(flashC.r, flashC.g, flashC.b, Mathf.Clamp01(flashA)));
            Graphics.Blit(src, dst, mat, 3);

            for (int i = 0; i < n; i++) { RenderTexture.ReleaseTemporary(chain[i]); chain[i] = null; }
        }
    }
}
