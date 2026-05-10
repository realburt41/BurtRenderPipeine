using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Camera、GL、HideFlags、Material、Matrix4x4、Shader 和 Vector4。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 CommandBufferPool 和 MeshTopology。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让 Deferred Lighting Pass 可以访问 RenderGraph 上下文和资源句柄。
{
    internal sealed class BurtDeferredLightingPass : BurtRenderPass // 定义 Deferred Lighting 全屏 Pass，负责把 GBuffer 合成为 CameraColor。
    {
        private const string DeferredLightingShaderName = "Hidden/BurtRP/DeferredLighting"; // 定义 Deferred Lighting shader 的查找名称，Gibbs 的 shader 侧需要提供同名 shader。
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id; // 缓存 GBuffer0 全局纹理 ID，避免每帧重复查找字符串。
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id; // 缓存 GBuffer1 全局纹理 ID，避免每帧重复查找字符串。
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id; // 缓存 GBuffer2 全局纹理 ID，避免每帧重复查找字符串。
        private static readonly int CameraDepthId = BurtRenderGraphResourceRegistry.CameraDepthTextureId; // 缓存 CameraDepth 全局纹理 ID，Deferred Lighting 需要用它重建位置。
        private static readonly int InverseViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseViewProjectionMatrix"); // 缓存逆 ViewProjection 矩阵 ID，shader 可用它从屏幕和深度重建世界坐标。
        private static readonly int CameraWorldPositionId = Shader.PropertyToID("_BurtDeferredCameraWorldPosition"); // 缓存相机世界坐标 ID，shader 可用它计算 view direction。
        private static readonly int CameraClipPlanesId = Shader.PropertyToID("_BurtDeferredCameraClipPlanes"); // 缓存相机裁剪面参数 ID，shader 可用它做深度线性化兜底。
        private static readonly int ScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize"); // 缓存屏幕尺寸参数 ID，shader 可用它把像素坐标和 UV 互相转换。
        private Material deferredLightingMaterial; // 缓存 Deferred Lighting 运行时材质，避免每帧重复创建 Material。
        private bool hasLoggedMissingShader; // 记录是否已经提示过 shader 缺失，避免 Console 每帧刷屏。

        public override string Name => "Burt Deferred Lighting"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别合成阶段。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源读写关系。
        {
            builder.ReadGBuffer0(); // 声明 Deferred Lighting 会读取 GBuffer0 中的 baseColor 和 occlusion。
            builder.ReadGBuffer1(); // 声明 Deferred Lighting 会读取 GBuffer1 中的 normal、metallic 和 smoothness。
            builder.ReadGBuffer2(); // 声明 Deferred Lighting 会读取 GBuffer2 中的 emission 和 reflectance。
            builder.ReadCameraDepth(); // 声明 Deferred Lighting 会读取 CameraDepth 来重建世界坐标。
            builder.ReadLightingGlobals(); // 声明 Deferred Lighting 会读取 Setup Lighting 上传的主光和环境光全局状态。
            builder.ReadShadowGlobals(); // 声明 Deferred Lighting 会读取阴影矩阵、强度和 texel size 等全局状态。

            if (BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 真的生成了主光阴影图，就把 shadow map 声明为输入。
            {
                builder.ReadMainLightShadowMap(); // 声明 Deferred Lighting 会采样 MainLightShadowMap。
            }

            builder.WriteCameraColor(); // 声明 Deferred Lighting 的合成结果会写入 CameraColor，后续 Skybox、Transparent 和 PostProcess 继续使用它。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 Deferred Lighting 全屏合成。
        {
            if (!TryGetRequiredTargets(context, out var cameraColorTarget, out var cameraDepthTarget, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target)) // 先读取并验证本 Pass 需要的全部 RT。
            {
                return; // 资源不完整时直接跳过，避免向错误目标绘制全屏三角形。
            }

            var material = GetDeferredLightingMaterial(); // 获取或创建 Deferred Lighting 材质。

            if (material == null) // 如果 shader 缺失或材质创建失败，就不能执行合成。
            {
                return; // 直接跳过；当前 Deferred 组装器仍保留 Forward fallback，画面不会因为 shader 缺失而黑屏。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称标记它。

            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier); // 绑定 CameraColor 作为输出，同时保留 CameraDepth 供后续 Skybox 和透明物体继续深度测试。
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier); // 把当前 request 的 GBuffer0 绑定给 Deferred Lighting shader。
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier); // 把当前 request 的 GBuffer1 绑定给 Deferred Lighting shader。
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier); // 把当前 request 的 GBuffer2 绑定给 Deferred Lighting shader。
            cmd.SetGlobalTexture(CameraDepthId, cameraDepthTarget.Identifier); // 确保 _BurtCameraDepthTexture 指向当前 request 的深度纹理。
            BindShadowMapIfValid(context, cmd); // 如果当前 request 有主光阴影图，就重新绑定一次，避免多相机全局纹理残留。
            UploadCameraReconstructionGlobals(context, cmd); // 上传深度重建和 view direction 需要的相机参数。
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1); // 绘制一个全屏三角形，把 GBuffer 光照结果写进 CameraColor。

            context.ScriptableContext.ExecuteCommandBuffer(cmd); // 把合成命令提交给 Unity 渲染上下文。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 放回池子，避免每帧产生额外 GC。
        }

        private static bool TryGetRequiredTargets( // 安全读取 Deferred Lighting 需要的全部渲染目标。
            BurtRenderGraphContext context, // 接收当前 RenderGraph 执行上下文。
            out BurtRenderTargetHandle cameraColorTarget, // 输出 CameraColor 句柄。
            out BurtRenderTargetHandle cameraDepthTarget, // 输出 CameraDepth 句柄。
            out BurtRenderTargetHandle gbuffer0Target, // 输出 GBuffer0 句柄。
            out BurtRenderTargetHandle gbuffer1Target, // 输出 GBuffer1 句柄。
            out BurtRenderTargetHandle gbuffer2Target) // 输出 GBuffer2 句柄。
        {
            cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName); // context 有效时读取 CameraColor，否则返回无效句柄。
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName); // context 有效时读取 CameraDepth，否则返回无效句柄。
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name); // context 有效时读取 GBuffer0，否则返回无效句柄。
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name); // context 有效时读取 GBuffer1，否则返回无效句柄。
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name); // context 有效时读取 GBuffer2，否则返回无效句柄。

            return cameraColorTarget.IsValid && cameraDepthTarget.IsValid && gbuffer0Target.IsValid && gbuffer1Target.IsValid && gbuffer2Target.IsValid; // 只有所有目标有效时才允许执行全屏合成。
        }

        private static void BindShadowMapIfValid( // 根据资源表状态把主光阴影图重新绑定给 shader。
            BurtRenderGraphContext context, // 接收当前 RenderGraph 执行上下文。
            CommandBuffer cmd) // 接收要写入命令的 CommandBuffer。
        {
            if (context == null) // context 为空时没有资源表可以读取。
            {
                return; // 直接跳过阴影纹理绑定。
            }

            var shadowMapTarget = context.MainLightShadowMapTarget; // 从资源表读取当前 request 的主光阴影图句柄。

            if (!shadowMapTarget.IsValid) // 如果没有主光阴影图，说明当前 request 不需要实时主光阴影。
            {
                return; // 直接跳过，让 shader 使用 Setup Lighting 里上传的无阴影默认状态。
            }

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.MainLightShadowMapId, shadowMapTarget.Identifier); // 绑定当前 request 的主光阴影图，供 Deferred Lighting 采样。
        }

        private static void UploadCameraReconstructionGlobals( // 上传 Deferred Lighting 重建世界坐标需要的相机参数。
            BurtRenderGraphContext context, // 接收当前 RenderGraph 执行上下文。
            CommandBuffer cmd) // 接收要写入命令的 CommandBuffer。
        {
            var request = context != null ? context.Request : null; // 从上下文读取当前 request。
            var camera = request != null ? request.Camera : null; // 从 request 读取当前相机。

            if (camera == null) // 没有相机就无法计算 ViewProjection 矩阵。
            {
                return; // 直接跳过参数上传，让 shader 使用已有 Unity 内置矩阵兜底。
            }

            var viewMatrix = camera.worldToCameraMatrix; // 读取世界到相机空间矩阵。
            var projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true); // 把 Unity 投影矩阵转换成 GPU 渲染到 RT 时使用的投影矩阵。
            var viewProjectionMatrix = projectionMatrix * viewMatrix; // 合成 GPU 视图投影矩阵，和当前 CameraDepth/GBuffer 渲染路径保持一致。
            var inverseViewProjectionMatrix = viewProjectionMatrix.inverse; // 计算逆矩阵，shader 可用它把 NDC 和深度还原到世界空间。
            var pixelWidth = Mathf.Max(1, camera.pixelWidth); // 读取相机像素宽度，并保证最小为 1。
            var pixelHeight = Mathf.Max(1, camera.pixelHeight); // 读取相机像素高度，并保证最小为 1。
            var screenSize = new Vector4(pixelWidth, pixelHeight, 1f / pixelWidth, 1f / pixelHeight); // 组织屏幕尺寸和倒数，方便 shader 做 UV/像素换算。
            var clipPlanes = new Vector4(camera.nearClipPlane, camera.farClipPlane, 1f / Mathf.Max(camera.nearClipPlane, 0.0001f), 1f / Mathf.Max(camera.farClipPlane, 0.0001f)); // 组织近远裁剪面和倒数，给 shader 做深度线性化兜底。
            var cameraPosition = camera.transform.position; // 读取相机世界坐标，后面会扩展成 Vector4 上传给 shader。

            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix); // 上传逆 ViewProjection 矩阵。
            cmd.SetGlobalVector(CameraWorldPositionId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f)); // 上传相机世界坐标，w 固定为 1 方便 shader 识别这是位置。
            cmd.SetGlobalVector(CameraClipPlanesId, clipPlanes); // 上传相机裁剪面参数。
            cmd.SetGlobalVector(ScreenSizeId, screenSize); // 上传屏幕尺寸参数。
        }

        private Material GetDeferredLightingMaterial() // 获取或创建 Deferred Lighting 材质。
        {
            if (deferredLightingMaterial != null) // 如果之前已经创建过材质，就直接复用。
            {
                return deferredLightingMaterial; // 返回缓存材质。
            }

            var shader = Shader.Find(DeferredLightingShaderName); // 按约定名称查找 Deferred Lighting shader。

            if (shader == null) // 如果 shader 还没有由 Gibbs 接入或 Unity 尚未导入，就会查找失败。
            {
                if (!hasLoggedMissingShader) // 如果还没有输出过缺失提示，就输出一次。
                {
                    Debug.LogWarning("BurtRP could not find shader: " + DeferredLightingShaderName); // 输出缺失 shader 警告，方便定位 shader 侧是否还没完成。
                    hasLoggedMissingShader = true; // 标记已经提示过，避免每帧刷屏。
                }

                return null; // 返回空材质，让 Execute 安全跳过。
            }

            deferredLightingMaterial = new Material(shader); // 使用找到的 shader 创建运行时材质。
            deferredLightingMaterial.hideFlags = HideFlags.HideAndDontSave; // 隐藏运行时材质并避免它被保存到场景或资产中。
            return deferredLightingMaterial; // 返回创建好的材质。
        }
    }
}
