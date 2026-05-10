// BurtRP 的基础光照工具库，当前提供 Simple Lit、单主光 PBR 直接光，以及基于 Unity SH / Reflection Probe 的 PBR 间接光。
#ifndef BURT_LIGHTING_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义光照函数。
#define BURT_LIGHTING_INCLUDED // 标记 BurtLighting.hlsl 已经被包含过，后续重复 include 会被跳过。

#include "BurtCommon.hlsl" // 引入安全数学函数，例如 BurtSafeNormalize。
#include "BurtInput.hlsl" // 引入 BurtSurfaceData，用来读取材质基础色、金属度、光滑度和环境遮蔽。
#include "BurtBRDF.hlsl" // 引入 PBR BRDF 函数，当前单主光和临时间接高光都会复用这里的 Fresnel/粗糙度工具。

// 保存从当前着色点指向主方向光的世界空间方向，由 Burt Setup Lighting Pass 上传。
float4 _BurtMainLightDirection;

// 保存主方向光颜色，由 Burt Setup Lighting Pass 从 Unity 可见光数据中上传。
float4 _BurtMainLightColor;

// 保存 BurtRP 当前 Simple Lit 路径使用的环境光颜色，PBR 间接光会改用 Unity 内置 SH 和 Reflection Probe。
float4 _BurtAmbientLightColor;

// 保存 BurtRP 当前光照函数需要的一盏灯的数据。
struct BurtLight
{
    // 保存从表面点指向灯光的世界空间单位方向。
    float3 directionWS;

    // 保存灯光 RGB 颜色，用于直接漫反射和直接高光计算。
    float3 color;

    // 保存阴影可见性，1 表示完全受光，0 表示完全在阴影中。
    float shadowAttenuation;
};

// 根据 BurtRP 的全局 shader 变量创建当前主光数据。
BurtLight BurtCreateMainLight(float shadowAttenuation)
{
    // 创建一个输出光源结构体，下面逐项填充。
    BurtLight light;

    // 对上传的主光方向做安全归一化，避免方向长度影响 Lambert 或 BRDF 结果。
    light.directionWS = BurtSafeNormalize(_BurtMainLightDirection.xyz);

    // 拷贝主光颜色的 RGB 部分，忽略 alpha。
    light.color = _BurtMainLightColor.rgb;

    // 保存当前片元采样得到的阴影衰减值。
    light.shadowAttenuation = shadowAttenuation;

    // 返回填充完成的主光数据。
    return light;
}

// 获取 BurtRP 当前简单光照和 PBR 间接光使用的环境光颜色。
float3 BurtGetAmbientLightColor()
{
    // 返回 C# 上传的环境光 RGB，并把负值夹到 0，避免异常颜色产生负光照。
    return max(_BurtAmbientLightColor.rgb, float3(0.0f, 0.0f, 0.0f));
}

// 计算经典 Lambert 漫反射项。
float BurtLambert(float3 normalWS, float3 lightDirectionWS)
{
    // 点乘结果限制在 0 到 1，避免背光面产生负光照。
    return saturate(dot(normalWS, lightDirectionWS));
}

// 计算一盏 BurtLight 对表面的直接漫反射贡献。
float3 BurtEvaluateDiffuse(float3 baseColor, BurtLight light, float3 normalWS)
{
    // 计算 N dot L，表示表面朝向光源的程度。
    float diffuseTerm = BurtLambert(normalWS, light.directionWS);

    // 把 albedo、灯光颜色、Lambert 项和阴影衰减相乘，得到直接漫反射颜色。
    return baseColor * light.color * diffuseTerm * light.shadowAttenuation;
}

// 计算带环境遮蔽的环境光部分。
float3 BurtEvaluateAmbientOccluded(float3 baseColor, float3 ambientColor, float occlusion)
{
    // 用 albedo、环境光颜色和 AO 相乘，让 Mask Map 只压暗间接环境项，不影响直接光。
    return baseColor * ambientColor * saturate(occlusion);
}

// 计算当前简单 Lit 模型的环境光部分。
float3 BurtEvaluateAmbient(float3 baseColor, float3 ambientColor)
{
    // 旧调用不传 occlusion，所以默认按 1 处理，保持原有环境光亮度。
    return BurtEvaluateAmbientOccluded(baseColor, ambientColor, 1.0f);
}

// 采样 BurtRP 当前的间接漫反射环境照度。
float3 BurtSampleIndirectDiffuseIrradiance(float3 normalWS)
{
    // 归一化世界空间法线，让 Unity SH 查询使用稳定方向。
    float3 safeNormalWS = BurtSafeNormalize(normalWS);

    // 把 normal 扩展成 float4，因为 Unity 的 ShadeSH9 约定 xyz 是方向，w 是常数项。
    float4 shNormal = float4(safeNormalWS, 1.0f);

    // 直接读取 Unity 内置 spherical harmonics，也就是 Lighting/Light Probe 写入的环境漫反射。
    float3 shIrradiance = ShadeSH9(shNormal);

    // 返回非负环境照度，避免 SH 过冲时产生负光。
    return max(shIrradiance, float3(0.0f, 0.0f, 0.0f));
}


// 出处：XRender/Shaders/Library/ShadingIBL.hlsl::ComputeReflectionCaptureMipFromRoughness；roughness 到反射探针 mip 的 log2 曲线。
float ComputeReflectionCaptureMipFromRoughness(float perceptualRoughness, float cubemapMaxMip)
{
    // log2 曲线不能接受 0，所以使用 BurtRP 的最小感知粗糙度保护镜面端。
    float safeRoughness = max(saturate(perceptualRoughness), BURT_MIN_PERCEPTUAL_ROUGHNESS);

    // 对齐 XRender / UE 的启发式：粗糙端走高 mip，光滑端尽量贴近 mip0。
    float levelFrom1x1 = 1.0f - 1.2f * log2(safeRoughness);

    // Unity 内置 spec cube 常见有效 mip 近似为 0..6，这里由调用方传入上限方便 Deferred 后续替换资源。
    return clamp(cubemapMaxMip - 1.0f - levelFrom1x1, 0.0f, cubemapMaxMip);
}

// BurtRP 适配函数：采样 Unity 当前绑定的 reflection probe / sky reflection cubemap。
float3 SampleIndirectSpecularRadiance(float3 reflectionDirectionWS, float roughness)
{
    // 归一化反射方向，让 cubemap 采样方向稳定。
    float3 safeReflectionDirectionWS = BurtSafeNormalize(reflectionDirectionWS);

    // 使用 XRender 风格 roughness->mip 曲线，避免高 smoothness 反射被线性映射过早打糊。
    const float maxMipLevel = 6.0f;
    float mipLevel = ComputeReflectionCaptureMipFromRoughness(roughness, maxMipLevel);

    // 采样 Unity 当前绑定的 reflection probe / sky reflection cubemap，恢复之前的探针反射效果。
    float4 encodedSpecular = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, safeReflectionDirectionWS, mipLevel);

    // 使用 Unity 提供的 HDR 解码参数把 RGBM/编码反射颜色还原为线性 HDR 颜色。
    float3 specularRadiance = DecodeHDR(encodedSpecular, unity_SpecCube0_HDR);

    // 返回非负环境镜面颜色，避免异常编码产生负光。
    return max(specularRadiance, float3(0.0f, 0.0f, 0.0f));
}

// 计算 PBR 间接漫反射：环境 irradiance 乘以能量守恒后的 diffuseColor。
float3 BurtEvaluateIndirectDiffusePBR(BurtSurfaceData surfaceData, float3 normalWS)
{
    // 根据世界空间法线采样 Unity SH，得到当前方向的环境漫反射照度。
    float3 diffuseIrradiance = BurtSampleIndirectDiffuseIrradiance(normalWS);

    // 取出 PBR 的 diffuseColor，金属材质会自动降低或移除漫反射。
    float3 diffuseColor = DiffuseColorFromBaseColor(surfaceData.baseColor.rgb, surfaceData.metallic);

    // 把 diffuseColor、环境照度和 AO 相乘，得到间接漫反射贡献。
    return diffuseColor * diffuseIrradiance * saturate(surfaceData.occlusion);
}

// 计算 PBR 间接镜面反射：Reflection Probe radiance 乘以视角 Fresnel。
float3 BurtEvaluateIndirectSpecularPBR(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS)
{
    // 安全归一化法线，保证 reflect 和 NdotV 稳定。
    float3 n = BurtSafeNormalize(normalWS);

    // 安全归一化视线方向，当前约定 viewDirectionWS 是从表面指向相机。
    float3 v = BurtSafeNormalize(viewDirectionWS);

    // 根据观察方向和法线计算环境反射方向，reflect 的入射方向需要从相机指向表面，所以使用 -v。
    float3 reflectionDirectionWS = reflect(-v, n);

    // 从 smoothness 得到感知 roughness，同时也用于选择 reflection probe 的模糊 mip。
    float roughness = GetSurfacePerceptualRoughness(surfaceData);

    // 采样 Unity 当前 reflection probe / sky reflection，得到已经按 roughness 预过滤过的环境高光。
    float3 specularRadiance = SampleIndirectSpecularRadiance(reflectionDirectionWS, roughness);

    // 计算当前材质的 F0，非金属接近 0.04，金属使用 baseColor。
    float3 f0 = DielectricReflectanceToF0(surfaceData.baseColor.rgb, surfaceData.reflectance, surfaceData.metallic);

    // 计算当前材质的 F90，用来让环境高光和直接高光使用同一套 Fresnel 端点。
    float3 f90 = ApproximateF90(f0);

    // 计算 NdotV，视线越掠射 DFG 近似会给出越强的边缘反射权重。
    float nDotV = saturate(dot(n, v));

    // 优先从 PreintegratedFG LUT 读取 DFG.xy，未绑定时回退到解析近似。
    float2 dfg = GetSpecularDFGTerms(roughness, nDotV);

    // 把 DFG 应用到 F0/F90 上，比单纯 Fresnel 更接近预积分环境 BRDF。
    float3 envBRDF = EvalSpecularDFG(f0, f90, dfg);

    // 根据 AO、NdotV 和粗糙度计算间接高光遮蔽。
    // 用 Specular Occlusion 替代直接乘 AO，保留掠射角高光。
    float specularOcclusion = GetIndirectSpecularOcclusion(nDotV, surfaceData.occlusion, roughness);

    // 只影响间接镜面反射，直接高光仍由阴影和 NdotL 控制。
    return specularRadiance * envBRDF * specularOcclusion;
}

// 保存 PBR 间接光拆分结果，Deferred 后续可以复用同一套 SH / Reflection Probe 评估逻辑。
struct BurtIndirectPBRComponents
{
    // 保存间接漫反射贡献，数据来源是 Unity SH / Light Probe。
    float3 diffuse;

    // 保存间接镜面贡献，数据来源是 Unity Reflection Probe / Sky Reflection。
    float3 specular;
};

// 计算 PBR 间接光拆分结果，让 Forward 和未来 Deferred 都能拿到一致的间接漫反射与间接高光。
BurtIndirectPBRComponents BurtEvaluateIndirectPBRComponents(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS)
{
    // 创建输出结构体，下面分别写入 diffuse 和 specular。
    BurtIndirectPBRComponents components;

    // 计算来自 Unity SH / Light Probe 的漫反射环境光。
    components.diffuse = BurtEvaluateIndirectDiffusePBR(surfaceData, normalWS);

    // 计算来自 Unity Reflection Probe / Sky Reflection 的镜面环境光。
    components.specular = BurtEvaluateIndirectSpecularPBR(surfaceData, normalWS, viewDirectionWS);

    // 返回拆分后的间接光，Debug View 和 Deferred 光照都可以直接读取。
    return components;
}

// 计算 PBR 间接光总和：间接漫反射 + 间接镜面反射。
float3 BurtEvaluateIndirectPBR(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS)
{
    // 复用拆分版本，保证总和接口与 Debug 拆项使用完全相同的结果。
    BurtIndirectPBRComponents components = BurtEvaluateIndirectPBRComponents(surfaceData, normalWS, viewDirectionWS);

    // 返回完整间接光，后续会和主光直接光相加。
    return components.diffuse + components.specular;
}

// 保存一次完整 PBR shading 的可复用拆分结果，Forward 只负责调用，Deferred 后续可从 GBuffer 还原输入后复用。
struct BurtPBRShadingComponents
{
    // 保存直接漫反射贡献，已经包含主光颜色、NdotL 和阴影。
    float3 directDiffuse;

    // 保存直接镜面高光贡献，已经包含 GGX、Fresnel、主光颜色、NdotL 和阴影。
    float3 directSpecular;

    // 保存直接光总和，等于 directDiffuse + directSpecular。
    float3 directLighting;

    // 保存间接漫反射贡献，主要来自 Unity SH / Light Probe。
    float3 indirectDiffuse;

    // 保存间接镜面高光贡献，主要来自 Unity Reflection Probe / Sky Reflection。
    float3 indirectSpecular;

    // 保存间接光总和，等于 indirectDiffuse + indirectSpecular。
    float3 indirectLighting;

    // 保存最终 PBR 光照，等于 directLighting + indirectLighting，不包含自发光。
    float3 lighting;

    // 保存材质感知粗糙度，也就是 1 - smoothness 后并经过最小粗糙度保护的结果。
    float perceptualRoughness;

    // 保存直接高光实际使用的感知粗糙度，包含 Specular AA 对极光滑高光的拓宽。
    float specularAARoughness;

    // 保存直接高光能量补偿，方便 Debug View 和 Deferred 调试确认 LUT.z 的影响。
    float3 specularEnergyCompensation;

    // 保存间接高光遮蔽，方便 Debug View 和 Deferred 调试确认 AO 对反射探针的影响。
    float specularOcclusion;
};

// 统一评估一次完整 PBR shading；Forward 和未来 Deferred 都应优先调用这个入口拿拆分结果。
BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS)
{
    // 创建输出结构体，下面逐项填充，避免调用方自己重复拼装 PBR 结果。
    BurtPBRShadingComponents components;

    // 先计算间接光拆分结果，保证 SH 和 Reflection Probe 的来源在一个入口内管理。
    BurtIndirectPBRComponents indirectComponents = BurtEvaluateIndirectPBRComponents(surfaceData, normalWS, viewDirectionWS);

    // 保存间接漫反射结果，Debug View 可以直接显示这一项。
    components.indirectDiffuse = indirectComponents.diffuse;

    // 保存间接镜面结果，Debug View 可以直接显示这一项。
    components.indirectSpecular = indirectComponents.specular;

    // 合并间接漫反射和间接镜面，得到完整间接光。
    components.indirectLighting = components.indirectDiffuse + components.indirectSpecular;

    // 计算直接光拆分结果，未来多光源或 Deferred 光照也应继续复用这个 BRDF 入口。
    BurtDirectPBRComponents directComponents = BurtEvaluateDirectPBRComponents(surfaceData, mainLight.color, mainLight.directionWS, normalWS, viewDirectionWS, mainLight.shadowAttenuation);

    // 保存直接漫反射结果，已经包含灯光颜色、NdotL 和阴影。
    components.directDiffuse = directComponents.diffuse;

    // 保存直接镜面结果，已经包含 GGX、Fresnel、能量补偿、灯光颜色、NdotL 和阴影。
    components.directSpecular = directComponents.specular;

    // 合并直接漫反射和直接镜面，得到完整直接光。
    components.directLighting = components.directDiffuse + components.directSpecular;

    // 合并直接光和间接光，得到不含自发光的 PBR 总光照。
    components.lighting = components.directLighting + components.indirectLighting;

    // 保存材质本身的感知粗糙度，Deferred 调试时可以直接验证 GBuffer 中的 smoothness 还原是否正确。
    components.perceptualRoughness = GetSurfacePerceptualRoughness(surfaceData);

    // 保存直接高光使用的 AA 后粗糙度，用来观察极光滑高光是否被屏幕空间法线变化拓宽。
    components.specularAARoughness = GetDirectSpecularPerceptualRoughness(surfaceData, normalWS);

    // 归一化法线，后续 NdotV、DFG 和遮蔽项都使用同一条安全法线。
    float3 n = BurtSafeNormalize(normalWS);

    // 归一化视线方向，确保从 Forward 插值或 Deferred 重建得到的方向都稳定。
    float3 v = BurtSafeNormalize(viewDirectionWS);

    // 计算 NdotV，供能量补偿和间接高光遮蔽复用。
    float nDotV = saturate(dot(n, v));

    // 保存直接高光能量补偿，使用和直接 BRDF 一致的 F0、AA 后粗糙度和 NdotV。
    components.specularEnergyCompensation = GetSpecularEnergyCompensation(DielectricReflectanceToF0(surfaceData.baseColor.rgb, surfaceData.reflectance, surfaceData.metallic), components.specularAARoughness, nDotV);

    // 保存间接高光遮蔽，使用材质 AO、NdotV 和未 AA 的感知粗糙度，与间接高光路径保持一致。
    components.specularOcclusion = GetIndirectSpecularOcclusion(nDotV, surfaceData.occlusion, components.perceptualRoughness);

    // 返回完整拆分结果，调用方只需要决定是否叠加自发光或进入 Debug View。
    return components;
}

// 计算 Blinn-Phong 高光项，用来给旧 Simple Lit 路径保留第一版 specular。
float3 BurtEvaluateSpecular(BurtSurfaceData surfaceData, BurtLight light, float3 normalWS, float3 viewDirectionWS)
{
    // 计算 N dot L，保证背光面不会产生不合理的高光。
    float diffuseVisibility = BurtLambert(normalWS, light.directionWS);

    // 把光线方向和视线方向相加并归一化，得到 Blinn-Phong 使用的半角向量。
    float3 halfDirectionWS = BurtSafeNormalize(light.directionWS + viewDirectionWS);

    // 计算法线和半角向量的夹角，数值越接近 1 表示越接近镜面反射方向。
    float specularNdotH = saturate(dot(normalWS, halfDirectionWS));

    // 把 0 到 1 的 smoothness 映射到高光指数，smoothness 越高高光越集中。
    float specularPower = lerp(8.0f, 256.0f, surfaceData.smoothness);

    // 用 pow 计算 Blinn-Phong 高光强度。
    float specularTerm = pow(specularNdotH, specularPower);

    // 把内部 F0、灯光颜色、受光可见性和阴影衰减相乘得到最终高光。
    return DielectricReflectanceToF0(surfaceData.baseColor.rgb, surfaceData.reflectance, surfaceData.metallic) * light.color * specularTerm * diffuseVisibility * light.shadowAttenuation;
}

// 计算 BurtRP 当前的完整简单 Lit 模型：环境光 + 一个带阴影的 Lambert 主光。
float3 BurtEvaluateSimpleLit(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS)
{
    // 使用材质基础色和全局环境光计算 ambient 部分。
    float3 ambientColor = BurtEvaluateAmbient(surfaceData.baseColor.rgb, BurtGetAmbientLightColor());

    // 使用材质基础色、主光数据和法线计算 direct diffuse 部分。
    float3 diffuseColor = BurtEvaluateDiffuse(surfaceData.baseColor.rgb, mainLight, normalWS);

    // 返回环境光和直接光相加后的最终 RGB。
    return ambientColor + diffuseColor;
}

// 计算带高光的旧 Simple Lit 模型：环境光 + 漫反射主光 + Blinn-Phong 高光。
float3 BurtEvaluateSimpleLitSpecular(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS)
{
    // 先复用已有简单 Lit，得到环境光和漫反射直接光。
    float3 baseLighting = BurtEvaluateSimpleLit(surfaceData, mainLight, normalWS);

    // 再额外计算主光高光项。
    float3 specularLighting = BurtEvaluateSpecular(surfaceData, mainLight, normalWS, viewDirectionWS);

    // 返回基础光照和高光相加后的结果。
    return baseLighting + specularLighting;
}

// 计算单主光 PBR 光照：PBR 间接光 + Cook-Torrance 直接光。
float3 BurtEvaluateSimpleLitPBR(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS)
{
    // 复用统一 PBR shading 入口，避免 Forward 和未来 Deferred 维护两套组合逻辑。
    BurtPBRShadingComponents components = BurtEvaluatePBRShadingComponents(surfaceData, mainLight, normalWS, viewDirectionWS);

    // 返回不含自发光的完整 PBR 光照。
    return components.lighting;
}

#endif // BURT_LIGHTING_INCLUDED // 结束 BurtLighting.hlsl 的 include guard。
