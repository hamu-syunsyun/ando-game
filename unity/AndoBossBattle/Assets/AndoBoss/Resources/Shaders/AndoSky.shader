// グラデーションの空。太陽・雲・星をすべて計算で描く
// _Storm を 0→1 にすると、第2形態の雷雲の空に変わる
Shader "AndoBoss/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.2,0.5,0.95,1)
        _Horizon ("Horizon", Color) = (0.72,0.87,1,1)
        _Bottom ("Bottom", Color) = (0.85,0.93,1,1)
        _StormTop ("Storm Top", Color) = (0.08,0.03,0.18,1)
        _StormHorizon ("Storm Horizon", Color) = (0.55,0.22,0.6,1)
        _SunDir ("Sun Dir", Vector) = (0.4,0.55,0.35,0)
        _Storm ("Storm", Range(0,1)) = 0
        _Flash ("Lightning Flash", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Top, _Horizon, _Bottom, _StormTop, _StormHorizon;
            float4 _SunDir; half _Storm, _Flash;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1,0)), f.x), lerp(hash(i + float2(0,1)), hash(i + 1), f.x), f.y);
            }
            float fbm(float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { s += noise(p) * a; p *= 2.03; a *= 0.5; }
                return s;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float3 top = lerp(_Top.rgb, _StormTop.rgb, _Storm);
                float3 hor = lerp(_Horizon.rgb, _StormHorizon.rgb, _Storm);
                float3 col = y > 0 ? lerp(hor, top, pow(saturate(y), 0.55)) : lerp(hor, _Bottom.rgb * (1 - _Storm * 0.7), saturate(-y * 4));

                // 太陽
                float3 sd = normalize(_SunDir.xyz);
                float sun = saturate(dot(d, sd));
                col += float3(1, 0.9, 0.7) * (pow(sun, 900) * 6 + pow(sun, 18) * 0.35) * (1 - _Storm);

                // 雲（上半分だけ）
                if (y > 0)
                {
                    float2 uv = d.xz / (y + 0.12) * 0.9;
                    float t = _Time.y * (0.02 + _Storm * 0.08);
                    float c = fbm(uv * 1.3 + float2(t, t * 0.4));
                    c = smoothstep(0.5 - _Storm * 0.2, 0.78, c) * saturate(y * 5);
                    float3 cloudCol = lerp(float3(1, 1, 1), float3(0.25, 0.14, 0.35), _Storm);
                    col = lerp(col, cloudCol + _Flash * float3(0.7, 0.6, 1), c * 0.85);
                    // 星（嵐のときだけ）
                    float st = step(0.997, hash(floor(d.xz / (y + 0.3) * 160)));
                    col += st * _Storm * 0.8 * (1 - c);
                }
                col += _Flash * float3(0.5, 0.4, 0.9) * 0.6;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
