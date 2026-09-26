// 画面全体のポストエフェクト（ビルトインの OnRenderImage 用）
// 0: ブルームの抽出  1: 縮小  2: 拡大して加算  3: 仕上げ（ブルーム合成・色収差・放射ブラー・ビネット・色調・フラッシュ）
Shader "Hidden/AndoBoss/Post"
{
    Properties { _MainTex ("", 2D) = "white" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex; float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    half _Threshold, _Knee, _BloomIntensity;
    half _Chroma, _Vignette, _Saturation, _Contrast, _Exposure, _RadialBlur;
    half4 _Flash, _Tint;

    half3 SampleBox(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-d, d).xxyy;
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
                + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag(v2f_img i) : SV_Target
            {
                half3 c = SampleBox(i.uv, 1);
                half br = max(c.r, max(c.g, c.b));
                half soft = clamp(br - _Threshold + _Knee, 0, 2 * _Knee);
                soft = soft * soft / (4 * _Knee + 1e-4);
                half w = max(soft, br - _Threshold) / max(br, 1e-4);
                return half4(c * w, 1);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag(v2f_img i) : SV_Target { return half4(SampleBox(i.uv, 1), 1); }
            ENDCG
        }

        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag(v2f_img i) : SV_Target { return half4(SampleBox(i.uv, 0.5), 1); }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 dc = uv - 0.5;
                half3 col;
                // 色収差（画面の端ほど強い）
                float2 off = dc * _Chroma * 0.02 * (0.4 + dot(dc, dc) * 3);
                col.r = tex2D(_MainTex, uv + off).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv - off).b;
                // 放射ブラー（元素爆発・ブレイクの瞬間）
                if (_RadialBlur > 0.001)
                {
                    half3 acc = col;
                    for (int k = 1; k < 8; k++)
                        acc += tex2Dlod(_MainTex, float4(uv - dc * _RadialBlur * 0.03 * k, 0, 0)).rgb;
                    col = acc / 8;
                }
                col += tex2D(_BloomTex, uv).rgb * _BloomIntensity;
                col *= _Exposure;
                // フィルム調のトーンマップ（明るいところが白く飛ばないようにする）
                col = saturate((col * (2.51 * col + 0.03)) / (col * (2.43 * col + 0.59) + 0.14));
                // 色調（彩度・コントラスト・色味）
                half l = dot(col, half3(0.299, 0.587, 0.114));
                col = lerp(l.xxx, col, _Saturation);
                col = (col - 0.5) * _Contrast + 0.5;
                col *= _Tint.rgb;
                half v = saturate(1 - dot(dc, dc) * _Vignette * 2.2);
                col *= lerp(0.25, 1, v);
                col = lerp(col, _Flash.rgb, _Flash.a);
                return half4(saturate(col), 1);
            }
            ENDCG
        }
    }
}
