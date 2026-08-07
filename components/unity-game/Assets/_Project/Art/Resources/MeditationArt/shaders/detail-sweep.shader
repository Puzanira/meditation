// Луч-подсветка деталей (SCREENS «Детали в сцене», заказ founder 2026-08-07).
//
// «Светит по спрайтам деталей, а не по фону» is the whole requirement, and it is why this is a shader
// rather than a white quad sliding across the screen: a quad lights the plate, and the plate is meant
// to stay dark (the drop of 2026-08-07 darkened all five by ×0.49 precisely so the objects would read).
// Multiplying the band by the sprite's own ALPHA is what makes the light land on the object and stop
// at its edge — the mask is the detail itself, for free, whatever shape it is.
//
// The band's position arrives in UV rather than in pixels, for the same reason ThoughtBacking's radius
// does: the details run from a 40 px paperclip to a 484 px contrail, and one pixel width would be half
// the paperclip and a twentieth of the plane. DetailSweep computes the band in design px and LevelView
// converts it into each detail's own UV space, so a band 320 px wide is 320 px wide on every sprite.
//
// _SweepSlant tilts the band («мягкая наклонная полоса света»): the band's x is shifted by the
// fragment's height inside the sprite, so the light leans instead of standing straight up.
Shader "Meditation/DetailSweep"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SweepU ("Sweep centre (UV)", Float) = -1
        _SweepWidthU ("Sweep width (UV)", Float) = 0.5
        _SweepStrength ("Sweep strength", Range(0,1)) = 0
        _SweepSlant ("Sweep slant (UV)", Float) = 0.25

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
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _SweepU;
            float _SweepWidthU;
            float _SweepStrength;
            float _SweepSlant;

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
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // The band, leaning, in this sprite's own UV. A soft falloff (1 - d²)² rather than a
                // hard edge: «мягкая полоса света», and a hard-edged one on a 40 px paperclip is a
                // white rectangle rather than a highlight.
                float halfWidth = max(_SweepWidthU, 1e-4);
                float centre = _SweepU + (IN.texcoord.y - 0.5) * _SweepSlant;
                float d = saturate(abs(IN.texcoord.x - centre) / halfWidth);
                float band = (1.0 - d * d);
                band = band * band;

                // The light is added flat and left for the BLEND to mask: `Blend SrcAlpha
                // OneMinusSrcAlpha` already multiplies everything this returns by color.a, so the band
                // stops at the object's edge and never touches the plate behind it — for free, and
                // exactly once.
                //
                // It used to be multiplied by color.a here as well, «so a soft-edged sprite gets a
                // soft-edged highlight», and that squared the mask: what reached the screen was
                // strength × band × α². On solid art the second mask does nothing (α = 1), which is
                // why it survived a drop — but half this game's details are thin or translucent: the
                // librarian's glasses are antialiased wire with translucent lenses, the city's cloud
                // and the moon's halo are see-through blobs. At α ≈ 0.36 they were getting ×0.13 of
                // the light where the spec promises ×0.36, and «облако у столба» came off the frame at
                // +26 units of 255 against +25…+115 on the solid details (it reads +32 now). SCREENS
                // asks for «маска по их альфе» — one mask, not two.
                color.rgb += _SweepStrength * band;

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
