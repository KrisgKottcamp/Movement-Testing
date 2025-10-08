Shader "Sprites/OutlineBruise"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Outline
        _OutlinePx ("Outline Width (px)", Float) = 1
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.1
        _OutlineAlpha ("Outline Alpha", Range(0,1)) = 1.0

        // Bruise Å® color gradient
        _ColorA ("Low-Bruise Color", Color) = (0.1,0.9,0.2,1)
        _ColorB ("High-Bruise Color", Color) = (1,0.2,0.2,1)
        _Bruise01 ("Bruise 0..1", Range(0,1)) = 0

        // Flash when near break
        _FlashOn ("Flash On (0/1)", Float) = 0
        _FlashSpeed ("Flash Speed (Hz)", Float) = 8
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f {
                float4 vertex      : SV_POSITION;
                fixed4 color       : COLOR;
                float2 uv          : TEXCOORD0;
                float4 texelSize   : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;

            float _OutlinePx;
            float _AlphaThreshold;
            float _OutlineAlpha;

            fixed4 _ColorA;
            fixed4 _ColorB;
            float  _Bruise01;

            float  _FlashOn;
            float  _FlashSpeed;
            fixed4 _FlashColor;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex    = UnityObjectToClipPos(v.vertex);
                o.uv        = v.uv;
                o.color     = v.color * _Color;
                o.texelSize = _MainTex_TexelSize;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c  = tex2D(_MainTex, i.uv) * i.color;

                // sample neighborhood at pixel radius
                float px = max(1.0, _OutlinePx);
                float2 t = i.texelSize.xy;
                float2 o = float2(t.x * px, t.y * px);

                float aC = c.a;
                float aN = 0.0;
                aN = max(aN, tex2D(_MainTex, i.uv + float2( o.x,  0)).a);
                aN = max(aN, tex2D(_MainTex, i.uv + float2(-o.x,  0)).a);
                aN = max(aN, tex2D(_MainTex, i.uv + float2( 0,   o.y)).a);
                aN = max(aN, tex2D(_MainTex, i.uv + float2( 0,  -o.y)).a);
                aN = max(aN, tex2D(_MainTex, i.uv + float2( o.x,  o.y)).a);
                aN = max(aN, tex2D(_MainTex, i.uv + float2(-o.x,  o.y)).a);
                aN = max(aN, tex2D(_MainTex, i.uv + float2( o.x, -o.y)).a);
                aN = max(aN, tex2D(_MainTex, i.uv + float2(-o.x, -o.y)).a);

                // outline where current is transparent and any neighbor is filled
                float outlineMask = step(_AlphaThreshold, aN) * (1.0 - step(_AlphaThreshold, aC));

                // bruise Å® color
                fixed4 baseOutline = lerp(_ColorA, _ColorB, saturate(_Bruise01));

                // flash when near break
                float  pulse = 0.5 + 0.5 * sin(_Time.y * _FlashSpeed * 6.2831853);
                fixed4 flashCol = lerp(baseOutline, _FlashColor, pulse);
                fixed4 finalOutline = lerp(baseOutline, flashCol, saturate(_FlashOn));

                finalOutline.a *= _OutlineAlpha;

                // composite: sprite + (outside) outline
                fixed4 outCol = c;
                outCol.rgb = lerp(outCol.rgb, finalOutline.rgb, outlineMask * finalOutline.a);
                outCol.a   = max(outCol.a,     outlineMask * finalOutline.a);
                return outCol;
            }
            ENDCG
        }
    }
}
