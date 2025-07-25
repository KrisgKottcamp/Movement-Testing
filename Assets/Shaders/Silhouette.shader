Shader "Custom/Silhouette"
{
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Sprite Texture", 2D) = "white" {}
    }
    SubShader {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float2 uv       : TEXCOORD0;
                float4 vertex   : SV_POSITION;
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _Color;

            v2f vert(appdata_t v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                // sample the spriteÅfs alpha
                fixed4 src = tex2D(_MainTex, i.uv);
                // output only our _Color, using src.a to mask
                return fixed4(_Color.rgb, src.a * _Color.a);
            }
            ENDCG
        }
    }
}
