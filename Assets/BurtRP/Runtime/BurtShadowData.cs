using UnityEngine; // 引入 UnityEngine 命名空间，用来读取 Light 阴影设置并做数值保护。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来访问 VisibleLight、LightShadows 等 SRP 数据。
namespace Burt.RenderPipeline // 让阴影数据和渲染请求、灯光数据保持在同一个 BurtRP 运行时命名空间里。
{
    public sealed class BurtShadowData // 保存一次 BurtRenderRequest 内主光阴影需要的全部运行时参数。
    {
        public const int DefaultMainLightShadowResolution = 1024; // 定义没有资产配置或 Light 自定义分辨率时使用的安全默认阴影分辨率。
        public const float DefaultMainLightShadowDistance = 50f; // 定义默认阴影剔除距离，避免 Unity culling 阶段因为 shadowDistance 为 0 而不收集投影物。
        public const float DefaultMainLightShadowDepthBias = 0.05f; // 定义默认常量深度偏移，数值贴近 Unity Light 的 shadowBias 默认量级。
        public const float DefaultMainLightShadowNormalBias = 0.4f; // 定义默认 ShadowCaster normal bias 倍率，后续会按 shadow texel 折算成世界空间顶点偏移。
        public const float DefaultMainLightShadowSampleBias = 0.001f; // 定义默认接收端采样偏移，替代 shader 中不可调的硬编码偏移。
        public const float DefaultMainLightShadowPCSSLightSize = 6f;
        public const float DefaultMainLightShadowPCSSBlockerSearchRadius = 8f;
        public const float DefaultMainLightShadowPCSSMaxFilterRadius = 24f;
        public const int MaxMainLightShadowCascadeCount = 4;
        public const int DefaultMainLightShadowCascadeCount = 1;
        public const float DefaultMainLightShadowCascadeSplit1 = 0.1f;
        public const float DefaultMainLightShadowCascadeSplit2 = 0.25f;
        public const float DefaultMainLightShadowCascadeSplit3 = 0.5f;
        public const float DefaultMainLightShadowCascadeBlendDistance = 0.05f;
        public const float DefaultMainLightShadowFadeDistance = 5f;
        private bool mainLightUsesCustomResolution; // 记录主光是否填写了 shadowCustomResolution，让 PipelineAsset 只覆盖默认分辨率场景。
        public bool HasMainLightShadow { get; private set; } // 告诉后续 Pass 当前主光是否真的需要渲染和采样 shadow map。
        public int MainLightIndex { get; private set; } // 保存主光在 CullingResults.visibleLights 中的索引，DrawShadows 需要用它找到投影灯光。
        public LightShadows MainLightShadowType { get; private set; } // 保存主光阴影类型，Lit shader 会根据 Hard 或 Soft 选择采样策略。
        public float MainLightShadowStrength { get; private set; } // 保存主光阴影强度，范围限制在 0 到 1，shader 用它混合阴影衰减。
        public float MainLightShadowNearPlane { get; private set; } // 保存渲染主光 shadow map 时使用的近裁剪面，来自 Unity Light 设置。
        public int MainLightShadowResolution { get; private set; } // 保存主光 shadow map 最终分辨率，已经合并 Light 和 PipelineAsset 的设置。
        public float MainLightShadowDepthBias { get; private set; } // 保存 ShadowCaster 阶段的常量深度偏移，用于 CommandBuffer.SetGlobalDepthBias。
        public float MainLightShadowNormalBias { get; private set; } // 保存 ShadowCaster 阶段的 normal bias 倍率，C# 会上传给 shader 做顶点法线偏移。
        public float MainLightShadowSampleBias { get; private set; } // 保存 Receiver 阶段的采样偏移，Lit shader 比较 shadow map 前会使用它。
        public bool EnableMainLightShadowPCSS { get; private set; }
        public float MainLightShadowPCSSLightSize { get; private set; }
        public float MainLightShadowPCSSBlockerSearchRadius { get; private set; }
        public float MainLightShadowPCSSMaxFilterRadius { get; private set; }
        public int MainLightShadowCascadeCount { get; private set; }
        public float MainLightShadowCascadeSplit1 { get; private set; }
        public float MainLightShadowCascadeSplit2 { get; private set; }
        public float MainLightShadowCascadeSplit3 { get; private set; }
        public float MainLightShadowCascadeBlendDistance { get; private set; }
        public float MainLightShadowFadeDistance { get; private set; }
        public bool IsMainLightShadowSoft => MainLightShadowType == LightShadows.Soft; // 把 Unity 的 Soft Shadows 枚举转成 shader 更容易使用的布尔语义。
        public bool IsMainLightShadowHard => MainLightShadowType == LightShadows.Hard; // PCSS 作为 BurtRP 自己的软化路径，挂在 Unity Hard Shadows 模式下启用。
        private BurtShadowData() // 隐藏构造函数，强制所有调用方通过工厂函数创建已初始化的数据。
        {
        } // 构造函数本身不写逻辑，所有默认值都集中在 ResetToDefaults，避免遗漏字段。
        public static BurtShadowData None() // 创建一个“当前 request 没有主光阴影”的安全数据对象。
        {
            var data = new BurtShadowData(); // 分配新的阴影数据对象，避免调用方拿到空引用。
            data.ResetToDefaults(); // 把所有字段填成无阴影但可安全上传给 shader 的值。
            return data; // 返回已经初始化好的无阴影数据。
        }
        public static BurtShadowData CreateForMainLight(VisibleLight visibleLight, int mainLightIndex) // 从已选中的主 Directional Light 提取阴影数据。
        {
            var data = None(); // 先从无阴影默认值开始，保证任何早退都能返回安全数据。
            data.MainLightIndex = mainLightIndex; // 记录主光索引，即使这盏灯最后不投影也保留诊断信息。
            var light = visibleLight.light; // 读取 Unity Light 组件，因为阴影开关、强度、bias 都挂在 Light 上。
            if (light == null) // 如果 Unity 没有提供托管 Light 引用，说明无法读取具体阴影设置。
            {
                return data; // 返回无阴影数据，避免后续 Pass 使用缺失的 Light 参数。
            }
            data.MainLightShadowType = light.shadows; // 复制 Light 的阴影类型，保留 None、Hard、Soft 三种语义。
            data.MainLightShadowStrength = Mathf.Clamp01(light.shadowStrength); // 复制并限制阴影强度，避免 shader 收到无效混合系数。
            data.MainLightShadowNearPlane = Mathf.Max(0.001f, light.shadowNearPlane); // 复制 shadow near plane，并保证它大于 0，避免矩阵计算失败。
            data.mainLightUsesCustomResolution = light.shadowCustomResolution > 0; // 记录 Light 是否显式指定分辨率，PipelineAsset 不应覆盖这种逐灯调试需求。
            data.MainLightShadowResolution = ResolveShadowResolution(light); // 解析 Light 自身的分辨率设置，后续可再被 PipelineAsset 默认值覆盖。
            data.MainLightShadowDepthBias = Mathf.Max(0f, light.shadowBias); // 读取 Light 的常量 bias 作为资产缺失时的兜底值。
            data.MainLightShadowNormalBias = Mathf.Max(0f, light.shadowNormalBias); // 读取 Light 的 normal bias 作为资产缺失时的顶点偏移兜底值。
            data.MainLightShadowSampleBias = DefaultMainLightShadowSampleBias; // 接收端采样偏移由 BurtRP 自己控制，默认使用很小的正数。
            data.RefreshMainLightShadowEnabled(); // 根据阴影类型、强度和分辨率重新计算 HasMainLightShadow。
            return data; // 返回从主光提取出的阴影数据。
        }
        public BurtShadowData CreateMergedWithPipelineAsset(BurtRenderPipelineAsset asset) // 创建一份合并 PipelineAsset 配置后的阴影数据副本。
        {
            var merged = Clone(); // 复制当前 Light 数据，避免直接修改 request 上保存的原始灯光解析结果。
            merged.ApplyPipelineAssetSettings(asset); // 把 SRP 资产上的全局阴影开关、默认分辨率和 bias 合并进去。
            return merged; // 返回合并后的运行时阴影数据，供本帧 Pass 使用。
        }
        private BurtShadowData Clone() // 复制当前阴影数据对象的所有字段。
        {
            var data = new BurtShadowData(); // 创建新对象，让合并资产设置时不会污染原始数据。
            data.mainLightUsesCustomResolution = mainLightUsesCustomResolution; // 复制是否使用 Light 自定义分辨率的标记。
            data.HasMainLightShadow = HasMainLightShadow; // 复制当前阴影可用状态。
            data.MainLightIndex = MainLightIndex; // 复制 visible light 索引。
            data.MainLightShadowType = MainLightShadowType; // 复制阴影类型。
            data.MainLightShadowStrength = MainLightShadowStrength; // 复制阴影强度。
            data.MainLightShadowNearPlane = MainLightShadowNearPlane; // 复制 shadow near plane。
            data.MainLightShadowResolution = MainLightShadowResolution; // 复制 shadow map 分辨率。
            data.MainLightShadowDepthBias = MainLightShadowDepthBias; // 复制 ShadowCaster 常量深度偏移。
            data.MainLightShadowNormalBias = MainLightShadowNormalBias; // 复制 ShadowCaster normal bias 倍率。
            data.MainLightShadowSampleBias = MainLightShadowSampleBias; // 复制 Receiver 采样偏移。
            data.EnableMainLightShadowPCSS = EnableMainLightShadowPCSS;
            data.MainLightShadowPCSSLightSize = MainLightShadowPCSSLightSize;
            data.MainLightShadowPCSSBlockerSearchRadius = MainLightShadowPCSSBlockerSearchRadius;
            data.MainLightShadowPCSSMaxFilterRadius = MainLightShadowPCSSMaxFilterRadius;
            data.MainLightShadowCascadeCount = MainLightShadowCascadeCount;
            data.MainLightShadowCascadeSplit1 = MainLightShadowCascadeSplit1;
            data.MainLightShadowCascadeSplit2 = MainLightShadowCascadeSplit2;
            data.MainLightShadowCascadeSplit3 = MainLightShadowCascadeSplit3;
            data.MainLightShadowCascadeBlendDistance = MainLightShadowCascadeBlendDistance;
            data.MainLightShadowFadeDistance = MainLightShadowFadeDistance;
            return data; // 返回完整副本。
        }
        private void ApplyPipelineAssetSettings(BurtRenderPipelineAsset asset) // 阴影质量参数已经收敛到 Global Volume，asset 参数只保留调用兼容。
        {
            ApplyVolumeSettings(BurtShadowUtility.ResolveMainLightShadowVolumeComponent()); // 从当前 VolumeStack 应用分辨率、bias、CSM 和 PCSS override。
            RefreshMainLightShadowEnabled(); // 重新计算阴影是否仍然可用。
        }
        private void ApplyVolumeSettings(MainLightShadowVolumeComponent volumeSettings) // ??? VolumeStack ?????????????????????
        {
            if (volumeSettings == null || !volumeSettings.active) // ????????????????/Light ????????
            {
                return; // ????????????
            }
            if (volumeSettings.enabled.overrideState && !volumeSettings.enabled.value) // ?? Volume ?????????????????????
            {
                DisableMainLightShadow(); // ????????????? shadow map ???????
                return; // ??????????????
            }
            if (!mainLightUsesCustomResolution && volumeSettings.resolution.overrideState) // ?? Light ??????????Volume ????? shadow map ????
            {
                MainLightShadowResolution = Mathf.Clamp(volumeSettings.resolution.value, 16, 8192); // ????????????????
            }
            if (volumeSettings.cascadeCount.overrideState)
            {
                MainLightShadowCascadeCount = NormalizeMainLightShadowCascadeCount(volumeSettings.cascadeCount.value);
            }
            if (volumeSettings.cascadeSplit1.overrideState)
            {
                MainLightShadowCascadeSplit1 = Mathf.Clamp(volumeSettings.cascadeSplit1.value, 0.01f, 0.99f);
            }
            if (volumeSettings.cascadeSplit2.overrideState)
            {
                MainLightShadowCascadeSplit2 = Mathf.Clamp(volumeSettings.cascadeSplit2.value, 0.02f, 0.995f);
            }
            if (volumeSettings.cascadeSplit3.overrideState)
            {
                MainLightShadowCascadeSplit3 = Mathf.Clamp(volumeSettings.cascadeSplit3.value, 0.03f, 0.999f);
            }
            if (volumeSettings.cascadeBlendDistance.overrideState)
            {
                MainLightShadowCascadeBlendDistance = Mathf.Clamp(volumeSettings.cascadeBlendDistance.value, 0f, 0.5f);
            }
            if (volumeSettings.shadowFadeDistance.overrideState)
            {
                MainLightShadowFadeDistance = Mathf.Max(0f, volumeSettings.shadowFadeDistance.value);
            }
            if (volumeSettings.depthBias.overrideState) // Volume ?? Depth Bias ??? ShadowCaster ?????
            {
                MainLightShadowDepthBias = Mathf.Max(0f, volumeSettings.depthBias.value); // ????? GPU ? bias ???
            }
            if (volumeSettings.normalBias.overrideState) // Volume ?? Normal Bias ????????????
            {
                MainLightShadowNormalBias = Mathf.Max(0f, volumeSettings.normalBias.value); // ?? normal bias ???
            }
            if (volumeSettings.sampleBias.overrideState) // Volume ?? Sample Bias ???????????
            {
                MainLightShadowSampleBias = Mathf.Max(0f, volumeSettings.sampleBias.value); // ????? bias ???
            }
            if (volumeSettings.pcssEnabled.overrideState)
            {
                EnableMainLightShadowPCSS = volumeSettings.pcssEnabled.value;
            }
            if (volumeSettings.pcssLightSize.overrideState)
            {
                MainLightShadowPCSSLightSize = Mathf.Max(0f, volumeSettings.pcssLightSize.value);
            }
            if (volumeSettings.pcssBlockerSearchRadius.overrideState)
            {
                MainLightShadowPCSSBlockerSearchRadius = Mathf.Max(0f, volumeSettings.pcssBlockerSearchRadius.value);
            }
            if (volumeSettings.pcssMaxFilterRadius.overrideState)
            {
                MainLightShadowPCSSMaxFilterRadius = Mathf.Max(0f, volumeSettings.pcssMaxFilterRadius.value);
            }

            NormalizeMainLightShadowCascadeSettings();
            NormalizeMainLightShadowPCSSSettings();
        }

        private void NormalizeMainLightShadowCascadeSettings()
        {
            MainLightShadowCascadeCount = NormalizeMainLightShadowCascadeCount(MainLightShadowCascadeCount);
            MainLightShadowCascadeSplit1 = Mathf.Clamp(MainLightShadowCascadeSplit1, 0.01f, 0.99f);
            MainLightShadowCascadeSplit2 = Mathf.Clamp(MainLightShadowCascadeSplit2, MainLightShadowCascadeSplit1 + 0.01f, 0.995f);
            MainLightShadowCascadeSplit3 = Mathf.Clamp(MainLightShadowCascadeSplit3, MainLightShadowCascadeSplit2 + 0.01f, 0.999f);
            MainLightShadowCascadeBlendDistance = Mathf.Clamp(MainLightShadowCascadeBlendDistance, 0f, 0.5f);
            MainLightShadowFadeDistance = Mathf.Max(0f, MainLightShadowFadeDistance);
        }

        private void NormalizeMainLightShadowPCSSSettings()
        {
            MainLightShadowPCSSLightSize = Mathf.Max(0f, MainLightShadowPCSSLightSize);
            MainLightShadowPCSSBlockerSearchRadius = Mathf.Max(0f, MainLightShadowPCSSBlockerSearchRadius);
            MainLightShadowPCSSMaxFilterRadius = Mathf.Max(0f, MainLightShadowPCSSMaxFilterRadius);

            if (MainLightShadowPCSSMaxFilterRadius > 0f)
            {
                MainLightShadowPCSSBlockerSearchRadius = Mathf.Min(MainLightShadowPCSSBlockerSearchRadius, MainLightShadowPCSSMaxFilterRadius);
                MainLightShadowPCSSLightSize = Mathf.Min(MainLightShadowPCSSLightSize, MainLightShadowPCSSMaxFilterRadius);
            }
        }
        private void DisableMainLightShadow() // 把当前数据切换为明确的无阴影状态。
        {
            HasMainLightShadow = false; // 标记后续 Pass 不应该申请或绘制主光 shadow map。
            MainLightShadowStrength = 0f; // 把强度清零，确保 shader 即使读到旧矩阵或旧纹理也不会产生阴影。
        }
        private void RefreshMainLightShadowEnabled() // 根据当前字段重新计算主光阴影是否可用。
        {
            HasMainLightShadow = MainLightShadowType != LightShadows.None && MainLightShadowStrength > 0f && MainLightShadowResolution > 0; // 有阴影类型、有强度、有有效分辨率才开启阴影。
        }
        private void ResetToDefaults() // 初始化为稳定的无阴影默认状态。
        {
            mainLightUsesCustomResolution = false; // 默认没有 Light 自定义分辨率。
            HasMainLightShadow = false; // 默认不渲染主光阴影。
            MainLightIndex = -1; // 使用 -1 表示没有对应的 visible light 条目。
            MainLightShadowType = LightShadows.None; // 默认阴影类型为 None。
            MainLightShadowStrength = 0f; // 默认阴影强度为 0，shader 会直接走完全受光路径。
            MainLightShadowNearPlane = 0.1f; // 默认 near plane 使用一个安全的正数。
            MainLightShadowResolution = DefaultMainLightShadowResolution; // 默认分辨率使用 BurtRP 主光阴影常量。
            MainLightShadowDepthBias = DefaultMainLightShadowDepthBias; // 默认 ShadowCaster 常量 bias 使用 BurtRP 常量。
            MainLightShadowNormalBias = DefaultMainLightShadowNormalBias; // 默认 ShadowCaster normal bias 使用 BurtRP 常量。
            MainLightShadowSampleBias = DefaultMainLightShadowSampleBias; // 默认 Receiver 采样 bias 使用 BurtRP 常量。
            EnableMainLightShadowPCSS = false;
            MainLightShadowPCSSLightSize = DefaultMainLightShadowPCSSLightSize;
            MainLightShadowPCSSBlockerSearchRadius = DefaultMainLightShadowPCSSBlockerSearchRadius;
            MainLightShadowPCSSMaxFilterRadius = DefaultMainLightShadowPCSSMaxFilterRadius;
            MainLightShadowCascadeCount = DefaultMainLightShadowCascadeCount;
            MainLightShadowCascadeSplit1 = DefaultMainLightShadowCascadeSplit1;
            MainLightShadowCascadeSplit2 = DefaultMainLightShadowCascadeSplit2;
            MainLightShadowCascadeSplit3 = DefaultMainLightShadowCascadeSplit3;
            MainLightShadowCascadeBlendDistance = DefaultMainLightShadowCascadeBlendDistance;
            MainLightShadowFadeDistance = DefaultMainLightShadowFadeDistance;
            NormalizeMainLightShadowCascadeSettings();
            NormalizeMainLightShadowPCSSSettings();
        }
        public static int NormalizeMainLightShadowCascadeCount(int cascadeCount)
        {
            if (cascadeCount <= 1)
            {
                return 1;
            }
            if (cascadeCount <= 2)
            {
                return 2;
            }
            return MaxMainLightShadowCascadeCount;
        }
        public Vector3 CreateMainLightShadowCascadeSplitVector()
        {
            var cascadeCount = NormalizeMainLightShadowCascadeCount(MainLightShadowCascadeCount);
            if (cascadeCount <= 1)
            {
                return new Vector3(1f, 0f, 0f);
            }
            if (cascadeCount == 2)
            {
                return new Vector3(MainLightShadowCascadeSplit1, 0f, 0f);
            }
            return new Vector3(MainLightShadowCascadeSplit1, MainLightShadowCascadeSplit2, MainLightShadowCascadeSplit3);
        }
        private static int ResolveShadowResolution(Light light) // 把 Unity Light 的分辨率设置转换成具体整数尺寸。
        {
            if (light == null) // 如果没有 Light，就只能使用 BurtRP 默认值。
            {
                return DefaultMainLightShadowResolution; // 返回默认分辨率作为安全兜底。
            }
            if (light.shadowCustomResolution > 0) // 如果 Light 显式填写了自定义分辨率，它优先于 PipelineAsset 默认分辨率。
            {
                return Mathf.Max(1, light.shadowCustomResolution); // 返回自定义分辨率，并保证至少为 1。
            }
            var shadowResolutionLevel = (int)light.shadowResolution; // 读取 Unity 的分辨率枚举整数，避免依赖具体枚举名称。
            switch (shadowResolutionLevel) // 在没有 PipelineAsset 时，仍然保留 Unity Light 分辨率档位的兜底映射。
            {
                case 0: return 512; // Low 档位返回较低分辨率。
                case 1: return 1024; // Medium 档位返回中等分辨率。
                case 2: return 2048; // High 档位返回高分辨率。
                case 3: return 4096; // VeryHigh 档位返回很高分辨率。
                default: return DefaultMainLightShadowResolution; // FromQualitySettings 或未知值返回 BurtRP 默认值。
            }
        }
    }
    internal static class BurtShadowUtility // 定义阴影渲染辅助工具，集中处理主光阴影数据读取、合并和常用派生参数。
    {
        public static BurtShadowData ResolveMainLightShadowData(BurtRenderRequest request) // 从 request 中安全读取原始主光阴影数据。
        {
            if (request == null) // 如果 request 为空，说明当前没有可分析的渲染任务。
            {
                return null; // 返回空值，让调用方用统一方式跳过阴影流程。
            }
            var lightingData = request.LightingData; // 从 request 中读取预先收集好的灯光数据。
            if (lightingData == null) // 如果灯光数据为空，说明 request 创建阶段没有成功生成灯光信息。
            {
                return null; // 返回空值，避免后续访问 ShadowData 时出现空引用。
            }
            return lightingData.ShadowData; // 返回灯光数据里保存的主光阴影数据，可能是无阴影的安全对象。
        }
        public static BurtShadowData ResolveMainLightShadowData(BurtRenderRequest request, BurtRenderPipelineAsset asset) // ??????????? PipelineAsset ???
        {
            if (request != null && request.TryGetCachedResolvedMainLightShadowData(asset, out var cachedShadowData)) // ??? request ??? pass ??????????????????????????
            {
                return cachedShadowData;
            }
            var shadowData = ResolveMainLightShadowData(request); // ??? request ?? Light ???????????
            if (shadowData == null) // ?? request ?????????????????
            {
                return null; // ????????????????
            }
            var mergedShadowData = shadowData.CreateMergedWithPipelineAsset(asset); // ??????????????????? request ?????
            request?.CacheResolvedMainLightShadowData(asset, mergedShadowData); // ?????????? request?? Setup/Shadow/Deferred pass ???
            return mergedShadowData;
        }
        public static bool ShouldUseMainLightShadow(BurtRenderRequest request) // 判断当前 request 是否应该启用主光阴影流程。
        {
            return ShouldUseMainLightShadow(request, null); // 没有资产上下文时只按 Light 自身设置和 BurtRP 默认值判断。
        }
        public static bool ShouldUseMainLightShadow(BurtRenderRequest request, BurtRenderPipelineAsset asset) // 判断当前 request 是否应该启用主光阴影流程，并纳入 PipelineAsset 总开关。
        {
            var shadowData = ResolveMainLightShadowData(request, asset); // 读取合并后的阴影数据，保证资产设置参与判断。
            if (shadowData == null) // 如果阴影数据不存在，说明当前 request 不具备渲染阴影的条件。
            {
                return false; // 返回 false，让调用方跳过阴影资源和阴影 Pass。
            }
            return shadowData.HasMainLightShadow; // 只有主光存在、开启阴影、强度大于 0，并且资产允许时才返回 true。
        }
        public static float ResolveMainLightShadowDistance(BurtRenderPipelineAsset asset) // ?? Global Volume ???????? cullingParameters ????????
        {
            var volumeSettings = ResolveMainLightShadowVolumeComponent(); // ?????? VolumeStack ????????? culling ?????????????
            if (volumeSettings != null && volumeSettings.active)
            {
                if (volumeSettings.enabled.overrideState && !volumeSettings.enabled.value)
                {
                    return 0f;
                }
                if (volumeSettings.distance.overrideState)
                {
                    return Mathf.Max(0f, volumeSettings.distance.value);
                }
            }
            return BurtShadowData.DefaultMainLightShadowDistance; // ?? Volume override ??? BurtRP ???????
        }
        public static MainLightShadowVolumeComponent ResolveMainLightShadowVolumeComponent() // ??? VolumeStack ?? BurtRP ?????????
        {
            var volumeManager = VolumeManager.instance; // ?? Unity ?? VolumeManager??????????????? VolumeStack?
            if (volumeManager == null) // ???????????????????
            {
                return null; // ???????????? PipelineAsset ???
            }
            var stack = volumeManager.stack; // ?????? VolumeStack?
            if (stack == null) // Volume ??????????????
            {
                return null; // ????????????????
            }
            return stack.GetComponent<MainLightShadowVolumeComponent>(); // ?????? Unity ?????/???????? overrideState ???????
        }
        public static float ResolveMainLightShadowDistance( // 根据管线资产和当前相机解析最终阴影剔除距离，保证日志与 culling 阶段看到的距离一致。
            BurtRenderPipelineAsset asset, // 接收当前管线资产，用来读取主光阴影距离和资产总开关。
            Camera camera) // 接收当前 BurtRenderRequest 里的相机，用它的 farClipPlane 对阴影距离做上限保护。
        {
            var shadowDistance = ResolveMainLightShadowDistance(asset); // 先读取资产层决定的阴影距离；资产关闭阴影时这里会得到 0。
            if (camera != null) // 如果当前请求带有有效相机，就可以使用相机远裁剪面进一步约束阴影距离。
            {
                shadowDistance = Mathf.Min(shadowDistance, camera.farClipPlane); // 阴影剔除距离不应超过相机可见范围，避免日志显示一个实际不可见的更大距离。
            }
            return Mathf.Max(0f, shadowDistance); // 返回非负距离，避免异常配置让 cullingParameters.shadowDistance 或诊断日志出现负值。
        }
        public static void LogMainLightShadowDiagnostics( // 输出一条结构化主光阴影诊断日志，帮助定位阴影为何启用或未启用。
            BurtRenderRequest request, // 接收当前渲染请求，用来读取相机、灯光数据和剔除结果相关信息。
            BurtRenderPipelineAsset asset, // 接收当前管线资产，用来读取日志开关和合并后的阴影配置。
            bool useMainLightShadow, // 接收 Graph Assembler 已经计算好的最终主光阴影启用决策。
            bool mainLightShadowMapIsValid) // 接收 RenderGraph 里 MainLightShadowMap 资源句柄是否有效，方便判断资源注册是否符合预期。
        {
            if (asset == null)
            {
                return;
            }
            if (!BurtShadingDebugSettings.IsMainLightShadowDebugMode(BurtShadingDebugSettings.Mode))
            {
                return;
            }
            var camera = request != null ? request.Camera : null; // 从 request 中安全读取当前相机，request 为空时保持 null。
            var cameraName = camera != null ? camera.name : "<null>"; // 解析相机名称，空相机使用明确占位文本。
            var cameraType = camera != null ? camera.cameraType.ToString() : "<null>"; // 解析相机类型，方便区分 Game、SceneView、Preview 等来源。
            var lightingData = request != null ? request.LightingData : null; // 从 request 中安全读取灯光数据，缺失时下面使用默认诊断值。
            var shadowData = ResolveMainLightShadowData(request, asset); // 读取并合并 PipelineAsset 后的阴影数据，保证日志反映最终运行时配置。
            var visibleLightCount = lightingData != null ? lightingData.VisibleLightCount : 0; // 读取 Unity culling 返回的可见灯光数量，缺失时记为 0。
            var hasMainLight = lightingData != null && lightingData.HasMainLight; // 读取 BurtRP 是否选中了主方向光。
            var mainLightIndex = lightingData != null ? lightingData.MainLightIndex : -1; // 读取主光在 visibleLights 里的索引，缺失时使用 -1。
            var shadowType = shadowData != null ? shadowData.MainLightShadowType : LightShadows.None; // 读取合并后的阴影类型，缺失时使用 None。
            var shadowStrength = shadowData != null ? shadowData.MainLightShadowStrength : 0f; // 读取合并后的阴影强度，缺失时使用 0。
            var shadowResolution = shadowData != null ? shadowData.MainLightShadowResolution : 0; // 读取最终 shadow map 分辨率，缺失时使用 0。
            var depthBias = shadowData != null ? shadowData.MainLightShadowDepthBias : 0f; // 读取最终 ShadowCaster 常量深度偏移，缺失时使用 0。
            var normalBias = shadowData != null ? shadowData.MainLightShadowNormalBias : 0f; // 读取最终 ShadowCaster 顶点 normal bias 倍率，缺失时使用 0。
            var sampleBias = shadowData != null ? shadowData.MainLightShadowSampleBias : 0f; // 读取最终 Receiver 采样偏移，缺失时使用 0。
            var shadowDistance = ResolveMainLightShadowDistance(asset, camera); // 读取本相机实际使用的阴影剔除距离，资产关闭阴影或相机 farClip 更近时会体现在日志里。
            BurtMainLightShadowCascadeCache cascadeCache = null;
            var cascadeCacheValid = shadowData != null && BurtMainLightShadowMatrixUtility.TryGetMainLightShadowCascadeCache(request, shadowData, out cascadeCache);
            var cascadeCount = cascadeCacheValid ? cascadeCache.CascadeCount : ResolveMainLightShadowCascadeCount(shadowData);
            var tileResolution = cascadeCacheValid ? cascadeCache.TileResolution : ResolveMainLightShadowTileResolution(shadowData);
            var atlasResolution = cascadeCacheValid ? cascadeCache.AtlasResolution : ResolveMainLightShadowAtlasResolution(shadowData);
            var cascadeSplit = shadowData != null ? shadowData.CreateMainLightShadowCascadeSplitVector() : new Vector3(1f, 0f, 0f);
            var cascadeBlendDistance = shadowData != null ? shadowData.MainLightShadowCascadeBlendDistance : 0f;
            var shadowFadeDistance = shadowData != null ? shadowData.MainLightShadowFadeDistance : 0f;
            var softSampling = shadowData != null && (shadowData.IsMainLightShadowSoft || (shadowData.IsMainLightShadowHard && shadowData.EnableMainLightShadowPCSS));
            var pcssEnabled = shadowData != null && shadowData.IsMainLightShadowHard && shadowData.EnableMainLightShadowPCSS;
            var pcssLightSize = shadowData != null ? shadowData.MainLightShadowPCSSLightSize : 0f;
            var pcssBlockerSearchRadius = shadowData != null ? shadowData.MainLightShadowPCSSBlockerSearchRadius : 0f;
            var pcssMaxFilterRadius = shadowData != null ? shadowData.MainLightShadowPCSSMaxFilterRadius : 0f;
            var cascadeSphereRadii = "<invalid>";
            if (cascadeCacheValid)
            {
                cascadeSphereRadii = $"({Mathf.Sqrt(Mathf.Max(0f, cascadeCache.CascadeSpheres[0].w)):0.###},{Mathf.Sqrt(Mathf.Max(0f, cascadeCache.CascadeSpheres[1].w)):0.###},{Mathf.Sqrt(Mathf.Max(0f, cascadeCache.CascadeSpheres[2].w)):0.###},{Mathf.Sqrt(Mathf.Max(0f, cascadeCache.CascadeSpheres[3].w)):0.###})";
            }
            Debug.Log($"[BurtRP][MainLightShadowDiagnostic] Camera=\"{cameraName}\" CameraType={cameraType} DebugMode={BurtShadingDebugSettings.Mode} VisibleLightCount={visibleLightCount} HasMainLight={hasMainLight} MainLightIndex={mainLightIndex} LightShadowType={shadowType} ShadowStrength={shadowStrength:0.###} ShadowDistance={shadowDistance:0.###} ShadowResolution={shadowResolution} CascadeCount={cascadeCount} TileResolution={tileResolution} AtlasResolution={atlasResolution} CascadeCacheValid={cascadeCacheValid} CascadeSphereRadii={cascadeSphereRadii} CascadeSplits=({cascadeSplit.x:0.###},{cascadeSplit.y:0.###},{cascadeSplit.z:0.###}) CascadeBlendDistance={cascadeBlendDistance:0.###} ShadowFadeDistance={shadowFadeDistance:0.###} DepthBias={depthBias:0.#####} NormalBias={normalBias:0.#####} SampleBias={sampleBias:0.#####} SoftSampling={softSampling} PCSS={pcssEnabled} PCSSLightSize={pcssLightSize:0.###} PCSSBlockerSearchRadius={pcssBlockerSearchRadius:0.###} PCSSMaxFilterRadius={pcssMaxFilterRadius:0.###} UseMainLightShadow={useMainLightShadow} MainLightShadowMapValid={mainLightShadowMapIsValid}"); // 输出单行 key=value 结构化日志，方便在 Console 里搜索、复制和对比每台相机的阴影状态。
        }
        public static int ResolveMainLightShadowCascadeCount(BurtShadowData shadowData)
        {
            if (shadowData == null)
            {
                return BurtShadowData.DefaultMainLightShadowCascadeCount;
            }
            return BurtShadowData.NormalizeMainLightShadowCascadeCount(shadowData.MainLightShadowCascadeCount);
        }
        public static int ResolveMainLightShadowTileResolution(BurtShadowData shadowData)
        {
            if (shadowData == null || shadowData.MainLightShadowResolution <= 0)
            {
                return BurtShadowData.DefaultMainLightShadowResolution;
            }
            var maxAtlasResolution = Mathf.Max(1, SystemInfo.maxTextureSize);
            var maxTileResolution = ResolveMainLightShadowCascadeCount(shadowData) > 1 ? Mathf.Max(1, maxAtlasResolution / 2) : maxAtlasResolution;
            return Mathf.Clamp(shadowData.MainLightShadowResolution, 1, maxTileResolution);
        }
        public static int ResolveMainLightShadowAtlasResolution(BurtShadowData shadowData)
        {
            var tileResolution = ResolveMainLightShadowTileResolution(shadowData);
            return ResolveMainLightShadowCascadeCount(shadowData) > 1 ? tileResolution * 2 : tileResolution;
        }
        public static Vector4 CreateMainLightShadowTexelSize(BurtShadowData shadowData) // 根据阴影分辨率生成 shader 常用的 texel size 向量。
        {
            if (shadowData == null || shadowData.MainLightShadowResolution <= 0) // 如果没有有效阴影分辨率，就返回安全零值。
            {
                return Vector4.zero; // 返回零向量，shader 可以据此避免使用无效 texel 偏移。
            }
            var resolution = ResolveMainLightShadowAtlasResolution(shadowData); // 保护分辨率，避免除以 0。
            var invResolution = 1f / resolution; // 计算一个 shadow texel 在 UV 空间里的大小。
            return new Vector4(invResolution, invResolution, resolution, resolution); // 返回 Unity 常用格式：(1/w, 1/h, w, h)。
        }
    }
}
