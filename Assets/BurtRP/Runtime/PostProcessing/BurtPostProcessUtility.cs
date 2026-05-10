using UnityEngine; // 引入 UnityEngine 命名空间，用来访问 Camera、LayerMask、Mathf 和 Debug。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来访问 VolumeManager 和 VolumeStack。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让后处理工具可以被 RenderGraph 和 Pass 共享。
{
    internal static class BurtPostProcessUtility // 定义后处理工具类，用来集中判断后处理框架是否应该运行。
    {
        public static bool ShouldUsePostProcessFramework( // 定义判断当前 request 是否需要后处理框架的统一入口。
            BurtRenderRequest request, // 接收当前渲染请求，用来确认相机任务是否有效。
            BurtRenderPipelineAsset asset) // 接收管线资产，用来读取后处理框架开关和 Volume 查询配置。
        {
            if (request == null) // 如果 request 为空，说明没有合法渲染任务。
            {
                return false; // 返回 false，避免为异常任务注册后处理资源或插入 Pass。
            }

            if (!request.IsValid) // 如果 request 已经被标记为无效，说明它不应该进入渲染图。
            {
                return false; // 返回 false，保持后处理和主渲染任务的有效性判断一致。
            }

            if (request.Camera == null) // 如果 request 没有关联 Camera，全屏后处理无法确定目标尺寸。
            {
                return false; // 返回 false，避免创建尺寸不明确的 PostProcessColor RT。
            }

            if (IsPreviewOrReflectionRequest(request)) // Unity Inspector/Asset Preview 和 ReflectionProbe 捕获不应该被项目里的 Volume Tonemapping 或调色影响。
            {
                return false; // 返回 false，避免 Cubemap/ReflectionProbe 等辅助渲染被后处理链改变颜色或曝光。
            }

            if (asset == null) // 如果管线资产为空，说明当前没有 Inspector 配置来源。
            {
                return false; // 返回 false，默认不启用后处理，避免异常配置改变画面。
            }

            var settings = asset.PostProcessSettings; // 从管线资产读取后处理框架设置，资产内部会处理旧数据为空的兜底情况。

            if (settings == null) // 如果设置对象仍然为空，说明资产处于异常状态。
            {
                return false; // 返回 false，避免空设置导致后续访问失败。
            }

            if (!settings.EnablePostProcessing) // 如果资产关闭了后处理框架，就算 Volume 里有 Tonemapping 也不执行。
            {
                return false; // 返回 false，保持 Asset 作为后处理总开关。
            }

            return settings.ShouldRunNoOpCopy || HasActiveTonemappingVolume() || HasActiveColorAdjustmentsVolume(); // No-op、Tonemapping 或 Color Adjustments 任意一个需要执行，就注册资源并插入 Pass。
        }

        public static bool ShouldUseColorAdjustments( // 定义判断当前 request 是否需要 Color Adjustments 的统一入口。
            BurtRenderRequest request, // 接收当前渲染请求，用来确认相机任务是否有效。
            BurtRenderPipelineAsset asset) // 接收管线资产，用来确认后处理总开关是否打开。
        {
            if (request == null) // 如果 request 为空，说明没有合法渲染任务。
            {
                return false; // 返回 false，避免异常任务执行颜色调整。
            }

            if (!request.IsValid) // 如果 request 无效，就不应该执行任何后处理子链路。
            {
                return false; // 返回 false，保持后处理子效果和主渲染任务一致。
            }

            if (request.Camera == null) // 如果相机为空，就没有可靠的渲染上下文可以驱动 Volume。
            {
                return false; // 返回 false，避免在异常相机任务里执行颜色调整。
            }

            if (IsPreviewOrReflectionRequest(request)) // Preview / Reflection 不使用项目 Volume 调色，避免资产预览颜色被场景后处理污染。
            {
                return false; // 返回 false，让后处理 Pass 不上传 Color Adjustments。
            }

            if (!IsPostProcessEnabled(asset)) // 如果管线资产没有开启后处理框架，Volume 调色不允许改变画面。
            {
                return false; // 返回 false，让后处理 Pass 不上传颜色调整参数。
            }

            return HasActiveColorAdjustmentsVolume(); // 只有当前 VolumeStack 中存在有效 Color Adjustments 时，才需要执行颜色调整。
        }

        public static void UpdateVolumeStack( // 定义每个相机渲染前刷新 VolumeStack 的函数，保证后处理参数来自当前相机位置。
            BurtRenderRequest request, // 接收当前 request，用来读取相机 Transform。
            BurtRenderPipelineAsset asset) // 接收管线资产，用来读取 Volume 查询 LayerMask。
        {
            if (request == null) // 如果 request 为空，说明没有合法相机可以驱动 Volume 查询。
            {
                return; // 直接返回，避免访问空 request。
            }

            if (!request.IsValid) // 如果 request 无效，说明它不会进入正常渲染流程。
            {
                return; // 直接返回，避免无效任务刷新全局 VolumeStack。
            }

            var camera = request.Camera; // 从 request 里读取当前相机。

            if (camera == null) // 如果相机为空，就没有 Transform 可以参与本地 Volume 混合。
            {
                return; // 直接返回，后续解析会使用 VolumeStack 当前值或默认值。
            }

            if (IsPreviewOrReflectionRequest(request)) // Preview / Reflection 相机来自 Unity 辅助渲染，不应该刷新或继承场景 Volume。
            {
                return; // 直接返回，避免资产预览或 ReflectionProbe 捕获被场景 Tonemapping/Color Adjustments 影响。
            }

            if (asset == null) // 如果资产为空，就没有后处理 Volume LayerMask 配置来源。
            {
                return; // 直接返回，保持异常路径不改变全局 Volume 状态。
            }

            var settings = asset.PostProcessSettings; // 读取后处理框架设置，用来判断是否需要刷新 Volume。

            if (settings == null) // 如果后处理设置缺失，说明资产还没有完成兜底初始化。
            {
                return; // 直接返回，避免空设置导致刷新逻辑异常。
            }

            if (!settings.EnablePostProcessing) // 如果后处理框架关闭，就不需要为 BurtRP 更新后处理 Volume。
            {
                return; // 直接返回，减少不必要的 Volume 查询成本。
            }

            VolumeManager.instance.Update(camera.transform, asset.PostProcessVolumeLayerMask); // 按当前相机位置和资产 LayerMask 刷新 Unity VolumeStack。
        }

        public static bool ShouldLogPostProcessDebug(BurtRenderPipelineAsset asset) // 定义判断是否输出后处理调试日志的统一入口。
        {
            if (asset == null) // 如果没有资产配置，就没有日志开关来源。
            {
                return false; // 返回 false，避免异常路径刷日志。
            }

            var settings = asset.PostProcessSettings; // 读取后处理设置，便于检查调试日志开关。

            return settings != null && settings.EnableFrameworkDebugLog; // 只有设置存在并且显式打开日志时才允许 Pass 打印信息。
        }

        public static BurtTonemappingMode ResolveTonemappingMode(BurtRenderPipelineAsset asset) // 定义安全解析 Tonemapping 模式的函数，避免 Pass 直接处理 VolumeStack 细节。
        {
            if (!IsPostProcessEnabled(asset)) // 如果管线资产没有打开后处理框架，就不允许 Volume Tonemapping 改变画面。
            {
                return BurtTonemappingMode.None; // 返回 None，shader 会走原样输出。
            }

            var tonemapping = GetTonemappingVolumeComponent(); // 从当前 VolumeStack 读取 BurtRP Tonemapping 组件。

            if (tonemapping == null) // 如果当前 VolumeStack 没有 Tonemapping 组件，就没有正式 Tonemapping 效果。
            {
                return BurtTonemappingMode.None; // 返回 None，保持 No-op 或无后处理状态。
            }

            if (!tonemapping.IsEnabled()) // 如果组件未激活或模式为 None，就不执行 Tonemapping。
            {
                return BurtTonemappingMode.None; // 返回 None，避免 Volume 默认值改变画面。
            }

            return tonemapping.mode.value; // 返回当前 Volume 混合后的 Tonemapping 模式。
        }

        public static float ResolvePostExposureMultiplier(BurtRenderPipelineAsset asset) // 定义把 Volume EV 曝光转换为线性倍率的函数。
        {
            if (!IsPostProcessEnabled(asset)) // 如果后处理框架关闭，曝光参数不应该影响画面。
            {
                return 1f; // 返回 1，表示不改变颜色亮度。
            }

            var tonemapping = GetTonemappingVolumeComponent(); // 从当前 VolumeStack 读取 BurtRP Tonemapping 组件。

            if (tonemapping == null) // 如果没有 Tonemapping 组件，就没有曝光补偿来源。
            {
                return 1f; // 返回 1，保证 No-op Copy 不改变画面。
            }

            if (!tonemapping.IsEnabled()) // 如果 Tonemapping 未启用，曝光参数不应该参与 No-op Copy。
            {
                return 1f; // 返回 1，保证关闭 Tonemapping 时画面不变。
            }

            return Mathf.Pow(2f, tonemapping.postExposure.value); // 把 Volume 里的 EV 曝光转成线性倍率，+1 EV 等于乘以 2。
        }

        public static BurtTonemappingFilmSettings ResolveTonemappingFilmSettings(BurtRenderPipelineAsset asset) // 定义解析 UE/XRender Filmic 参数的函数，让 Pass 不直接访问 Volume 组件字段。
        {
            if (!IsPostProcessEnabled(asset)) // 如果后处理框架关闭，Film 参数不应该影响任何全屏拷贝。
            {
                return BurtTonemappingFilmSettings.Default; // 返回默认参数，保证 shader 即使被调用也处于稳定状态。
            }

            var tonemapping = GetTonemappingVolumeComponent(); // 从当前 VolumeStack 读取 BurtRP Tonemapping 组件。

            if (tonemapping == null) // 如果当前 VolumeStack 没有 Tonemapping 组件，就没有可覆盖的 Film 参数。
            {
                return BurtTonemappingFilmSettings.Default; // 返回默认参数，对齐 XRender/UE 的基础外观。
            }

            if (!tonemapping.IsEnabled()) // 如果 Tonemapping 组件未启用，Film 参数不应该参与 No-op Copy。
            {
                return BurtTonemappingFilmSettings.Default; // 返回默认参数，避免关闭模式下上传无意义的自定义值。
            }

            return new BurtTonemappingFilmSettings( // 把当前 Volume 混合后的参数收拢成不可变设置，供 Pass 一次性上传给 shader。
                tonemapping.filmSlope.value, // 读取 Film Slope。
                tonemapping.filmToe.value, // 读取 Film Toe。
                tonemapping.filmShoulder.value, // 读取 Film Shoulder。
                tonemapping.filmBlackClip.value, // 读取 Film Black Clip。
                tonemapping.filmWhiteClip.value, // 读取 Film White Clip。
                tonemapping.blueCorrection.value, // 读取 Blue Correction。
                tonemapping.expandGamut.value, // 读取 Expand Gamut。
                tonemapping.toneCurveAmount.value); // 读取 Tone Curve Amount。
        }

        public static BurtColorAdjustmentsSettings ResolveColorAdjustmentsSettings(BurtRenderPipelineAsset asset) // 定义解析 Color Adjustments 参数的函数，让 Pass 不直接访问 Volume 组件字段。
        {
            if (!IsPostProcessEnabled(asset)) // 如果后处理框架关闭，调色参数不应该影响任何全屏拷贝。
            {
                return BurtColorAdjustmentsSettings.Default; // 返回默认调色参数，保证 shader 处于稳定的中性状态。
            }

            var colorAdjustments = GetColorAdjustmentsVolumeComponent(); // 从当前 VolumeStack 读取 BurtRP Color Adjustments 组件。

            if (colorAdjustments == null) // 如果当前 VolumeStack 没有 Color Adjustments 组件，就没有可覆盖的调色参数。
            {
                return BurtColorAdjustmentsSettings.Default; // 返回默认调色参数，让后处理保持中性。
            }

            if (!colorAdjustments.IsEnabled()) // 如果 Color Adjustments 组件没有真正启用，就不应该上传非中性调色参数。
            {
                return BurtColorAdjustmentsSettings.Default; // 返回默认调色参数，避免 Volume 默认值改变画面。
            }

            return new BurtColorAdjustmentsSettings( // 把当前 Volume 混合后的参数收拢成不可变设置，供后处理 Pass 一次性上传。
                colorAdjustments.saturation.value, // 读取饱和度。
                colorAdjustments.contrast.value, // 读取对比度。
                colorAdjustments.gamma.value, // 读取 Gamma。
                colorAdjustments.colorFilter.value); // 读取颜色滤镜。
        }

        public static void LogPostProcessExecuted( // 定义后处理执行日志，集中格式避免 Pass 内部堆字符串逻辑。
            BurtRenderGraphContext context, // 接收当前 RenderGraph 执行上下文，用来读取相机和资产设置。
            BurtTonemappingMode tonemappingMode, // 接收本次执行使用的 Tonemapping 模式。
            float postExposureMultiplier, // 接收本次执行使用的线性曝光倍率。
            bool useColorAdjustments) // 接收本次执行是否启用了 Color Adjustments。
        {
            if (context == null) // 如果上下文为空，说明调用方状态异常。
            {
                return; // 直接返回，避免日志函数自己触发空引用。
            }

            if (!ShouldLogPostProcessDebug(context.Asset)) // 如果用户没有打开后处理调试日志，就不输出任何内容。
            {
                return; // 直接返回，保持默认运行时不刷 Console。
            }

            var request = context.Request; // 读取当前渲染请求，用来输出相机名称。

            var camera = request != null ? request.Camera : null; // 读取 request 里的相机，request 为空时保持 camera 为空。

            var cameraName = camera != null ? camera.name : "<null>"; // 把相机名转换成安全字符串，避免日志里出现空引用。

            Debug.Log("[BurtRP][PostProcess] Executed. Camera=" + cameraName + " Tonemapping=" + tonemappingMode + " ExposureMul=" + postExposureMultiplier + " ColorAdjustments=" + useColorAdjustments); // 输出后处理执行摘要，说明当前模式、曝光倍率和颜色调整状态。
        }

        private static bool IsPostProcessEnabled(BurtRenderPipelineAsset asset) // 定义判断资产是否允许后处理运行的统一辅助函数。
        {
            if (asset == null) // 如果资产为空，说明没有后处理总开关来源。
            {
                return false; // 返回 false，避免异常路径改变画面。
            }

            var settings = asset.PostProcessSettings; // 读取资产上的后处理框架设置。

            return settings != null && settings.EnablePostProcessing; // 只有设置存在且总开关打开时，Volume 效果才允许生效。
        }

        private static bool IsPreviewOrReflectionRequest(BurtRenderRequest request) // 判断当前 request 是否来自 Unity 编辑器预览或 ReflectionProbe 捕获。
        {
            return request != null && (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection); // 这些 request 都不应继承场景后处理。
        }

        private static bool HasActiveTonemappingVolume() // 定义判断当前 VolumeStack 是否存在有效 Tonemapping 的辅助函数。
        {
            var tonemapping = GetTonemappingVolumeComponent(); // 从当前 VolumeStack 中读取 BurtRP Tonemapping 组件。

            return tonemapping != null && tonemapping.IsEnabled(); // 只有组件存在、激活且模式不是 None 时，才认为 Tonemapping 需要运行。
        }

        private static bool HasActiveColorAdjustmentsVolume() // 定义判断当前 VolumeStack 是否存在有效 Color Adjustments 的辅助函数。
        {
            var colorAdjustments = GetColorAdjustmentsVolumeComponent(); // 从当前 VolumeStack 中读取 BurtRP Color Adjustments 组件。

            return colorAdjustments != null && colorAdjustments.IsEnabled(); // 只有组件存在、激活且参数被覆盖或偏离中性值时，才认为颜色调整需要运行。
        }

        private static BurtTonemappingVolumeComponent GetTonemappingVolumeComponent() // 定义从 Unity VolumeStack 读取 BurtRP Tonemapping 组件的辅助函数。
        {
            var volumeManager = VolumeManager.instance; // 取得 Unity 当前全局 VolumeManager 实例。

            if (volumeManager == null) // 理论上 VolumeManager 是单例，但这里仍然做保护，避免异常域重载阶段出错。
            {
                return null; // 返回空组件，调用方会按无 Tonemapping 处理。
            }

            var stack = volumeManager.stack; // 读取当前已经由相机刷新过的 VolumeStack。

            if (stack == null) // 如果 VolumeStack 为空，说明 Volume 系统还没有准备好。
            {
                return null; // 返回空组件，保证后处理回退到 No-op 或关闭状态。
            }

            return stack.GetComponent<BurtTonemappingVolumeComponent>(); // 返回 BurtRP Tonemapping 组件，未添加时 Unity 会返回默认组件或空值。
        }

        private static BurtColorAdjustmentsVolumeComponent GetColorAdjustmentsVolumeComponent() // 定义从 Unity VolumeStack 读取 BurtRP Color Adjustments 组件的辅助函数。
        {
            var volumeManager = VolumeManager.instance; // 取得 Unity 当前全局 VolumeManager 实例。

            if (volumeManager == null) // 理论上 VolumeManager 是单例，但域重载阶段仍可能为空。
            {
                return null; // 返回空组件，调用方会按无 Color Adjustments 处理。
            }

            var stack = volumeManager.stack; // 读取当前已经由相机刷新过的 VolumeStack。

            if (stack == null) // 如果 VolumeStack 为空，说明 Volume 系统还没有准备好。
            {
                return null; // 返回空组件，保证后处理回退到无颜色调整状态。
            }

            return stack.GetComponent<BurtColorAdjustmentsVolumeComponent>(); // 返回 BurtRP Color Adjustments 组件，未添加时 Unity 会返回默认组件或空值。
        }
    }
}
