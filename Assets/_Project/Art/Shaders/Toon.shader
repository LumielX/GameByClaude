// BOO-TRAYAL toon look (GDD 11: low-poly toon + outline): three light bands from the main light with shadows,
// soft additional lights (lamps, flashlights), SH ambient, a rim and an inverted-hull outline.
Shader "BooTrayal/Toon"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Shade ("Shadow band", Range(0, 1)) = 0.42
        _RimColor ("Rim (alpha = strength)", Color) = (1, 0.95, 0.9, 0.25)
        _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _OutlineColor ("Outline", Color) = (0.07, 0.047, 0.11, 1)
        _OutlineWidth ("Outline width (m)", Float) = 0.02
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Shade;
            half4 _RimColor;
            half4 _EmissionColor;
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half Band(half ndl)
            {
                return ndl > 0.45h ? 1.0h : (ndl > 0.05h ? 0.72h : _Shade);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = dot(n, mainLight.direction) * mainLight.shadowAttenuation;
                half3 baseColor = _BaseColor.rgb;
                half3 color = baseColor * mainLight.color * Band(ndl);
                color += baseColor * SampleSH(n) * 0.8h;

                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    half d = saturate(dot(n, light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
                    color += baseColor * light.color * (d > 0.2h ? d * 1.2h : d * 0.5h);
                LIGHT_LOOP_END
                #endif

                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0h - saturate(dot(n, viewDir)), 3.0h);
                color += _RimColor.rgb * _RimColor.a * rim;
                color += _EmissionColor.rgb;
                color = MixFog(color, i.fog);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                return TransformWorldToHClip(positionWS + normalWS * _OutlineWidth);
            }

            half4 Frag() : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack Off
}
