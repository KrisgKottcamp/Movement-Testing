Shader "Sprites/OutlineBruiseInner"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Outline (inner)
        _OutlinePx ("Outline Width (px)", Range(1,3)) = 1
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.55
        _OutlineAlpha ("Outline Alpha", Range(0,1)) = 1.0
        _UseDiagonals ("Use Diagonal Neighbors (thicker)", Float) = 0
        _SolidifyNeighbors ("Require solid neighbors (kills fringe specks)", Float) = 1

        // Bruise → color gradient
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

            struct appdata_t { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 texelSize:TEXCOORD1; };

            sampler2D _MainTex; float4 _MainTex_TexelSize; fixed4 _Color;

            float _OutlinePx, _AlphaThreshold, _OutlineAlpha, _UseDiagonals, _SolidifyNeighbors;
            fixed4 _ColorA, _ColorB; float _Bruise01;
            float _FlashOn, _FlashSpeed; fixed4 _FlashColor;

            inline float A(float2 uv){ return tex2D(_MainTex, uv).a; }

            v2f vert (appdata_t v){
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; o.texelSize=_MainTex_TexelSize; return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c  = tex2D(_MainTex, i.uv) * i.color;
                float  aC = c.a;

                float2 tex = i.texelSize.xy;

                // --- NEW: "solidify" requires at least one solid 4-way neighbor at r=1 ---
                float isSolidA = step(_AlphaThreshold, aC);
                float2 o1 = float2(tex.x, tex.y);
                float sE1 = step(_AlphaThreshold, A(i.uv + float2( o1.x, 0)));
                float sW1 = step(_AlphaThreshold, A(i.uv + float2(-o1.x, 0)));
                float sN1 = step(_AlphaThreshold, A(i.uv + float2(0,  o1.y)));
                float sS1 = step(_AlphaThreshold, A(i.uv + float2(0, -o1.y)));
                float neighborSolid4 = max(max(sE1, sW1), max(sN1, sS1));

                float isSolid = lerp(isSolidA, isSolidA * neighborSolid4, _SolidifyNeighbors);
                if (isSolid < 0.5) return c;   // outside/isolated → no tint, no specks

                // ---- Inner edge detection (4-way thin by default; diagonals optional) ----
                float hasEmpty = 0.0;

                [unroll] for (int r=1; r<=3; r++)
                {
                    float active = step((float)r - 0.5, _OutlinePx);
                    if (active < 0.5) continue;

                    float2 o = float2(tex.x * r, tex.y * r);

                    // 4-way
                    float sE = step(_AlphaThreshold, A(i.uv + float2( o.x, 0)));
                    float sW = step(_AlphaThreshold, A(i.uv + float2(-o.x, 0)));
                    float sN = step(_AlphaThreshold, A(i.uv + float2(0,  o.y)));
                    float sS = step(_AlphaThreshold, A(i.uv + float2(0, -o.y)));
                    float allCardSolid = sE * sW * sN * sS;
                    float emptyCard    = 1.0 - allCardSolid;

                    float emptyDiag = 0.0;
                    if (_UseDiagonals > 0.5)
                    {
                        float sNE = step(_AlphaThreshold, A(i.uv + float2( o.x,  o.y)));
                        float sNW = step(_AlphaThreshold, A(i.uv + float2(-o.x,  o.y)));
                        float sSE = step(_AlphaThreshold, A(i.uv + float2( o.x, -o.y)));
                        float sSW = step(_AlphaThreshold, A(i.uv + float2(-o.x, -o.y)));
                        float allDiagSolid = sNE * sNW * sSE * sSW;
                        emptyDiag = 1.0 - allDiagSolid;
                    }

                    hasEmpty = max(hasEmpty, max(emptyCard, emptyDiag));
                }

                float innerMask = isSolid * saturate(hasEmpty);

                // ---- Color: bruise gradient + optional flash ----
                fixed4 baseCol = lerp(_ColorA, _ColorB, saturate(_Bruise01));
                float  pulse   = 0.5 + 0.5 * sin(_Time.y * _FlashSpeed * 6.2831853);
                fixed4 flashCol= lerp(baseCol, _FlashColor, pulse);
                fixed4 finalCol= lerp(baseCol, flashCol, saturate(_FlashOn));
                finalCol.a *= _OutlineAlpha;

                fixed4 outCol = c;
                outCol.rgb = lerp(outCol.rgb, finalCol.rgb, innerMask * finalCol.a);
                return outCol;
            }
            ENDCG
        }
    }
}
