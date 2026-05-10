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

        // 定义 PBR Mask Map：R=金属度，G=环境遮蔽，B=预留，A=光滑度。
        _MaskMap ("Mask Map (R Metallic, G Occlusion, A Smoothness)", 2D) = "white" {}

        // 定义切线空间法线贴图，Forward 光照会用它改变每个片元的世界空间法线。
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}

        // 定义法线贴图强度，0 表示退回几何法线，1 表示使用贴图原始强度。
        _NormalScale ("Normal Scale", Range(0, 2)) = 1

        // 定义 XRender / Frostbite 风格的介质反射率，0.5 会映射到常见非金属 F0=0.04。
        _Reflectance ("Reflectance", Range(0, 1)) = 0.5

        // 定义金属度，0 表示非金属介质，1 表示金属材质。
        _Metallic ("Metallic", Range(0, 1)) = 0

        // 定义材质光滑度，数值越高高光越小越锐利。
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5

        // 定义环境遮蔽强度，0 表示忽略 Mask Map 的 G 通道，1 表示完全使用 G 通道。
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1

        // 定义自发光贴图，Forward 光照会把它作为不受灯光影响的颜色叠加到最终结果。
        _EmissionMap ("Emission Map", 2D) = "white" {}

        // 定义自发光颜色，RGB 表示自发光颜色和强度。
        [HDR]_EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

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

            // 引入 BurtRP Lit 统一材质 CBUFFER，让 DepthOnly、ShadowCaster、Forward 的 SRP Batcher 布局完全一致。
            #include "ShaderLibrary/BurtLitProperties.hlsl"

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

            // 引入 BurtRP Lit 统一材质 CBUFFER，让 ShadowCaster 和其它 Lit pass 使用同一份材质字段顺序。
            #include "ShaderLibrary/BurtLitProperties.hlsl"

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

            // 使用 Unity reflection probe 的 cubemap mip 采样，需要 shader target 3.0 支持显式 LOD。
            #pragma target 3.0

            // Includes Unity helper functions for transforms and normal conversion.
            #include "UnityCG.cginc"

            // Includes BurtRP common helper functions such as safe normalization.
            #include "ShaderLibrary/BurtCommon.hlsl"

            // Includes BurtRP surface/input data structures used by the Lit forward pass.
            #include "ShaderLibrary/BurtInput.hlsl"

            // 引入 BurtRP 法线贴图工具，Forward pass 会用它把切线空间法线转换到世界空间。
            #include "ShaderLibrary/BurtNormal.hlsl"

            // 引入 BurtRP 自发光工具，Forward pass 会用它采样自发光贴图并叠加最终颜色。
            #include "ShaderLibrary/BurtEmission.hlsl"

            // Includes BurtRP simple main-light diffuse and ambient lighting helpers.
            #include "ShaderLibrary/BurtLighting.hlsl"

            // 引入 BurtRP GBuffer 编解码约定；这里只做 shader 侧 roundtrip debug，不绑定任何 RenderTarget 生命周期。
            #include "ShaderLibrary/BurtGBuffer.hlsl"

            // Includes BurtRP main-light shadow receiver helpers.
            #include "ShaderLibrary/BurtShadows.hlsl"

            // 引入 BurtRP shading debug 工具，Forward pass 会根据 Overlay 选择输出 Albedo、Normal、Smoothness、Metallic 或 Detail Lighting。
            #include "ShaderLibrary/BurtShadingDebug.hlsl"

            // 引入 BurtRP Lit 统一材质 CBUFFER，Forward pass 直接使用同一份 SRP Batcher 字段布局。
            #include "ShaderLibrary/BurtLitProperties.hlsl"

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

                // 保存世界空间位置，片元阶段会用它计算从表面指向相机的视线方向。
                float3 positionWS : TEXCOORD4;

                // 保存自发光贴图 UV，片元阶段会用它采样 Emission Map。
                float2 emissionMapUV : TEXCOORD5;

                // 保存 Mask Map UV，片元阶段会用它采样 metallic、occlusion 和 smoothness。
                float2 maskMapUV : TEXCOORD6;
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

                // 保存世界空间位置，后续 fragment 会用它计算 view direction。
                output.positionWS = positionWS.xyz;

                // Transforms the world-space position into main-light shadow-map coordinate space through the shared shadow helper.
                output.shadowCoord = BurtTransformWorldToMainLightShadow(positionWS);

                // Transforms the object-space normal into world space and normalizes it.
                output.normalWS = normalize(UnityObjectToWorldNormal(input.normalOS));

                // 把模型空间切线转换到世界空间，并保留 tangent.w 里的副切线方向信息。
                output.tangentWS = BurtObjectToWorldTangent(input.tangentOS);

                // Applies the material Base Map tiling and offset to mesh UV0 for fragment texture sampling.
                output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);

                // 按自发光贴图自己的 Tiling/Offset 转换 UV，允许 Emission 和 Base Map 使用不同缩放。
                output.emissionMapUV = BurtTransformEmissionMapUV(input.uv0, _EmissionMap_ST);

                // 按 Mask Map 自己的 Tiling/Offset 转换 UV，允许 PBR 打包贴图独立缩放。
                output.maskMapUV = BurtTransformMaskMapUV(input.uv0, _MaskMap_ST);

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

                // 计算从当前片元指向相机的世界空间方向，Specular 高光需要知道观察方向。
                float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);

                // 采样 Mask Map，R/G/A 分别参与 metallic、occlusion 和 smoothness 的最终计算。
                float4 maskMap = BurtSampleMaskMap(input.maskMapUV);

                // 使用基础色、高光颜色、标量参数和 Mask Map 构建完整 PBR 表面数据。
                BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, _Reflectance, _Smoothness, _Metallic, maskMap, _OcclusionStrength);

                // Samples the main-light shadow attenuation using the shared shadow receiver helper.
                float shadowAttenuation = BurtSampleMainLightShadow(input.shadowCoord);

                // Builds the current main light from BurtRP global lighting variables and this pixel's shadow value.
                BurtLight mainLight = BurtCreateMainLight(shadowAttenuation);

                // 为当前 shading 准备一份可按 Debug View 覆盖的 SurfaceData，正常渲染时它和原始 surfaceData 完全一致。
                BurtSurfaceData shadingSurfaceData = surfaceData;

                // 出处：XRender/Shaders/SlabDebug/SlabDebugEvaluator/SlabDebug.Evaluate.Prev.hlsl::DEBUGID_LIGHTING_DETAIL_LIGHTING；Detail Lighting 用 0.18 中灰替换 Base.Color 来观察光照细节。
                if (BurtIsShadingDebugEnabled() && BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
                {
                    // 只替换参与 shading 的 BaseColor，不改原始 surfaceData，避免 Albedo / Reflectance 等材质 Debug 读到被覆盖后的值。
                    shadingSurfaceData.baseColor.rgb = float3(0.18f, 0.18f, 0.18f);
                }

                // 统一调用 PBR shading 入口，让 Forward 和未来 Deferred 共享同一套光照拆分结果。
                BurtPBRShadingComponents pbrComponents = BurtEvaluatePBRShadingComponents(shadingSurfaceData, mainLight, normalWS, viewDirectionWS);

                // 取出不含自发光的 PBR 总光照，后续 finalColor 会在它基础上叠加 Emission。
                float3 lightingColor = pbrComponents.lighting;

                // 用 Forward 当前片元数据做一次 GBuffer 编码再解码，提前验证 Deferred 后续会消费的材质/法线还原路径。
                BurtGBufferData debugGBufferSourceData = BurtCreateGBufferData(surfaceData, normalWS, float3(0.0f, 0.0f, 0.0f));

                // 按 BurtGBuffer.hlsl 顶部约定生成三张逻辑 GBuffer；这里只在 shader 内部 roundtrip，不写入真实 RT。
                BurtEncodedGBuffer debugEncodedGBuffer = BurtEncodeGBuffer(debugGBufferSourceData);

                // 从逻辑 GBuffer 解码回语义数据，Debug View 读取的是这份解码结果。
                BurtGBufferData debugDecodedGBufferData = BurtDecodeGBuffer(debugEncodedGBuffer);

                // 把解码后的 GBuffer 数据转成 PBRMaterialData，提前验证 Deferred shading 前的材质重建口径。
                BurtPBRMaterialData debugGBufferMaterialData = BurtPreparePBRMaterialData(debugDecodedGBufferData);

                // 创建 Shading Debug 数据结构，确保 Debug View 读取的就是当前片元真实渲染使用的数据。
                BurtShadingDebugData debugData;

                // 写入世界空间法线，NormalWS Debug View 会把它编码成颜色。
                debugData.normalWS = normalWS;

                // 写入 Detail Lighting 结果，DetailLighting Debug View 会显示中灰 BaseColor 下的光照细节。
                debugData.detailLightingColor = pbrComponents.lighting;

                // 写入直接漫反射结果，DirectDiffuse Debug View 会显示它。
                debugData.directDiffuseColor = pbrComponents.directDiffuse;

                // 写入直接高光结果，DirectSpecular Debug View 会显示它。
                debugData.directSpecularColor = pbrComponents.directSpecular;

                // 写入间接漫反射结果，IndirectDiffuse Debug View 会显示它。
                debugData.indirectDiffuseColor = pbrComponents.indirectDiffuse;

                // 写入间接高光结果，IndirectSpecular Debug View 会显示它。
                debugData.indirectSpecularColor = pbrComponents.indirectSpecular;

                // 写入材质 reflectance，Reflectance Debug View 会用它检查非金属反射率输入。
                debugData.reflectance = surfaceData.reflectance;

                // 写入材质感知粗糙度，Roughness Debug View 会显示 1 - smoothness 后的结果。
                debugData.perceptualRoughness = pbrComponents.perceptualRoughness;

                // 写入直接高光实际粗糙度，SpecularAARoughness Debug View 会显示 AA 后的结果。
                debugData.specularAARoughness = pbrComponents.specularAARoughness;

                // 写入直接高光能量补偿，便于检查 LUT.z 是否过亮或过暗。
                debugData.specularEnergyCompensation = pbrComponents.specularEnergyCompensation;

                // 写入间接高光能量补偿，便于检查 Reflection Probe 高光是否也对齐 XRender 补能。
                debugData.indirectSpecularEnergyCompensation = pbrComponents.indirectSpecularEnergyCompensation;

                // 写入 XRender EnergyPreservation，EnergyPreservation Debug View 会显示 diffuse 底层保能比例。
                debugData.energyPreservation = pbrComponents.energyPreservation;

                // 写入间接高光遮蔽项，保持和间接镜面反射一样的 AO、NdotV 和粗糙度输入。
                debugData.specularOcclusion = pbrComponents.specularOcclusion;

                // 写入 XRender DiffuseColor，DiffuseColor Debug View 会显示 metallic 扣除后的漫反射颜色。
                debugData.diffuseColor = pbrComponents.diffuseColor;

                // 写入直接 GGX D 项，DirectBRDFD Debug View 会缩放显示高 smoothness 下的 NDF 峰值。
                debugData.directBRDFD = pbrComponents.directBRDFD;

                // 写入直接 Smith Joint Visibility 项，用来检查几何遮蔽是否压暗高光。
                debugData.directBRDFVisibility = pbrComponents.directBRDFVisibility;

                // 写入直接 Schlick Fresnel 项，用来检查 F0 和视角输入。
                debugData.directBRDFFresnel = pbrComponents.directBRDFFresnel;

                // 写入直接 diffuse lobe，当前默认 Lambert，后续启用 Burley 时这里会同步变化。
                debugData.directDiffuseLobe = pbrComponents.directDiffuseLobe;

                // 写入未乘灯光颜色、NdotL 和阴影的直接 diffuse BRDF。
                debugData.directDiffuseBRDF = pbrComponents.directDiffuseBRDF;

                // 写入未乘灯光颜色、NdotL 和阴影的直接 specular BRDF。
                debugData.directSpecularBRDF = pbrComponents.directSpecularBRDF;

                // 写入 Specular AA 法线方差，SpecularAANormalVariance Debug View 会放大显示。
                debugData.specularAANormalVariance = pbrComponents.specularAANormalVariance;

                // 写入 Specular AA 增加的感知粗糙度，SpecularAARoughnessDelta Debug View 会放大显示。
                debugData.specularAARoughnessDelta = pbrComponents.specularAARoughnessDelta;

                // 写入间接高光 DFG.xy，IndirectSpecularDFG Debug View 会显示为 R/G 通道。
                debugData.indirectSpecularDFG = pbrComponents.indirectSpecularDFG;

                // 写入 F0/F90 应用 DFG 后的环境 BRDF，用来检查 Reflection Probe 前的 BRDF 权重。
                debugData.indirectSpecularEnvBRDF = pbrComponents.indirectSpecularEnvBRDF;

                // 写入 GBuffer 解码后的 BaseColor，用来检查 GBuffer0.rgb 的材质颜色还原。
                debugData.gbufferBaseColor = debugDecodedGBufferData.baseColor;

                // 写入 GBuffer 解码后的世界空间法线，用来检查 octahedron normal 编码精度和方向。
                debugData.gbufferNormalWS = debugDecodedGBufferData.normalWS;

                // 写入 GBuffer 解码后的 Metallic，用来检查 GBuffer1.b 的材质还原。
                debugData.gbufferMetallic = debugDecodedGBufferData.metallic;

                // 写入 GBuffer 解码后的 Smoothness，用来检查 GBuffer1.a 的面板语义还原。
                debugData.gbufferSmoothness = debugDecodedGBufferData.smoothness;

                // 写入 GBuffer 解码后的 AO，用来检查 GBuffer0.a 的间接光遮蔽输入。
                debugData.gbufferOcclusion = debugDecodedGBufferData.occlusion;

                // 写入 GBuffer 解码后的 Reflectance，用来检查 GBuffer2.a 的 XRender reflectance 输入。
                debugData.gbufferReflectance = debugDecodedGBufferData.reflectance;

                // 写入从 GBuffer Smoothness 还原出的感知粗糙度，用来和 Forward Roughness 对照。
                debugData.gbufferRoughness = debugGBufferMaterialData.perceptualRoughness;

                // 写入从 GBuffer 重建出的 DiffuseColor，用来检查 metallic 对 diffuse 的扣除是否和 Forward 一致。
                debugData.gbufferDiffuseColor = debugGBufferMaterialData.diffuseColor;

                // 创建一个临时调试颜色变量，只有命中材质 debug 模式时才会被真正输出。
                float3 debugColor;

                // 如果 Overlay 选择了材质类 debug 模式，就直接输出调试颜色，避免自发光或后处理干扰观察。
                if (BurtTryEvaluateMaterialShadingDebug(surfaceData, debugData, debugColor))
                {
                    // 返回材质 debug 颜色，同时保留材质 alpha，方便后续透明调试继续沿用同一逻辑。
                    return float4(debugColor, surfaceData.alpha);
                }

                // 采样自发光颜色，它不受灯光和阴影影响，会直接叠加到最终颜色。
                float3 emissionColor = BurtEvaluateEmission(input.emissionMapUV, _EmissionColor.rgb);

                // 用光照结果初始化最终颜色，后续再叠加自发光，便于 Detail Lighting debug 单独观察不含自发光的部分。
                float3 finalColor = lightingColor;

                // 把自发光叠加到光照结果上，让材质可以自己发亮。
                finalColor += emissionColor;

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
