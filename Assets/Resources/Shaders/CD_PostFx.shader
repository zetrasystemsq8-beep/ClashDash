Shader "CLASHDASH/PostFx"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        // Pass 0: bright-pass (keeps only the glowing parts of the image)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half _Threshold;

            half4 frag (v2f_img i) : SV_Target
            {
                half3 c = tex2D(_MainTex, i.uv).rgb;
                half br = max(c.r, max(c.g, c.b));
                half contrib = max(0.0, br - _Threshold) / max(br, 0.0001);
                return half4(c * contrib, 1.0);
            }
            ENDCG
        }

        // Pass 1: horizontal blur
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            half _BlurSize;

            half4 frag (v2f_img i) : SV_Target
            {
                float2 off = float2(_MainTex_TexelSize.x * _BlurSize, 0.0);
                half3 c = tex2D(_MainTex, i.uv).rgb * 0.2270270270;
                c += tex2D(_MainTex, i.uv + off * 1.3846153846).rgb * 0.3162162162;
                c += tex2D(_MainTex, i.uv - off * 1.3846153846).rgb * 0.3162162162;
                c += tex2D(_MainTex, i.uv + off * 3.2307692308).rgb * 0.0702702703;
                c += tex2D(_MainTex, i.uv - off * 3.2307692308).rgb * 0.0702702703;
                return half4(c, 1.0);
            }
            ENDCG
        }

        // Pass 2: vertical blur
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            half _BlurSize;

            half4 frag (v2f_img i) : SV_Target
            {
                float2 off = float2(0.0, _MainTex_TexelSize.y * _BlurSize);
                half3 c = tex2D(_MainTex, i.uv).rgb * 0.2270270270;
                c += tex2D(_MainTex, i.uv + off * 1.3846153846).rgb * 0.3162162162;
                c += tex2D(_MainTex, i.uv - off * 1.3846153846).rgb * 0.3162162162;
                c += tex2D(_MainTex, i.uv + off * 3.2307692308).rgb * 0.0702702703;
                c += tex2D(_MainTex, i.uv - off * 3.2307692308).rgb * 0.0702702703;
                return half4(c, 1.0);
            }
            ENDCG
        }

        // Pass 3: composite (bloom + tone mapping + grading + vignette)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BloomTex;
            half _BloomIntensity;
            half _Exposure;
            half _Contrast;
            half _Saturation;
            half _Vignette;
            half4 _Tint;

            half4 frag (v2f_img i) : SV_Target
            {
                half3 col = tex2D(_MainTex, i.uv).rgb;
                col += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity;
                col *= _Exposure;

                // Filmic tone mapping (ACES approximation)
                col = (col * (2.51 * col + 0.03)) / (col * (2.43 * col + 0.59) + 0.14);
                col = saturate(col);

                // Saturation and contrast
                half l = dot(col, half3(0.299, 0.587, 0.114));
                col = lerp(half3(l, l, l), col, _Saturation);
                col = (col - 0.5) * _Contrast + 0.5;
                col *= _Tint.rgb;

                // Vignette
                float2 d = i.uv - 0.5;
                half v = 1.0 - dot(d, d) * _Vignette * 2.0;
                col *= saturate(v);

                return half4(saturate(col), 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
