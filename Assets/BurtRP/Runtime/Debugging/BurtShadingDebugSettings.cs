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
        Lighting = 200, // 光照调试：显示不含自发光的 PBR 总光照结果。
        IndirectLighting = 201, // 光照调试：只显示 PBR 间接光，方便检查 SH 漫反射和 Reflection Probe 镜面反射。
        DirectDiffuse = 202, // 光照调试：只显示直接漫反射，方便检查 NdotL、阴影和 1/PI。
        DirectSpecular = 203, // 光照调试：只显示直接高光，方便排查 smoothness 拉满后的高光宽度。
        IndirectDiffuse = 204, // 光照调试：只显示间接漫反射，方便检查 SH / Light Probe。
        IndirectSpecular = 205, // 光照调试：只显示间接高光，方便检查 Reflection Probe 和 DFG。
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
