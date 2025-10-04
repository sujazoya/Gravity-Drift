Shader "Custom/PointLightCookiePreview"
{
    Properties
    {
        _Color("Base Color", Color) = (1,1,1,1)
        _LightPos("Light Position", Vector) = (0,0,0,0)
        _LightRange("Light Range", Float) = 10
        _LightIntensity("Light Intensity", Float) = 1
        _CookieCube("Cookie Cubemap", Cube) = "" {}
        _CookieBias("Cookie Bias", Float) = 0.01
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            float4 _LightPos;
            float _LightRange;
            float _LightIntensity;
            samplerCUBE _CookieCube;
            float _CookieBias;
            fixed4 _Color;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Direction from light to fragment
                float3 dir = normalize(i.worldPos - _LightPos.xyz);

                // Distance attenuation
                float distance = length(i.worldPos - _LightPos.xyz);
                float att = saturate(1.0 - distance / _LightRange);

                // Sample cubemap cookie with bias
                float3 sampleDir = dir;
                fixed4 cookieSample = texCUBE(_CookieCube, sampleDir);
                cookieSample.rgb = pow(cookieSample.rgb, 2.2); // gamma correction
                float cookieFactor = cookieSample.r - _CookieBias;
                cookieFactor = saturate(cookieFactor);

                fixed4 final = _Color * cookieFactor * att * _LightIntensity;
                return final;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
