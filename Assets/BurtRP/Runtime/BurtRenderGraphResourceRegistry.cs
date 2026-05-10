using System.Collections.Generic; // 引入泛型集合命名空间，用来使用 Dictionary 和 HashSet 保存资源表与外部导入标记。
using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Shader.PropertyToID 生成临时 RT 的整数 ID。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 RenderTargetIdentifier。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让资源注册表和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderGraphResourceRegistry // 定义 RenderGraph 资源注册表，用来集中保存当前图可访问的渲染资源。
    {
        public const string CameraColorName = "CameraColor"; // 定义 BurtRP 中间相机颜色目标的统一资源名，后续所有场景绘制都先写到这个临时颜色 RT。

        public const string IntermediateCameraColorName = CameraColorName; // 给中间颜色 RT 提供更直观的别名，方便后续代码表达“先渲染到中间目标”的语义。

        public const string FinalCameraTargetName = "FinalCameraTarget"; // 定义最终相机输出目标的统一资源名，用来保存 request.TargetIdentifier 指向的 backbuffer 或 targetTexture。

        public const string CameraColorTextureShaderName = "_BurtCameraColorTexture"; // 定义中间颜色 RT 暴露给 shader 的全局纹理名称，FinalBlit 会通过它采样相机颜色。

        public static readonly int CameraColorTextureId = Shader.PropertyToID(CameraColorTextureShaderName); // 把中间颜色 RT 的 shader 名称转换成整数 ID，保证申请、绑定、释放都使用同一个临时 RT。

        public const string CameraDepthName = "CameraDepth"; // 定义相机深度目标的统一资源名，后续 DepthPrepass、透明排序和后处理会依赖它。

        public const string CameraDepthTextureShaderName = "_BurtCameraDepthTexture"; // 定义真实相机深度 RT 的 shader 名称，后续 shader 采样深度时会使用它。

        public static readonly int CameraDepthTextureId = Shader.PropertyToID(CameraDepthTextureShaderName); // 把 shader 名称转换成整数 ID，CommandBuffer 使用整数 ID 会更稳定也更高效。

        public const string PostProcessColorName = "PostProcessColor"; // 定义后处理中间颜色目标的统一资源名，No-op Copy 和后续效果链会通过它做 ping-pong。

        public const string PostProcessColorTextureShaderName = "_BurtPostProcessColorTexture"; // 定义后处理中间颜色 RT 暴露给 shader 的全局纹理名称。

        public static readonly int PostProcessColorTextureId = Shader.PropertyToID(PostProcessColorTextureShaderName); // 把后处理中间颜色名称转换成整数 ID，申请、绑定和释放都会复用它。

        public const string MainLightShadowMapName = "MainLightShadowMap"; // 定义主光阴影图在 RenderGraph 里的统一资源名，后续阴影绘制和光照采样都通过它建立依赖。

        public const string MainLightShadowMapShaderName = "_BurtMainLightShadowMap"; // 定义主光阴影图暴露给 shader 的全局纹理名称，后续 Lit shader 会用这个名字采样阴影。

        public static readonly int MainLightShadowMapId = Shader.PropertyToID(MainLightShadowMapShaderName); // 把主光阴影图 shader 名称转换成整数 ID，让 CommandBuffer 申请、释放和绑定同一个临时 RT。

        private const string UnnamedRenderTargetName = "UnnamedRenderTarget"; // 定义空资源名的兜底名称，避免 Dictionary 接收 null 或空字符串。

        private readonly Dictionary<string, BurtRenderTargetHandle> renderTargets = new Dictionary<string, BurtRenderTargetHandle>(); // 创建渲染目标字典，用资源名映射到渲染目标句柄。

        private readonly HashSet<string> externalRenderTargets = new HashSet<string>(); // 记录由相机或外部系统提供的资源，Read-before-Write 校验会把它们视为已有生产者。

        public void Clear() // 定义清空函数，每次重新组装 RenderGraph 前调用。
        {
            renderTargets.Clear(); // 清空上一轮 request 注册的所有渲染目标，避免资源残留到下一轮渲染。

            externalRenderTargets.Clear(); // 清空外部导入标记，避免跨相机误判资源生产者。
        }

        public BurtRenderTargetHandle RegisterRenderTarget( // 定义注册渲染目标的函数，外部通过它把 RenderTargetIdentifier 放进资源表。
            string name, // 接收资源逻辑名称，例如 CameraColor 或 CameraDepth。
            RenderTargetIdentifier identifier) // 接收 Unity 实际渲染目标标识。
        {
            return RegisterRenderTarget(name, identifier, false); // 默认注册为图内资源，需要有 Pass 写入后才算生产完成。
        }

        public BurtRenderTargetHandle RegisterRenderTarget( // 定义带外部导入标记的注册函数，供 FinalCameraTarget 等外部资源使用。
            string name, // 接收资源逻辑名称，例如 FinalCameraTarget。
            RenderTargetIdentifier identifier, // 接收 Unity 实际渲染目标标识。
            bool isExternal) // 标记这个资源是否由 RenderGraph 外部已经提供。
        {
            var safeName = NormalizeResourceName(name); // 统一处理空资源名，保证资源表 key 可用且 Debug 输出稳定。

            var handle = new BurtRenderTargetHandle(safeName, identifier); // 把资源名和 Unity 渲染目标标识包装成 BurtRenderTargetHandle。

            renderTargets[safeName] = handle; // 把句柄写入资源表，如果同名资源已存在就覆盖旧值。

            if (isExternal) // 外部资源不需要图内生产者，例如相机最终输出目标。
            {
                externalRenderTargets.Add(safeName); // 记录外部导入资源名，供 Read-before-Write 校验使用。
            }
            else
            {
                externalRenderTargets.Remove(safeName); // 图内资源被重新注册时清理外部标记，避免校验误判。
            }

            return handle; // 返回刚注册好的资源句柄，方便调用方立刻使用。
        }

        public BurtRenderTargetHandle GetRenderTarget(string name) // 定义根据名称读取渲染目标句柄的函数。
        {
            var safeName = NormalizeResourceName(name); // 统一处理空名称，避免后续字典查询不稳定。

            if (renderTargets.TryGetValue(safeName, out var handle)) // 尝试从资源表里找到指定名称的渲染目标。
            {
                return handle; // 找到时返回资源表里保存的有效句柄。
            }

            return BurtRenderTargetHandle.Invalid(safeName); // 找不到时返回带资源名的无效句柄，方便调试缺失资源。
        }

        public bool ContainsRenderTarget(string name) // 判断某个资源名是否已经注册到当前资源表。
        {
            return renderTargets.ContainsKey(NormalizeResourceName(name)); // 使用同一套名称归一化逻辑，避免空名判断和 GetRenderTarget 分叉。
        }

        public bool IsExternalRenderTarget(string name) // 判断某个资源是否来自 RenderGraph 外部。
        {
            return externalRenderTargets.Contains(NormalizeResourceName(name)); // 外部资源可被读取而不需要图内写入生产者。
        }

        public BurtRenderTargetHandle RegisterCameraColor(RenderTargetIdentifier identifier) // 定义注册 CameraColor 的快捷函数。
        {
            return RegisterRenderTarget(CameraColorName, identifier); // 使用统一名称把相机颜色目标注册进资源表。
        }

        public BurtRenderTargetHandle GetCameraColor() // 定义读取 CameraColor 的快捷函数。
        {
            return GetRenderTarget(CameraColorName); // 使用统一名称从资源表读取相机颜色目标。
        }

        public BurtRenderTargetHandle RegisterCameraColorTexture() // 定义注册 BurtRP 自己创建的 CameraColor 临时颜色 RT 的快捷函数。
        {
            return RegisterCameraColor(new RenderTargetIdentifier(CameraColorTextureId)); // 使用统一 ID 创建 RenderTargetIdentifier，并把它注册为 CameraColor 中间目标。
        }

        public BurtRenderTargetHandle RegisterFinalCameraTarget(RenderTargetIdentifier identifier) // 定义注册最终相机输出目标的快捷函数。
        {
            return RegisterRenderTarget(FinalCameraTargetName, identifier, true); // 最终输出来自相机/backbuffer，校验时视为外部已存在资源。
        }

        public BurtRenderTargetHandle GetFinalCameraTarget() // 定义读取最终相机输出目标的快捷函数。
        {
            return GetRenderTarget(FinalCameraTargetName); // 使用统一名称从资源表读取 backbuffer 或相机 targetTexture。
        }

        public BurtRenderTargetHandle RegisterCameraDepthTexture() // 定义注册 BurtRP 自己创建的 CameraDepth 临时 RT 的快捷函数。
        {
            return RegisterCameraDepth(new RenderTargetIdentifier(CameraDepthTextureId)); // 使用统一 ID 创建 RenderTargetIdentifier，并把它注册为 CameraDepth。
        }

        public BurtRenderTargetHandle RegisterCameraDepth(RenderTargetIdentifier identifier) // 定义注册 CameraDepth 的快捷函数。
        {
            return RegisterRenderTarget(CameraDepthName, identifier); // 使用统一名称把相机深度目标注册进资源表。
        }

        public BurtRenderTargetHandle GetCameraDepth() // 定义读取 CameraDepth 的快捷函数。
        {
            return GetRenderTarget(CameraDepthName); // 使用统一名称从资源表读取相机深度目标。
        }

        public BurtRenderTargetHandle RegisterPostProcessColorTexture() // 定义注册 BurtRP 后处理中间颜色临时 RT 的快捷函数。
        {
            return RegisterPostProcessColor(new RenderTargetIdentifier(PostProcessColorTextureId)); // 使用统一 ID 创建 RenderTargetIdentifier，并把它注册为 PostProcessColor。
        }

        public BurtRenderTargetHandle RegisterPostProcessColor(RenderTargetIdentifier identifier) // 定义注册 PostProcessColor 的快捷函数。
        {
            return RegisterRenderTarget(PostProcessColorName, identifier); // 使用统一名称把后处理中间颜色目标注册进资源表。
        }

        public BurtRenderTargetHandle GetPostProcessColor() // 定义读取 PostProcessColor 的快捷函数。
        {
            return GetRenderTarget(PostProcessColorName); // 使用统一名称从资源表读取后处理中间颜色目标。
        }

        public BurtRenderTargetHandle RegisterMainLightShadowMapTexture() // 定义注册 BurtRP 主光阴影图临时 RT 的快捷函数。
        {
            return RegisterMainLightShadowMap(new RenderTargetIdentifier(MainLightShadowMapId)); // 使用统一 ID 创建 RenderTargetIdentifier，并把它注册为 MainLightShadowMap。
        }

        public BurtRenderTargetHandle RegisterMainLightShadowMap(RenderTargetIdentifier identifier) // 定义注册 MainLightShadowMap 的快捷函数。
        {
            return RegisterRenderTarget(MainLightShadowMapName, identifier); // 使用统一名称把主光阴影图目标注册进资源表。
        }

        public BurtRenderTargetHandle GetMainLightShadowMap() // 定义读取 MainLightShadowMap 的快捷函数。
        {
            return GetRenderTarget(MainLightShadowMapName); // 使用统一名称从资源表读取主光阴影图目标。
        }

        private static string NormalizeResourceName(string name) // 归一化资源名，避免 null 或空字符串破坏资源表和依赖校验。
        {
            return string.IsNullOrEmpty(name) ? UnnamedRenderTargetName : name; // 空名统一映射到兜底名称，Debug 中仍会看到异常资源名。
        }
    }
}
