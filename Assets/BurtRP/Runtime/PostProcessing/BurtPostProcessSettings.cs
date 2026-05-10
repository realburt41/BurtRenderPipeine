using System; // 引入基础命名空间，用来给设置类添加 Serializable 特性。
using Sirenix.OdinInspector; // 引入 Odin Inspector 命名空间，用来给后处理设置提供更清晰的 Inspector 分组。
using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 SerializeField 等 Unity 序列化能力。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让后处理设置可以被管线资产和 Pass 直接访问。
{
    public enum BurtTonemappingMode // 定义 BurtRP 的 Tonemapping 模式，数值会被 Volume Profile 序列化，所以新增模式时要保持旧数值稳定。
    {
        None = 0, // 不执行 Tonemapping，只做 No-op Copy 或完全跳过后处理。
        Neutral = 1, // 使用简单中性压缩曲线，适合先验证 HDR 到 LDR 的基础链路。
        [InspectorName("XRender / UE Filmic (ACES)")] ACES = 2 // 使用 XRender 当前对齐 UE Filmic/ACES 的曲线，名称保留 ACES 是为了不破坏旧 Volume 序列化值。
    }

    internal readonly struct BurtTonemappingFilmSettings // 定义一组 UE/XRender Filmic Tonemapping 参数，方便 C# 和 Shader 用同一套默认值。
    {
        public const float DefaultSlope = 0.88f; // 定义默认 Film Slope，对齐 XRender TonemappingComponent 和 UE Film 默认值。
        public const float DefaultToe = 0.55f; // 定义默认 Film Toe，对齐 XRender TonemappingComponent 和 UE Film 默认值。
        public const float DefaultShoulder = 0.26f; // 定义默认 Film Shoulder，对齐 XRender TonemappingComponent 和 UE Film 默认值。
        public const float DefaultBlackClip = 0.0f; // 定义默认 Film Black Clip，对齐 XRender TonemappingComponent 和 UE Film 默认值。
        public const float DefaultWhiteClip = 0.04f; // 定义默认 Film White Clip，对齐 XRender TonemappingComponent 和 UE Film 默认值。
        public const float DefaultBlueCorrection = 0.6f; // 定义默认 Blue Correction，对齐 XRender ColorGradingComponent 中参与 Tonemapping LUT 的默认值。
        public const float DefaultExpandGamut = 1.0f; // 定义默认 Expand Gamut，对齐 XRender ColorGradingComponent 中参与 Tonemapping LUT 的默认值。
        public const float DefaultToneCurveAmount = 1.0f; // 定义默认 Tone Curve Amount，对齐 XRender ColorGradingComponent 中 Tonemapping 曲线全量生效的默认值。
        public static readonly BurtTonemappingFilmSettings Default = new BurtTonemappingFilmSettings(DefaultSlope, DefaultToe, DefaultShoulder, DefaultBlackClip, DefaultWhiteClip, DefaultBlueCorrection, DefaultExpandGamut, DefaultToneCurveAmount); // 提供默认参数集合，供 Volume 缺失或关闭时安全回退。

        public float Slope { get; } // 保存 Film Slope，控制 Tonemapping 中间直线段的斜率。
        public float Toe { get; } // 保存 Film Toe，控制暗部压缩和黑位过渡。
        public float Shoulder { get; } // 保存 Film Shoulder，控制高光肩部压缩。
        public float BlackClip { get; } // 保存 Film Black Clip，控制暗部裁切位置。
        public float WhiteClip { get; } // 保存 Film White Clip，控制高光白位裁切位置。
        public float BlueCorrection { get; } // 保存 Blue Correction，控制 UE/XRender 的蓝色修正矩阵混合强度。
        public float ExpandGamut { get; } // 保存 Expand Gamut，控制 XRender 在 AP1 空间扩展高饱和颜色的强度。
        public float ToneCurveAmount { get; } // 保存 Tone Curve Amount，控制原始 AP1 颜色和 FilmToneMap 结果的混合比例。

        public BurtTonemappingFilmSettings( // 定义完整参数构造函数，避免调用方分散维护默认值。
            float slope, // 接收 Film Slope。
            float toe, // 接收 Film Toe。
            float shoulder, // 接收 Film Shoulder。
            float blackClip, // 接收 Film Black Clip。
            float whiteClip, // 接收 Film White Clip。
            float blueCorrection, // 接收 Blue Correction。
            float expandGamut, // 接收 Expand Gamut。
            float toneCurveAmount) // 接收 Tone Curve Amount。
        {
            Slope = slope; // 保存 Film Slope。
            Toe = toe; // 保存 Film Toe。
            Shoulder = shoulder; // 保存 Film Shoulder。
            BlackClip = blackClip; // 保存 Film Black Clip。
            WhiteClip = whiteClip; // 保存 Film White Clip。
            BlueCorrection = blueCorrection; // 保存 Blue Correction。
            ExpandGamut = expandGamut; // 保存 Expand Gamut。
            ToneCurveAmount = toneCurveAmount; // 保存 Tone Curve Amount。
        }
    }

    internal readonly struct BurtColorAdjustmentsSettings // 定义 Color Adjustments 的运行时参数包，避免 Pass 直接依赖 Volume 组件字段。
    {
        public const float DefaultSaturation = 1f; // 定义默认饱和度，1 表示不改变颜色离灰轴的距离。
        public const float DefaultContrast = 1f; // 定义默认对比度，1 表示不改变中间调附近的对比关系。
        public const float DefaultGamma = 1f; // 定义默认 Gamma，1 表示不做明暗伽马调整。
        public static readonly Color DefaultColorFilter = Color.white; // 定义默认颜色滤镜，白色表示不额外染色。
        public static readonly BurtColorAdjustmentsSettings Default = new BurtColorAdjustmentsSettings(DefaultSaturation, DefaultContrast, DefaultGamma, DefaultColorFilter); // 提供默认参数集合，供 Volume 缺失或关闭时回退。

        public float Saturation { get; } // 保存饱和度参数，后处理 shader 会用它控制颜色鲜艳程度。
        public float Contrast { get; } // 保存对比度参数，后处理 shader 会用它控制明暗差异。
        public float Gamma { get; } // 保存 Gamma 参数，后处理 shader 会用它控制整体明暗曲线。
        public Color ColorFilter { get; } // 保存颜色滤镜参数，后处理 shader 会用它做通道乘法染色。

        public BurtColorAdjustmentsSettings( // 定义完整参数构造函数，让调用方一次性生成不可变设置。
            float saturation, // 接收饱和度。
            float contrast, // 接收对比度。
            float gamma, // 接收 Gamma。
            Color colorFilter) // 接收颜色滤镜。
        {
            Saturation = saturation; // 保存饱和度。
            Contrast = contrast; // 保存对比度。
            Gamma = gamma; // 保存 Gamma。
            ColorFilter = colorFilter; // 保存颜色滤镜。
        }
    }

    [Serializable] // 标记这个类可以被 Unity 序列化，这样它可以作为 BurtRenderPipelineAsset 的内嵌配置显示在 Inspector 中。
    public sealed class BurtPostProcessSettings // 定义 BurtRP 后处理框架设置；具体效果参数后续统一放到 Global Volume。
    {
        public static readonly BurtPostProcessSettings Disabled = new BurtPostProcessSettings(false); // 提供关闭配置，避免旧资产字段为空时访问设置出现空引用。

        [TitleGroup("后处理框架", "用于控制 DrawTransparent 之后到 FinalBlit 之前的全屏后处理链路。")] // 使用 Odin 标题组把后处理框架配置集中显示。
        [InfoBox("Asset 只保留后处理框架级开关；Tonemapping、Bloom 等效果参数后续统一从 Global Volume 读取。")] // 用 Odin 信息框说明效果参数不再继续放在 Asset 上。
        [SerializeField, ToggleLeft, LabelText("启用后处理框架")] private bool enablePostProcessing; // 控制 BurtRP 是否在 Forward 图中插入后处理框架 Pass。

        [TitleGroup("后处理框架")] // 继续放在同一个 Odin 分组里，让框架相关开关保持在一起。
        [SerializeField, ToggleLeft, LabelText("启用 No-op Copy 验证 Pass"), ShowIf(nameof(enablePostProcessing))] private bool enableNoOpCopy = true; // 控制没有正式效果时是否仍执行 CameraColor 到 PostProcessColor 再回写 CameraColor 的验证拷贝。

        [TitleGroup("后处理框架 - 调试")] // 单独建立调试分组，避免普通使用时把日志开关和核心开关混在一起。
        [SerializeField, ToggleLeft, LabelText("输出后处理框架日志"), ShowIf(nameof(enablePostProcessing))] private bool enableFrameworkDebugLog; // 控制是否输出后处理 Pass 执行日志，默认关闭以避免每帧刷 Console。

        public bool EnablePostProcessing => enablePostProcessing; // 暴露后处理总开关给 RenderGraph 资源注册和 ForwardGraph 组装逻辑读取。

        public bool EnableNoOpCopy => enableNoOpCopy; // 暴露 No-op Copy 开关，让第一版框架可以单独关闭验证 Pass。

        public bool EnableFrameworkDebugLog => enableFrameworkDebugLog; // 暴露日志开关，Pass 执行时会先检查它再打印诊断信息。

        public bool ShouldRunNoOpCopy => enablePostProcessing && enableNoOpCopy; // Asset 只决定是否允许无效果验证链路，正式效果是否运行由 Volume 决定。

        public BurtPostProcessSettings() // 定义 Unity 序列化需要的默认构造函数。
        {
        }

        private BurtPostProcessSettings(bool defaultEnabled) // 定义内部构造函数，只给 Disabled 兜底配置使用。
        {
            enablePostProcessing = defaultEnabled; // 按传入默认值初始化总开关，Disabled 会传入 false。
        }
    }
}
