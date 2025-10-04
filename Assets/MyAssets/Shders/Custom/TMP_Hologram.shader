Shader "TextMeshPro/Hologram SDF Pro"
{
    Properties
    {
        _FaceColor ("Face Color", Color) = (0, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0,1)) = 0.1

        _GlowIntensity ("Glow Intensity", Float) = 2
        _Transparency ("Transparency", Range(0,1)) = 0.7
        _ScanlineDensity ("Scanline Density", Float) = 200
        _GlitchStrength ("Glitch Strength", Float) = 0.02

        _ColorShiftSpeed ("Color Shift Speed", Float) = 0.5
        _DistortionStrength ("Distortion Strength", Float) = 0.02
        _DistortionSpeed ("Distortion Speed", Float) = 1.0

        _DepthFadeDistance ("Depth Fade Distance", Float) = 5.0

        _NoiseTex ("Noise Texture (optional)", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend One One
        Cull Off
        Lighting Off
        ZWrite Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _NoiseTex;

            float4 _FaceColor;
            float4 _OutlineColor;
            float _OutlineWidth;

            float _GlowIntensity;
            float _Transparency;
            float _ScanlineDensity;
            float _GlitchStrength;

            float _ColorShiftSpeed;
            float _DistortionStrength;
            float _DistortionSpeed;
            float _DepthFadeDistance;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

fixed4 frag (v2f i) : SV_Target
{
    // Safe UV distortion
    float2 uv = i.uv;
    float n = tex2D(_NoiseTex, uv * 2 + _Time.y * _DistortionSpeed).r;
    uv += (n - 0.5) * _DistortionStrength;

    // Clamp UV to avoid atlas bleeding
    uv = clamp(uv, 0.001, 0.999);

    // Correct TMP SDF sampling
    float4 texCol = tex2D(_MainTex, uv);
    float sdf = texCol.a;

    // Smooth edge (fixes blocky look)
    float softness = fwidth(sdf) * 1.5;  // dynamic softness
    float face = smoothstep(0.5 - softness, 0.5 + softness, sdf);
    float outline = smoothstep(0.5 - _OutlineWidth - softness, 0.5 - softness, sdf);

    // Base colors
    float3 col = _FaceColor.rgb * face + _OutlineColor.rgb * outline;

    // Scanlines
    float scan = sin(i.uv.y * _ScanlineDensity + _Time.y * 20) * 0.05 + 0.95;

    // Glow + color shift
    float glowPulse = 0.5 + 0.5 * sin(_Time.y * 3);
    float3 shiftColor = lerp(_FaceColor.rgb, float3(0.5, 0.2, 1.0), (sin(_Time.y * _ColorShiftSpeed) * 0.5 + 0.5));

    col = shiftColor * (face + outline) * (scan + glowPulse);

    // Depth fade
    float camDist = distance(_WorldSpaceCameraPos, i.worldPos);
    float depthFade = saturate(1 - camDist / _DepthFadeDistance);

    // Final alpha
    float alpha = (face + outline) * _Transparency * depthFade;

    return float4(col * _GlowIntensity, alpha);
}

            ENDCG
        }
    }
}
