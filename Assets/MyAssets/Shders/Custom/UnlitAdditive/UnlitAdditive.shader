Shader "Custom/UnlitAdditive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.0,0.8,1.0,1)
        _Intensity ("Intensity", Range(0,10)) = 2
        _Softness("Softness", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        Cull Off
        ZWrite Off
        Blend One One   // additive
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Color;
            float _Intensity;
            float _Softness;

            struct appdata_t { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata_t v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 t = tex2D(_MainTex, i.uv);
                // apply softness by raising alpha a bit (gives brighter center)
                float alpha = pow(t.a, 1.0 - _Softness);
                fixed4 col = _Color * t * _Intensity;
                col.a = alpha * _Color.a;
                return col;
            }
            ENDCG
        }
    }
}
