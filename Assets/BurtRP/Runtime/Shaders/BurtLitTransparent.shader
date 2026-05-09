// Defines a separate shader menu entry so transparent Lit materials do not modify or depend on BurtLit.shader.
Shader "BurtRP/Lit Transparent"
{
    // Defines material properties shown in Unity's Inspector for transparent Lit materials.
    Properties
    {
        // Defines the main albedo texture sampled by the transparent forward pass from mesh UV0.
        _BaseMap ("Base Map", 2D) = "white" {}

        // Stores the tint color and alpha used by the transparent forward pass.
        _BaseColor ("Base Color", Color) = (1, 1, 1, 0.5)
    }

    // Defines the runtime SubShader that BurtRP should select for this material.
    SubShader
    {
        // Marks this shader as transparent, puts it in Unity's Transparent queue, and binds it to BurtRenderPipeline.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "BurtRenderPipeline" }

        // Defines the transparent forward color pass consumed by BurtRP draw passes using LightMode BurtForward.
        Pass
        {
            // Names this pass for Unity's Frame Debugger and material pass inspection.
            Name "Burt Lit Transparent Forward"

            // Matches BurtForward because BurtRP's forward draw code filters renderers by this LightMode.
            Tags { "LightMode" = "BurtForward" }

            // Disables depth writes so transparent surfaces do not block later transparent layers behind them.
            ZWrite Off

            // Keeps normal depth testing so transparent pixels still respect opaque geometry already in the depth buffer.
            ZTest LEqual

            // Uses standard alpha blending where source alpha controls how much of the lit color is composited over the destination.
            Blend SrcAlpha OneMinusSrcAlpha

            // Starts the HLSL program for this transparent forward pass.
            HLSLPROGRAM

            // Declares Vert as the vertex shader entry point for this pass.
            #pragma vertex Vert

            // Declares Frag as the fragment shader entry point for this pass.
            #pragma fragment Frag

            // BurtLighting.hlsl 里包含 reflection probe 的显式 LOD 采样函数，即使透明路径暂时不调用也保持 target 能力一致。
            #pragma target 3.0

            // Includes Unity transform helpers such as UnityObjectToClipPos, UnityObjectToWorldNormal, and TRANSFORM_TEX.
            #include "UnityCG.cginc"

            // Includes BurtRP common helper functions such as BurtSafeNormalize.
            #include "ShaderLibrary/BurtCommon.hlsl"

            // Includes BurtRP surface/input data structures such as BurtSurfaceData and BurtCreateSurfaceData.
            #include "ShaderLibrary/BurtInput.hlsl"

            // Includes BurtRP lighting helpers such as BurtCreateMainLight and BurtEvaluateSimpleLit.
            #include "ShaderLibrary/BurtLighting.hlsl"

            // Includes BurtRP shadow receiver helpers such as BurtTransformWorldToMainLightShadow and BurtSampleMainLightShadow.
            #include "ShaderLibrary/BurtShadows.hlsl"

            // Defines per-material constants in UnityPerMaterial so the SRP Batcher can group compatible materials efficiently.
            CBUFFER_START(UnityPerMaterial)

                // Stores the material color tint and transparent alpha selected in the Inspector.
                float4 _BaseColor;

                // Stores Unity-generated Base Map tiling in xy and offset in zw for BurtTransformBaseMapUV.
                float4 _BaseMap_ST;

            // Ends the per-material constant buffer used by Unity's SRP Batcher.
            CBUFFER_END

            // Defines the mesh input data required by the transparent Lit forward pass.
            struct Attributes
            {
                // Reads the object-space vertex position from the mesh POSITION stream.
                float4 positionOS : POSITION;

                // Reads the object-space vertex normal from the mesh NORMAL stream for diffuse lighting.
                float3 normalOS : NORMAL;

                // Reads the first UV channel from the mesh so the transparent pass can sample the Base Map.
                float2 uv : TEXCOORD0;
            };

            // Defines the interpolated data passed from the vertex shader to the fragment shader.
            struct Varyings
            {
                // Stores clip-space position for GPU rasterization.
                float4 positionCS : SV_POSITION;

                // Stores Base Map UVs after applying material tiling and offset from _BaseMap_ST.
                float2 baseMapUV : TEXCOORD0;

                // Stores world-space normal for BurtRP's Lambert lighting helper.
                float3 normalWS : TEXCOORD1;

                // Stores the projected main-light shadow coordinate for shadow receiver sampling.
                float4 shadowCoord : TEXCOORD2;
            };

            // Transforms mesh data into the spaces needed by the transparent Lit fragment shader.
            Varyings Vert(Attributes input)
            {
                // Creates the output structure that will be filled and returned to the GPU pipeline.
                Varyings output;

                // Converts the object-space vertex position into clip space for rasterization.
                output.positionCS = UnityObjectToClipPos(input.positionOS);

                // Converts the object-space vertex position into world space for BurtRP shadow projection.
                float4 positionWS = mul(unity_ObjectToWorld, input.positionOS);

                // Projects the world-space position into the main-light shadow map using the shared BurtRP helper.
                output.shadowCoord = BurtTransformWorldToMainLightShadow(positionWS);

                // Converts the object-space normal into world space and normalizes it for stable lighting.
                output.normalWS = normalize(UnityObjectToWorldNormal(input.normalOS));

                // Applies the material Base Map tiling and offset to mesh UV0 for fragment texture sampling.
                output.baseMapUV = BurtTransformBaseMapUV(input.uv, _BaseMap_ST);

                // Returns all interpolators needed by the transparent Lit fragment shader.
                return output;
            }

            // Computes BurtRP's Lit lighting model and outputs alpha suitable for transparent blending.
            float4 Frag(Varyings input) : SV_Target
            {
                // Normalizes the interpolated world-space normal with BurtRP's shared safe-normalize helper.
                float3 normalWS = BurtSafeNormalize(input.normalWS);

                // Samples the Base Map with transformed mesh UV0 and multiplies it by the transparent material tint.
                float4 baseColor = BurtSampleBaseMap(input.baseMapUV) * _BaseColor;

                // Builds BurtRP surface data from the texture-tinted transparent base color.
                BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor);

                // Samples main-light shadow attenuation through the shared BurtRP shadow receiver helper.
                float shadowAttenuation = BurtSampleMainLightShadow(input.shadowCoord);

                // Builds BurtRP's current main-light data using global light values and the sampled shadow attenuation.
                BurtLight mainLight = BurtCreateMainLight(shadowAttenuation);

                // Evaluates the existing ambient plus Lambert direct-light model used by BurtLit.shader.
                float3 finalColor = BurtEvaluateSimpleLit(surfaceData, mainLight, normalWS);

                // Returns lit RGB with _BaseColor alpha so the Blend state controls final transparent compositing.
                return float4(finalColor, surfaceData.alpha);
            }

            // Ends the HLSL program for this pass.
            ENDHLSL
        }
    }

    // Disables fallback so shader errors stay visible in BurtRP instead of silently using another pipeline shader.
    Fallback Off
}


