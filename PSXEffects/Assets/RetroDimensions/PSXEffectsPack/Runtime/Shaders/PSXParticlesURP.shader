Shader "RetroDimensions/PSX Effects/Particles URP"
{
    Properties
    {
        [MainTexture] _BaseMap("Particle Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1,1,1,1)
        _TintColor("Override Colour", Color) = (1,0.45,0.1,1)
        _TintAmount("Colour Override", Range(0,1)) = 0
        [HideInInspector] _EffectOpacity("Effect Opacity", Range(0,1)) = 1
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.025
        _AlphaSteps("Opacity Steps", Range(1,16)) = 6
        [Toggle] _DitherAlpha("Dither Opacity", Float) = 0
        _FogStrength("Scene Fog", Range(0,1)) = 1
        [HideInInspector] _FogToBlack("Additive Fog", Float) = 0
        [HideInInspector] _SrcBlend("Source Blend", Float) = 5
        [HideInInspector] _DstBlend("Destination Blend", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }
        Pass
        {
            Name "PSXParticles"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend]
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _TintColor;
                float _TintAmount;
                float _EffectOpacity;
                float _Cutoff;
                float _AlphaSteps;
                float _DitherAlpha;
                float _FogStrength;
                float _FogToBlack;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color * _BaseColor;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color;
                half luminance = dot(colour.rgb, half3(0.2126, 0.7152, 0.0722));
                colour.rgb = lerp(colour.rgb, luminance * _TintColor.rgb, _TintAmount);
                colour.a *= lerp(1.0h, _TintColor.a, _TintAmount) * _EffectOpacity;
                float steps = max(1.0, floor(_AlphaSteps + 0.5));
                colour.a = floor(saturate(colour.a) * steps + 0.5) / steps;
                clip(colour.a - max(_Cutoff, 0.0001));

                if (_DitherAlpha > 0.5)
                {
                    // Bayer 4x4, expressed without dynamically indexed arrays.
                    float2 pixel = floor(input.positionCS.xy);
                    float2 low = fmod(pixel, 2.0);
                    float2 high = fmod(floor(pixel / 2.0), 2.0);
                    float threshold = (4.0 * (2.0 * low.x + 3.0 * low.y - 4.0 * low.x * low.y)
                        + (2.0 * high.x + 3.0 * high.y - 4.0 * high.x * high.y) + 0.5) / 16.0;
                    clip(colour.a - threshold);
                    colour.a = 1.0h;
                }

                half3 fogColour = lerp(unity_FogColor.rgb, half3(0,0,0), _FogToBlack);
                half3 fogged = MixFogColor(colour.rgb, fogColour, input.fogFactor);
                colour.rgb = lerp(colour.rgb, fogged, _FogStrength);
                return colour;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
