using System; // 引入基础命名空间，用来给设置类添加 Serializable 特性。
using Sirenix.OdinInspector; // 引入 Odin Inspector 命名空间，用来给后处理设置提供更清晰的 Inspector 分组。
using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 SerializeField 等 Unity 序列化能力。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让后处理设置可以被管线资产和 Pass 直接访问。
{
    public enum BurtTonemappingMode // 定义 BurtRP 第一版 Tonemapping 模式，后续可以继续追加 Filmic、自定义曲线等模式。
    {
        None = 0, // 不执行 Tonemapping，只做 No-op Copy 或完全跳过后处理。
        Neutral = 1, // 使用简单中性压缩曲线，适合先验证 HDR 到 LDR 的基础链路。
        ACES = 2 // 使用常见 ACES 近似曲线，适合作为第一版默认电影感色调映射候选。
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
