using System; // 引入基础命名空间，用来给 Volume 参数类型添加 Serializable 特性。
using Sirenix.OdinInspector; // 引入 Odin Inspector 命名空间，用来让 Volume 组件在 Inspector 中也有清晰说明。
using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Color、Mathf 等基础类型。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来继承 VolumeComponent 和 VolumeParameter。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让 Volume 组件和后处理 Pass 使用同一套类型。
{
    [Serializable] // 标记这个参数类可以被 Unity 序列化，这样 Volume Profile 可以保存 Tonemapping 枚举值。
    public sealed class BurtTonemappingModeParameter : VolumeParameter<BurtTonemappingMode> // 定义 BurtTonemappingMode 的 Volume 参数包装类型。
    {
        public BurtTonemappingModeParameter( // 定义构造函数，让 Volume 系统可以创建带默认值和 override 状态的枚举参数。
            BurtTonemappingMode value, // 接收默认 Tonemapping 模式。
            bool overrideState = false) // 接收这个参数是否被当前 Volume 覆盖。
            : base(value, overrideState) // 把默认值和 override 状态传给 Unity VolumeParameter 基类。
        {
        }
    }

    [Serializable] // 标记这个类可以被 Unity 序列化，这样它可以作为 Volume Profile 里的 Override 组件保存。
    [VolumeComponentMenu("BurtRP/Post Processing/Tonemapping")] // 把这个组件注册到 Global Volume 的 Add Override 菜单下。
    public sealed class BurtTonemappingVolumeComponent : VolumeComponent // 定义 BurtRP Tonemapping 的 Global Volume 组件。
    {
        [Title("BurtRP Tonemapping")] // 使用 Odin 标题让组件在 Inspector 里更容易识别。
        [InfoBox("Tonemapping 参数放在 Global Volume 中；BurtRenderPipelineAsset 只负责是否启用后处理框架。")] // 提醒使用者效果参数不再放到管线资产上。
        public BurtTonemappingModeParameter mode = new BurtTonemappingModeParameter(BurtTonemappingMode.None); // 定义 Tonemapping 模式，None 表示关闭这个效果。

        public ClampedFloatParameter postExposure = new ClampedFloatParameter(0f, -8f, 8f); // 定义 Tonemapping 前的 EV 曝光补偿，0 表示不改变亮度。

        [Title("UE / XRender Filmic 参数")] // 用 Odin 标题把 UE/XRender Filmic 曲线参数和基础开关分开显示。
        [InfoBox("默认值对齐 XRender TonemappingComponent，也就是 UE Filmic Tonemapper 的常用 Film 参数。")] // 说明这组参数的来源，避免把它误认为 BurtRP 自定义曲线。
        public ClampedFloatParameter filmSlope = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultSlope, 0f, 1f); // 定义 Film Slope，数值越高，中间调对比越强。

        public ClampedFloatParameter filmToe = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultToe, 0f, 1f); // 定义 Film Toe，数值越高，暗部过渡越明显。

        public ClampedFloatParameter filmShoulder = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultShoulder, 0f, 1f); // 定义 Film Shoulder，数值越高，高光压缩越明显。

        public ClampedFloatParameter filmBlackClip = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultBlackClip, 0f, 1f); // 定义 Film Black Clip，控制黑位裁切。

        public ClampedFloatParameter filmWhiteClip = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultWhiteClip, 0f, 1f); // 定义 Film White Clip，控制白位裁切。

        [Title("XRender LUT 兼容参数")] // 用 Odin 标题把 XRender CombineLUT 中的 Tonemapping 兼容参数单独分组。
        public ClampedFloatParameter blueCorrection = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultBlueCorrection, 0f, 1f); // 定义 Blue Correction，模拟 XRender 在 FilmToneMap 前后的蓝色修正矩阵混合。

        public ClampedFloatParameter expandGamut = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultExpandGamut, 0f, 1f); // 定义 Expand Gamut，模拟 XRender 在 FilmToneMap 前对高饱和颜色做的 AP1 扩展。

        public ClampedFloatParameter toneCurveAmount = new ClampedFloatParameter(BurtTonemappingFilmSettings.DefaultToneCurveAmount, 0f, 1f); // 定义 Tone Curve Amount，0 表示不套曲线，1 表示完全使用 UE/XRender FilmToneMap。

        public bool IsEnabled() // 定义运行时判断函数，集中表达这个 Volume 组件是否真的需要执行。
        {
            if (!active) // 如果 Volume 组件自身被禁用，就不应该影响画面。
            {
                return false; // 返回 false，后处理 Pass 会把它视为关闭。
            }

            return mode.value != BurtTonemappingMode.None; // 只有模式不是 None 时才认为 Tonemapping 生效。
        }
    }

    [Serializable] // 标记这个类可以被 Unity 序列化，这样它可以保存到 Volume Profile 资产里。
    [VolumeComponentMenu("BurtRP/Post Processing/Color Adjustments")] // 把 Color Adjustments 组件注册到 Global Volume 的 Add Override 菜单里。
    public sealed class BurtColorAdjustmentsVolumeComponent : VolumeComponent // 定义 BurtRP Color Adjustments 的 Global Volume 组件。
    {
        private const float NeutralEpsilon = 0.0001f; // 定义中性值比较容差，避免浮点误差导致默认参数误判为启用。

        [Title("BurtRP Color Adjustments")] // 使用 Odin 标题让基础颜色调整参数在 Inspector 中更容易识别。
        [InfoBox("第一版直接合并进 Burt Post Process Pass，在 Tonemapping 之后执行；postExposure 继续沿用 Tonemapping 组件，避免重复曝光参数。")] // 说明当前实现范围，避免和 Tonemapping 的曝光参数冲突。
        public ClampedFloatParameter saturation = new ClampedFloatParameter(BurtColorAdjustmentsSettings.DefaultSaturation, 0f, 2f); // 定义饱和度参数，1 表示不改变饱和度。

        public ClampedFloatParameter contrast = new ClampedFloatParameter(BurtColorAdjustmentsSettings.DefaultContrast, 0f, 2f); // 定义对比度参数，1 表示不改变对比度。

        public ClampedFloatParameter gamma = new ClampedFloatParameter(BurtColorAdjustmentsSettings.DefaultGamma, 0.01f, 5f); // 定义 Gamma 参数，1 表示不改变明暗曲线。

        public ColorParameter colorFilter = new ColorParameter(BurtColorAdjustmentsSettings.DefaultColorFilter, false, false, true); // 定义颜色滤镜，默认白色表示不额外染色。

        public bool IsEnabled() // 定义运行时判断函数，集中表达这个 Volume 组件是否需要执行颜色调整。
        {
            if (!active) // 如果 Volume 组件整体被关闭，就不应该影响后处理链路。
            {
                return false; // 返回 false，后处理 Pass 会把它视为关闭。
            }

            return HasAnyOverride() || HasAnyNonNeutralValue(); // 参数被显式覆盖或参数值已经偏离中性值时，才认为 Color Adjustments 生效。
        }

        private bool HasAnyOverride() // 定义检查是否有参数被 Volume Profile 显式覆盖的辅助函数。
        {
            return saturation.overrideState || contrast.overrideState || gamma.overrideState || colorFilter.overrideState; // 只要任意参数勾选 override，就允许 Color Adjustments 运行。
        }

        private bool HasAnyNonNeutralValue() // 定义检查参数是否偏离中性值的辅助函数。
        {
            if (Mathf.Abs(saturation.value - BurtColorAdjustmentsSettings.DefaultSaturation) > NeutralEpsilon) // 如果饱和度不是 1，就说明调色会改变画面。
            {
                return true; // 返回 true，让后处理框架执行 Color Adjustments。
            }

            if (Mathf.Abs(contrast.value - BurtColorAdjustmentsSettings.DefaultContrast) > NeutralEpsilon) // 如果对比度不是 1，就说明调色会改变画面。
            {
                return true; // 返回 true，让后处理框架执行 Color Adjustments。
            }

            if (Mathf.Abs(gamma.value - BurtColorAdjustmentsSettings.DefaultGamma) > NeutralEpsilon) // 如果 Gamma 不是 1，就说明调色会改变画面。
            {
                return true; // 返回 true，让后处理框架执行 Color Adjustments。
            }

            var filter = colorFilter.value; // 读取颜色滤镜值，方便逐通道和白色默认值比较。

            if (Mathf.Abs(filter.r - BurtColorAdjustmentsSettings.DefaultColorFilter.r) > NeutralEpsilon) // 如果红色通道不是默认白色，就说明会产生染色。
            {
                return true; // 返回 true，让后处理框架执行 Color Adjustments。
            }

            if (Mathf.Abs(filter.g - BurtColorAdjustmentsSettings.DefaultColorFilter.g) > NeutralEpsilon) // 如果绿色通道不是默认白色，就说明会产生染色。
            {
                return true; // 返回 true，让后处理框架执行 Color Adjustments。
            }

            if (Mathf.Abs(filter.b - BurtColorAdjustmentsSettings.DefaultColorFilter.b) > NeutralEpsilon) // 如果蓝色通道不是默认白色，就说明会产生染色。
            {
                return true; // 返回 true，让后处理框架执行 Color Adjustments。
            }

            return false; // 所有参数都保持中性时，不额外启用 Color Adjustments。
        }
    }

    [Serializable] // 标记这个类可以被 Unity 序列化，这样它可以保存到 Volume Profile 资产里。
    [VolumeComponentMenu("BurtRP/Post Processing/Bloom")] // 把 Bloom 组件注册到 Global Volume 的 Add Override 菜单里。
    public sealed class BurtBloomVolumeComponent : VolumeComponent // 定义 BurtRP Bloom 的 Global Volume 组件。
    {
        private const float IntensityEpsilon = 0.0001f; // 定义强度阈值，避免默认或近零强度误触发 Bloom。

        [Title("BurtRP Bloom")] // 使用 Odin 标题让 Bloom 参数在 Inspector 中更容易识别。
        [InfoBox("第一版在 Burt Post Process Pass 内部申请临时 mip 链，并在 Tonemapping 前把 Bloom 合回 HDR CameraColor。")] // 说明当前实现范围。
        public ClampedFloatParameter threshold = new ClampedFloatParameter(BurtBloomSettings.DefaultThreshold, 0f, 10f); // 定义亮度阈值，超过阈值的 HDR 高光会进入 Bloom。

        public ClampedFloatParameter softKnee = new ClampedFloatParameter(BurtBloomSettings.DefaultSoftKnee, 0f, 1f); // 定义软阈值范围，让高光进入 Bloom 时更平滑。

        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 10f); // 定义 Bloom 合成强度，默认 0 保证不改变画面。

        public ClampedFloatParameter scatter = new ClampedFloatParameter(BurtBloomSettings.DefaultScatter, 0f, 1f); // 定义上采样叠加强度，数值越高光晕越扩散。

        public ClampedIntParameter maxIterations = new ClampedIntParameter(BurtBloomSettings.DefaultMaxMipCount, 1, 8); // 定义最多使用的 mip 数，第一版限制到 8 级。

        public bool IsEnabled() // 定义运行时判断函数，集中表达这个 Volume 组件是否需要执行 Bloom。
        {
            if (!active) // 如果 Volume 组件整体被关闭，就不应该影响后处理链路。
            {
                return false; // 返回 false，后处理 Pass 会把它视为关闭。
            }

            return intensity.value > IntensityEpsilon; // 只有强度大于阈值时才启用，默认 Volume 不改变画面。
        }
    }
}
