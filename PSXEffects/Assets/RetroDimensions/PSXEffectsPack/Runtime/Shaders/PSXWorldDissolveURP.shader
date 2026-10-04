Shader "RetroDimensions/PSX Effects/World Dissolve URP"
{
    Properties
    {
        [MainTexture] _BaseMap("Surface Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Surface Colour", Color) = (0.5,0.65,0.8,1)
        _NoiseMap("Pixel Dissolve Mask", 2D) = "white" {}
        _Dissolve("Dissolve", Range(0,1)) = 0
        _EdgeWidth("Edge Width", Range(0.001,0.3)) = 0.1
        _EdgeColor("Edge Colour", Color) = (0.3,0.8,1,1)
        _TintColor("Override Colour", Color) = (1,1,1,1)
        _TintAmount("Colour Override", Range(0,1)) = 0
        [HideInInspector] _EffectOpacity("Effect Opacity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Name "PSXWorldDissolve"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EdgeColor;
                half4 _TintColor;
                float _Dissolve;
                float _EdgeWidth;
                float _TintAmount;
                float _EffectOpacity;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 maskUV : TEXCOORD1;
                half shade : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.maskUV = input.uv;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float light = saturate(dot(normalize(normalWS), normalize(float3(0.4,0.8,-0.3))));
                output.shade = 0.45h + floor(light * 3.0) / 3.0 * 0.55h;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_EffectOpacity - 0.001);
                clip(0.9999 - _Dissolve);
                float mask = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.maskUV).r;
                clip(mask + 0.0001 - _Dissolve);
                half4 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(colour.a - 0.1);
                colour.rgb *= input.shade;
                half edge = (1.0 - step(_Dissolve + _EdgeWidth, mask)) * step(0.0001, _Dissolve);
                colour.rgb = lerp(colour.rgb, _EdgeColor.rgb, edge);
                half luminance = dot(colour.rgb, half3(0.2126,0.7152,0.0722));
                colour.rgb = lerp(colour.rgb, luminance * _TintColor.rgb, _TintAmount);
                colour.rgb = MixFog(colour.rgb, input.fogFactor);
                colour.a = 1;
                return colour;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
