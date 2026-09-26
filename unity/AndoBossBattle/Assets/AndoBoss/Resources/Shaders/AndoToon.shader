// 原神っぽいセル調シェーダー（ビルトインレンダーパイプライン用）
// 3段階の陰影・リムライト・自己発光・被弾時の白フラッシュ・アウトライン
Shader "AndoBoss/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _ShadeColor ("Shade Color", Color) = (0.62,0.6,0.78,1)
        _RimColor ("Rim Color", Color) = (1,1,1,0.55)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _SpecColor2 ("Spec Color", Color) = (1,1,1,0.25)
        _Emission ("Emission", Color) = (0,0,0,0)
        _Flash ("Flash", Range(0,1)) = 0
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0.11,0.1,0.17,1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color, _ShadeColor, _RimColor, _SpecColor2, _Emission, _FlashColor;
            sampler2D _MainTex; float4 _MainTex_ST;
            half _RimPower, _Flash;
            // 画風（0 = アニメ調, 1 = リアル調）。スクリプトから全体に設定する
            float _AndoRealism;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wn : TEXCOORD1;
                float3 wp : TEXCOORD2;
                SHADOW_COORDS(3)
                UNITY_FOG_COORDS(4)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                float3 n = normalize(i.wn);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                float3 h = normalize(l + v);
                float atten = SHADOW_ATTENUATION(i);
                float3 sh = ShadeSH9(float4(n, 1));

                // --- アニメ調：3段階のセル陰影＋リムライト ---
                float ndl = dot(n, l) * 0.5 + 0.5;
                float lit = ndl * lerp(0.35, 1, atten);
                float band = smoothstep(0.46, 0.5, lit) * 0.55 + smoothstep(0.7, 0.74, lit) * 0.45;
                float3 shade = lerp(_ShadeColor.rgb, 1, band);
                float3 toon = albedo.rgb * shade * (_LightColor0.rgb * 0.7 + 0.2);
                toon += albedo.rgb * sh * 0.35;
                toon += _SpecColor2.rgb * _SpecColor2.a * smoothstep(0.93, 0.95, dot(n, h)) * atten;
                float rim = pow(1 - saturate(dot(n, v)), _RimPower);
                rim = smoothstep(0.35, 0.6, rim) * (0.45 + 0.55 * ndl);
                toon += _RimColor.rgb * _RimColor.a * rim * 0.6;

                // --- リアル調：なめらかな陰影＋つや＋弱いふち光 ---
                float nl = saturate(dot(n, l));
                float wrap = saturate((dot(n, l) + 0.2) / 1.2);
                float3 real = albedo.rgb * (_LightColor0.rgb * wrap * lerp(0.25, 1, atten) * 0.9 + sh * 0.75);
                float gloss = pow(saturate(dot(n, h)), 40) * 0.35 + pow(saturate(dot(n, h)), 8) * 0.06;
                real += _LightColor0.rgb * gloss * atten * (0.5 + 0.5 * nl);
                float fres = pow(1 - saturate(dot(n, v)), 4);
                real += sh * fres * 0.25;

                float3 col = lerp(toon, real, _AndoRealism);
                col += _Emission.rgb * _Emission.a * 1.6;
                col = lerp(col, _FlashColor.rgb * 1.1, _Flash);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            fixed4 _OutlineColor, _FlashColor;
            half _OutlineWidth, _Flash;
            float _AndoRealism;
            struct v2f { float4 pos : SV_POSITION; UNITY_FOG_COORDS(0) };
            v2f vert(appdata_base v)
            {
                v2f o;
                float w = _OutlineWidth * (1 - _AndoRealism * 0.75);
                float3 p = v.vertex.xyz + normalize(v.normal) * w;
                o.pos = UnityObjectToClipPos(float4(p, 1));
                if (_OutlineWidth <= 0.0001) o.pos = float4(0, 0, -2, 1);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 c = lerp(_OutlineColor.rgb, _FlashColor.rgb, _Flash * 0.6);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return fixed4(c, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            struct v2f { V2F_SHADOW_CASTER; };
            v2f vert(appdata_base v) { v2f o; TRANSFER_SHADOW_CASTER_NORMALOFFSET(o) return o; }
            float4 frag(v2f i) : SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
    Fallback Off
}
