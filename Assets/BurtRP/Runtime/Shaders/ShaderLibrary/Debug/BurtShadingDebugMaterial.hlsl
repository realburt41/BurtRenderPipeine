// BurtRP material shading-debug final mode evaluation.
#ifndef BURT_SHADING_DEBUG_MATERIAL_INCLUDED
#define BURT_SHADING_DEBUG_MATERIAL_INCLUDED

#ifndef BURT_PI
#define BURT_PI (3.14159265359f)
#endif

bool BurtTryEvaluateMaterialShadingDebug(BurtSurfaceData surfaceData, BurtShadingDebugData data, out float3 debugColor) // 尝试根据当前模式生成材质调试颜色。
{
    debugColor = float3(0.0f, 0.0f, 0.0f); // 先清空输出颜色，保证未命中任何模式时不会返回未初始化值。

    if (!BurtIsShadingDebugEnabled()) // 如果全局 debug 开关关闭，就不接管材质输出。
    {
        return false; // 返回 false，告诉调用方继续走正常 Lit 渲染路径。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ALBEDO)) // Albedo 模式显示贴图和 BaseColor 合成后的基础色。
    {
        debugColor = max(surfaceData.BaseColor.rgb, float3(0.0f, 0.0f, 0.0f)); // 保留 HDR 基础色，便于检查 _BaseColor 是否真实进材质。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_NORMAL_WS)) // NormalWS 模式显示法线贴图影响后的世界空间法线。
    {
        debugColor = BurtEncodeNormalWSForDebug(data.NormalWS); // 把世界法线从方向值编码成可视化颜色。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SMOOTHNESS)) // Smoothness 模式显示当前材质光滑度。
    {
        debugColor = float3(surfaceData.Smoothness, surfaceData.Smoothness, surfaceData.Smoothness); // 把单通道光滑度复制到 RGB，形成灰度图。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_METALLIC)) // Metallic 模式显示当前材质金属度。
    {
        debugColor = float3(surfaceData.Metallic, surfaceData.Metallic, surfaceData.Metallic); // 把单通道金属度复制到 RGB，形成灰度图。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_OCCLUSION)) // Occlusion 模式显示当前材质环境遮蔽。
    {
        debugColor = float3(surfaceData.Occlusion, surfaceData.Occlusion, surfaceData.Occlusion); // 把单通道环境遮蔽复制到 RGB，形成灰度图。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HEIGHT))
    {
        debugColor = float3(surfaceData.Height, surfaceData.Height, surfaceData.Height);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PRESKIN_POSITION))
    {
        debugColor = data.PreSkinPositionAvailable > 0.5f ? saturate(data.PreSkinPositionDebugColor) : float3(0.0f, 0.0f, 0.0f);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_REFLECTANCE)) // Reflectance 模式显示材质介质反射率。
    {
        debugColor = float3(data.Reflectance, data.Reflectance, data.Reflectance); // 直接显示 reflectance，0.5 对应常见非金属 F0=0.04。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ROUGHNESS)) // Roughness 模式显示材质感知粗糙度。
    {
        debugColor = float3(data.PerceptualRoughness, data.PerceptualRoughness, data.PerceptualRoughness); // 把粗糙度复制到 RGB，越黑表示越光滑。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS)) // SpecularAARoughness 模式显示高光实际粗糙度。
    {
        debugColor = float3(data.SpecularAARoughness, data.SpecularAARoughness, data.SpecularAARoughness); // 越亮表示 Specular AA 把高光拓得越宽。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_ENERGY_COMPENSATION)) // SpecularEnergyCompensation 模式显示直接高光能量补偿。
    {
        debugColor = saturate((data.SpecularEnergyCompensation - 1.0f) * 0.5f); // 黑色表示无补偿，越亮表示多次散射补偿越强，2 倍补偿约为 0.5 灰。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENERGY_COMPENSATION)) // IndirectSpecularEnergyCompensation 模式显示间接高光能量补偿。
    {
        debugColor = saturate((data.IndirectSpecularEnergyCompensation - 1.0f) * 0.5f); // 黑色表示无补偿，越亮表示 Reflection Probe 高光补偿越强，2 倍补偿约为 0.5 灰。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ENERGY_PRESERVATION)) // EnergyPreservation 模式显示底层 diffuse 保能比例。
    {
        debugColor = float3(data.EnergyPreservation, data.EnergyPreservation, data.EnergyPreservation); // 越亮表示 diffuse 保留越多，越暗表示 specular 顶层占用能量越多。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_OCCLUSION)) // SpecularOcclusion 模式显示间接高光遮蔽。
    {
        debugColor = float3(data.SpecularOcclusion, data.SpecularOcclusion, data.SpecularOcclusion); // 越亮表示反射探针被保留越多。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIFFUSE_COLOR)) // DiffuseColor 模式显示 XRender GenericData.DiffuseColor。
    {
        debugColor = saturate(data.DiffuseColor); // 金属度越高 diffuse 越暗，方便检查 metallic 是否正确转移到 F0。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_D)) // DirectBRDFD 模式显示 GGX D 项。
    {
        float visibleD = saturate(data.DirectBRDFD * 0.05f); // D 项在极光滑时会很高，所以缩放 0.05 让普通范围更容易观察。
        debugColor = float3(visibleD, visibleD, visibleD); // 把缩放后的 D 项复制到 RGB 形成灰度调试图。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_VISIBILITY)) // DirectBRDFVisibility 模式显示 Smith Joint Visibility。
    {
        float visibleVisibility = saturate(data.DirectBRDFVisibility); // 越亮表示几何遮蔽越少，掠射角可能接近白色。
        debugColor = float3(visibleVisibility, visibleVisibility, visibleVisibility); // 把单通道 visibility 复制到 RGB。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_FRESNEL)) // DirectBRDFFresnel 模式显示 Schlick Fresnel。
    {
        debugColor = saturate(data.DirectBRDFFresnel); // 可以直接观察 reflectance、metallic 和视角对 F 的影响。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_LOBE)) // DirectDiffuseLobe 模式显示 Lambert / Burley diffuse lobe。
    {
        float visibleDiffuseLobe = saturate(data.DirectDiffuseLobe); // Lambert 默认约为 0.318，切 Burley 后会随角度和 roughness 改变。
        debugColor = float3(visibleDiffuseLobe, visibleDiffuseLobe, visibleDiffuseLobe); // 把 diffuse lobe 复制到 RGB 形成灰度调试图。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_BRDF)) // DirectDiffuseBRDF 模式显示未乘灯光的 diffuse BRDF。
    {
        debugColor = saturate(data.DirectDiffuseBRDF); // 只看材质、diffuse lobe 和 EnergyPreservation，不含灯光颜色、NdotL 或阴影。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR_BRDF)) // DirectSpecularBRDF 模式显示未乘灯光的 specular BRDF。
    {
        debugColor = saturate(data.DirectSpecularBRDF); // 只看 D/V/F 和 EnergyCompensation，不含灯光颜色、NdotL 或阴影。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_PRIMARY_LOBE)) // HairPrimaryLobe 模式显示 Hair R/primary 高光 lobe。
    {
        float visiblePrimary = saturate(data.HairPrimaryLobe * 0.05f); // Hair lobe 峰值可能很高，沿用 DirectBRDFD 的缩放口径。
        debugColor = float3(visiblePrimary, visiblePrimary, visiblePrimary);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_SECONDARY_LOBE)) // HairSecondaryLobe 模式显示 Hair TT/secondary 高光 lobe。
    {
        float visibleSecondary = saturate(data.HairSecondaryLobe * 0.25f); // secondary lobe 通常低于 primary，给更高可见度。
        debugColor = float3(visibleSecondary, visibleSecondary, visibleSecondary);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_TRANSMISSION_LOBE)) // HairTransmissionLobe 模式显示 Hair 背光透射近似。
    {
        float visibleTransmission = saturate(data.HairTransmissionLobe);
        debugColor = float3(visibleTransmission, visibleTransmission, visibleTransmission);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_SCATTER)) // HairScatter 模式显示参与 Hair lighting 的 scatter。
    {
        debugColor = float3(data.HairScatter, data.HairScatter, data.HairScatter);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_AA_NORMAL_VARIANCE)) // SpecularAANormalVariance 模式显示 Normal Filtering 的法线方差。
    {
        float visibleVariance = saturate(data.SpecularAANormalVariance * 100.0f); // 方差通常很小，放大 100 倍后更容易看出法线贴图引起的拓宽区域。
        debugColor = float3(visibleVariance, visibleVariance, visibleVariance); // 把放大后的法线方差复制到 RGB。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS_DELTA)) // SpecularAARoughnessDelta 模式显示 Specular AA 增加的 roughness。
    {
        float visibleRoughnessDelta = saturate(data.SpecularAARoughnessDelta * 5.0f); // delta 最大约受 threshold 限制，放大 5 倍能让 0.2 附近接近白色。
        debugColor = float3(visibleRoughnessDelta, visibleRoughnessDelta, visibleRoughnessDelta); // 把放大后的 roughness delta 复制到 RGB。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_DFG)) // IndirectSpecularDFG 模式显示间接高光预积分 DFG.xy。
    {
        debugColor = saturate(float3(data.IndirectSpecularDFG.x, data.IndirectSpecularDFG.y, 0.0f)); // R/G 分别显示 DFG.x/y，B 保持 0 便于区分。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENV_BRDF)) // IndirectSpecularEnvBRDF 模式显示 F0/F90 套用 DFG 后的环境 BRDF。
    {
        debugColor = saturate(data.IndirectSpecularEnvBRDF); // Reflection Probe 会乘这个项，可用来确认 DFG 和 F0 是否合理。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_PROFILE_ID))
    {
        float visibleProfileIndex = saturate(data.SubsurfaceProfileIndex / max(BURT_SHADING_DEBUG_SUBSURFACE_PROFILE_SLOT_COUNT - 1.0f, 1.0f));
        debugColor = float3(visibleProfileIndex, visibleProfileIndex, visibleProfileIndex);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION))
    {
        debugColor = saturate(data.SubsurfaceTransmission);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_DIRECT_TRANSMISSION))
    {
        debugColor = max(data.SubsurfaceDirectTransmission, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_BRDF))
    {
        debugColor = saturate(data.SubsurfaceTransmissionBRDF);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_SHADOW))
    {
        debugColor = data.SubsurfaceTransmissionShadow.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_PHASE))
    {
        float visiblePhase = saturate(data.SubsurfaceTransmissionPhase * (4.0f * BURT_PI));
        debugColor = visiblePhase.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_THICKNESS))
    {
        debugColor = saturate(data.SubsurfaceTransmissionThickness * 0.1f).xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_KERNEL_WEIGHT))
    {
        debugColor = saturate(data.SubsurfaceKernelWeight * 4.0f);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_INDIRECT))
    {
        debugColor = max(data.SubsurfaceIndirect, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    float foliageMask = saturate(max(data.FoliageMask, BurtIsFoliageShadingModel(surfaceData.ShadingModelID) ? 1.0f : 0.0f));
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION))
    {
        debugColor = saturate(data.FoliageTransmission) * foliageMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_DIRECT_TRANSMISSION))
    {
        debugColor = max(data.FoliageDirectTransmission, float3(0.0f, 0.0f, 0.0f)) * foliageMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION_BRDF))
    {
        debugColor = saturate(data.FoliageTransmissionBRDF) * foliageMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION_SHADOW))
    {
        debugColor = data.FoliageTransmissionShadow.xxx * foliageMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_SPECULAR_BRDF))
    {
        debugColor = saturate(data.FoliageSpecularBRDF) * foliageMask;
        return true;
    }

    float grassMask = foliageMask * saturate(surfaceData.FoliageIsGrass);
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION))
    {
        debugColor = saturate(data.FoliageTransmission) * grassMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_DIRECT_TRANSMISSION))
    {
        debugColor = max(data.FoliageDirectTransmission, float3(0.0f, 0.0f, 0.0f)) * grassMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION_BRDF))
    {
        debugColor = saturate(data.FoliageTransmissionBRDF) * grassMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION_SHADOW))
    {
        debugColor = data.FoliageTransmissionShadow.xxx * grassMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_SPECULAR_BRDF))
    {
        debugColor = saturate(data.FoliageSpecularBRDF) * grassMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_BASE_COLOR)) // GBufferBaseColor 模式显示编码再解码后的 BaseColor。
    {
        debugColor = max(data.GBufferBaseColor, float3(0.0f, 0.0f, 0.0f)); // 观察 GBuffer0.rgb 是否能还原材质基础色和 HDR 范围。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_NORMAL_WS)) // GBufferNormalWS 模式显示编码再解码后的向量槽。
    {
        debugColor = BurtEncodeNormalWSForDebug(data.GBufferNormalWS); // 把解码法线从方向值编码成屏幕可读颜色。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_METALLIC)) // GBufferMetallic 模式显示解码后的材质通道。
    {
        debugColor = float3(data.GBufferMetallic, data.GBufferMetallic, data.GBufferMetallic); // 把单通道材质值复制到 RGB。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_MASK))
    {
        debugColor = float3(data.GBufferClearCoatMask, data.GBufferClearCoatMask, data.GBufferClearCoatMask);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_NORMAL_WS))
    {
        debugColor = BurtEncodeNormalWSForDebug(data.GBufferClearCoatNormalWS);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_ROUGHNESS))
    {
        debugColor = float3(data.GBufferClearCoatRoughness, data.GBufferClearCoatRoughness, data.GBufferClearCoatRoughness);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_STRENGTH))
    {
        debugColor = float3(data.GBufferSubsurfaceStrength, data.GBufferSubsurfaceStrength, data.GBufferSubsurfaceStrength);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_THICKNESS))
    {
        debugColor = float3(data.GBufferSubsurfaceThickness, data.GBufferSubsurfaceThickness, data.GBufferSubsurfaceThickness);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_PROFILE_INDEX))
    {
        float visibleProfileIndex = saturate(data.GBufferSubsurfaceProfileIndex / max(BURT_SHADING_DEBUG_SUBSURFACE_PROFILE_SLOT_COUNT - 1.0f, 1.0f));
        debugColor = float3(visibleProfileIndex, visibleProfileIndex, visibleProfileIndex);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_ANISOTROPY))
    {
        float encodedAnisotropy = saturate(data.GBufferAnisotropy * 0.5f + 0.5f);
        debugColor = float3(encodedAnisotropy, encodedAnisotropy, encodedAnisotropy);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_TANGENT_WS))
    {
        debugColor = BurtEncodeNormalWSForDebug(data.GBufferTangentWS);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SMOOTHNESS)) // GBufferSmoothness 模式显示解码后的光滑度。
    {
        debugColor = float3(data.GBufferSmoothness, data.GBufferSmoothness, data.GBufferSmoothness); // Smoothness 越亮表示材质越光滑。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_OCCLUSION)) // GBufferOcclusion 模式显示解码后的 AO。
    {
        debugColor = float3(data.GBufferOcclusion, data.GBufferOcclusion, data.GBufferOcclusion); // AO 越暗表示间接光被遮蔽越强。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_REFLECTANCE)) // GBufferReflectance 模式显示解码后的 XRender reflectance。
    {
        debugColor = float3(data.GBufferReflectance, data.GBufferReflectance, data.GBufferReflectance); // Reflectance 仍按 XRender 输入语义显示，不显示 F0。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_ROUGHNESS)) // GBufferRoughness 模式显示解码后还原出的 XRender Base.Roughness。
    {
        debugColor = float3(data.GBufferRoughness, data.GBufferRoughness, data.GBufferRoughness); // Roughness 越黑表示越光滑，口径和 Forward Roughness 一致。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_DIFFUSE_COLOR)) // GBufferDiffuseColor 模式显示 GBuffer 还原后的 DiffuseColor。
    {
        debugColor = saturate(data.GBufferDiffuseColor); // 观察 metallic 扣除 diffuse 后是否和 Forward DiffuseColor 一致。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING)) // DetailLighting 模式显示中灰 BaseColor 下的 PBR 光照。
    {
        debugColor = max(data.DetailLightingColor, float3(0.0f, 0.0f, 0.0f)); // 保留 HDR 光照强度但去掉负值，便于观察阴影、高光和间接光分布。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_LIGHTING)) // IndirectLighting 模式只显示 PBR 间接光。
    {
        debugColor = max(data.IndirectDiffuseColor + data.IndirectSpecularColor, float3(0.0f, 0.0f, 0.0f)); // 把间接漫反射和间接高光相加后显示。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE)) // DirectDiffuse 模式只显示直接漫反射。
    {
        debugColor = max(data.DirectDiffuseColor, float3(0.0f, 0.0f, 0.0f)); // 显示主光漫反射项，方便排查 1/PI、NdotL 和阴影。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR)) // DirectSpecular 模式只显示直接高光。
    {
        debugColor = max(data.DirectSpecularColor, float3(0.0f, 0.0f, 0.0f)); // 显示主光 GGX 高光项，方便排查 smoothness 拉满后高光是否过窄。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING)) // AdditionalLighting 模式只显示追加光直接光。
    {
        debugColor = max(data.AdditionalDiffuseColor + data.AdditionalSpecularColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_DIFFUSE)) // AdditionalDiffuse 模式只显示追加光漫反射。
    {
        debugColor = max(data.AdditionalDiffuseColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SPECULAR)) // AdditionalSpecular 模式只显示追加光高光。
    {
        debugColor = max(data.AdditionalSpecularColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING_UNSHADOWED)) // AdditionalLightingUnshadowed 模式显示追加光未乘阴影的贡献。
    {
        debugColor = max(data.AdditionalUnshadowedColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_ADDITIONAL_LIGHTING)) // HairAdditionalLighting 模式只显示 Hair 追加光。
    {
#if BURT_ACTIVE_HAIR_SHADING_MODEL
        debugColor = max(data.AdditionalDiffuseColor + data.AdditionalSpecularColor, float3(0.0f, 0.0f, 0.0f));
#elif BURT_ENABLE_HAIR_SHADING
        float3 hairAdditionalLighting = max(data.AdditionalDiffuseColor + data.AdditionalSpecularColor, float3(0.0f, 0.0f, 0.0f));
        debugColor = BurtIsActiveHairShadingModel(surfaceData.ShadingModelID) ? hairAdditionalLighting : float3(0.0f, 0.0f, 0.0f);
#else
        debugColor = float3(0.0f, 0.0f, 0.0f);
#endif
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_DIFFUSE)) // IndirectDiffuse 模式只显示间接漫反射。
    {
        debugColor = max(data.IndirectDiffuseColor, float3(0.0f, 0.0f, 0.0f)); // 显示 Unity SH / Light Probe 漫反射，方便检查间接漫反射是否存在。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GI_PROBE_IRRADIANCE))
    {
        debugColor = max(data.GIProbeIrradiance, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GI_PROBE_VALIDITY))
    {
        debugColor = data.GIProbeValidity.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GI_PROBE_SKY_VISIBILITY))
    {
        debugColor = data.GIProbeSkyVisibility.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR)) // IndirectSpecular 模式只显示间接高光。
    {
        debugColor = max(data.IndirectSpecularColor, float3(0.0f, 0.0f, 0.0f)); // 显示 Reflection Probe 镜面项，方便检查探针和 DFG 是否生效。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_ATTENUATION)) // ShadowAttenuation 模式显示主光阴影衰减。
    {
        debugColor = float3(data.ShadowAttenuation, data.ShadowAttenuation, data.ShadowAttenuation); // 白色表示无阴影，黑色表示完全被遮挡。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_RECEIVER_DEPTH))
    {
        debugColor = float3(data.MainLightShadowReceiverDepth, data.MainLightShadowReceiverDepth, data.MainLightShadowReceiverDepth);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_RAW_DEPTH))
    {
        debugColor = float3(data.MainLightShadowRawDepth, data.MainLightShadowRawDepth, data.MainLightShadowRawDepth);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_COMPARE))
    {
        debugColor = float3(data.MainLightShadowCompare, data.MainLightShadowCompare, data.MainLightShadowCompare);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_PROJECTION_VALIDITY))
    {
        debugColor = data.MainLightShadowProjectionValidity;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_ATTENUATION)) // AdditionalShadowAttenuation 模式显示追加光阴影衰减。
    {
        debugColor = float3(data.AdditionalShadowAttenuation, data.AdditionalShadowAttenuation, data.AdditionalShadowAttenuation); // 白色表示追加光未被阴影遮挡，黑色表示完全被遮挡。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_FACE))
    {
        debugColor = saturate(data.AdditionalShadowFaceColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_UV))
    {
        debugColor = saturate(data.AdditionalShadowUVColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH))
    {
        debugColor = saturate(data.AdditionalShadowDepthColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH_DELTA))
    {
        debugColor = saturate(data.AdditionalShadowDepthDeltaColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_OBJECT_INDEX))
    {
        debugColor = saturate(data.PerObjectShadowObjectIndexColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_SLICE))
    {
        debugColor = saturate(data.PerObjectShadowSliceColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_UV))
    {
        debugColor = saturate(data.PerObjectShadowUVColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_DEPTH))
    {
        debugColor = saturate(data.PerObjectShadowDepthColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_COMPARE))
    {
        debugColor = saturate(data.PerObjectShadowCompareColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_TRANSMISSION_DEPTH))
    {
        debugColor = saturate(data.PerObjectShadowTransmissionDepthColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_TRANSMISSION_THICKNESS))
    {
        debugColor = saturate(data.PerObjectShadowTransmissionThicknessColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_INDEX)) // ShadowCascadeIndex 模式用颜色显示当前 CSM cascade。
    {
        debugColor = saturate(data.ShadowCascadeColor); // 不同 cascade 使用固定调试颜色，黑色表示没有命中任何 cascade。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_BLEND)) // ShadowCascadeBlend 模式显示 cascade 边界混合权重。
    {
        debugColor = float3(data.ShadowCascadeBlend, data.ShadowCascadeBlend, data.ShadowCascadeBlend); // 白色表示正在强混合到下一级 cascade。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_DISTANCE_FADE)) // ShadowDistanceFade 模式显示远距离阴影淡出。
    {
        debugColor = float3(data.ShadowDistanceFade, data.ShadowDistanceFade, data.ShadowDistanceFade); // 白色表示已经淡出到无阴影。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_RADIUS)) // ShadowPCSSRadius 模式显示当前 PCSS 半影半径。
    {
        debugColor = float3(data.ShadowPCSSRadius, data.ShadowPCSSRadius, data.ShadowPCSSRadius); // 白色表示接近当前 PCSS 最大过滤半径。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_RECEIVER_DEPTH_DELTA)) // ShadowReceiverDepthDelta 模式显示 receiver 与 shadow map 深度差。
    {
        debugColor = float3(data.ShadowReceiverDepthDelta, data.ShadowReceiverDepthDelta, data.ShadowReceiverDepthDelta); // 0.5 灰表示基本对齐，越亮表示 acne 压力越大，越暗表示 bias 过强。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_BLOCKER_FRACTION)) // ShadowPCSSBlockerFraction 模式显示 blocker 搜索命中比例。
    {
        debugColor = float3(data.ShadowPCSSBlockerFraction, data.ShadowPCSSBlockerFraction, data.ShadowPCSSBlockerFraction); // 越白表示 blocker search 半径里越多采样落在遮挡体上。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_AMBIENT_OCCLUSION)) // AmbientOcclusion 模式显示参与间接光遮蔽的 AO。
    {
        debugColor = float3(data.AmbientOcclusion, data.AmbientOcclusion, data.AmbientOcclusion); // AO 越暗表示间接漫反射和间接高光越容易被压暗。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_EMISSION)) // Emission 模式只显示自发光贡献。
    {
        debugColor = max(data.EmissionColor, float3(0.0f, 0.0f, 0.0f)); // 保留 HDR 自发光强度但去掉负值，方便检查贴图和颜色是否进入最终颜色。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FINAL_LIGHTING)) // FinalLighting 模式显示写入 CameraColor 前的材质最终颜色。
    {
        debugColor = max(data.FinalLightingColor, float3(0.0f, 0.0f, 0.0f)); // 显示 PBR 光照加自发光后的最终材质输出，用来对比后处理前后的差异。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    return false; // Depth、Shadow、SSR、TAA 等全屏/后处理 debug 由各自 pass 接管，材质 shader 不在这里输出。
}

#endif // BURT_SHADING_DEBUG_MATERIAL_INCLUDED
