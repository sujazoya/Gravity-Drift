Shader "Unlit/URPNeonParticle" {
    Properties {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0, 1, 1, 1)
    }
    SubShader {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass {
            Blend SrcAlpha One
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float4 _BaseColor;

            Varyings vert (Attributes v) {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return tex * _BaseColor;
            }
            ENDHLSL
        }
    }
}
