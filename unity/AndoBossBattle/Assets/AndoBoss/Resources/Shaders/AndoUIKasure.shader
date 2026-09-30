// UIの文字を「かすれた筆文字」に見せるシェーダー（菅原先生の字幕用）。
// 横向きのかすれ筋と細かいざらつきで文字の一部を抜き、ときどきチラつかせて
// 声がガラガラ・かすかすな感じを出す
Shader "AndoBoss/UIKasure"
{
    Properties
    {
        [PerRendererData] _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float2 sp : TEXCOORD1; };

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + 1), f.x), f.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                o.sp = v.vertex.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float a = tex2D(_MainTex, i.uv).a;
                // 0.12 秒ごとに模様を切り替えて、かすれがチラチラ動くようにする
                float t = floor(_Time.y * 8);
                // 横に長いかすれ筋
                float streak = noise(float2(i.sp.x * 0.02 + t * 3.1, i.sp.y * 0.55 + t * 1.7));
                // 細かいざらつき
                float grain = hash(floor(i.sp * 0.5) + t);
                float keep = smoothstep(0.28, 0.5, streak) * lerp(0.55, 1, grain);
                a *= saturate(keep * 1.4);
                return fixed4(i.color.rgb, a * i.color.a);
            }
            ENDCG
        }
    }
}
