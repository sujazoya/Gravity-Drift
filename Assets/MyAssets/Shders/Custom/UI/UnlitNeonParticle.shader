Shader "Custom/UnlitNeonParticle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0,0.9,1,1)
        _RimColor ("Rim Color", Color) = (0.3,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 2
        _PulseSpeed ("Pulse Speed", Range(0,5)) = 1
        _PulseStrength ("Pulse Strength", Range(0,1)) = 0.25
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        LOD 100
        Pass
        {
            ZWrite Off
            Blend One One // additive
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Color;
            float4 _RimColor;
            float _RimPower;
            float _PulseSpeed;
            float _PulseStrength;
            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv * 2.0 - 1.0;
                float dist = length(uv);
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float rim = pow(saturate(1.0 - dist), _RimPower);
                float pulse = 1.0 + _PulseStrength * sin(_Time.y * _PulseSpeed + dist * 10.0);
                float4 col = (_Color * IN.color) * tex.a * pulse;
                col.rgb += _RimColor.rgb * rim * tex.a * pulse;
                col.a = tex.a * IN.color.a;
                return col;
            }
            ENDHLSL
        }
    }
}
