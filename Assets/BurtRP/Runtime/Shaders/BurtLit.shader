// Defines the Shader menu path for the first BurtRP lit material model.
Shader "BurtRP/Lit"
{
    // Defines material properties shown in Unity's Inspector.
    Properties
    {
        // Defines the main albedo texture sampled by the forward Lit pass from mesh UV0.
        _BaseMap ("Base Map", 2D) = "white" {}

        // Defines the surface tint multiplied by the sampled Base Map before lighting.
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        // 定义切线空间法线贴图，Forward 光照会用它改变每个片元的世界空间法线。
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}

        // 定义法线贴图强度，0 表示退回几何法线，1 表示使用贴图原始强度。
        _NormalScale ("Normal Scale", Range(0, 2)) = 1

        // Enables cutout rendering when set to 1 so every Lit pass discards pixels below the same alpha threshold.
        [Toggle] _AlphaClip ("Alpha Clip", Float) = 0

        // Stores the alpha cutoff threshold used by Forward, DepthOnly, and ShadowCaster to keep color, depth, and shadows consistent.
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
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

            // Includes shared Base Map sampling and alpha-clip helpers so DepthOnly uses the same material logic as Forward.
            #include "ShaderLibrary/BurtInput.hlsl"

            // Defines material constants in UnityPerMaterial so SRP Batcher sees the same field layout as the other Lit passes.
            CBUFFER_START(UnityPerMaterial)

                // Stores the material base color selected in the Inspector and multiplied with the sampled Base Map alpha.
                float4 _BaseColor;

                // Stores Unity-generated Base Map tiling in xy and offset in zw so depth clipping samples the same texel as Forward.
                float4 _BaseMap_ST;

                // Stores the material alpha-clip switch as a float because Unity material properties are uploaded as float values.
                float _AlphaClip;

                // Stores the cutoff threshold that determines whether a sampled alpha writes depth for cutout surfaces.
                float _Cutoff;

                // DepthOnly 不采样法线贴图，但保留字段让 UnityPerMaterial 布局和 Forward pass 一致。
                float _NormalScale;

            // Ends the material constant buffer with the same ordering used by Forward and ShadowCaster for SRP Batcher compatibility.
            CBUFFER_END

            // Defines the mesh input needed for depth rendering.
            struct DepthAttributes
            {
                // Reads object-space vertex position from the mesh.
                float4 positionOS : POSITION;

                // Reads mesh UV0 so DepthOnly can sample the same Base Map alpha that the Forward pass uses for cutout decisions.
                float2 uv0 : TEXCOORD0;
            };

            // Defines the vertex-to-fragment data for depth rendering.
            struct DepthVaryings
            {
                // Stores clip-space position for rasterization.
                float4 positionCS : SV_POSITION;

                // Stores Base Map UVs after material tiling and offset so the fragment can alpha-clip before writing depth.
                float2 baseMapUV : TEXCOORD0;
            };

            // Converts object-space depth vertices into clip space.
            DepthVaryings VertDepth(DepthAttributes input)
            {
                // Creates the output structure that will be returned to the GPU pipeline.
                DepthVaryings output;

                // Transforms the object-space vertex position into clip-space position.
                output.positionCS = UnityObjectToClipPos(input.positionOS);

                // Applies the same Base Map tiling and offset as Forward so cutout depth follows the visible texture exactly.
                output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);

                // Returns the transformed vertex data.
                return output;
            }

            // Runs for each depth fragment and discards cutout pixels before the depth value is committed.
            float4 FragDepth(DepthVaryings input) : SV_Target
            {
                // Samples Base Map and material tint exactly like Forward so invisible cutout texels do not write CameraDepth.
                float4 baseColor = BurtSampleBaseMap(input.baseMapUV) * _BaseColor;

                // Applies the shared alpha-clip rule before returning so rejected fragments leave no depth behind.
                BurtApplyAlphaClip(baseColor.a, _AlphaClip, _Cutoff);

                // Returns a dummy color because this pass writes only depth after the alpha-clip decision succeeds.
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

            // Includes shared Base Map sampling and alpha-clip helpers so ShadowCaster matches the Forward visible silhouette.
            #include "ShaderLibrary/BurtInput.hlsl"

            // Defines material constants in UnityPerMaterial so SRP Batcher sees the same field layout as the other Lit passes.
            CBUFFER_START(UnityPerMaterial)

                // Stores the material base color selected in the Inspector and multiplied with the sampled Base Map alpha.
                float4 _BaseColor;

                // Stores Unity-generated Base Map tiling in xy and offset in zw so shadow clipping samples the same texel as Forward.
                float4 _BaseMap_ST;

                // Stores the material alpha-clip switch as a float because Unity material properties are uploaded as float values.
                float _AlphaClip;

                // Stores the cutoff threshold that determines whether a sampled alpha casts into the shadow map.
                float _Cutoff;

                // ShadowCaster 不采样法线贴图，但保留字段让 UnityPerMaterial 布局和 Forward pass 一致。
                float _NormalScale;

            // Ends the material constant buffer with the same ordering used by Forward and DepthOnly for SRP Batcher compatibility.
            CBUFFER_END

            // 保存当前 request 的主光方向，ShadowCaster 顶点偏移需要用它计算法线和光向夹角。
            float4 _BurtMainLightDirection;

            // 保存 C# 已折算到世界单位的 normal bias，ShadowCaster 只在顶点阶段使用它。
            float _BurtMainLightShadowNormalBias;

            // Defines the mesh input needed for shadow rendering.
            struct ShadowAttributes
            {
                // Reads object-space vertex position from the mesh.
                float4 positionOS : POSITION;

                // 读取模型空间法线，ShadowCaster normal bias 需要沿世界法线推开顶点。
                float3 normalOS : NORMAL;

                // Reads mesh UV0 so ShadowCaster can sample the same Base Map alpha that the Forward pass uses for cutout decisions.
                float2 uv0 : TEXCOORD0;
            };

            // Defines the vertex-to-fragment data for shadow rendering.
            struct ShadowVaryings
            {
                // Stores clip-space position for shadow-map rasterization.
                float4 positionCS : SV_POSITION;

                // Stores Base Map UVs after material tiling and offset so the fragment can alpha-clip before writing shadow depth.
                float2 baseMapUV : TEXCOORD0;
            };

            // 根据法线和主光方向计算 ShadowCaster 顶点的世界空间 normal bias。
            float3 ApplyBurtShadowCasterNormalBias(float4 positionOS, float3 normalOS)
            {
                // 先把顶点转到世界空间再偏移，避免非等比缩放时模型空间距离不一致。
                float3 positionWS = mul(unity_ObjectToWorld, positionOS).xyz;

                // 法线和光向必须处在同一世界空间，才能正确判断表面是否处于掠射角。
                float3 normalWS = UnityObjectToWorldNormal(normalOS);
                normalWS *= rsqrt(max(dot(normalWS, normalWS), 0.000001f));

                // C# 每次 ShadowCaster 绘制前都会上传当前主光方向，这里做安全归一化避免长度影响偏移。
                float3 lightDirectionWS = _BurtMainLightDirection.xyz;
                lightDirectionWS *= rsqrt(max(dot(lightDirectionWS, lightDirectionWS), 0.000001f));

                // C# 已按 shadow texel 把 bias 转成世界单位，这里只做非负保护避免反向拉回表面。
                float normalBias = max(0.0f, _BurtMainLightShadowNormalBias);

                // 表面越接近掠射角越容易出现 self-shadow，所以用 1 - NdotL 放大法线偏移。
                float normalBiasScale = (1.0f - saturate(dot(normalWS, lightDirectionWS))) * normalBias;

                // 沿世界法线推出 caster 顶点，让 shadow map 深度和接收面错开一小段距离。
                return positionWS + normalWS * normalBiasScale;
            }

            // Converts object-space vertices into the current light clip space.
            ShadowVaryings VertShadow(ShadowAttributes input)
            {
                // Creates the output structure that will be returned to the GPU pipeline.
                ShadowVaryings output;

                // 在进入主光裁剪空间前先应用 normal bias，这样偏移会真实写进 shadow map 深度。
                float3 biasedPositionWS = ApplyBurtShadowCasterNormalBias(input.positionOS, input.normalOS);

                // 使用 BurtDrawMainLightShadowCasterPass 设置的主光 VP 矩阵，把偏移后的世界坐标写入 shadow map。
                output.positionCS = mul(UNITY_MATRIX_VP, float4(biasedPositionWS, 1.0f));

                // Applies the same Base Map tiling and offset as Forward so cast shadows match the visible cutout silhouette.
                output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);

                // Returns the transformed vertex data.
                return output;
            }

            // Runs for each shadow fragment and discards cutout pixels before the shadow-map depth value is committed.
            float4 FragShadow(ShadowVaryings input) : SV_Target
            {
                // Samples Base Map and material tint exactly like Forward so transparent cutout texels do not cast shadows.
                float4 baseColor = BurtSampleBaseMap(input.baseMapUV) * _BaseColor;

                // Applies the shared alpha-clip rule before returning so rejected fragments leave no shadow-map depth behind.
                BurtApplyAlphaClip(baseColor.a, _AlphaClip, _Cutoff);

                // Returns a dummy color because this pass writes only shadow depth after the alpha-clip decision succeeds.
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

            // Includes BurtRP common helper functions such as safe normalization.
            #include "ShaderLibrary/BurtCommon.hlsl"

            // Includes BurtRP surface/input data structures used by the Lit forward pass.
            #include "ShaderLibrary/BurtInput.hlsl"

            // 引入 BurtRP 法线贴图工具，Forward pass 会用它把切线空间法线转换到世界空间。
            #include "ShaderLibrary/BurtNormal.hlsl"

            // Includes BurtRP simple main-light diffuse and ambient lighting helpers.
            #include "ShaderLibrary/BurtLighting.hlsl"

            // Includes BurtRP main-light shadow receiver helpers.
            #include "ShaderLibrary/BurtShadows.hlsl"

            // Defines material constants in UnityPerMaterial so SRP Batcher can keep this shader compatible.
            CBUFFER_START(UnityPerMaterial)

                // Stores the material base color selected in the Inspector.
                float4 _BaseColor;

                // Stores Unity-generated Base Map tiling in xy and offset in zw for TRANSFORM_TEX-compatible UV adjustment.
                float4 _BaseMap_ST;

                // Stores the material alpha-clip switch as a float so the Forward pass can share the same CBUFFER layout as depth passes.
                float _AlphaClip;

                // Stores the alpha threshold used by the shared clip helper after Base Map and Base Color have been combined.
                float _Cutoff;

                // 保存法线贴图强度，Forward pass 会用它缩放切线空间法线的 xy 分量。
                float _NormalScale;

            // Ends the material constant buffer with the same ordering used by DepthOnly and ShadowCaster for SRP Batcher compatibility.
            CBUFFER_END

            // BurtLighting.hlsl and BurtShadows.hlsl declare BurtRP global lighting and shadow variables for this pass.

            // Defines the mesh input needed by the lit forward pass.
            struct Attributes
            {
                // Reads object-space vertex position from the mesh.
                float4 positionOS : POSITION;

                // Reads object-space vertex normal from the mesh.
                float3 normalOS : NORMAL;

                // 读取模型空间切线，法线贴图需要用切线、法线和副切线组成 TBN 矩阵。
                float4 tangentOS : TANGENT;

                // Reads the first mesh UV channel so the forward pass can sample the Base Map.
                float2 uv0 : TEXCOORD0;
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

                // Stores Base Map UVs after applying material tiling and offset from _BaseMap_ST.
                float2 baseMapUV : TEXCOORD2;

                // 保存世界空间切线和 handedness，片元阶段会用它重建 TBN 矩阵。
                float4 tangentWS : TEXCOORD3;
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

                // Transforms the world-space position into main-light shadow-map coordinate space through the shared shadow helper.
                output.shadowCoord = BurtTransformWorldToMainLightShadow(positionWS);

                // Transforms the object-space normal into world space and normalizes it.
                output.normalWS = normalize(UnityObjectToWorldNormal(input.normalOS));

                // 把模型空间切线转换到世界空间，并保留 tangent.w 里的副切线方向信息。
                output.tangentWS = BurtObjectToWorldTangent(input.tangentOS);

                // Applies the material Base Map tiling and offset to mesh UV0 for fragment texture sampling.
                output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);

                // Returns the transformed vertex data.
                return output;
            }

            // Main-light shadow receiver sampling now lives in BurtShadows.hlsl so future PCF/cascade work has one owner.

            // Computes BurtRP's current minimal Lit model through ShaderLibrary helpers.
            float4 Frag(Varyings input) : SV_Target
            {
                // Samples the Base Map with transformed mesh UV0 and multiplies it by the material tint before evaluating visibility.
                float4 baseColor = BurtSampleBaseMap(input.baseMapUV) * _BaseColor;

                // Applies the shared alpha-clip rule so Forward visibility matches DepthOnly and ShadowCaster silhouettes.
                BurtApplyAlphaClip(baseColor.a, _AlphaClip, _Cutoff);

                // 采样法线贴图并把切线空间法线转换成世界空间法线，后续光照会使用这个最终法线。
                float3 normalWS = BurtSampleNormalWS(input.baseMapUV, input.normalWS, input.tangentWS, _NormalScale);

                // Builds the current surface data from the texture-tinted base color after the alpha-clip decision succeeds.
                BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor);

                // Samples the main-light shadow attenuation using the shared shadow receiver helper.
                float shadowAttenuation = BurtSampleMainLightShadow(input.shadowCoord);

                // Builds the current main light from BurtRP global lighting variables and this pixel's shadow value.
                BurtLight mainLight = BurtCreateMainLight(shadowAttenuation);

                // Evaluates the same ambient + Lambert direct-light model that used to live inline in this shader.
                float3 finalColor = BurtEvaluateSimpleLit(surfaceData, mainLight, normalWS);

                // Returns the lit color and preserves the material alpha value for future transparent/alpha-clip work.
                return float4(finalColor, surfaceData.alpha);
            }

            // Ends the HLSL program for this pass.
            ENDHLSL
        }
    }

    // Disables fallback so BurtRP shader errors do not silently use another pipeline shader.
    Fallback Off
}
