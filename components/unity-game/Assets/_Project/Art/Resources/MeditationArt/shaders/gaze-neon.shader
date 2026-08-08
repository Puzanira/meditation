// The gaze circle as neon (founder, 2026-08-07: «круг взгляда крупнее и заметнее, неон-стилизация»).
//
// The old circle was three UGUI primitives — a translucent blue disc, a dashed ring sprite and a
// progress arc — and on the art plates the first two simply disappear: a 0.25-alpha fill over a
// photograph moves the pixel by a few values, and a 5 px dashed outline in #4a7096 is darker than
// half the backgrounds it has to be seen against. Making that combination «заметнее» by raising its
// alpha turns it into a grey plate over the level; the picture the founder asked for is LIGHT ADDED,
// not paint laid on.
//
// So the ring is generated here rather than drawn from a sprite: the fragment knows its distance from
// the quad's centre, which is the one thing a dashed-ring texture cannot tell it — a texture has to be
// re-authored for every radius, and the radius is now a slider. Three bands come out of that distance:
//
//   core   — a tight gaussian on the ring itself (the tube),
//   bloom  — a wide gaussian around it (the glass glow neon actually reads by),
//   inside — a faint wash within the circle, so the aim still has a BODY and the player can see what
//            is enclosed rather than only where the edge is.
//
// Additive blending (SrcAlpha One) for the same reason the detail sweep adds rather than tints: on a
// dark plate neon is the brightest thing in the frame, and on a light one it must not turn into a grey
// smear. Colour comes in as _Neon so the level's own palette can own it.
Shader "Meditation/GazeNeon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Neon ("Neon colour", Color) = (0.35, 0.87, 0.9, 1)

        // All three in fractions of the quad's half-size, so the material never needs to know how many
        // design pixels it was drawn at.
        _RingU ("Ring radius (0..0.5)", Range(0.05, 0.5)) = 0.36
        _RingWidthU ("Ring half-width", Range(0.002, 0.25)) = 0.035
        _Glow ("Glow", Range(0, 4)) = 1.2
        _InsideWash ("Inside wash", Range(0, 1)) = 0.10

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
        Blend SrcAlpha One
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
            fixed4 _Neon;
            float _RingU;
            float _RingWidthU;
            float _Glow;
            float _InsideWash;
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
                float2 p = IN.texcoord - 0.5;
                float r = length(p);

                float w = max(0.002, _RingWidthU);
                float d = (r - _RingU) / w;
                float core = exp(-d * d);

                // The bloom is the same band four times as wide and a quarter as strong: that ratio is
                // what makes a line read as a lit TUBE rather than as a fat stroke.
                float db = (r - _RingU) / (w * 4.0);
                float bloom = exp(-db * db) * 0.45;

                // …and the wash stops AT the ring, so the circle is a circle and not a blurred dot.
                float inside = _InsideWash * saturate((_RingU - r) / max(0.001, _RingU));

                float a = saturate((core + bloom) * _Glow + inside);

                half4 color = _Neon;
                color.a = a * IN.color.a;

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
