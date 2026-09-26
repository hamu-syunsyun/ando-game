// ボスの攻撃予告（赤い円・帯）。_Fill で内側が満ちていき、満ちたら着弾
Shader "AndoBoss/Telegraph"
{
    Properties
    {
        _Color ("Color", Color) = (1,0.25,0.25,1)
        _Fill ("Fill", Range(0,1)) = 0
        _Shape ("Shape 0=circle 1=rect", Float) = 0
        _Alpha ("Alpha", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color; half _Fill, _Shape, _Alpha;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float a;
                if (_Shape < 0.5)
                {
                    float d = length(p);
                    if (d > 1) discard;
                    float edge = smoothstep(0.9, 0.97, d) * (1 - smoothstep(0.97, 1, d));
                    float fill = step(d, _Fill) * 0.35;
                    float front = smoothstep(_Fill - 0.06, _Fill, d) * step(d, _Fill) * 0.6;
                    float stripes = (sin((p.x + p.y) * 28 - _Time.y * 6) * 0.5 + 0.5) * 0.08;
                    a = edge + fill + front + 0.12 + stripes;
                }
                else
                {
                    // 帯：uv.y 方向に伸びる。_Fill はボスから先端へ満ちる
                    float ex = abs(p.x);
                    float edge = smoothstep(0.86, 0.95, ex);
                    float fill = step(i.uv.y, _Fill) * 0.35;
                    float arrows = step(0.7, frac(i.uv.y * 6 - abs(p.x) * 0.5 - _Time.y * 2)) * 0.2;
                    a = edge * 0.9 + fill + 0.1 + arrows;
                }
                return fixed4(_Color.rgb * 1.6, saturate(a) * _Alpha);
            }
            ENDCG
        }
    }
}
