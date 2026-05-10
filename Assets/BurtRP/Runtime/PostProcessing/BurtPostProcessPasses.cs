using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Material、Shader、Matrix4x4 和 MeshTopology。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 CommandBufferPool 和 RenderTarget 相关 API。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让后处理 Pass 可以直接接入现有 RenderGraph。
{
    internal sealed class BurtAllocatePostProcessColorPass : BurtRenderPass // 定义后处理中间颜色分配 Pass，负责申请 PostProcessColor 临时 RT。
    {
        public override string Name => "Burt Allocate Post Process Color"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtPostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset)) // 如果当前 request 没有启用后处理框架，就不声明资源写入。
            {
                return; // 直接结束配置，保持未启用时的 RenderGraph 干净。
            }

            builder.WritePostProcessColor(); // 声明这个 Pass 会创建并写入 PostProcessColor 资源。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行后处理中间颜色 RT 的申请。
        {
            if (!BurtPostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset)) // 执行阶段再次判断，防止配置和执行之间状态变化。
            {
                return; // 未启用时直接跳过，不申请任何临时 RT。
            }

            var renderContext = context.ScriptableContext; // 从执行上下文中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从执行上下文中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出相机，用来创建匹配尺寸的后处理 RT。

            var postProcessColorTarget = context.PostProcessColorTarget; // 从资源表中取出 PostProcessColor 句柄。

            if (!postProcessColorTarget.IsValid) // 如果资源句柄无效，说明 RenderGraph 没有注册 PostProcessColor。
            {
                return; // 直接跳过，避免申请一个后续 Pass 无法找到的 RT。
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera); // 创建和 CameraColor 匹配的后处理颜色 RT 描述。

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取 CommandBuffer，并用当前 Pass 名称命名。

            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.PostProcessColorTextureId, descriptor, FilterMode.Bilinear); // 申请 PostProcessColor 临时 RT，后续 No-op Copy 会先写入它。

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.PostProcessColorTextureId, postProcessColorTarget.Identifier); // 把 PostProcessColor 暴露为全局纹理，方便调试或后续效果链采样。

            renderContext.ExecuteCommandBuffer(cmd); // 把申请 RT 的命令提交给 Unity 渲染上下文。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtPostProcessPass : BurtRenderPass // 定义第一版正式后处理 Pass，支持 No-op Copy 和 Tonemapping。
    {
        private const string PostProcessShaderName = "Hidden/BurtRP/PostProcessCopy"; // 定义后处理 shader 的查找名称，必须和 shader 文件里的 Shader 名称一致。

        private static readonly int SourceTextureId = Shader.PropertyToID("_BurtPostProcessSourceTexture"); // 缓存源纹理属性 ID，避免每帧通过字符串查找。

        private static readonly int TonemappingModeId = Shader.PropertyToID("_BurtTonemappingMode"); // 缓存 Tonemapping 模式属性 ID，避免每帧通过字符串查找。

        private static readonly int PostExposureId = Shader.PropertyToID("_BurtPostExposure"); // 缓存后处理曝光倍率属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmSlopeId = Shader.PropertyToID("_BurtFilmSlope"); // 缓存 UE/XRender Film Slope 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmToeId = Shader.PropertyToID("_BurtFilmToe"); // 缓存 UE/XRender Film Toe 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmShoulderId = Shader.PropertyToID("_BurtFilmShoulder"); // 缓存 UE/XRender Film Shoulder 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmBlackClipId = Shader.PropertyToID("_BurtFilmBlackClip"); // 缓存 UE/XRender Film Black Clip 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmWhiteClipId = Shader.PropertyToID("_BurtFilmWhiteClip"); // 缓存 UE/XRender Film White Clip 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmBlueCorrectionId = Shader.PropertyToID("_BurtFilmBlueCorrection"); // 缓存 XRender Blue Correction 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmExpandGamutId = Shader.PropertyToID("_BurtFilmExpandGamut"); // 缓存 XRender Expand Gamut 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmToneCurveAmountId = Shader.PropertyToID("_BurtFilmToneCurveAmount"); // 缓存 XRender Tone Curve Amount 属性 ID，避免每帧通过字符串查找。

        private Material postProcessMaterial; // 缓存运行时后处理材质，避免每帧重复创建 Material。

        private bool hasLoggedMissingShader; // 记录缺失 shader 警告是否已经输出，避免 Console 每帧刷屏。

        public override string Name => "Burt Post Process"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtPostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset)) // 如果当前 request 没启用后处理框架，就不声明任何资源。
            {
                return; // 直接结束配置，保持关闭状态下没有额外依赖。
            }

            builder.ReadCameraColor(); // 声明先读取场景渲染完成后的 CameraColor。

            builder.WritePostProcessColor(); // 声明第一段拷贝会写入 PostProcessColor。

            builder.ReadPostProcessColor(); // 声明第二段拷贝会读取 PostProcessColor。

            builder.WriteCameraColor(); // 声明最终会把结果写回 CameraColor，供 FinalBlit 继续输出。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行无效果后处理拷贝。
        {
            if (!BurtPostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset)) // 执行阶段再次检查开关，保证关闭后不会提交绘制命令。
            {
                return; // 未启用时直接跳过。
            }

            var renderContext = context.ScriptableContext; // 从上下文中取出 Unity SRP 渲染上下文。

            var cameraColorTarget = context.CameraColorTarget; // 读取 CameraColor 句柄，作为后处理源和最终回写目标。

            var postProcessColorTarget = context.PostProcessColorTarget; // 读取 PostProcessColor 句柄，作为中间 ping-pong 目标。

            if (!cameraColorTarget.IsValid) // 如果 CameraColor 无效，说明场景颜色还没有可采样的源。
            {
                return; // 直接跳过，避免采样无效纹理。
            }

            if (!postProcessColorTarget.IsValid) // 如果 PostProcessColor 无效，说明分配 Pass 或资源注册没有生效。
            {
                return; // 直接跳过，避免写入无效目标。
            }

            var material = GetPostProcessMaterial(); // 获取或创建后处理材质。

            if (material == null) // 如果材质为空，说明 shader 没找到或创建失败。
            {
                return; // 直接跳过，避免提交无效绘制。
            }

            var tonemappingMode = BurtPostProcessUtility.ResolveTonemappingMode(context.Asset); // 从当前 VolumeStack 安全解析本次后处理应该使用的 Tonemapping 模式。

            var postExposureMultiplier = BurtPostProcessUtility.ResolvePostExposureMultiplier(context.Asset); // 把 Global Volume 中的 EV 曝光转换成本次 shader 使用的线性倍率。

            var filmSettings = BurtPostProcessUtility.ResolveTonemappingFilmSettings(context.Asset); // 从 Global Volume 读取 UE/XRender Filmic 曲线参数，缺失时回退到默认值。

            var cmd = CommandBufferPool.Get(Name); // 从命令缓冲池获取 CommandBuffer，并用 Pass 名称命名。

            cmd.SetRenderTarget(postProcessColorTarget.Identifier); // 先绑定 PostProcessColor，让第一段全屏拷贝写入后处理中间目标。

            cmd.SetGlobalTexture(SourceTextureId, cameraColorTarget.Identifier); // 把 CameraColor 设置为当前拷贝 shader 的源纹理。

            cmd.SetGlobalFloat(TonemappingModeId, (float)tonemappingMode); // 上传 Tonemapping 模式，None 会让 shader 原样输出，其他模式会执行对应曲线。

            cmd.SetGlobalFloat(PostExposureId, postExposureMultiplier); // 上传线性曝光倍率，让 Tonemapping 前可以整体调整 HDR 亮度。

            cmd.SetGlobalFloat(FilmSlopeId, filmSettings.Slope); // 上传 Film Slope，让 shader 的 UE/XRender 曲线和 Volume 参数一致。

            cmd.SetGlobalFloat(FilmToeId, filmSettings.Toe); // 上传 Film Toe，让 shader 控制暗部过渡。

            cmd.SetGlobalFloat(FilmShoulderId, filmSettings.Shoulder); // 上传 Film Shoulder，让 shader 控制高光压缩。

            cmd.SetGlobalFloat(FilmBlackClipId, filmSettings.BlackClip); // 上传 Film Black Clip，让 shader 控制黑位裁切。

            cmd.SetGlobalFloat(FilmWhiteClipId, filmSettings.WhiteClip); // 上传 Film White Clip，让 shader 控制白位裁切。

            cmd.SetGlobalFloat(FilmBlueCorrectionId, filmSettings.BlueCorrection); // 上传 Blue Correction，让 shader 对齐 XRender CombineLUT 中的蓝色修正。

            cmd.SetGlobalFloat(FilmExpandGamutId, filmSettings.ExpandGamut); // 上传 Expand Gamut，让 shader 对齐 XRender CombineLUT 中的高饱和颜色扩展。

            cmd.SetGlobalFloat(FilmToneCurveAmountId, filmSettings.ToneCurveAmount); // 上传 Tone Curve Amount，让 shader 支持按 XRender 的方式混合曲线强度。

            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1); // 绘制全屏三角形，把 CameraColor 处理到 PostProcessColor。

            cmd.SetRenderTarget(cameraColorTarget.Identifier); // 再绑定回 CameraColor，让第二段拷贝把后处理结果写回主颜色目标。

            cmd.SetGlobalTexture(SourceTextureId, postProcessColorTarget.Identifier); // 把 PostProcessColor 设置为当前拷贝 shader 的源纹理。

            cmd.SetGlobalFloat(TonemappingModeId, (float)BurtTonemappingMode.None); // 第二段只负责回写 CameraColor，必须关闭 Tonemapping，避免同一帧重复套曲线。

            cmd.SetGlobalFloat(PostExposureId, 1f); // 第二段回写使用 1 倍曝光，保证它是纯拷贝。

            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1); // 再绘制一次全屏三角形，把 PostProcessColor 原样写回 CameraColor。

            renderContext.ExecuteCommandBuffer(cmd); // 把两段拷贝命令一次性提交给 Unity 渲染上下文。

            CommandBufferPool.Release(cmd); // 释放 CommandBuffer，避免每帧产生 GC。

            BurtPostProcessUtility.LogPostProcessExecuted(context, tonemappingMode, postExposureMultiplier); // 如果用户开启了后处理调试日志，就输出本次后处理执行信息。
        }

        private Material GetPostProcessMaterial() // 定义获取后处理材质的内部辅助函数。
        {
            if (postProcessMaterial != null) // 如果材质之前已经创建过，就直接复用。
            {
                return postProcessMaterial; // 返回缓存材质，避免重复创建。
            }

            var shader = Shader.Find(PostProcessShaderName); // 按名称查找后处理 shader。

            if (shader == null) // 如果 shader 查找失败，说明资源未导入或名称不一致。
            {
                if (!hasLoggedMissingShader) // 如果还没有输出过缺失 shader 警告，就输出一次。
                {
                    Debug.LogWarning("BurtRP could not find shader: " + PostProcessShaderName); // 输出缺失 shader 警告，方便定位资源问题。

                    hasLoggedMissingShader = true; // 标记警告已输出，避免每帧重复刷屏。
                }

                return null; // 返回空材质，让调用方安全跳过后处理 Pass。
            }

            postProcessMaterial = new Material(shader); // 使用找到的 shader 创建运行时材质。

            postProcessMaterial.hideFlags = HideFlags.HideAndDontSave; // 隐藏运行时材质，并避免它被保存进场景或资源。

            return postProcessMaterial; // 返回创建好的材质。
        }
    }

    internal sealed class BurtReleasePostProcessColorPass : BurtRenderPass // 定义后处理中间颜色释放 Pass，负责释放 PostProcessColor 临时 RT。
    {
        public override string Name => "Burt Release Post Process Color"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtPostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset)) // 如果后处理框架没有启用，就不声明资源依赖。
            {
                return; // 直接结束配置，避免关闭状态下出现无效资源读取。
            }

            builder.ReadPostProcessColor(); // 声明这个 Pass 依赖 PostProcessColor，表示它要结束这个临时资源的生命周期。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 PostProcessColor 临时 RT 的释放。
        {
            if (!BurtPostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset)) // 执行阶段再次确认后处理框架仍然启用。
            {
                return; // 未启用时直接跳过，不释放未申请的资源。
            }

            var renderContext = context.ScriptableContext; // 从上下文中取出 Unity SRP 渲染上下文。

            var postProcessColorTarget = context.PostProcessColorTarget; // 从资源表中读取 PostProcessColor 句柄。

            if (!postProcessColorTarget.IsValid) // 如果句柄无效，说明当前图没有注册后处理中间 RT。
            {
                return; // 直接跳过，避免释放不存在的临时 RT。
            }

            var cmd = CommandBufferPool.Get(Name); // 从命令缓冲池获取 CommandBuffer，并用当前 Pass 名称命名。

            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.PostProcessColorTextureId); // 释放前面申请的 PostProcessColor 临时 RT，避免资源泄漏到下一帧或下一个 request。

            renderContext.ExecuteCommandBuffer(cmd); // 把释放命令提交给 Unity 渲染上下文。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }
}
