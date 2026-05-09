using System.Collections.Generic; // 引入泛型集合命名空间，用来使用 Dictionary 保存资源表。
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

        public const string MainLightShadowMapName = "MainLightShadowMap"; // 定义主光阴影图在 RenderGraph 里的统一资源名，后续阴影绘制和光照采样都通过它建立依赖。

        public const string MainLightShadowMapShaderName = "_BurtMainLightShadowMap"; // 定义主光阴影图暴露给 shader 的全局纹理名称，后续 Lit shader 会用这个名字采样阴影。

        public static readonly int MainLightShadowMapId = Shader.PropertyToID(MainLightShadowMapShaderName); // 把主光阴影图 shader 名称转换成整数 ID，让 CommandBuffer 申请、释放和绑定同一个临时 RT。

        private readonly Dictionary<string, BurtRenderTargetHandle> renderTargets = new Dictionary<string, BurtRenderTargetHandle>(); // 创建渲染目标字典，用资源名映射到渲染目标句柄。

        public void Clear() // 定义清空函数，每次重新组装 RenderGraph 前调用。
        {
            renderTargets.Clear(); // 清空上一轮 request 注册的所有渲染目标，避免资源残留到下一轮渲染。
        }

        public BurtRenderTargetHandle RegisterRenderTarget( // 定义注册渲染目标的函数，外部通过它把 RenderTargetIdentifier 放进资源表。
            string name, // 接收资源逻辑名称，例如 CameraColor 或 CameraDepth。
            RenderTargetIdentifier identifier) // 接收 Unity 实际渲染目标标识。
        {
            if (string.IsNullOrEmpty(name)) // 如果传入名称为空，说明调用方没有给资源提供有效名字。
            {
                name = "UnnamedRenderTarget"; // 使用一个兜底名称，避免 Dictionary 使用 null 或空字符串。
            }

            var handle = new BurtRenderTargetHandle(name, identifier); // 把资源名和 Unity 渲染目标标识包装成 BurtRenderTargetHandle。

            renderTargets[name] = handle; // 把句柄写入资源表，如果同名资源已存在就覆盖旧值。

            return handle; // 返回刚注册好的资源句柄，方便调用方立刻使用。
        }

        public BurtRenderTargetHandle GetRenderTarget(string name) // 定义根据名称读取渲染目标句柄的函数。
        {
            if (string.IsNullOrEmpty(name)) // 如果传入名称为空，说明调用方请求的是无效资源。
            {
                return BurtRenderTargetHandle.Invalid("UnnamedRenderTarget"); // 返回无效句柄，避免调用方误用空名称资源。
            }

            if (renderTargets.TryGetValue(name, out var handle)) // 尝试从资源表里找到指定名称的渲染目标。
            {
                return handle; // 找到时返回资源表里保存的有效句柄。
            }

            return BurtRenderTargetHandle.Invalid(name); // 找不到时返回带资源名的无效句柄，方便调试缺失资源。
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
            return RegisterRenderTarget(FinalCameraTargetName, identifier); // 使用统一名称把 request.TargetIdentifier 保存为 FinalCameraTarget，避免它再被误当成 CameraColor。
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
    }
}
