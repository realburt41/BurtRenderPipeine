// BurtRP 的材质 Shading Debug 工具库，负责把 Editor Overlay 选择的调试模式转换成片元颜色。
#ifndef BURT_SHADING_DEBUG_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义调试函数。
#define BURT_SHADING_DEBUG_INCLUDED // 标记 BurtShadingDebug.hlsl 已经被包含过，后续重复 include 会被跳过。

#include "BurtCommon.hlsl" // 引入 BurtSafeNormalize，用来安全归一化世界空间法线。
#include "BurtInput.hlsl" // 引入 BurtSurfaceData，让调试函数可以读取基础色、光滑度、金属度和环境遮蔽。

float _BurtShadingDebugMode; // 保存 C# 侧 BurtShadingDebugSettings 上传的当前调试模式编号。
float _BurtShadingDebugEnabled; // 保存 C# 侧上传的调试开关，0 表示关闭，1 表示开启。

static const float BURT_SHADING_DEBUG_MODE_ALBEDO = 100.0f; // 对应 C# BurtShadingDebugMode.Albedo，用来显示材质基础色。
static const float BURT_SHADING_DEBUG_MODE_NORMAL_WS = 101.0f; // 对应 C# BurtShadingDebugMode.NormalWS，用来显示世界空间法线。
static const float BURT_SHADING_DEBUG_MODE_SMOOTHNESS = 102.0f; // 对应 C# BurtShadingDebugMode.Smoothness，用来显示最终材质光滑度。
static const float BURT_SHADING_DEBUG_MODE_METALLIC = 103.0f; // 对应 C# BurtShadingDebugMode.Metallic，用来显示最终材质金属度。
static const float BURT_SHADING_DEBUG_MODE_OCCLUSION = 104.0f; // 对应 C# BurtShadingDebugMode.Occlusion，用来显示材质环境遮蔽。
static const float BURT_SHADING_DEBUG_MODE_LIGHTING = 200.0f; // 对应 C# BurtShadingDebugMode.Lighting，用来显示不含自发光的 PBR 总光照结果。
static const float BURT_SHADING_DEBUG_MODE_INDIRECT_LIGHTING = 201.0f; // 对应 C# BurtShadingDebugMode.IndirectLighting，用来显示 PBR 间接光。

bool BurtIsShadingDebugEnabled() // 判断当前是否启用了任意 shading debug 模式。
{
    return _BurtShadingDebugEnabled > 0.5f; // 使用 0.5 作为阈值，兼容 C# 上传的 0/1 float 开关。
}

bool BurtIsSameShadingDebugMode(float mode, float expectedMode) // 判断当前模式是否等于指定模式。
{
    return abs(mode - expectedMode) < 0.5f; // 用半个整数范围做比较，避免 float/int 上传转换产生精度边界问题。
}

float3 BurtEncodeNormalWSForDebug(float3 normalWS) // 把世界空间法线编码成可以显示到屏幕上的 RGB 颜色。
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS); // 先安全归一化，避免插值或贴图采样造成长度偏差。
    return safeNormalWS * 0.5f + 0.5f; // 把 [-1, 1] 的法线范围映射到 [0, 1] 的颜色范围。
}

bool BurtTryEvaluateMaterialShadingDebug(BurtSurfaceData surfaceData, float3 normalWS, float3 lightingColor, float3 indirectLightingColor, out float3 debugColor) // 尝试根据当前模式生成材质调试颜色。
{
    debugColor = float3(0.0f, 0.0f, 0.0f); // 先清空输出颜色，保证未命中任何模式时不会返回未初始化值。

    if (!BurtIsShadingDebugEnabled()) // 如果全局 debug 开关关闭，就不接管材质输出。
    {
        return false; // 返回 false，告诉调用方继续走正常 Lit 渲染路径。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ALBEDO)) // Albedo 模式显示贴图和 BaseColor 合成后的基础色。
    {
        debugColor = saturate(surfaceData.baseColor.rgb); // 把基础色限制到 0 到 1，避免普通 LDR 调试视图过曝。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_NORMAL_WS)) // NormalWS 模式显示法线贴图影响后的世界空间法线。
    {
        debugColor = BurtEncodeNormalWSForDebug(normalWS); // 把世界法线从方向值编码成可视化颜色。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SMOOTHNESS)) // Smoothness 模式显示当前材质光滑度。
    {
        debugColor = float3(surfaceData.smoothness, surfaceData.smoothness, surfaceData.smoothness); // 把单通道光滑度复制到 RGB，形成灰度图。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_METALLIC)) // Metallic 模式显示当前材质金属度。
    {
        debugColor = float3(surfaceData.metallic, surfaceData.metallic, surfaceData.metallic); // 把单通道金属度复制到 RGB，形成灰度图。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_OCCLUSION)) // Occlusion 模式显示当前材质环境遮蔽。
    {
        debugColor = float3(surfaceData.occlusion, surfaceData.occlusion, surfaceData.occlusion); // 把单通道环境遮蔽复制到 RGB，形成灰度图。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_LIGHTING)) // Lighting 模式显示 PBR 总光照结果。
    {
        debugColor = max(lightingColor, float3(0.0f, 0.0f, 0.0f)); // 保留 HDR 光照强度但去掉负值，便于观察阴影、高光和间接光分布。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_LIGHTING)) // IndirectLighting 模式只显示 PBR 间接光。
    {
        debugColor = max(indirectLightingColor, float3(0.0f, 0.0f, 0.0f)); // 保留 HDR 间接光强度，方便检查 SH 和 Reflection Probe 是否生效。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    return false; // CameraDepth 和 MainLightShadow 属于全屏 debug pass，材质 shader 不在这里接管输出。
}

#endif // BURT_SHADING_DEBUG_INCLUDED // 结束 BurtShadingDebug.hlsl 的 include guard。