// Neon rim around a collectable detail, [toggle], generated from the sprite's OWN alpha — no art was
// drawn for it (founder, 2026-08-07: «неон-обводка деталей … кодом, без отрисовки арта», so she can
// compare it against the pulse and the light sweep and switch the losers off).
//
// Same trick as Meditation/ThoughtBacking and for the same reason: the mask is the sprite's alpha, so
// it is free, it is exactly the drawn shape, and it works on the plane's contrail and on the
// librarian's wire-frame glasses alike. The difference is one line — the dilated alpha has the
// sprite's own alpha SUBTRACTED from it, so what is left is the rim outside the ink instead of a
// glowing copy of the whole detail. That subtraction is why this can be drawn ON TOP of the sprite,
// and drawing on top is what lets the rim be a CHILD of the detail's Image: it then inherits the
// position, the pulse scale, the flight along the thread and the collected-and-hidden state for free,
// where a sibling would have to be walked through every one of those by hand.
//
// Additive (SrcAlpha One) — neon on a darkened plate is added light. _SpreadUV arrives in UV for the
// same reason the sweep's band does: the details run from a 40 px paperclip to a 484 px contrail, and
// one texel radius would be half of the first and a twentieth of the second.
//
// The quad is drawn BIGGER than the sprite (_PadUV) and the sampling is remapped back into it, because
// a rim is by definition outside the ink and a quad that stops at the ink's own edge has nowhere to put
// it. Until 2026-08-08 the rim was simply cut off by straight lines along the sprite's border — the
// slippers of the office lost their bottom and both ends (design skeptic). Two halves to the fix and
// both are needed: the pad gives the rim room, and every tap that lands outside the sprite answers ZERO
// instead of the clamped edge texel, or a sprite touching its own border would smear its edge row
// outwards into a turquoise skirt.
Shader "Meditation/DetailOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Neon ("Neon colour", Color) = (0.56, 0.84, 0.84, 1)
        _SpreadUV ("Dilation, UV", Vector) = (0.02, 0.02, 0, 0)
        _PadUV ("Padding of the quad, share of it", Vector) = (0, 0, 0, 0)
        _UvRect ("Sprite's UV rect (min, size)", Vector) = (0, 0, 1, 1)
        _OutlineStrength ("Strength", Range(0, 1)) = 0.8

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
            float4 _SpreadUV;
            float4 _PadUV;
            float4 _UvRect;
            float _OutlineStrength;
            float4 _ClipRect;
            float4 _MainTex_ST;

            // Alpha of the sprite at a point of its OWN 0..1 box — zero outside it, so the pad around
            // the quad is empty air and not the clamped edge texel repeated.
            half SpriteAlpha(float2 s)
            {
                if (s.x < 0 || s.x > 1 || s.y < 0 || s.y > 1) return 0;
                return tex2D(_MainTex, _UvRect.xy + s * _UvRect.zw).a;
            }

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
                float2 r = _SpreadUV.xy;
                float2 h = r * 0.5;
                float2 d = r * 0.70710678;

                // Where this pixel of the PADDED quad falls on the sprite: 0..1 across the drawing,
                // negative or above one in the padding around it.
                float2 quad = (IN.texcoord - _UvRect.xy) / max(_UvRect.zw, 1e-6);
                float2 uv = (quad - _PadUV.xy) / max(1.0 - 2.0 * _PadUV.xy, 1e-6);

                half own = SpriteAlpha(uv);

                // Twelve taps, the ring-plus-half-ring of ThoughtBacking: eight at the full radius keep
                // the rim continuous around a corner, four at half of it stop a thin sprite (the gull
                // is 28 px tall) from beading into dots.
                half a = own;
                a = max(a, SpriteAlpha(uv + float2( r.x, 0)));
                a = max(a, SpriteAlpha(uv + float2(-r.x, 0)));
                a = max(a, SpriteAlpha(uv + float2(0,  r.y)));
                a = max(a, SpriteAlpha(uv + float2(0, -r.y)));
                a = max(a, SpriteAlpha(uv + float2( d.x,  d.y)));
                a = max(a, SpriteAlpha(uv + float2( d.x, -d.y)));
                a = max(a, SpriteAlpha(uv + float2(-d.x,  d.y)));
                a = max(a, SpriteAlpha(uv + float2(-d.x, -d.y)));
                a = max(a, SpriteAlpha(uv + float2( h.x,  h.y)));
                a = max(a, SpriteAlpha(uv + float2( h.x, -h.y)));
                a = max(a, SpriteAlpha(uv + float2(-h.x,  h.y)));
                a = max(a, SpriteAlpha(uv + float2(-h.x, -h.y)));

                // The rim is what the dilation gained: everything the sprite already covers is left to
                // the sprite. Half-transparent art (the moon's halo, the cloud) therefore glows a
                // little through itself, which is what a neon tube behind frosted glass does.
                half rim = saturate(a - own);

                half4 color = _Neon;
                color.a = rim * _OutlineStrength * IN.color.a;

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
