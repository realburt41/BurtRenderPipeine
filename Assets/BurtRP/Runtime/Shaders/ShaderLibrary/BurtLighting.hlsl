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

// 采样 BurtRP 当前的间接镜面反射环境色。
float3 BurtSampleIndirectSpecularRadiance(float3 reflectionDirectionWS, float roughness)
{
    // 归一化反射方向，让 cubemap 采样方向稳定。
    float3 safeReflectionDirectionWS = BurtSafeNormalize(reflectionDirectionWS);

    // 把 roughness 映射到 Unity reflection probe 的 mip 级别；6 是 Unity 常见的 spec cube LOD 步数近似。
    float mipLevel = saturate(roughness) * 6.0f;

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
    float3 diffuseColor = BurtBRDFDiffuseColor(surfaceData);

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
    float roughness = BurtBRDFRoughness(surfaceData);

    // 采样 Unity 当前 reflection probe / sky reflection，得到已经按 roughness 预过滤过的环境高光。
    float3 specularRadiance = BurtSampleIndirectSpecularRadiance(reflectionDirectionWS, roughness);

    // 计算当前材质的 F0，非金属接近 0.04，金属使用 baseColor。
    float3 f0 = BurtBRDFSpecularF0(surfaceData);

    // 计算当前材质的 F90，用来让环境高光和直接高光使用同一套 Fresnel 端点。
    float3 f90 = BurtBRDFF90(f0);

    // 计算 NdotV，视线越掠射 DFG 近似会给出越强的边缘反射权重。
    float nDotV = saturate(dot(n, v));

    // 参考 XRender 的 PrefilteredDFG_Approx，得到 IBL 高光需要的 F0/F90 权重。
    float2 dfg = BurtPrefilteredDFGApprox(roughness, nDotV);

    // 把 DFG 应用到 F0/F90 上，比单纯 Fresnel 更接近预积分环境 BRDF。
    float3 envBRDF = BurtEvaluateSpecularDFG(f0, f90, dfg);

    // AO 当前只影响间接光，不影响主光直接光；这里让反射探针也被环境遮蔽控制。
    return specularRadiance * envBRDF * saturate(surfaceData.occlusion);
}

// 计算 PBR 间接光总和：间接漫反射 + 间接镜面反射。
float3 BurtEvaluateIndirectPBR(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS)
{
    // 先计算来自 Unity SH / Light Probe 的漫反射环境光。
    float3 indirectDiffuse = BurtEvaluateIndirectDiffusePBR(surfaceData, normalWS);

    // 再计算来自 Unity Reflection Probe / Sky Reflection 的镜面环境光。
    float3 indirectSpecular = BurtEvaluateIndirectSpecularPBR(surfaceData, normalWS, viewDirectionWS);

    // 返回完整间接光，后续会和主光直接光相加。
    return indirectDiffuse + indirectSpecular;
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
    return BurtBRDFSpecularF0(surfaceData) * light.color * specularTerm * diffuseVisibility * light.shadowAttenuation;
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
    // 计算 PBR 间接光，当前使用 Unity SH 漫反射和 Unity Reflection Probe 镜面反射。
    float3 indirectColor = BurtEvaluateIndirectPBR(surfaceData, normalWS, viewDirectionWS);

    // 计算单主光直接光，内部包含 GGX specular、能量守恒 diffuse 和阴影衰减。
    float3 directColor = BurtEvaluateDirectPBR(surfaceData, mainLight.color, mainLight.directionWS, normalWS, viewDirectionWS, mainLight.shadowAttenuation);

    // 返回间接光和直接光相加的 PBR 结果。
    return indirectColor + directColor;
}

#endif // BURT_LIGHTING_INCLUDED // 结束 BurtLighting.hlsl 的 include guard。
