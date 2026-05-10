using System; // 引入基础命名空间，用来给 Volume 参数类型添加 Serializable 特性。
using Sirenix.OdinInspector; // 引入 Odin Inspector 命名空间，用来让 Volume 组件在 Inspector 中也有清晰说明。
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
}
