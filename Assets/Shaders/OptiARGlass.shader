// Clear glass for the lenses: looks through the lens at what is behind it (the camera feed and the bench)
// and bends that view by the surface slope, so a convex lens magnifies and a concave lens shrinks.
// Edges reflect the surroundings (Fresnel) and catch a highlight from the main light.
// Needs "Opaque Texture" on in the URP asset.
Shader "OptiAR/Glass"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.86, 0.95, 1, 1)
        _Refraction ("Refraction strength", Range(0, 0.4)) = 0.12
        _FresnelPower ("Fresnel power", Range(0.5, 8)) = 3
        _Reflectivity ("Edge reflection", Range(0, 1)) = 0.7
        _Highlight ("Highlight", Range(0, 4)) = 1.5
        _EdgeColor ("Ground edge colour", Color) = (0.55, 0.75, 0.62, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Glass"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Refraction;
                half _FresnelPower;
                half _Reflectivity;
                half _Highlight;
                half4 _EdgeColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.screenPos = ComputeScreenPos(position.positionCS);
                output.normalOS = input.normalOS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normal);

                // a surface tilted away from the view shifts what is seen through it:
                // outward-tilted edges (convex) pull the view inwards = magnify, inward-tilted (concave) push it out = shrink
                float2 uv = input.screenPos.xy / input.screenPos.w;
                uv -= normalVS.xy * _Refraction;
                half3 behind = SampleSceneColor(saturate(uv)) * _Tint.rgb;

                // glass always reflects ~4% head-on, much more at grazing angles
                half fresnel = 0.04 + pow(1.0 - saturate(dot(normal, view)), _FresnelPower) * _Reflectivity;
                half3 reflection = GlossyEnvironmentReflection(reflect(-view, normal), 0.03, 1.0);

                Light light = GetMainLight();
                float3 halfway = normalize(light.direction + view);
                half highlight = pow(saturate(dot(normal, halfway)), 200.0) * _Highlight;

                half3 color = lerp(behind, reflection, saturate(fresnel)) + highlight * light.color;

                // the rim of a real lens is ground glass: frosted and slightly green (the glass's own colour)
                half rim = smoothstep(0.75, 0.97, 1.0 - abs(normalize(input.normalOS).x));
                color = lerp(color, _EdgeColor.rgb * (0.45 + 0.55 * Luminance(behind)) + reflection * 0.15, rim * 0.85);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
