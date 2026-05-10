// BurtRP 的基础光照工具库，当前提供 Simple Lit、单主光 PBR 直接光，以及 BurtRP 自有全局 SH / Sky Reflection 的 PBR 间接光。
#ifndef BURT_LIGHTING_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义光照函数。
#define BURT_LIGHTING_INCLUDED // 标记 BurtLighting.hlsl 已经被包含过，后续重复 include 会被跳过。

#include "BurtCommon.hlsl" // 引入安全数学函数，例如 BurtSafeNormalize。
#include "BurtInput.hlsl" // 引入 BurtSurfaceData，用来读取材质基础色、金属度、光滑度和环境遮蔽。
#include "BurtBRDF.hlsl" // 引入 PBR BRDF 函数，当前单主光和临时间接高光都会复用这里的 Fresnel/粗糙度工具。
#include "BurtGBuffer.hlsl" // 引入 shader-only GBuffer 编解码和 PBRData 桥接函数；这里不采样真实 RT，也不触碰 RenderTarget 生命周期。

// 保存从当前着色点指向主方向光的世界空间方向，由 Burt Setup Lighting Pass 上传。
float4 _BurtMainLightDirection;

// 保存主方向光颜色，由 Burt Setup Lighting Pass 从 Unity 可见光数据中上传。
float4 _BurtMainLightColor;

// 保存 BurtRP 当前环境光颜色，Simple Lit 会直接使用它，PBR 间接光会把它作为 SH / Sky Reflection 兜底。
float4 _BurtAmbientLightColor;

// 保存 BurtRP 自己上传的 ambient probe SH R 通道 L0/L1 常量，Deferred 全屏 Pass 会优先使用它。
float4 _BurtAmbientSHAr;

// 保存 BurtRP 自己上传的 ambient probe SH G 通道 L0/L1 常量，Deferred 全屏 Pass 会优先使用它。
float4 _BurtAmbientSHAg;

// 保存 BurtRP 自己上传的 ambient probe SH B 通道 L0/L1 常量，Deferred 全屏 Pass 会优先使用它。
float4 _BurtAmbientSHAb;

// 保存 BurtRP 自己上传的 ambient probe SH R 通道 L2 常量，匹配 UnityCG ShadeSH9 打包方式。
float4 _BurtAmbientSHBr;

// 保存 BurtRP 自己上传的 ambient probe SH G 通道 L2 常量，匹配 UnityCG ShadeSH9 打包方式。
float4 _BurtAmbientSHBg;

// 保存 BurtRP 自己上传的 ambient probe SH B 通道 L2 常量，匹配 UnityCG ShadeSH9 打包方式。
float4 _BurtAmbientSHBb;

// 保存 BurtRP 自己上传的 ambient probe SH C 项常量，匹配 UnityCG ShadeSH9 打包方式。
float4 _BurtAmbientSHC;

// 标记 BurtRP 自己的 ambient SH 是否已经上传，1 表示可用，0 表示回退到 Unity 内置 ShadeSH9。
float _BurtAmbientSHEnabled;

// 保存 BurtRP 全局天空反射 cubemap；UNITY_DECLARE_TEXCUBE 会同时声明纹理和 sampler_BurtSkyReflectionTexture，匹配 UNITY_SAMPLE_TEXCUBE_LOD 的宏展开。
UNITY_DECLARE_TEXCUBE(_BurtSkyReflectionTexture);

// 保存 BurtRP 全局天空反射 HDR 解码参数，当前第一版默认按原始 RGB 使用。
float4 _BurtSkyReflectionHDR;

// 保存 BurtRP 全局天空反射强度，对应 Unity Lighting 面板的 Reflection Intensity。
float _BurtSkyReflectionIntensity;

// Optional tint supplied by BurtSkyLight; legacy RenderSettings path uploads white.
float4 _BurtSkyReflectionTint;

// 标记 BurtRP 全局天空反射 cubemap 是否可用，0 表示回退到环境光颜色。
float _BurtSkyReflectionEnabled;

// 标记 SkyLight 是否显式接管 specular；启用时无 cubemap/零强度应返回黑色，而不是回退 unity_SpecCube0。
float _BurtSkyReflectionOverride;

// 保存 BurtRP 全局天空反射 cubemap 的最大 mip 索引，避免所有 cubemap 都写死按 0..6 采样。
float _BurtSkyReflectionMaxMip;

// 保存 SkyLight 指定 cubemap 的水平旋转参数，xy 分别是 cos/sin。
float4 _BurtSkyReflectionRotation;

// 保存 SkyLight 下半球覆盖参数；diffuse/specular 分开预乘各自强度，alpha 是覆盖混合权重。
float _BurtSkyLowerHemisphereEnabled;
float4 _BurtSkyLowerHemisphereDiffuseColor;
float4 _BurtSkyLowerHemisphereSpecularColor;

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

float BurtLuminanceForIndirectFallback(float3 color) // 计算一个很轻量的亮度值，用来判断 Unity 内置间接光变量是否明显为黑。
{
    return dot(max(color, float3(0.0f, 0.0f, 0.0f)), float3(0.2126f, 0.7152f, 0.0722f)); // 使用 Rec.709 亮度权重，先去掉负值避免 SH 过冲影响判断。
}

float3 BurtSelectIndirectFallbackIfBlack(float3 sampledColor, float3 fallbackColor) // 当 Unity 内置 SH 或 Reflection Probe 没绑定时，回退到 BurtRP 上传的环境光。
{
    float sampledLuminance = BurtLuminanceForIndirectFallback(sampledColor); // 计算采样结果亮度，后续用它判断是否接近全黑。

    float useSampledColor = step(0.0001f, sampledLuminance); // 亮度大于阈值时认为 Unity 内置间接光有效，否则走 fallback。

    return lerp(max(fallbackColor, float3(0.0f, 0.0f, 0.0f)), max(sampledColor, float3(0.0f, 0.0f, 0.0f)), useSampledColor); // 在黑色采样时使用环境色兜底，非黑时保留真实 SH/Probe 结果。
}

float3 BurtApplySkyLowerHemisphere(float3 sourceColor, float3 directionWS, float4 lowerHemisphereColor)
{
    float lowerBlend = (_BurtSkyLowerHemisphereEnabled > 0.5f && BurtSafeNormalize(directionWS).y < 0.0f) ? saturate(lowerHemisphereColor.a) : 0.0f;
    return lerp(max(sourceColor, float3(0.0f, 0.0f, 0.0f)), max(lowerHemisphereColor.rgb, float3(0.0f, 0.0f, 0.0f)), lowerBlend);
}

float3 BurtRotateSkyReflectionDirection(float3 directionWS)
{
    float3 safeDirectionWS = BurtSafeNormalize(directionWS);
    float cosPhi = _BurtSkyReflectionRotation.x;
    float sinPhi = _BurtSkyReflectionRotation.y;
    float3 rotDirX = float3(cosPhi, 0.0f, -sinPhi);
    float3 rotDirZ = float3(sinPhi, 0.0f, cosPhi);
    return BurtSafeNormalize(float3(dot(rotDirX, safeDirectionWS), safeDirectionWS.y, dot(rotDirZ, safeDirectionWS)));
}

float3 BurtEvaluateAmbientSH9(float3 normalWS) // 使用 BurtRP 自己上传的 SH 常量评估环境漫反射照度。
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS); // 先安全归一化世界空间法线，避免异常法线放大 SH 结果。

    float4 shNormal = float4(safeNormalWS, 1.0f); // 组装 UnityCG ShadeSH9 所需的 normal.xyzw，其中 w 是常数项。

    float3 linearL0L1; // 创建 L0/L1 输出变量，下面逐通道 dot SH 常量。

    linearL0L1.r = dot(_BurtAmbientSHAr, shNormal); // 计算 R 通道 L0/L1 环境照度。

    linearL0L1.g = dot(_BurtAmbientSHAg, shNormal); // 计算 G 通道 L0/L1 环境照度。

    linearL0L1.b = dot(_BurtAmbientSHAb, shNormal); // 计算 B 通道 L0/L1 环境照度。

    float4 vB = shNormal.xyzz * shNormal.yzzx; // 组装 UnityCG 二阶 SH 使用的 xy、yz、zz、zx 项。

    float3 linearL2; // 创建 L2 输出变量，下面逐通道 dot SHB 常量。

    linearL2.r = dot(_BurtAmbientSHBr, vB); // 计算 R 通道二阶 SH 中的 B 项。

    linearL2.g = dot(_BurtAmbientSHBg, vB); // 计算 G 通道二阶 SH 中的 B 项。

    linearL2.b = dot(_BurtAmbientSHBb, vB); // 计算 B 通道二阶 SH 中的 B 项。

    float vC = safeNormalWS.x * safeNormalWS.x - safeNormalWS.y * safeNormalWS.y; // 计算 UnityCG 二阶 SH 的 C 项输入。

    float3 shIrradiance = linearL0L1 + linearL2 + _BurtAmbientSHC.rgb * vC; // 合成完整 L0/L1/L2 环境照度。
    shIrradiance = BurtApplySkyLowerHemisphere(shIrradiance, safeNormalWS, _BurtSkyLowerHemisphereDiffuseColor); // SkyLight 要求下半球固定颜色时，在当前方向覆盖 diffuse irradiance。

#ifdef UNITY_COLORSPACE_GAMMA // 如果项目运行在 Gamma 色彩空间，需要对齐 Unity Core SampleSH9 的 Gamma 输出语义。
    shIrradiance = pow(max(shIrradiance, float3(0.0f, 0.0f, 0.0f)), 1.0f / 2.2f); // 把线性 SH 结果近似转换到 Gamma 空间，避免自有 SH 和 ShadeSH9 fallback 明显不一致。
#endif // 结束 Gamma 色彩空间下的 SH 输出转换。

    return max(shIrradiance, float3(0.0f, 0.0f, 0.0f)); // 去掉 SH 过冲产生的负值，保持间接光非负。
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
    if (_BurtAmbientSHEnabled > 0.5f) // 如果 C# 已经上传 BurtRP 自己的 ambient probe SH。
    {
        return BurtEvaluateAmbientSH9(normalWS); // 直接使用稳定的 BurtRP 全局 SH 数据源，避免 Deferred fullscreen pass 依赖 per-object 状态。
    }

    float3 safeNormalWS = BurtSafeNormalize(normalWS); // 归一化世界空间法线，让 Unity SH 查询使用稳定方向。

    float4 shNormal = float4(safeNormalWS, 1.0f); // 把 normal 扩展成 float4，因为 Unity 的 ShadeSH9 约定 xyz 是方向，w 是常数项。

    float3 shIrradiance = ShadeSH9(shNormal); // 兼容旧路径：直接读取 Unity 内置 spherical harmonics。
    shIrradiance = BurtApplySkyLowerHemisphere(shIrradiance, safeNormalWS, _BurtSkyLowerHemisphereDiffuseColor); // 让 legacy SH fallback 也遵守 SkyLight 下半球设置。

    // 当 Unity 内置 SH 没有被 DrawRenderers 刷新时，用 _BurtAmbientLightColor 兜底，避免间接漫反射全黑。
    return BurtSelectIndirectFallbackIfBlack(shIrradiance, BurtGetAmbientLightColor());
}


// XRender SkyLight 以 256 cubemap 的 9 个 mip 为基准；这里使用 max mip index 8 作为有效输入上限。
static const float BURT_REFLECTION_CAPTURE_SPECULAR_MIP_MAX_INDEX = 8.0f;

// 出处：XRender/Shaders/Library/ShadingIBL.hlsl::ComputeReflectionCaptureMipFromRoughness；roughness 到反射探针 mip 的 log2 曲线。
float ComputeReflectionCaptureMipFromRoughness(float perceptualRoughness, float cubemapMaxMipIndex)
{
    // C# 上传的是真实最大 mip 索引；XRender 原函数输入 mip count 后会先减 1，所以这里直接使用 max index。
    float specularMipMaxIndex = min(max(cubemapMaxMipIndex, 0.0f), BURT_REFLECTION_CAPTURE_SPECULAR_MIP_MAX_INDEX);

    // log2 曲线不能接受 0，所以使用 BurtRP 的最小感知粗糙度保护镜面端。
    float safeRoughness = max(saturate(perceptualRoughness), BURT_MIN_PERCEPTUAL_ROUGHNESS);

    // 对齐 XRender / UE 的启发式：粗糙端走高 mip，光滑端尽量贴近 mip0。
    float levelFrom1x1 = 1.0f - 1.2f * log2(safeRoughness);

    // 对齐 XRender：256 sky 的 max index 为 8，roughness=1 会落到 mip6，而不是之前错误的 mip4。
    return clamp(specularMipMaxIndex - 1.0f - levelFrom1x1, 0.0f, specularMipMaxIndex);
}

// BurtRP 适配函数：采样 BurtRP 全局天空反射 cubemap，旧路径才回退 Unity 当前绑定的 reflection probe。
float3 SampleIndirectSpecularRadiance(float3 reflectionDirectionWS, float roughness)
{
    // 归一化反射方向，让 cubemap 采样方向稳定。
    float3 safeReflectionDirectionWS = BurtSafeNormalize(reflectionDirectionWS);

    if (_BurtSkyReflectionEnabled > 0.5f) // 如果 C# 已经上传 BurtRP 全局天空反射 cubemap。
    {
        float3 skySampleDirectionWS = BurtRotateSkyReflectionDirection(safeReflectionDirectionWS); // 对指定 cubemap 应用 XRender 风格水平旋转；默认参数为 identity。

        float skyReflectionMaxMip = max(_BurtSkyReflectionMaxMip, 0.0f); // 使用 C# 上传的实际 mip 上限，避免不同尺寸 cubemap 都套用固定 6。

        float skyReflectionMipLevel = ComputeReflectionCaptureMipFromRoughness(roughness, skyReflectionMaxMip); // 使用 XRender 风格 roughness->mip 曲线计算全局天空反射 LOD。

        float4 encodedSkyReflection = UNITY_SAMPLE_TEXCUBE_LOD(_BurtSkyReflectionTexture, skySampleDirectionWS, skyReflectionMipLevel); // 按 roughness mip 采样全局天空反射。

        float3 skyReflectionRadiance = DecodeHDR(encodedSkyReflection, _BurtSkyReflectionHDR) * max(_BurtSkyReflectionTint.rgb, float3(0.0f, 0.0f, 0.0f)) * max(0.0f, _BurtSkyReflectionIntensity); // 解码 HDR 并乘 Lighting 面板的反射强度。
        skyReflectionRadiance = BurtApplySkyLowerHemisphere(skyReflectionRadiance, safeReflectionDirectionWS, _BurtSkyLowerHemisphereSpecularColor); // 没有离线 convolve 时，用方向覆盖近似 XRender 的 lower hemisphere 输入处理。

        return max(skyReflectionRadiance, float3(0.0f, 0.0f, 0.0f)); // 显式 sky reflection 数据源启用后尊重 cubemap 本身颜色，不再把黑色 custom reflection 误判成需要环境色兜底。
    }

    if (_BurtSkyReflectionOverride > 0.5f)
    {
        return BurtApplySkyLowerHemisphere(float3(0.0f, 0.0f, 0.0f), safeReflectionDirectionWS, _BurtSkyLowerHemisphereSpecularColor);
    }

    const float legacyUnitySpecCubeMaxMip = 6.0f; // Unity 内置 reflection probe legacy 路径暂时保留 0..6 的常见 mip 上限，后续有专用数据源后再替换。

    float legacyUnitySpecCubeMipLevel = ComputeReflectionCaptureMipFromRoughness(roughness, legacyUnitySpecCubeMaxMip); // 为 legacy unity_SpecCube0 路径单独计算 mip，避免误用全局 sky 的 mip 上限。

    // 兼容旧路径：采样 Unity 当前绑定的 reflection probe / sky reflection cubemap。
    float4 encodedSpecular = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, safeReflectionDirectionWS, legacyUnitySpecCubeMipLevel);

    // 使用 Unity 提供的 HDR 解码参数把 RGBM/编码反射颜色还原为线性 HDR 颜色。
    float3 specularRadiance = DecodeHDR(encodedSpecular, unity_SpecCube0_HDR);

    // 当 Deferred 全屏 Pass 没有 Renderer per-object Reflection Probe 时，用环境色做低频镜面兜底。
    return BurtSelectIndirectFallbackIfBlack(specularRadiance, BurtGetAmbientLightColor());
}

// 计算 PBR 间接漫反射：环境 irradiance 乘以 XRender EnergyPreservation 后的 diffuseColor。
float3 BurtEvaluateIndirectDiffusePBR(BurtPBRMaterialData materialData, float3 normalWS, float energyPreservation)
{
    // 根据世界空间法线采样 Unity SH，得到当前方向的环境漫反射照度。
    float3 diffuseIrradiance = BurtSampleIndirectDiffuseIrradiance(normalWS);

    // XRender 的 SlabOperator_Layering 会用 EnergyPreservation 缩放底层 diffuse，这里让 SH 间接漫反射也走同一保能比例。
    return materialData.diffuseColor * diffuseIrradiance * saturate(materialData.occlusion) * saturate(energyPreservation);
}

// 保留 SurfaceData 旧入口：内部准备 PBRMaterialData，避免旧调用绕过 EnergyPreservation。
float3 BurtEvaluateIndirectDiffusePBR(BurtSurfaceData surfaceData, float3 normalWS, float energyPreservation)
{
    // 从 SurfaceData 准备材质数据；Deferred 后续会直接传入 BurtPBRMaterialData。
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    // 复用准备数据版本，保证旧接口和 Deferred 入口输出一致。
    return BurtEvaluateIndirectDiffusePBR(materialData, normalWS, energyPreservation);
}

// BurtRP 适配函数：对齐 XRender GenericData.EnergyCompensation_GGX，间接高光直接读取统一准备好的 EnergyTerms。
float3 GetIndirectSpecularEnergyCompensation(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, BurtPBREnergyTerms energyTerms)
{
    // materialData 和 geometryData 保留在签名里，方便调用点看出这个值依赖材质 F0、Base.Roughness 和 NdotV。
    return energyTerms.indirectSpecularEnergyCompensation;
}

// 保留 SurfaceData 旧入口：内部统一准备 material / geometry / energy terms 后返回间接高光补偿。
float3 GetIndirectSpecularEnergyCompensation(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS)
{
    // 从 SurfaceData 准备材质数据；Deferred 后续可以从 GBuffer 还原同一结构。
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    // 准备几何数据，统一 NdotV 和 reflection direction 的来源。
    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, viewDirectionWS);

    // 直接高光 roughness 只用于填充完整 energy terms，间接补偿本身仍使用材质 Base.Roughness。
    float directSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(materialData, geometryData);

    // 一次性准备所有 energy terms，再返回间接高光需要的那一项。
    BurtPBREnergyTerms energyTerms = BurtPreparePBREnergyTerms(materialData, geometryData, directSpecularPerceptualRoughness);

    // 返回 XRender EnergyCompensation_GGX，对应 Slab_SkySpecular / Slab_EnvProbeSpecular 中的 Fs *= EnergyCompensation_GGX。
    return GetIndirectSpecularEnergyCompensation(materialData, geometryData, energyTerms);
}

// 计算 PBR 间接镜面反射：Reflection Probe radiance 乘以视角 Fresnel 和 XRender EnergyCompensation_GGX。
float3 BurtEvaluateIndirectSpecularPBR(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, float3 indirectSpecularEnergyCompensation)
{
    // 从准备好的几何数据读取环境反射方向，Forward 和 Deferred 保持同一 reflect 约定。
    float3 reflectionDirectionWS = geometryData.reflectionDirectionWS;

    // 间接高光使用材质 Base.Roughness，同时也用于选择 reflection probe 的模糊 mip。
    float roughness = materialData.perceptualRoughness;

    // 采样 Unity 当前 reflection probe / sky reflection，得到已经按 roughness 预过滤过的环境高光。
    float3 specularRadiance = SampleIndirectSpecularRadiance(reflectionDirectionWS, roughness);

    // 优先从 PreintegratedFG LUT 读取 DFG.xy，未绑定时回退到解析近似。
    float2 dfg = GetSpecularDFGTerms(roughness, geometryData.nDotV);

    // 把 DFG 应用到 F0/F90 上，比单纯 Fresnel 更接近预积分环境 BRDF。
    float3 envBRDF = EvalSpecularDFG(materialData.f0, materialData.f90, dfg);

    // 根据 AO、NdotV 和粗糙度计算间接高光遮蔽；用 Specular Occlusion 替代直接乘 AO，保留掠射角高光。
    float specularOcclusion = GetIndirectSpecularOcclusion(geometryData.nDotV, materialData.occlusion, roughness);

    // 出处：XRender/Shaders/SlabsIndirectLight/Slab_SkySpecular.hlsl 与 Slab_EnvProbeSpecular.hlsl；先对 Fs 做能量补偿，再乘 SpecularOcclusion。
    return specularRadiance * envBRDF * indirectSpecularEnergyCompensation * specularOcclusion;
}

// 保留 SurfaceData 旧入口：调用方不关心拆项时，在函数内部按 XRender 规则计算 indirect specular energy compensation。
float3 BurtEvaluateIndirectSpecularPBR(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS, float3 indirectSpecularEnergyCompensation)
{
    // 从 SurfaceData 准备材质数据，保持旧接口和准备数据版本一致。
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    // 准备几何数据，统一 NdotV 和 reflection direction 的来源。
    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, viewDirectionWS);

    // 复用准备数据版本，确保间接高光补偿的应用位置一致。
    return BurtEvaluateIndirectSpecularPBR(materialData, geometryData, indirectSpecularEnergyCompensation);
}

// 保留 SurfaceData 旧入口：调用方不关心拆项时，在函数内部按 XRender 规则计算 indirect specular energy compensation。
float3 BurtEvaluateIndirectSpecularPBR(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS)
{
    // 先计算间接高光能量补偿，再交给完整入口，避免旧调用绕过 XRender EnergyCompensation_GGX。
    float3 indirectSpecularEnergyCompensation = GetIndirectSpecularEnergyCompensation(surfaceData, normalWS, viewDirectionWS);

    // 复用带显式能量补偿的入口，保证旧接口和 Debug 拆项看到同一套结果。
    return BurtEvaluateIndirectSpecularPBR(surfaceData, normalWS, viewDirectionWS, indirectSpecularEnergyCompensation);
}

// 保存 PBR 间接光拆分结果，Deferred 后续可以复用同一套 SH / Reflection Probe 评估逻辑。
struct BurtIndirectPBRComponents
{
    // 保存间接漫反射贡献，数据来源是 Unity SH / Light Probe。
    float3 diffuse;

    // 保存间接镜面贡献，数据来源是 Unity Reflection Probe / Sky Reflection。
    float3 specular;

    // 保存间接镜面能量补偿，数据来源是 XRender PreIntegratedFG.z。
    float3 specularEnergyCompensation;
};

// 计算 PBR 间接光拆分结果，让 Forward 和未来 Deferred 都能拿到一致的间接漫反射与间接高光。
BurtIndirectPBRComponents BurtEvaluateIndirectPBRComponents(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, BurtPBREnergyTerms energyTerms)
{
    // 创建输出结构体，下面分别写入 diffuse、specular 和间接高光能量补偿。
    BurtIndirectPBRComponents components;

    // 计算来自 Unity SH / Light Probe 的漫反射环境光，并用 XRender EnergyPreservation 做底层 diffuse 保能。
    components.diffuse = BurtEvaluateIndirectDiffusePBR(materialData, geometryData.normalWS, energyTerms.energyPreservation);

    // 直接读取统一准备好的 XRender 间接高光能量补偿，对应 GenericData.EnergyCompensation_GGX。
    components.specularEnergyCompensation = GetIndirectSpecularEnergyCompensation(materialData, geometryData, energyTerms);

    // 计算来自 Unity Reflection Probe / Sky Reflection 的镜面环境光。
    components.specular = BurtEvaluateIndirectSpecularPBR(materialData, geometryData, components.specularEnergyCompensation);

    // 返回拆分后的间接光，Debug View 和 Deferred 光照都可以直接读取。
    return components;
}

// 保留旧拆分入口：内部准备 material / geometry / energy terms，兼容现有调用点。
BurtIndirectPBRComponents BurtEvaluateIndirectPBRComponents(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS, float energyPreservation)
{
    // 从 SurfaceData 准备材质数据；Deferred 后续可以从 GBuffer 还原同一结构。
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    // 准备几何数据，统一 NdotV 和 reflection direction 的来源。
    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, viewDirectionWS);

    // 准备完整 energy terms；随后用调用方传入的 preservation 覆盖，保持旧入口语义不变。
    float directSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(materialData, geometryData);
    BurtPBREnergyTerms energyTerms = BurtPreparePBREnergyTerms(materialData, geometryData, directSpecularPerceptualRoughness);
    energyTerms.energyPreservation = energyPreservation;

    // 复用准备数据版本，保证旧入口也能拿到间接高光能量补偿。
    return BurtEvaluateIndirectPBRComponents(materialData, geometryData, energyTerms);
}

// 计算 PBR 间接光总和：间接漫反射 + 间接镜面反射。
float3 BurtEvaluateIndirectPBR(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS)
{
    // 从 SurfaceData 准备材质数据；Deferred 后续可以从 GBuffer 还原同一结构。
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    // 准备几何数据，统一 NdotV 和 reflection direction 的来源。
    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, viewDirectionWS);

    // 直接高光的 roughness 单独包含 Specular AA，不回写材质 Base.Roughness。
    float directSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(materialData, geometryData);

    // 一次性准备能量项，避免 indirect diffuse / indirect specular 分别查 FG LUT。
    BurtPBREnergyTerms energyTerms = BurtPreparePBREnergyTerms(materialData, geometryData, directSpecularPerceptualRoughness);

    // 复用拆分版本，保证总和接口与 Debug 拆项使用完全相同的结果。
    BurtIndirectPBRComponents components = BurtEvaluateIndirectPBRComponents(materialData, geometryData, energyTerms);

    // 返回完整间接光，后续会和主光直接光相加。
    return components.diffuse + components.specular;
}

// 保存一次 PBR shading 评估前的核心准备数据；Deferred 后续从 GBuffer 还原 material / geometry 后也应先进入这里。
struct BurtPBRShadingCoreData
{
    // 保存已经准备好的材质数据，对应 XRender GenericData 和 SlabParams 的核心材质语义。
    BurtPBRMaterialData materialData;

    // 保存已经准备好的几何数据，对应 XRender PosData / Geometry 常用方向项。
    BurtPBRGeometryData geometryData;

    // 保存直接高光的 Specular AA 中间项，避免 direct shading 和 Debug View 各算一遍。
    BurtSpecularAATerms specularAATerms;

    // 保存直接高光实际使用的感知粗糙度，等于 Specular AA 过滤后的 roughness。
    float directSpecularPerceptualRoughness;

    // 保存 XRender EnergyCompensation_GGX 和 EnergyPreservation，供 direct / indirect / compose 共用。
    BurtPBREnergyTerms energyTerms;
};

// Prepare 阶段：集中准备材质、几何、Specular AA 和能量项；后续 Direct / Indirect 都只消费这份 core data。
BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData)
{
    // 创建输出结构体，下面按 XRender shading 前置数据的依赖顺序填充。
    BurtPBRShadingCoreData coreData;

    // 保留材质数据本身，Deferred 后续可直接从 GBuffer 还原到同一结构。
    coreData.materialData = materialData;

    // 保留几何数据本身，Deferred 后续可用 GBuffer normal 和重建 view direction 得到同一结构。
    coreData.geometryData = geometryData;

    // 一次性评估 Specular AA 中间项，让直接高光和 Debug View 读取完全相同的数据。
    coreData.specularAATerms = BurtEvaluateSpecularAATerms(materialData, geometryData);

    // 直接高光的 roughness 单独包含 Specular AA，不回写材质 Base.Roughness。
    coreData.directSpecularPerceptualRoughness = coreData.specularAATerms.filteredPerceptualRoughness;

    // 一次性准备 direct/indirect energy compensation 和 EnergyPreservation，避免各路径重复采样 FG LUT。
    coreData.energyTerms = BurtPreparePBREnergyTerms(materialData, geometryData, coreData.directSpecularPerceptualRoughness);

    // 返回完整核心准备数据，Direct / Indirect / Compose 都从这里取依赖。
    return coreData;
}

// Prepare 阶段的 SurfaceData 入口：Forward 负责把材质面板数据和插值几何数据整理到核心结构。
BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS)
{
    // 从 SurfaceData 准备材质数据；Deferred 后续可以从 GBuffer 还原到同一结构。
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    // 从 Forward 输入准备几何数据；Deferred 后续可以用 GBuffer normal 和重建 view direction 生成同一结构。
    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, viewDirectionWS);

    // 复用主 Prepare 入口，保证 Forward 和 Deferred 的 shading core 数据口径一致。
    return BurtPreparePBRShadingCoreData(materialData, geometryData);
}

// Prepare 阶段的 GBufferData 入口：Deferred Lighting 先解码 GBuffer，再从这里进入和 Forward 相同的 shading core。
BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtGBufferData gbufferData, float3 viewDirectionWS)
{
    // 从 GBuffer 还原材质数据；出处：XRender SlabGbuffers/SlabGbufferUnpack.hlsl 会先 Unpack 成 SlabParams，再进入 SlabParamsContext。
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);

    // 从 GBuffer normal 和屏幕空间重建出的 viewDirectionWS 准备几何数据，保证 NdotV / reflection direction 和 Forward 使用同一套函数。
    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(gbufferData, viewDirectionWS);

    // 复用主 Prepare 入口，Deferred 不维护第二套 Specular AA、Energy Terms 或 BRDF 输入。
    return BurtPreparePBRShadingCoreData(materialData, geometryData);
}

// Prepare 阶段的 EncodedGBuffer 入口：Deferred Pass 从三张 RT 采样得到 float4 后，可先填成 BurtEncodedGBuffer 再调用这里。
BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtEncodedGBuffer encodedGBuffer, float3 viewDirectionWS)
{
    // 只做 shader 侧解码，不绑定 _BurtGBuffer0/1/2 的采样方式，避免和主 agent 的 Deferred Pass 接入工作冲突。
    BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);

    // 解码后继续走 GBufferData 入口，保证所有 Deferred 输入最终收敛到同一条 prepare 路径。
    return BurtPreparePBRShadingCoreData(gbufferData, viewDirectionWS);
}

// 保存一次完整 PBR shading 的可复用拆分结果，Forward 只负责调用，Deferred 后续可从 GBuffer 还原输入后复用。
struct BurtPBRShadingComponents
{
    // 保存材质漫反射颜色，对应 XRender GenericData.DiffuseColor，Deferred Debug 可以检查 GBuffer 还原是否正确。
    float3 diffuseColor;

    // 保存由 reflectance / metallic / baseColor 还原得到的 F0，材质面板不直接暴露 F0，只在 Debug 中溯源查看。
    float3 f0;

    // 保存默认掠射角反射端点，对应 XRender GenericData.F90。
    float3 f90;

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

    // 保存 Specular AA 估算出的屏幕空间法线方差，用来观察高光是否因为像素内法线变化被拓宽。
    float specularAANormalVariance;

    // 保存 Specular AA 额外增加的感知粗糙度，0 表示未触发拓宽。
    float specularAARoughnessDelta;

    // 保存直接高光能量补偿，方便 Debug View 和 Deferred 调试确认 LUT.z 的影响。
    float3 specularEnergyCompensation;

    // 保存间接高光能量补偿，方便 Debug View 和 Deferred 调试确认 Reflection Probe 是否也补回多次散射能量。
    float3 indirectSpecularEnergyCompensation;

    // 保存 XRender EnergyPreservation，表示 specular 顶层之后 diffuse 底层还能保留的能量比例。
    float energyPreservation;

    // 保存间接高光遮蔽，方便 Debug View 和 Deferred 调试确认 AO 对反射探针的影响。
    float specularOcclusion;

    // 保存直接 GGX D 项，方便拆开检查高 smoothness 时的 NDF 峰值。
    float directBRDFD;

    // 保存直接 Smith Joint Visibility 项，方便检查几何遮蔽是否压暗高光。
    float directBRDFVisibility;

    // 保存直接 Schlick Fresnel 项，方便检查 reflectance / metallic 到 F0 的映射。
    float3 directBRDFFresnel;

    // 保存直接 diffuse lobe，默认来自 Lambert，后续可切 XRender Burley。
    float directDiffuseLobe;

    // 保存未乘灯光颜色、NdotL 和阴影的直接 diffuse BRDF。
    float3 directDiffuseBRDF;

    // 保存未乘灯光颜色、NdotL 和阴影的直接 specular BRDF。
    float3 directSpecularBRDF;

    // 保存间接高光 DFG.xy，方便检查 PreIntegratedFG LUT 或解析 fallback。
    float2 indirectSpecularDFG;

    // 保存 F0/F90 套用 DFG 后的环境 BRDF，Reflection Probe 会乘这一项。
    float3 indirectSpecularEnvBRDF;
};

// Direct 阶段：只计算主方向光直接光；未来多光源或 Deferred tiled/cluster lighting 可以复用这个入口。
BurtDirectPBRComponents BurtEvaluatePBRDirectFromCore(BurtPBRShadingCoreData coreData, BurtLight mainLight)
{
    // 复用 BRDF 层的 direct components，确保 Forward / Deferred / Debug 都走同一套 D、V、F 和 diffuse lobe。
    return BurtEvaluateDirectPBRComponents(
        coreData.materialData,
        coreData.geometryData,
        coreData.energyTerms,
        coreData.directSpecularPerceptualRoughness,
        mainLight.color,
        mainLight.directionWS,
        mainLight.shadowAttenuation);
}

// Indirect 阶段：只计算 SH diffuse 与 Reflection Probe / Sky specular，方便 Deferred 后续独立替换间接光来源。
BurtIndirectPBRComponents BurtEvaluatePBRIndirectFromCore(BurtPBRShadingCoreData coreData)
{
    // 间接光继续复用统一 energy terms，保证 diffuse preservation 和 specular compensation 口径一致。
    return BurtEvaluateIndirectPBRComponents(coreData.materialData, coreData.geometryData, coreData.energyTerms);
}

// Compose 阶段：把 Prepare、Direct、Indirect 的结果合成 Debug View 和最终 shading 需要的统一结构。
BurtPBRShadingComponents BurtComposePBRShadingComponents(BurtPBRShadingCoreData coreData, BurtDirectPBRComponents directComponents, BurtIndirectPBRComponents indirectComponents)
{
    // 创建输出结构体，下面只做数据合成和 Debug 中间项收口，不再重新评估 BRDF。
    BurtPBRShadingComponents components;

    // 保存材质本身的感知粗糙度，Deferred 调试时可以直接验证 GBuffer 中的 smoothness 还原是否正确。
    components.perceptualRoughness = coreData.materialData.perceptualRoughness;

    // 保存材质准备阶段得到的核心颜色项，Debug View 可以直接检查 XRender GenericData 语义。
    components.diffuseColor = coreData.materialData.diffuseColor;

    // 保存 reflectance 还原出的 F0，注意它不是材质面板输入，只是 Debug 溯源数据。
    components.f0 = coreData.materialData.f0;

    // 保存 F90，当前 XRender DefaultLit 路径使用默认 1。
    components.f90 = coreData.materialData.f90;

    // 保存直接高光使用的 AA 后粗糙度，用来观察极光滑高光是否被屏幕空间法线变化拓宽。
    components.specularAARoughness = coreData.directSpecularPerceptualRoughness;

    // 保存屏幕空间法线方差，Debug View 会放大显示以便定位 Normal Filtering 是否生效。
    components.specularAANormalVariance = coreData.specularAATerms.normalVariance;

    // 保存 Specular AA 增加的 roughness，Deferred 后续也可以用它检查法线过滤成本和效果。
    components.specularAARoughnessDelta = coreData.specularAATerms.roughnessDelta;

    // 保存直接高光能量补偿，使用和直接 BRDF 一致的 F0、AA 后粗糙度和 NdotV。
    components.specularEnergyCompensation = coreData.energyTerms.directSpecularEnergyCompensation;

    // 保存间接高光能量补偿，使用材质 Base.Roughness，对齐 XRender Sky/EnvProbe Specular。
    components.indirectSpecularEnergyCompensation = coreData.energyTerms.indirectSpecularEnergyCompensation;

    // 保存 XRender EnergyPreservation，让 Debug 和 diffuse 层都读取同一个保能比例。
    components.energyPreservation = coreData.energyTerms.energyPreservation;

    // 记录直接 BRDF 的 D 项，后续 Deferred 可以复用同一套拆项做诊断。
    components.directBRDFD = directComponents.brdfTerms.d;

    // 记录直接 BRDF 的 visibility 项，方便确认高光变暗是否来自几何遮蔽。
    components.directBRDFVisibility = directComponents.brdfTerms.visibility;

    // 记录直接 Fresnel 项，方便确认 reflectance 和 metallic 输入是否正确。
    components.directBRDFFresnel = directComponents.brdfTerms.fresnel;

    // 记录直接 diffuse lobe，当前默认 Lambert，后续切 Burley 时 Debug View 会同步变化。
    components.directDiffuseLobe = directComponents.brdfTerms.diffuseLobe;

    // 记录未乘灯光可见性的 diffuse BRDF，方便把材质项和灯光项拆开。
    components.directDiffuseBRDF = directComponents.brdfTerms.diffuseBRDF;

    // 记录未乘灯光可见性的 specular BRDF，方便检查 D/V/F/补能合成后的高光强度。
    components.directSpecularBRDF = directComponents.brdfTerms.specularBRDF;

    // 保存直接漫反射结果，已经包含灯光颜色、NdotL、阴影和 EnergyPreservation。
    components.directDiffuse = directComponents.diffuse;

    // 保存直接镜面结果，已经包含 GGX、Fresnel、能量补偿、灯光颜色、NdotL 和阴影。
    components.directSpecular = directComponents.specular;

    // 合并直接漫反射和直接镜面，得到完整直接光。
    components.directLighting = components.directDiffuse + components.directSpecular;

    // 保存间接漫反射结果，Debug View 可以直接显示这一项。
    components.indirectDiffuse = indirectComponents.diffuse;

    // 保存间接镜面结果，Debug View 可以直接显示这一项。
    components.indirectSpecular = indirectComponents.specular;

    // 合并间接漫反射和间接镜面，得到完整间接光。
    components.indirectLighting = components.indirectDiffuse + components.indirectSpecular;

    // 合并直接光和间接光，得到不含自发光的 PBR 总光照。
    components.lighting = components.directLighting + components.indirectLighting;

    // 保存间接高光遮蔽，使用材质 AO、NdotV 和未 AA 的感知粗糙度，与间接高光路径保持一致。
    components.specularOcclusion = GetIndirectSpecularOcclusion(coreData.geometryData.nDotV, coreData.materialData.occlusion, coreData.materialData.perceptualRoughness);

    // 记录间接高光使用的 DFG.xy；出处同 BurtEvaluateIndirectSpecularPBR，便于检查 LUT 采样或 fallback。
    components.indirectSpecularDFG = GetSpecularDFGTerms(coreData.materialData.perceptualRoughness, coreData.geometryData.nDotV);

    // 记录 F0/F90 经过 DFG 后得到的环境 BRDF，Reflection Probe 最终会乘这一项。
    components.indirectSpecularEnvBRDF = EvalSpecularDFG(coreData.materialData.f0, coreData.materialData.f90, components.indirectSpecularDFG);

    // 返回完整拆分结果，调用方只需要决定是否叠加自发光或进入 Debug View。
    return components;
}

// 使用已经准备好的 PBR 数据统一评估完整 shading；Deferred 从 GBuffer 还原后应优先调用这个入口。
BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, BurtLight mainLight)
{
    // Prepare：集中准备 Specular AA、Energy Terms 和后续所有阶段需要的稳定输入。
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(materialData, geometryData);

    // Direct：只评估主光直接光，输出同时包含最终贡献和 BRDF 拆项。
    BurtDirectPBRComponents directComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);

    // Indirect：只评估 SH / Reflection Probe 间接光，输出间接 diffuse / specular 拆项。
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);

    // Compose：把各阶段结果收口为 Forward 和未来 Deferred 共用的 shading components。
    return BurtComposePBRShadingComponents(coreData, directComponents, indirectComponents);
}

// 统一评估一次完整 PBR shading；Forward 和未来 Deferred 都应优先调用这个入口拿拆分结果。
BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS)
{
    // Prepare：Forward 从 SurfaceData 和插值几何数据进入统一 shading core，Deferred 后续可从 GBuffer 进入同一 core。
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(surfaceData, normalWS, viewDirectionWS);

    // Direct：复用 core data 计算主光直接光。
    BurtDirectPBRComponents directComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);

    // Indirect：复用 core data 计算 SH / Reflection Probe 间接光。
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);

    // Compose：统一合成最终 shading components，避免 SurfaceData 入口和 Deferred 入口维护两套拼装逻辑。
    return BurtComposePBRShadingComponents(coreData, directComponents, indirectComponents);
}

// GBufferData 入口：Deferred Lighting 从已经解码好的 GBuffer 直接评估 PBR 拆分结果。
BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtGBufferData gbufferData, BurtLight mainLight, float3 viewDirectionWS)
{
    // Prepare：把 GBuffer 材质/法线还原成 PBRMaterialData + PBRGeometryData，并进入统一 shading core。
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(gbufferData, viewDirectionWS);

    // Direct：复用和 Forward 相同的主光直接光评估，避免 Deferred 第一版出现另一套 D/V/F/能量项。
    BurtDirectPBRComponents directComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);

    // Indirect：复用和 Forward 相同的 SH / Reflection Probe 评估，后续主 agent 可在 Deferred Pass 中替换数据来源。
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);

    // Compose：输出 Forward 与 Deferred 共用的 BurtPBRShadingComponents，调用方再决定是否叠加 gbufferData.emission。
    return BurtComposePBRShadingComponents(coreData, directComponents, indirectComponents);
}

// EncodedGBuffer 入口：Deferred Lighting Pass 从三张 RT 采样后可直接把采样值打包进 BurtEncodedGBuffer 调用。
BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtEncodedGBuffer encodedGBuffer, BurtLight mainLight, float3 viewDirectionWS)
{
    // 解码 GBuffer0/1/2 的逻辑仍集中在 BurtGBuffer.hlsl，保证 Debug roundtrip 和真实 Deferred Pass 使用同一套布局。
    BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);

    // 复用 GBufferData 入口，避免 Encoded 和 Decoded 两套路径在能量项或粗糙度处理上分叉。
    return BurtEvaluatePBRShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS);
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
