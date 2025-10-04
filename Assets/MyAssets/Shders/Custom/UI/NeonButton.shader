// NeonButtonShader.shader
Shader "Custom/NeonButton"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.1, 0.1, 0.3, 1)
        _NeonColor ("Neon Color", Color) = (0, 0.8, 1, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1
        _GlowSpeed ("Glow Speed", Range(0, 5)) = 2
        _BorderThickness ("Border Thickness", Range(0, 0.1)) = 0.02
        _ScanlineSpeed ("Scanline Speed", Range(0, 10)) = 2
        _NoiseStrength ("Noise Strength", Range(0, 0.1)) = 0.02
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float3 worldPos : TEXCOORD1;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _BaseColor;
            float4 _NeonColor;
            float _GlowIntensity;
            float _GlowSpeed;
            float _BorderThickness;
            float _ScanlineSpeed;
            float _NoiseStrength;
            
            // Simple noise function for holographic effect
            float noise(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Base texture
                fixed4 col = tex2D(_MainTex, i.uv) * _BaseColor;
                
                // Calculate distance from center for border effect
                float2 center = float2(0.5, 0.5);
                float distFromCenter = distance(i.uv, center);
                float border = smoothstep(0.5 - _BorderThickness, 0.5, distFromCenter);
                
                // Pulsing glow effect
                float glowPulse = (sin(_Time.y * _GlowSpeed) + 1.0) * 0.5;
                float glowIntensity = _GlowIntensity * (0.7 + 0.3 * glowPulse);
                
                // Scanline effect
                float scanline = sin((i.uv.y + _Time.y * _ScanlineSpeed) * 50) * 0.1 + 0.9;
                
                // Holographic noise
                float holographicNoise = noise(i.uv + _Time.x) * _NoiseStrength;
                
                // Combine neon effects
                float neonEffect = border * glowIntensity * scanline + holographicNoise;
                fixed4 neonGlow = _NeonColor * neonEffect;
                
                // Final color combination
                col.rgb += neonGlow.rgb;
                col.a = max(col.a, neonGlow.a * 0.3);
                
                return col * i.color;
            }
            ENDCG
        }
    }
    
    FallBack "UI/Default"
}