Shader "Custom/NeonArena"
{
    Properties
    {
        _MainTex ("Base Color", 2D) = "white" {}
        _GridTex ("Grid Mask", 2D) = "white" {}
        _Tint ("Tint", Color) = (0.0, 0.6, 1.0, 1.0)
        _EmissionGain ("Emission Gain", Float) = 1.0
        _GridScrollSpeed ("Grid Scroll Speed", Float) = 0.1
        _RimPower ("Rim Power", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Attributes {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct Varyings {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            sampler2D _GridTex;
            float4 _Tint;
            float _EmissionGain;
            float _GridScrollSpeed;
            float _RimPower;
            float4 _MainTex_ST;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 gridUV = i.uv;
                gridUV.y += _Time.y * _GridScrollSpeed;
                float4 baseCol = tex2D(_MainTex, i.uv) * _Tint;

                float grid = tex2D(_GridTex, gridUV).r;
                float rim = pow(saturate(1.0 - dot(normalize(i.worldNormal), normalize(_WorldSpaceCameraPos - i.worldPos))), _RimPower);
                float emission = (_EmissionGain * (0.8 * grid + 0.2 * rim));

                float3 outCol = baseCol.rgb + emission * _Tint.rgb;
                return float4(outCol, 1.0);
            }
            ENDHLSL
        }
    }
}
