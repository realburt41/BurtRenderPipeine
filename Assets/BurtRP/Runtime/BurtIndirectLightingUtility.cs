using System; // 引入 ArgumentException，用来安全兜住 RenderSettings.customReflection 指向非 Cubemap 时 Unity 抛出的异常。
using System.Text; // 引入 StringBuilder，用来把间接光数据源状态写进 RenderGraph Debug 文本。
using UnityEngine; // 引入 UnityEngine，用来读取 RenderSettings、ReflectionProbe、Texture、Vector4 和 Shader.PropertyToID。
using UnityEngine.Rendering; // 引入 UnityEngine.Rendering，用来使用 CommandBuffer 和 SphericalHarmonicsL2。

namespace Burt.RenderPipeline // 定义 BurtRP 运行时命名空间，让 SetupLightingPass 和 DebugUtility 都能访问这个工具。
{
    internal static class BurtIndirectLightingUtility // 定义 BurtRP 自己的全局间接光数据入口，避免 Deferred Lighting 依赖 Forward DrawRenderers 的副作用。
    {
        private static readonly int AmbientSHArId = Shader.PropertyToID("_BurtAmbientSHAr"); // 缓存 BurtRP 自有 SH R 通道 L0/L1 常量属性 ID。
        private static readonly int AmbientSHAgId = Shader.PropertyToID("_BurtAmbientSHAg"); // 缓存 BurtRP 自有 SH G 通道 L0/L1 常量属性 ID。
        private static readonly int AmbientSHAbId = Shader.PropertyToID("_BurtAmbientSHAb"); // 缓存 BurtRP 自有 SH B 通道 L0/L1 常量属性 ID。
        private static readonly int AmbientSHBrId = Shader.PropertyToID("_BurtAmbientSHBr"); // 缓存 BurtRP 自有 SH R 通道 L2 常量属性 ID。
        private static readonly int AmbientSHBgId = Shader.PropertyToID("_BurtAmbientSHBg"); // 缓存 BurtRP 自有 SH G 通道 L2 常量属性 ID。
        private static readonly int AmbientSHBbId = Shader.PropertyToID("_BurtAmbientSHBb"); // 缓存 BurtRP 自有 SH B 通道 L2 常量属性 ID。
        private static readonly int AmbientSHCId = Shader.PropertyToID("_BurtAmbientSHC"); // 缓存 BurtRP 自有 SH C 项常量属性 ID。
        private static readonly int AmbientSHEnabledId = Shader.PropertyToID("_BurtAmbientSHEnabled"); // 缓存 BurtRP 自有 SH 是否有效的开关属性 ID。
        private static readonly int SkyReflectionTextureId = Shader.PropertyToID("_BurtSkyReflectionTexture"); // 缓存 BurtRP 全局天空反射 cubemap 属性 ID。
        private static readonly int SkyReflectionHDRId = Shader.PropertyToID("_BurtSkyReflectionHDR"); // 缓存 BurtRP 天空反射 HDR 解码参数属性 ID。
        private static readonly int SkyReflectionIntensityId = Shader.PropertyToID("_BurtSkyReflectionIntensity"); // 缓存 BurtRP 天空反射强度属性 ID。
        private static readonly int SkyReflectionEnabledId = Shader.PropertyToID("_BurtSkyReflectionEnabled"); // 缓存 BurtRP 天空反射是否有效的开关属性 ID。
        private static readonly int SkyReflectionMaxMipId = Shader.PropertyToID("_BurtSkyReflectionMaxMip"); // 缓存 BurtRP 全局天空反射最大 mip 属性 ID，避免 shader 继续写死 0..6。
        private static readonly int UnitySHArId = Shader.PropertyToID("unity_SHAr"); // 缓存 Unity 内置 SH R 通道 L0/L1 属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private static readonly int UnitySHAgId = Shader.PropertyToID("unity_SHAg"); // 缓存 Unity 内置 SH G 通道 L0/L1 属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private static readonly int UnitySHAbId = Shader.PropertyToID("unity_SHAb"); // 缓存 Unity 内置 SH B 通道 L0/L1 属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private static readonly int UnitySHBrId = Shader.PropertyToID("unity_SHBr"); // 缓存 Unity 内置 SH R 通道 L2 属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private static readonly int UnitySHBgId = Shader.PropertyToID("unity_SHBg"); // 缓存 Unity 内置 SH G 通道 L2 属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private static readonly int UnitySHBbId = Shader.PropertyToID("unity_SHBb"); // 缓存 Unity 内置 SH B 通道 L2 属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private static readonly int UnitySHCId = Shader.PropertyToID("unity_SHC"); // 缓存 Unity 内置 SH C 项属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private static readonly Vector4 DefaultSkyReflectionHDR = new Vector4(1f, 1f, 0f, 0f); // 定义默认 HDR 解码参数，表示按原始 cubemap RGB 直接使用。

        public static void UploadGlobalIndirectLighting(CommandBuffer cmd) // 上传当前帧/当前 request 使用的全局间接光数据。
        {
            if (cmd == null) // 如果命令缓冲为空，就没有可写入的 GPU 状态。
            {
                return; // 直接返回，避免工具影响渲染主流程。
            }

            var ambientProbe = RenderSettings.ambientProbe; // 读取 Unity Lighting 设置里的全局 ambient probe SH。
            var skyReflection = ResolveSkyReflectionTexture(); // 解析当前可用的天空/自定义反射纹理。
            var skyReflectionIntensity = Mathf.Max(0f, RenderSettings.reflectionIntensity); // 读取并保护 Unity Lighting 面板里的反射强度。

            UploadAmbientProbe(cmd, ambientProbe); // 把 ambient probe 上传到 BurtRP 自有 SH 常量和 Unity 兼容 SH 常量。
            UploadSkyReflection(cmd, skyReflection, skyReflectionIntensity); // 把天空反射 cubemap 和强度上传给 BurtRP 自有 IBL 入口。
        }

        public static void AppendDebugState(StringBuilder builder) // 把当前间接光数据源状态写入 RenderGraph Debug。
        {
            if (builder == null) // 如果没有字符串构建器，就没有安全输出目标。
            {
                return; // 直接返回，保持 Debug 工具空值安全。
            }

            builder.Append("  IndirectDiffuseSource=RenderSettings.ambientProbe"); // 写入当前间接漫反射数据源，当前第一版使用全局 ambient probe。
            builder.Append(" IndirectSpecularSource=").Append(ResolveSkyReflectionDebugSource()); // 写入当前间接高光数据源，方便判断是否有 cubemap 或只走环境色兜底。
            builder.Append(" ReflectionIntensity=").Append(Mathf.Max(0f, RenderSettings.reflectionIntensity).ToString("0.###")); // 写入反射强度，便于排查间接高光过强或过弱。
            builder.Append(" SkyReflectionMaxMip=").Append(ResolveSkyReflectionMaxMip().ToString("0.###")); // 写入当前反射 cubemap 的 mip 上限，方便排查 roughness 到 mip 的映射是否过糊或过锐。
            builder.AppendLine(); // 当前间接光状态行结束。
        }

        private static void UploadAmbientProbe(CommandBuffer cmd, SphericalHarmonicsL2 ambientProbe) // 上传 ambient probe 的 SH 常量。
        {
            var shAr = CreateUnitySHA(ambientProbe, 0); // 计算 R 通道 L0/L1 打包结果。
            var shAg = CreateUnitySHA(ambientProbe, 1); // 计算 G 通道 L0/L1 打包结果。
            var shAb = CreateUnitySHA(ambientProbe, 2); // 计算 B 通道 L0/L1 打包结果。
            var shBr = CreateUnitySHB(ambientProbe, 0); // 计算 R 通道 L2 打包结果。
            var shBg = CreateUnitySHB(ambientProbe, 1); // 计算 G 通道 L2 打包结果。
            var shBb = CreateUnitySHB(ambientProbe, 2); // 计算 B 通道 L2 打包结果。
            var shC = CreateUnitySHC(ambientProbe); // 计算 RGB 三通道共用的 L2 C 打包结果。

            cmd.SetGlobalVector(AmbientSHArId, shAr); // 上传 BurtRP 自有 R 通道 L0/L1 SH。
            cmd.SetGlobalVector(AmbientSHAgId, shAg); // 上传 BurtRP 自有 G 通道 L0/L1 SH。
            cmd.SetGlobalVector(AmbientSHAbId, shAb); // 上传 BurtRP 自有 B 通道 L0/L1 SH。
            cmd.SetGlobalVector(AmbientSHBrId, shBr); // 上传 BurtRP 自有 R 通道 L2 SH。
            cmd.SetGlobalVector(AmbientSHBgId, shBg); // 上传 BurtRP 自有 G 通道 L2 SH。
            cmd.SetGlobalVector(AmbientSHBbId, shBb); // 上传 BurtRP 自有 B 通道 L2 SH。
            cmd.SetGlobalVector(AmbientSHCId, shC); // 上传 BurtRP 自有 SH C 项。
            cmd.SetGlobalFloat(AmbientSHEnabledId, 1f); // 标记 BurtRP 自有 ambient SH 已经上传，可以被 Deferred fullscreen pass 稳定读取。
            cmd.SetGlobalVector(UnitySHArId, shAr); // 同步 Unity 兼容 R 通道 SH，避免仍使用 ShadeSH9 的现有 shader 读到旧值。
            cmd.SetGlobalVector(UnitySHAgId, shAg); // 同步 Unity 兼容 G 通道 SH。
            cmd.SetGlobalVector(UnitySHAbId, shAb); // 同步 Unity 兼容 B 通道 SH。
            cmd.SetGlobalVector(UnitySHBrId, shBr); // 同步 Unity 兼容 R 通道二阶 SH。
            cmd.SetGlobalVector(UnitySHBgId, shBg); // 同步 Unity 兼容 G 通道二阶 SH。
            cmd.SetGlobalVector(UnitySHBbId, shBb); // 同步 Unity 兼容 B 通道二阶 SH。
            cmd.SetGlobalVector(UnitySHCId, shC); // 同步 Unity 兼容 SH C 项。
        }

        private static void UploadSkyReflection(CommandBuffer cmd, Texture skyReflection, float intensity) // 上传全局天空反射纹理。
        {
            if (skyReflection == null) // 如果项目没有可用的天空反射纹理。
            {
                cmd.SetGlobalFloat(SkyReflectionEnabledId, 0f); // 标记天空反射无效，shader 会回退到环境色兜底。
                cmd.SetGlobalFloat(SkyReflectionIntensityId, 0f); // 清零强度，避免 shader 误用上一帧状态。
                cmd.SetGlobalFloat(SkyReflectionMaxMipId, 0f); // 清零 mip 上限，避免 shader 在无 cubemap 时沿用上一帧的 mip 数。
                cmd.SetGlobalVector(SkyReflectionHDRId, DefaultSkyReflectionHDR); // 仍上传默认解码参数，保持 shader 变量稳定。
                return; // 没有纹理时不绑定 samplerCUBE，避免 2D fallback 误绑定到 cube 采样器。
            }

            var skyReflectionMaxMip = CalculateSkyReflectionMaxMip(skyReflection); // 根据实际 cubemap mip 数计算最大可采样 mip，避免不同尺寸反射贴图都使用固定 6。
            var skyReflectionHDR = ResolveSkyReflectionHDR(); // 解析当前天空反射纹理对应的 HDR 解码参数，自定义 cubemap 第一版仍按原始 RGB 处理。

            cmd.SetGlobalTexture(SkyReflectionTextureId, skyReflection); // 绑定当前全局 sky/custom reflection cubemap。
            cmd.SetGlobalVector(SkyReflectionHDRId, skyReflectionHDR); // 上传 HDR 解码参数，默认反射使用 Unity 提供的 decode，自定义反射暂时直读 RGB。
            cmd.SetGlobalFloat(SkyReflectionIntensityId, intensity); // 上传反射强度，让 Lighting 面板的 Reflection Intensity 影响 Deferred IBL。
            cmd.SetGlobalFloat(SkyReflectionEnabledId, intensity > 0f ? 1f : 0f); // 只有有 cubemap 且强度大于 0 时才启用天空反射采样。
            cmd.SetGlobalFloat(SkyReflectionMaxMipId, skyReflectionMaxMip); // 上传实际 mip 上限，让 shader 的 roughness->mip 曲线匹配当前反射贴图。
        }

        private static Texture ResolveSkyReflectionTexture() // 解析当前 BurtRP 全局天空反射纹理。
        {
            if (TryResolveCustomReflection(out var customReflection)) // 如果 Lighting 面板确实处于 Custom Reflection 模式，并且引用的是合法 Cubemap。
            {
                return customReflection; // 优先使用自定义 cubemap，符合 Unity Lighting 面板语义。
            }

            return ReflectionProbe.defaultTexture; // 没有自定义 cubemap 时使用 Unity 公开的默认反射纹理，避免访问当前版本不存在的 RenderSettings 默认反射属性。
        }

        private static Vector4 ResolveSkyReflectionHDR() // 解析当前 BurtRP 全局天空反射的 HDR 解码参数。
        {
            if (TryResolveCustomReflection(out _)) // 如果当前确实使用用户指定的合法自定义 cubemap。
            {
                return DefaultSkyReflectionHDR; // 自定义 cubemap 没有统一公开的 decode 参数，第一版按原始 RGB 直接使用。
            }

            if (ReflectionProbe.defaultTexture != null) // 如果 Unity 提供了默认反射纹理。
            {
                return ReflectionProbe.defaultTextureHDRDecodeValues; // 使用 Unity 同步给默认反射纹理的 HDR 解码参数，避免 default reflection 偏暗。
            }

            return DefaultSkyReflectionHDR; // 没有默认反射纹理时返回直读参数，保持 shader 状态稳定。
        }

        private static string ResolveSkyReflectionDebugSource() // 返回当前天空反射数据源名称。
        {
            if (TryResolveCustomReflection(out _)) // 如果当前确实使用用户指定的合法自定义 cubemap。
            {
                return "RenderSettings.customReflection"; // Debug 中写出自定义来源。
            }

            if (RenderSettings.defaultReflectionMode == DefaultReflectionMode.Custom) // 如果用户选择了 Custom Reflection，但上面的安全读取没有拿到合法 Cubemap。
            {
                return "InvalidCustomReflectionFallback"; // Debug 中写出自定义反射无效并已回退，方便定位 Lighting 面板配置问题。
            }

            if (ReflectionProbe.defaultTexture != null) // 如果 Unity 已经生成默认天空反射纹理。
            {
                return "ReflectionProbe.defaultTexture"; // Debug 中写出当前 Unity 版本可公开访问的默认天空反射来源。
            }

            return "AmbientColorFallback"; // 没有 cubemap 时写出环境色兜底来源。
        }

        private static bool TryResolveCustomReflection(out Texture customReflection) // 安全读取 Lighting 面板里的 Custom Reflection 引用。
        {
            customReflection = null; // 先把输出清空，保证失败路径不会把上一帧数据误传出去。

            if (RenderSettings.defaultReflectionMode != DefaultReflectionMode.Custom) // 只有 Reflection Source 真的选择 Custom 时，才允许访问 customReflection。
            {
                return false; // Skybox 模式下不读取 customReflection，避免 Unity 在引用不是 Cubemap 时抛 ArgumentException。
            }

            try // Unity 在 customReflection 引用非 Cubemap 时会从 getter 直接抛异常，所以必须保护读取。
            {
                customReflection = RenderSettings.customReflection; // 读取 Lighting 面板中用户配置的自定义反射 Cubemap。
            }
            catch (ArgumentException) // 如果引用不是 Cubemap，说明 Lighting 面板配置无效。
            {
                customReflection = null; // 清空输出，让调用方安全回退到 ReflectionProbe.defaultTexture 或环境色兜底。

                return false; // 返回失败，避免异常继续中断整条渲染管线。
            }

            return customReflection != null; // 只有真正拿到 Cubemap 时才告诉调用方可以使用自定义反射。
        }

        private static float ResolveSkyReflectionMaxMip() // 返回当前天空反射 cubemap 的最大 mip，用于 Debug 输出。
        {
            return CalculateSkyReflectionMaxMip(ResolveSkyReflectionTexture()); // 复用同一套解析和计算逻辑，保证 Debug 行与真正上传到 shader 的值一致。
        }

        private static float CalculateSkyReflectionMaxMip(Texture skyReflection) // 根据纹理资源计算 shader 可用的最大 mip。
        {
            if (skyReflection == null) // 如果没有可用纹理，就没有合法的 mip 链。
            {
                return 0f; // 返回 0，表示 shader 只能走环境色兜底或 legacy 路径。
            }

            return Mathf.Max(0f, skyReflection.mipmapCount - 1f); // Unity 的 mipmapCount 是数量，采样 LOD 上限需要转成最后一个 mip 的索引。
        }

        private static Vector4 CreateUnitySHA(SphericalHarmonicsL2 sh, int channel) // 创建 UnityCG.cginc 中 unity_SHA* 的打包结果。
        {
            return new Vector4(sh[channel, 3], sh[channel, 1], sh[channel, 2], sh[channel, 0] - sh[channel, 6]); // xyz 对应 L1，w 把 L0 和 L2 常数项合并，匹配 Unity 的 ShadeSH9 约定。
        }

        private static Vector4 CreateUnitySHB(SphericalHarmonicsL2 sh, int channel) // 创建 UnityCG.cginc 中 unity_SHB* 的打包结果。
        {
            return new Vector4(sh[channel, 4], sh[channel, 5], sh[channel, 6] * 3f, sh[channel, 7]); // z 乘 3 是 Unity 内置二阶 SH 打包要求，保证 ShadeSH9 还原方向项正确。
        }

        private static Vector4 CreateUnitySHC(SphericalHarmonicsL2 sh) // 创建 UnityCG.cginc 中 unity_SHC 的打包结果。
        {
            return new Vector4(sh[0, 8], sh[1, 8], sh[2, 8], 1f); // rgb 保存三个颜色通道的第 8 个二阶系数，w 暂未使用但保持为 1。
        }
    }
}
