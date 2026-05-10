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
