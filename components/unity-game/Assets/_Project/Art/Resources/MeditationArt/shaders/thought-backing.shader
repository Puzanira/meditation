// A light backing UNDER the marker hatching (art drop 2026-08-05).
//
// The drop turned the thoughts from turquoise silhouettes into black cross-hatch on transparent, and
// measured on the rendered frames black ink lands at 1.6–3.5:1 against the plates (median), with
// 40–97 % of the ink below 3:1 — worst on the L4 embankment (1.60) and on the L1/L3 dark halves.
// The walkthrough's own note asks for exactly this fix: «если мысли сливаются, решать подложкой /
// обводкой, а не переделкой арта».
//
// A tint cannot do it — a UGUI tint MULTIPLIES, and nothing multiplies black into something light.
// So the backing is a second draw of the SAME sprite through this shader: a flat light colour whose
// alpha is the sprite's alpha DILATED by a few design pixels. That gives a light halo hugging every
// stroke rather than a card behind the thought, which matters twice: SCREENS keeps thoughts «без
// обведённой рамки», and the screen under the defeat wallpaper has to stay «дырявым» (S5 counts the
// loss by sprite rectangles precisely because the scene shows between the strokes).
//
// The dilation radius arrives as _SpreadUV — UV, not texels — because the caller is the only one who
// knows the sprite's on-screen scale, and because _MainTex_TexelSize describes the MATERIAL's texture
// while a UGUI Image binds its sprite through the CanvasRenderer instead.
Shader "Meditation/ThoughtBacking"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Backing ("Backing colour", Color) = (0.949, 0.941, 0.918, 1)
        _SpreadUV ("Dilation, UV", Vector) = (0.01, 0.01, 0, 0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _Backing;
            float4 _SpreadUV;
            float4 _ClipRect;
            float4 _MainTex_ST;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Dilation: the widest alpha within _SpreadUV of this pixel. Twelve taps — a ring of
                // eight at the full radius plus a diagonal ring at half of it, which is what keeps a
                // hatch line from beading into dots when the sprite is drawn five times down.
                float2 r = _SpreadUV.xy;
                float2 h = r * 0.5;
                float2 d = r * 0.70710678;
                float2 uv = IN.texcoord;

                half a = tex2D(_MainTex, uv).a;
                a = max(a, tex2D(_MainTex, uv + float2( r.x, 0)).a);
                a = max(a, tex2D(_MainTex, uv + float2(-r.x, 0)).a);
                a = max(a, tex2D(_MainTex, uv + float2(0,  r.y)).a);
                a = max(a, tex2D(_MainTex, uv + float2(0, -r.y)).a);
                a = max(a, tex2D(_MainTex, uv + float2( d.x,  d.y)).a);
                a = max(a, tex2D(_MainTex, uv + float2( d.x, -d.y)).a);
                a = max(a, tex2D(_MainTex, uv + float2(-d.x,  d.y)).a);
                a = max(a, tex2D(_MainTex, uv + float2(-d.x, -d.y)).a);
                a = max(a, tex2D(_MainTex, uv + float2( h.x,  h.y)).a);
                a = max(a, tex2D(_MainTex, uv + float2( h.x, -h.y)).a);
                a = max(a, tex2D(_MainTex, uv + float2(-h.x,  h.y)).a);
                a = max(a, tex2D(_MainTex, uv + float2(-h.x, -h.y)).a);

                half4 color = _Backing;
                color.a = _Backing.a * a * IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
