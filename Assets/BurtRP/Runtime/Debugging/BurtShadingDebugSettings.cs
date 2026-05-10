using UnityEngine; // 引入 UnityEngine，下面需要使用 Shader.PropertyToID 和 Shader.SetGlobalXXX。

namespace Burt.RenderPipeline // 使用 BurtRP 运行时命名空间，让渲染侧和 shader debug 状态处在同一模块。
{
    // 定义 BurtRP 的 shading debug 模式枚举；数值按分组预留，方便后续继续扩展。
    public enum BurtShadingDebugMode
    {
        None = 0, // 不启用 shading debug，正常渲染 BurtRP 画面。
        Albedo = 100, // 材质调试：显示基础颜色，也就是 BaseMap 与 BaseColor 合成后的结果。
        NormalWS = 101, // 材质调试：显示法线贴图影响后的世界空间法线。
        Smoothness = 102, // 材质调试：显示最终光滑度，包含标量 Smoothness 与 Mask Map A 通道。
        Metallic = 103, // 材质调试：显示最终金属度，包含标量 Metallic 与 Mask Map R 通道。
        Occlusion = 104, // 材质调试：显示环境遮蔽，方便检查 Mask Map 的 G 通道和强度混合结果。
        Reflectance = 105, // 材质调试：显示 XRender 风格 reflectance，0.5 会映射到常见非金属 F0=0.04。
        Roughness = 106, // 材质调试：显示材质感知粗糙度，也就是 1 - Smoothness 后的结果。
        SpecularAARoughness = 107, // 材质调试：显示直接高光实际粗糙度，包含 Specular AA 的拓宽结果。
        SpecularEnergyCompensation = 108, // 材质调试：显示直接高光能量补偿。
        SpecularOcclusion = 109, // 材质调试：显示间接高光遮蔽。
        EnergyPreservation = 110, // 材质调试：显示 XRender EnergyPreservation，也就是底层 diffuse 保能比例。
        IndirectSpecularEnergyCompensation = 111, // 材质调试：显示间接高光能量补偿，也就是 Reflection Probe 高光补回的多次散射能量。
        DiffuseColor = 112, // 材质调试：显示 XRender GenericData.DiffuseColor，方便检查 metallic 是否正确扣除 diffuse。
        DirectBRDFD = 115, // 材质调试：显示直接光 GGX D 项，用来检查高 smoothness 时的 NDF 峰值。
        DirectBRDFVisibility = 116, // 材质调试：显示直接光 Smith Joint Visibility，用来排查几何遮蔽是否压暗高光。
        DirectBRDFFresnel = 117, // 材质调试：显示直接光 Schlick Fresnel，用来检查 F0 和视角输入。
        DirectDiffuseLobe = 118, // 材质调试：显示直接光 diffuse lobe，当前默认 Lambert，后续可切 XRender Burley。
        DirectDiffuseBRDF = 119, // 材质调试：显示未乘灯光颜色、NdotL 和阴影的直接 diffuse BRDF。
        DirectSpecularBRDF = 120, // 材质调试：显示未乘灯光颜色、NdotL 和阴影的直接 specular BRDF。
        SpecularAANormalVariance = 121, // 材质调试：显示 XRender Normal Filtering 估算出的屏幕空间法线方差。
        SpecularAARoughnessDelta = 122, // 材质调试：显示 Specular AA 额外增加的感知粗糙度。
        IndirectSpecularDFG = 123, // 材质调试：显示间接高光使用的 PreIntegratedFG DFG.xy。
        IndirectSpecularEnvBRDF = 124, // 材质调试：显示 F0/F90 套用 DFG 后的环境 BRDF。
        GBufferBaseColor = 130, // GBuffer 调试：显示按 BurtGBuffer 约定编码再解码后的 BaseColor。
        GBufferNormalWS = 131, // GBuffer 调试：显示按 octahedron normal 编码再解码后的世界空间法线。
        GBufferMetallic = 132, // GBuffer 调试：显示 GBuffer 解码后的 Metallic。
        GBufferSmoothness = 133, // GBuffer 调试：显示 GBuffer 解码后的 Smoothness，后续 Deferred 再从它还原 Roughness。
        GBufferOcclusion = 134, // GBuffer 调试：显示 GBuffer 解码后的 Ambient Occlusion。
        GBufferReflectance = 135, // GBuffer 调试：显示 GBuffer 解码后的 XRender Reflectance。
        GBufferRoughness = 136, // GBuffer 调试：显示从 GBuffer Smoothness 还原出的 XRender Base.Roughness。
        GBufferDiffuseColor = 137, // GBuffer 调试：显示从 GBuffer 还原 PBRMaterialData 后的 DiffuseColor。
        DetailLighting = 200, // 光照调试：参考 XRender Detail Lighting，用 0.18 中灰 BaseColor 重新计算光照，方便只看明暗细节。
        IndirectLighting = 201, // 光照调试：只显示 PBR 间接光，方便检查 SH 漫反射和 Reflection Probe 镜面反射。
        DirectDiffuse = 202, // 光照调试：只显示直接漫反射，方便检查 NdotL、阴影和 1/PI。
        DirectSpecular = 203, // 光照调试：只显示直接高光，方便排查 smoothness 拉满后的高光宽度。
        IndirectDiffuse = 204, // 光照调试：只显示间接漫反射，方便检查 SH / Light Probe。
        IndirectSpecular = 205, // 光照调试：只显示间接高光，方便检查 Reflection Probe 和 DFG。
        ShadowAttenuation = 206, // 光照调试：只显示主光阴影衰减，白色表示不被阴影遮挡。
        AmbientOcclusion = 207, // 光照调试：只显示当前参与间接光遮蔽的 AO 输入。
        Emission = 208, // 光照调试：只显示自发光贡献，方便确认 GBuffer / Forward 是否正确叠加 emission。
        FinalLighting = 209, // 光照调试：显示写入 CameraColor 前的最终材质光照，包含 PBR 光照和自发光。
        CameraDepth = 300, // 全屏调试：复用 BurtRP 当前已有的 CameraDepth debug pass。
        MainLightShadow = 301 // 全屏调试：复用 BurtRP 当前已有的 MainLightShadow debug pass。
    }

    // 保存 Editor Overlay 和运行时渲染共享的 shading debug 状态。
    public static class BurtShadingDebugSettings
    {
        public const string ModeShaderName = "_BurtShadingDebugMode"; // 定义 shader 侧读取 debug 模式的全局属性名。
        public const string EnabledShaderName = "_BurtShadingDebugEnabled"; // 定义 shader 侧读取 debug 是否开启的全局属性名。

        private static readonly int ModeShaderId = Shader.PropertyToID(ModeShaderName); // 缓存模式属性 ID，避免每帧字符串查找。
        private static readonly int EnabledShaderId = Shader.PropertyToID(EnabledShaderName); // 缓存开关属性 ID，避免每帧字符串查找。
        private static BurtShadingDebugMode currentMode = BurtShadingDebugMode.None; // 保存当前 debug 模式，默认关闭。
        private static BurtShadingDebugMode previousMode = BurtShadingDebugMode.None; // 保存上一个 debug 模式，方便后续做返回上次模式或切换统计。

        public static BurtShadingDebugMode Mode // 暴露当前 debug 模式，Editor UI 和渲染侧都通过它读写状态。
        {
            get => currentMode; // 返回当前保存的 debug 模式。
            set // 设置新的 debug 模式，并立刻同步到 shader 全局参数。
            {
                if (currentMode == value) // 如果模式没有变化，说明只是需要刷新全局 shader 参数。
                {
                    ApplyGlobalShaderProperties(); // 重新上传全局参数，避免域重载或相机切换后 shader 状态丢失。
                    return; // 提前返回，避免重复写 previousMode。
                }

                previousMode = currentMode; // 记录切换前的模式，方便后续扩展返回上一个模式。
                currentMode = value; // 保存新的当前模式。
                ApplyGlobalShaderProperties(); // 把新的模式同步给 shader 全局参数。
            }
        }

        public static BurtShadingDebugMode PreviousMode => previousMode; // 暴露上一个模式，当前最小版本暂时只作为状态记录。

        public static bool IsDebugging => currentMode != BurtShadingDebugMode.None; // 只要不是 None，就认为 debug 已开启。

        public static void ApplyGlobalShaderProperties() // 把当前 shading debug 状态上传给 shader。
        {
            Shader.SetGlobalInt(ModeShaderId, (int)currentMode); // 上传整数模式 ID，后续 shader 可以 switch 或 if 判断。
            Shader.SetGlobalFloat(EnabledShaderId, IsDebugging ? 1f : 0f); // 上传 0/1 开关，方便 shader 快速判断是否走调试分支。
        }
    }
}
