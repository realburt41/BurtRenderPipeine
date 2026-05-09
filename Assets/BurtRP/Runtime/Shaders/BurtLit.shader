// Defines the Shader menu path for the first BurtRP lit material model.
Shader "BurtRP/Lit"
{
    // Defines material properties shown in Unity's Inspector.
    Properties
    {
        // Defines the surface color multiplied by BurtRP's simple diffuse lighting.
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }

    // Defines the runtime SubShader used by BurtRP.
    SubShader
    {
        // Marks this shader as a BurtRP opaque shader so materials are easy to identify.
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "BurtRenderPipeline" }

        // Defines the depth-only pass used by Burt Depth Prepass.
        Pass
        {
            // Names this pass for Frame Debugger readability.
            Name "Burt Lit Depth Only"

            // Matches BurtDepthPrepass because BurtRP looks for this LightMode.
            Tags { "LightMode" = "BurtDepthOnly" }

            // Disables color writes so this pass only affects CameraDepth.
            ColorMask 0

            // Enables depth writes so opaque lit objects can populate CameraDepth.
            ZWrite On

            // Uses less-equal depth testing, matching the forward color pass.
            ZTest LEqual

            // Starts the HLSL program for this pass.
            HLSLPROGRAM

            // Declares the depth vertex shader entry point.
            #pragma vertex VertDepth

            // Declares the depth fragment shader entry point.
            #pragma fragment FragDepth

            // Includes Unity helper functions such as UnityObjectToClipPos.
            #include "UnityCG.cginc"

            // Defines the mesh input needed for depth rendering.
            struct DepthAttributes
            {
                // Reads object-space vertex position from the mesh.
                float4 positionOS : POSITION;
            };

            // Defines the vertex-to-fragment data for depth rendering.
            struct DepthVaryings
            {
                // Stores clip-space position for rasterization.
                float4 positionCS : SV_POSITION;
            };

            // Converts object-space depth vertices into clip space.
            DepthVaryings VertDepth(DepthAttributes input)
            {
                // Creates the output structure that will be returned to the GPU pipeline.
                DepthVaryings output;

                // Transforms the object-space vertex position into clip-space position.
                output.positionCS = UnityObjectToClipPos(input.positionOS);

                // Returns the transformed vertex data.
                return output;
            }

            // Runs for each depth fragment even though ColorMask prevents color output.
            float4 FragDepth(DepthVaryings input) : SV_Target
            {
                // Returns a dummy color because this pass writes only depth.
                return 0;
            }

            // Ends the HLSL program for this pass.
            ENDHLSL
        }

        // Defines the shadow-caster pass used by Burt Draw Main Light Shadow Caster.
        Pass
        {
            // Names this pass for Frame Debugger readability.
            Name "Burt Lit Shadow Caster"

            // Uses Unity's standard ShadowCaster LightMode because ScriptableRenderContext.DrawShadows searches for this tag.
            Tags { "LightMode" = "ShadowCaster" }

            // Disables color writes because shadow maps only need depth.
            ColorMask 0

            // Enables depth writes so this pass can populate the main-light shadow map.
            ZWrite On

            // Uses less-equal depth testing, matching the other opaque depth passes.
            ZTest LEqual

            // Starts the HLSL program for this pass.
            HLSLPROGRAM

            // Declares the shadow vertex shader entry point.
            #pragma vertex VertShadow

            // Declares the shadow fragment shader entry point.
            #pragma fragment FragShadow

            // Includes Unity helper functions, including UnityObjectToClipPos which uses the current light view-projection matrix.
            #include "UnityCG.cginc"

            // Defines the mesh input needed for shadow rendering.
            struct ShadowAttributes
            {
                // Reads object-space vertex position from the mesh.
                float4 positionOS : POSITION;
            };

            // Defines the vertex-to-fragment data for shadow rendering.
            struct ShadowVaryings
            {
                // Stores clip-space position for shadow-map rasterization.
                float4 positionCS : SV_POSITION;
            };

            // Converts object-space vertices into the current light clip space.
            ShadowVaryings VertShadow(ShadowAttributes input)
            {
                // Creates the output structure that will be returned to the GPU pipeline.
                ShadowVaryings output;

                // Uses BurtDrawMainLightShadowCasterPass' light view-projection matrix to place this vertex in the shadow map.
                output.positionCS = UnityObjectToClipPos(input.positionOS);

                // Returns the transformed vertex data.
                return output;
            }

            // Runs for each shadow fragment even though ColorMask prevents color output.
            float4 FragShadow(ShadowVaryings input) : SV_Target
            {
                // Returns a dummy color because this pass writes only depth.
                return 0;
            }

            // Ends the HLSL program for this pass.
            ENDHLSL
        }

        // Defines the forward color pass used by Burt Draw Opaque and Burt Draw Transparent.
        Pass
        {
            // Names this pass for Frame Debugger readability.
            Name "Burt Lit Forward"

            // Matches BurtForward because BurtRP's main draw passes now only render this LightMode.
            Tags { "LightMode" = "BurtForward" }

            // Enables depth writes for opaque forward rendering.
            ZWrite On

            // Uses less-equal depth testing so pixels that match the prepass depth still draw.
            ZTest LEqual

            // Starts the HLSL program for this pass.
            HLSLPROGRAM

            // Declares the forward vertex shader entry point.
            #pragma vertex Vert

            // Declares the forward fragment shader entry point.
            #pragma fragment Frag

            // Includes Unity helper functions for transforms and normal conversion.
            #include "UnityCG.cginc"

            // Defines material constants in UnityPerMaterial so SRP Batcher can keep this shader compatible.
            CBUFFER_START(UnityPerMaterial)

                // Stores the material base color selected in the Inspector.
                float4 _BaseColor;

            // Ends the material constant buffer.
            CBUFFER_END

            // Stores the world-space direction from the shaded point toward the main light.
            float4 _BurtMainLightDirection;

            // Stores the main light color uploaded by Burt Setup Lighting.
            float4 _BurtMainLightColor;

            // Stores the ambient light color uploaded by Burt Setup Lighting.
            float4 _BurtAmbientLightColor;

            // Declares the main-light shadow map texture uploaded by Burt Allocate Main Light Shadow Map.
            UNITY_DECLARE_SHADOWMAP(_BurtMainLightShadowMap);

            // Stores the matrix that converts world-space positions into main-light shadow-map coordinates.
            float4x4 _BurtMainLightWorldToShadow;

            // Stores how strongly the main-light shadow should affect diffuse lighting.
            float _BurtMainLightShadowStrength;

            // Defines a small depth bias used while sampling the shadow map to reduce simple self-shadowing acne.
            static const float BurtMainLightShadowSampleBias = 0.001f;

            // Defines the mesh input needed by the lit forward pass.
            struct Attributes
            {
                // Reads object-space vertex position from the mesh.
                float4 positionOS : POSITION;

                // Reads object-space vertex normal from the mesh.
                float3 normalOS : NORMAL;
            };

            // Defines the data passed from the vertex shader to the fragment shader.
            struct Varyings
            {
                // Stores clip-space position for rasterization.
                float4 positionCS : SV_POSITION;

                // Stores world-space normal for diffuse lighting.
                float3 normalWS : TEXCOORD0;

                // Stores the projected main-light shadow coordinate for this vertex.
                float4 shadowCoord : TEXCOORD1;
            };

            // Transforms mesh vertices and normals for the lit forward pass.
            Varyings Vert(Attributes input)
            {
                // Creates the output structure that will be returned to the GPU pipeline.
                Varyings output;

                // Transforms the object-space vertex position into clip-space position.
                output.positionCS = UnityObjectToClipPos(input.positionOS);

                // Transforms the object-space vertex position into world space for shadow projection.
                float4 positionWS = mul(unity_ObjectToWorld, input.positionOS);

                // Transforms the world-space position into main-light shadow-map coordinate space.
                output.shadowCoord = mul(_BurtMainLightWorldToShadow, positionWS);

                // Transforms the object-space normal into world space and normalizes it.
                output.normalWS = normalize(UnityObjectToWorldNormal(input.normalOS));

                // Returns the transformed vertex data.
                return output;
            }

            // Samples the main-light shadow map and returns 1 for lit pixels or a lower value for shadowed pixels.
            float SampleMainLightShadow(float4 shadowCoord)
            {
                // Skips shadow sampling when the pipeline uploaded zero shadow strength.
                if (_BurtMainLightShadowStrength <= 0.0001f)
                {
                    // Returns fully lit attenuation because the current request has no active main-light shadow.
                    return 1.0f;
                }

                // Divides by w to convert homogeneous shadow coordinates into regular texture coordinates.
                float3 projectedShadowCoord = shadowCoord.xyz / max(shadowCoord.w, 0.00001f);

                // Detects pixels outside the shadow-map UV/depth range so they do not incorrectly become dark.
                bool outsideShadowMap = projectedShadowCoord.x <= 0.0f || projectedShadowCoord.x >= 1.0f || projectedShadowCoord.y <= 0.0f || projectedShadowCoord.y >= 1.0f || projectedShadowCoord.z <= 0.0f || projectedShadowCoord.z >= 1.0f;

                // Handles pixels outside the shadow projection as fully lit.
                if (outsideShadowMap)
                {
                    // Returns fully lit attenuation because this pixel is outside the current shadow map.
                    return 1.0f;
                }

                // Applies a tiny receiver-side depth bias before comparing against the shadow map.
                projectedShadowCoord.z = saturate(projectedShadowCoord.z - BurtMainLightShadowSampleBias);

                // Samples the shadow map using Unity's comparison-sampler macro, returning 1 when visible and 0 when blocked.
                float rawShadow = UNITY_SAMPLE_SHADOW(_BurtMainLightShadowMap, projectedShadowCoord);

                // Blends between fully lit and sampled shadow according to the light's Shadow Strength value.
                return lerp(1.0f, rawShadow, saturate(_BurtMainLightShadowStrength));
            }

            // Computes a simple Lambert diffuse color plus ambient light.
            float4 Frag(Varyings input) : SV_Target
            {
                // Normalizes the interpolated world-space normal before lighting.
                float3 normalWS = normalize(input.normalWS);

                // Normalizes the global main-light direction uploaded by Burt Setup Lighting.
                float3 lightDirectionWS = normalize(_BurtMainLightDirection.xyz);

                // Computes classic Lambert diffuse intensity and clamps it to the visible range.
                float diffuseTerm = saturate(dot(normalWS, lightDirectionWS));

                // Samples main-light shadow attenuation for the current world-space fragment.
                float shadowAttenuation = SampleMainLightShadow(input.shadowCoord);

                // Applies shadow attenuation only to the direct diffuse term so ambient light remains visible in shadowed areas.
                float shadowedDiffuseTerm = diffuseTerm * shadowAttenuation;

                // Multiplies base color by main light color and the shadowed Lambert intensity.
                float3 diffuseColor = _BaseColor.rgb * _BurtMainLightColor.rgb * shadowedDiffuseTerm;

                // Multiplies base color by ambient color to keep shadowed sides visible.
                float3 ambientColor = _BaseColor.rgb * _BurtAmbientLightColor.rgb;

                // Adds ambient and diffuse lighting into the final color.
                float3 finalColor = ambientColor + diffuseColor;

                // Returns the lit color and preserves the material alpha value.
                return float4(finalColor, _BaseColor.a);
            }

            // Ends the HLSL program for this pass.
            ENDHLSL
        }
    }

    // Disables fallback so BurtRP shader errors do not silently use another pipeline shader.
    Fallback Off
}
