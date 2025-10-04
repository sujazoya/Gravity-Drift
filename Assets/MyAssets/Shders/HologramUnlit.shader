Shader "Custom/HologramUnlit"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _NoiseTex("Noise Tex", 2D) = "white" {}
        _Color ("Base Color", Color) = (0.0,0.85,1.0,1)
        _EdgeColor("Edge Glow", Color) = (0.5,1.0,1.0,1)
        _ScanLineIntensity ("Scan intensity", Range(0,2)) = 0.6
        _ScanSpeed ("Scan speed", Range(0,10)) = 2.0
        _NoiseStrength ("Noise strength", Range(0,1)) = 0.15
        _Distortion ("Distortion", Range(0,0.1)) = 0.02
        _FresnelPower("Fresnel Power", Range(0.1,8)) = 2.0
        _RGBShift ("RGB Shift", Range(0,8)) = 1.5
        _Glow("Glow Intensity", Range(0,3)) = 1.2
        _MainTex_ST("MainTex ST", Vector) = (1,1,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _EdgeColor;
            float _ScanLineIntensity;
            float _ScanSpeed;
            float _NoiseStrength;
            float _Distortion;
            float _FresnelPower;
            float _RGBShift;
            float _Glow;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float3 normal : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float4 sampleRGBShift(float2 uv, float shift)
            {
                float3 col;
                col.r = tex2D(_MainTex, uv + float2(shift,0)).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv - float2(shift,0)).b;
                return float4(col, 1);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Time
                float t = _Time.y;

                // vertical scanning wave distortion
                float wave = sin((uv.y * 30.0) + t * _ScanSpeed) * _Distortion;
                uv.x += wave;

                // sample noise for flicker
                float2 noiseUV = uv * 4.0 + float2(t * 0.2, t * 0.1);
                float noise = tex2D(_NoiseTex, noiseUV).r;

                // scanline (thin horizontal bright/dim lines)
                float scan = sin(uv.y * 800.0 + t * _ScanSpeed * 6.0) * 0.5 + 0.5;
                float scanEffect = lerp(1.0, scan, _ScanLineIntensity);

                // RGB shift amount modulated by noise
                float shift = (_RGBShift * 0.001) * (0.5 + noise * 0.5);

                // sample with RGB shift
                float4 baseColor = sampleRGBShift(uv, shift);

                // Apply color tint
                baseColor.rgb *= _Color.rgb;

                // Edge fresnel (glow)
                float3 viewDir = normalize(_WorldSpaceCameraPos - IN.worldPos);
                float fres = pow(1.0 - saturate(dot(viewDir, normalize(IN.normal))), _FresnelPower);
                float4 edge = _EdgeColor * fres * _Glow;

                // Combine base + edge
                float3 color = baseColor.rgb * scanEffect + edge.rgb;

                // alpha from main texture luminance & noise flicker
                float luminance = dot(baseColor.rgb, float3(0.299,0.587,0.114));
                float alpha = saturate(luminance * (0.9 + noise * _NoiseStrength));
                // small pulsing
                alpha *= (0.85 + 0.15 * sin(t * 3.0));

                // final color boost
               color = pow(color, float3(1.0 / 1.1, 1.0 / 1.1, 1.0 / 1.1)); // explicit


                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Unlit/Transparent"
}
