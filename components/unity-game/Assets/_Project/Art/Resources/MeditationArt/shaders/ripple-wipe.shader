// Переход «круглая рябь» (founder, плейтест 2026-09-22, п.8).
//
// «Победа → наезд камеры на ведёрко → круглая рябь → заставка следующего уровня; тем же переходом
// рябь входит в следующий экран.» What replaces the 0.3 s cut to black the flow has used between
// every pair of screens since it was built — a black fade is the absence of a transition, and this
// game is about a bucket of water.
//
// It is a WIPE, not a distortion. A UGUI Image cannot read the pixels behind it (there is no grab
// pass in the 2D URP overlay canvas this game draws into), so nothing here bends the picture; what
// it draws is the water itself — a disc that closes over the frame from the vessel outwards, whose
// FRONT is a wobbling circle rather than a circle, with a band of crests riding just inside it. Past
// the crests it is flat, and flat is the point: at _Progress = 1 the frame is a solid colour and the
// screen underneath can be swapped without anybody seeing it happen.
//
// Aspect is corrected in the fragment (_Aspect = w/h) so the front is a CIRCLE on a 16:9 frame and
// not an ellipse — «круглая рябь» is the whole of the founder's word for it.
Shader "Meditation/RippleWipe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Water", Color) = (0.05,0.12,0.16,1)
        _CrestColor ("Crest", Color) = (0.56,0.86,0.86,1)

        _Centre ("Centre (UV)", Vector) = (0.5,0.5,0,0)
        _Progress ("Progress 0..1", Range(0,1)) = 0
        _Aspect ("Aspect w/h", Float) = 1.7777778
        _RingWidth ("Ring width (UV)", Float) = 0.14
        _Amplitude ("Front wobble (UV)", Float) = 0.035
        _Waves ("Crests in the ring", Float) = 3
        _Feather ("Front feather (UV)", Float) = 0.012

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
            fixed4 _CrestColor;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float4 _Centre;
            float _Progress;
            float _Aspect;
            float _RingWidth;
            float _Amplitude;
            float _Waves;
            float _Feather;

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
                // Distance from the vessel, in units where one unit is the frame's HEIGHT — so the
                // front is round rather than squashed into the 16:9 box.
                float2 p = float2((IN.texcoord.x - _Centre.x) * _Aspect, IN.texcoord.y - _Centre.y);
                float r = length(p);

                // The far corner of the frame from wherever the vessel is: the radius _Progress = 1
                // has to reach, or the transition ends with the corners still showing the old screen.
                float2 far = float2(max(_Centre.x, 1.0 - _Centre.x) * _Aspect,
                                    max(_Centre.y, 1.0 - _Centre.y));
                float reach = length(far) + _RingWidth;

                // The front, wobbling: a circle plus a few crests around its own angle. Both terms are
                // scaled by _Progress at the very start so the ripple LEAVES the vessel as a point and
                // does not pop into existence as a ragged blob.
                float angle = atan2(p.y, p.x);
                float ramp = saturate(_Progress * 6.0);
                float front = _Progress * reach
                            + _Amplitude * ramp * sin(angle * max(1.0, _Waves) + _Progress * 9.0);

                // Inside the front is water; the feather is the wet edge, one blend wide.
                float inside = smoothstep(front + _Feather, front - _Feather, r);

                // …and the crests: concentric bands riding the inner side of the front, dying out
                // towards the centre so the middle of the disc is calm water and not a target.
                float behind = saturate((front - r) / max(1e-4, _RingWidth));
                float crest = sin(behind * max(1.0, _Waves) * 6.2831853 - _Progress * 12.0);
                crest = saturate(crest) * (1.0 - behind) * inside;

                half4 water = _Color;
                water.rgb = lerp(water.rgb, _CrestColor.rgb, crest * 0.55);
                water.a = inside;

                half4 color = water * IN.color.a;
                color.rgb = water.rgb;

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
