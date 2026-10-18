// Far backdrop (mountains, sky) on a band around the camera. Drawn first and without depth, so the
// whole scene draws over it; ignores fog, which would otherwise swallow anything this far away.
// The image repeats around the band, every other copy mirrored so the copies join without a seam.
Shader "KaiForest/Backdrop"
{
    Properties
    {
        _BaseMap ("Image", 2D) = "black" {}
        _Brightness ("Brightness", Float) = 1
        _MixColor ("Mix Color", Color) = (0, 0, 0, 1)
        _Mix ("Mix", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" "RenderType" = "Background" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Backdrop"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Brightness;
                half4 _MixColor;
                half _Mix;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // uv.x runs 0..copies around the band; fold it so odd copies are mirrored.
                float t = frac(i.uv.x * 0.5) * 2.0;
                float2 uv = float2(1.0 - abs(t - 1.0), i.uv.y);
                half3 c = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, uv, ddx(i.uv), ddy(i.uv)).rgb * _Brightness;
                return half4(lerp(c, _MixColor.rgb, _Mix), 1.0);
            }
            ENDHLSL
        }
    }
}
