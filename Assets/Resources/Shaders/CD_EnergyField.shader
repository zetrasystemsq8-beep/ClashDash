Shader "CLASHDASH/EnergyField"
{
    Properties
    {
        _Color ("Primary Color", Color) = (0.2, 0.9, 1, 1)
        _Color2 ("Secondary Color", Color) = (0.7, 0.4, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 2
        _Alpha ("Alpha", Range(0, 1)) = 0.6
        _ScanDensity ("Scan Density", Float) = 18
        _ScanSpeed ("Scan Speed", Float) = 0.5
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _Color2;
            float _Intensity;
            float _Alpha;
            float _ScanDensity;
            float _ScanSpeed;
            float _FresnelPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fres = pow(1.0 - saturate(abs(dot(normalize(i.worldNormal), viewDir))), _FresnelPower);
                float scan = 0.5 + 0.5 * sin(i.worldPos.y * _ScanDensity - _Time.y * _ScanSpeed * 6.2831853);
                float swirl = 0.5 + 0.5 * sin((i.uv.x + i.uv.y) * 12.0 + _Time.y * 1.5);

                fixed3 tint = lerp(_Color.rgb, _Color2.rgb, saturate(scan * 0.6 + swirl * 0.4));
                float glow = 0.35 + fres * 1.4 + scan * 0.35;

                fixed4 col;
                col.rgb = tint * glow * _Intensity;
                col.a = saturate(0.25 + fres + scan * 0.25) * _Alpha;
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return col;
            }
            ENDCG
        }
    }

    FallBack Off
}
