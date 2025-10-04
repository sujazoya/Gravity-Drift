Shader "Custom/Hologram"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (0, 1, 1, 1)
        _GlowIntensity ("Glow Intensity", Float) = 2
        _Transparency ("Transparency", Range(0,1)) = 0.6
        _ScanlineDensity ("Scanline Density", Float) = 200
        _GlitchStrength ("Glitch Strength", Float) = 0.02
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _TintColor;
            float _GlowIntensity;
            float _Transparency;
            float _ScanlineDensity;
            float _GlitchStrength;
            float4 _MainTex_ST;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float rand(float2 co) {
                return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target {
                // Glitch UV
                float glitch = (rand(float2(floor(_Time.y*20), i.uv.y)) - 0.5) * _GlitchStrength;
                float2 uv = i.uv + float2(glitch, 0);

                // Base texture
                fixed4 col = tex2D(_MainTex, uv) * _TintColor;

                // Scanlines
                float scan = sin(i.uv.y * _ScanlineDensity + _Time.y * 20) * 0.1 + 0.9;

                // Transparency + glow
                col.rgb *= _GlowIntensity * scan;
                col.a = _Transparency * scan;

                return col;
            }
            ENDHLSL
        }
    }
}
