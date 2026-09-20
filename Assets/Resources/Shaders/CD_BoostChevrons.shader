Shader "CLASHDASH/BoostChevrons"
{
    Properties
    {
        _Color ("Chevron Color", Color) = (0.25, 1, 0.8, 1)
        _BaseColor ("Base Color", Color) = (0.02, 0.1, 0.12, 1)
        _Intensity ("Intensity", Range(0, 8)) = 2.5
        _Speed ("Scroll Speed", Float) = 1.6
        _Repeat ("Repeat", Float) = 3
        _Sharpness ("Chevron Sharpness", Float) = 0.9
    }

    SubShader
    {
        Tags { "Queue" = "Geometry+1" "RenderType" = "Opaque" }

        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _BaseColor;
            float _Intensity;
            float _Speed;
            float _Repeat;
            float _Sharpness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float side = abs(i.uv.x - 0.5);
                float c = frac(i.uv.y * _Repeat + side * _Sharpness * 2.0 - _Time.y * _Speed);
                float m = smoothstep(0.0, 0.1, c) * (1.0 - smoothstep(0.3, 0.45, c));
                float edge = smoothstep(0.42, 0.5, side);

                fixed3 rgb = lerp(_BaseColor.rgb, _Color.rgb * _Intensity, saturate(m + edge * 0.6));
                fixed4 col = fixed4(rgb, 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    FallBack "Unlit/Color"
}
