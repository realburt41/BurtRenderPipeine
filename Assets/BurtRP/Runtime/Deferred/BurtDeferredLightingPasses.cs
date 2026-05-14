using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Camera、GL、HideFlags、Material、Matrix4x4、Shader 和 Vector4。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 CommandBufferPool 和 MeshTopology。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让 Deferred Lighting Pass 可以访问 RenderGraph 上下文和资源句柄。
{
    internal sealed class BurtClearDeferredLightingTargetPass : BurtRenderPass // 在分 pass deferred lighting 前把 CameraColor 清成黑色，避免 stencil 跳过的 Hair 像素保留相机 clear color。
    {
        public override string Name => "Burt Clear Deferred Lighting Target";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            if (!cameraColorTarget.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.ClearRenderTarget(false, true, Color.clear);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal abstract class BurtDeferredLightingPass : BurtRenderPass // 定义 Deferred Lighting 全屏 Pass 基类，Lit/Hair 会各自以独立 pass index 执行。
    {
        private const string DeferredLightingShaderName = "Hidden/BurtRP/DeferredLighting"; // 定义 Deferred Lighting shader 的查找名称，Gibbs 的 shader 侧需要提供同名 shader。
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id; // 缓存 GBuffer0 全局纹理 ID，避免每帧重复查找字符串。
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id; // 缓存 GBuffer1 全局纹理 ID，避免每帧重复查找字符串。
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id; // 缓存 GBuffer2 全局纹理 ID，避免每帧重复查找字符串。
        private static readonly int CameraDepthId = BurtRenderGraphResourceRegistry.CameraDepthTextureId; // 缓存 CameraDepth 全局纹理 ID，Deferred Lighting 需要用它重建位置。
        private static readonly int ScreenSpaceAmbientOcclusionId = BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionTextureId;
        private static readonly int ScreenSpaceAmbientOcclusionEnabledId = Shader.PropertyToID("_BurtScreenSpaceAmbientOcclusionEnabled");
        private static readonly int InverseViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseViewProjectionMatrix"); // 缓存逆 ViewProjection 矩阵 ID，shader 可用它从屏幕和深度重建世界坐标。
        private static readonly int CameraWorldPositionId = Shader.PropertyToID("_BurtDeferredCameraWorldPosition"); // 缓存相机世界坐标 ID，shader 可用它计算 view direction。
        private static readonly int CameraClipPlanesId = Shader.PropertyToID("_BurtDeferredCameraClipPlanes"); // 缓存相机裁剪面参数 ID，shader 可用它做深度线性化兜底。
        private static readonly int ScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize"); // 缓存屏幕尺寸参数 ID，shader 可用它把像素坐标和 UV 互相转换。
        private readonly string passName; // 缓存当前 lighting pass 的调试名称，Frame Debugger 中会区分 Lit 和 Hair。
        private readonly int shaderPassIndex; // 缓存当前要执行的 shader pass index；0=Lit，1=Hair。
        private readonly bool readsExistingCameraColor; // Hair pass 使用加法混合，需要声明它依赖前一个 Lit pass 的 CameraColor。
        private Material deferredLightingMaterial; // 缓存 Deferred Lighting 运行时材质，避免每帧重复创建 Material。
        private bool hasLoggedMissingShader; // 记录是否已经提示过 shader 缺失，避免 Console 每帧刷屏。
        private bool hasLoggedMissingShaderPass;
        protected BurtDeferredLightingPass(string passName, int shaderPassIndex, bool readsExistingCameraColor) // 创建一个指定 shading model filter 的 Deferred Lighting pass。
        {
            this.passName = passName;
            this.shaderPassIndex = shaderPassIndex;
            this.readsExistingCameraColor = readsExistingCameraColor;
        }

        public override string Name => passName; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别 Lit/Hair 合成阶段。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源读写关系。
        {
            builder.ReadGBuffer0(); // 声明 Deferred Lighting 会读取 GBuffer0 中的 baseColor 和 occlusion。
            builder.ReadGBuffer1(); // 声明 Deferred Lighting 会读取 GBuffer1 中的 normal、metallic 和 smoothness。
            builder.ReadGBuffer2(); // 声明 Deferred Lighting 会读取 GBuffer2 中的 emission 和 reflectance。
            builder.ReadCameraDepth(); // 声明 Deferred Lighting 会读取 CameraDepth 来重建世界坐标。
            if (BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(builder.Request, builder.Asset))
            {
                builder.ReadScreenSpaceAmbientOcclusion();
            }

            if (ShouldUseRuntimeTiledLighting(builder.Request, builder.Asset, builder.ResourceRegistry))
            {
                builder.ReadTileLightCountBuffer();
                builder.ReadTileLightListBuffer();
                builder.ReadTileLightOffsetBuffer();
            }

            builder.ReadLightingGlobals(); // 声明 Deferred Lighting 会读取 Setup Lighting 上传的主光和环境光全局状态。
            builder.ReadShadowGlobals(); // 声明 Deferred Lighting 会读取阴影矩阵、强度和 texel size 等全局状态。

            if (BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 真的生成了主光阴影图，就把 shadow map 声明为输入。
            {
                builder.ReadMainLightShadowMap(); // 声明 Deferred Lighting 会采样 MainLightShadowMap。
            }

            if (readsExistingCameraColor) // Hair pass 以 additive 方式叠加到 Lit pass 结果上，所以在资源声明中保留这个依赖。
            {
                builder.ReadCameraColor();
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

            if (!HasRequiredShaderPass(material)) // 确认 shader 侧已经提供当前 pass index，避免 DrawProcedural 访问不存在的 pass。
            {
                return; // 直接跳过当前 shading model pass，让日志提示 shader 和 C# pass 拆分未对齐。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称标记它。

            cmd.SetRenderTarget(cameraColorTarget.Identifier); // Do not bind CameraDepth while sampling it; D3D can return invalid depth when the same resource is also a depth attachment.
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier); // 把当前 request 的 GBuffer0 绑定给 Deferred Lighting shader。
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier); // 把当前 request 的 GBuffer1 绑定给 Deferred Lighting shader。
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier); // 把当前 request 的 GBuffer2 绑定给 Deferred Lighting shader。
            cmd.SetGlobalTexture(CameraDepthId, cameraDepthTarget.Identifier); // 确保 _BurtCameraDepthTexture 指向当前 request 的深度纹理。
            BindScreenSpaceAmbientOcclusion(context, cmd);
            BindRuntimeTiledLighting(context, cmd);
            UploadMainLightShadowReceiverGlobals(context, cmd, material); // Rebind shadow globals and shadow map on the deferred material so fullscreen lighting cannot see stale globals.
            UploadAdditionalLightShadowReceiverGlobals(context, cmd, material);
            UploadCameraReconstructionGlobals(context, cmd, material); // 上传深度重建和 view direction 需要的相机参数。
            cmd.DrawProcedural(Matrix4x4.identity, material, shaderPassIndex, MeshTopology.Triangles, 3, 1); // 绘制全屏三角形，只处理当前 pass 负责的 shading model。

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

        private static void BindScreenSpaceAmbientOcclusion(BurtRenderGraphContext context, CommandBuffer cmd)
        {
            var enabled = false;
            var target = context != null ? context.ScreenSpaceAmbientOcclusionTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionName);

            if (context != null &&
                target.IsValid &&
                BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(context.Request, context.Asset))
            {
                cmd.SetGlobalTexture(ScreenSpaceAmbientOcclusionId, target.Identifier);
                enabled = true;
            }

            cmd.SetGlobalFloat(ScreenSpaceAmbientOcclusionEnabledId, enabled ? 1f : 0f);
        }

        private static bool ShouldUseRuntimeTiledLighting(BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtRenderGraphResourceRegistry resourceRegistry)
        {
            if (!BurtTiledLightData.ShouldUseRuntimeTiledLightingResources(request, asset, true))
            {
                return false;
            }

            return resourceRegistry != null &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.TileLightCountBufferName) &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.TileLightListBufferName) &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName);
        }

        private static void BindRuntimeTiledLighting(BurtRenderGraphContext context, CommandBuffer cmd)
        {
            var enabled = false;
            var lightingData = context != null && context.Request != null ? context.Request.LightingData : null;
            var countBuffer = context != null ? context.TileLightCountBuffer : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.TileLightCountBufferName);
            var listBuffer = context != null ? context.TileLightListBuffer : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.TileLightListBufferName);
            var offsetBuffer = context != null ? context.TileLightOffsetBuffer : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName);

            if (context != null &&
                BurtTiledLightData.ShouldUseRuntimeTiledLightingResources(context.Request, context.Asset, true) &&
                lightingData != null &&
                lightingData.TileLightDebugUploaded &&
                countBuffer.IsValid && countBuffer.HasBuffer &&
                listBuffer.IsValid && listBuffer.HasBuffer &&
                offsetBuffer.IsValid && offsetBuffer.HasBuffer)
            {
                var layout = BurtTiledLightData.CalculateLayout(context.Request != null ? context.Request.Camera : null);
                var maxLightsPerTile = lightingData.TileLightMaxLightsPerTile > 0
                    ? Mathf.Min(lightingData.TileLightMaxLightsPerTile, BurtTiledLightData.ResolveRuntimeMaxLightsPerTile())
                    : BurtTiledLightData.ResolveRuntimeMaxLightsPerTile();
                cmd.SetGlobalBuffer(BurtTiledLightData.TileLightCountBufferId, countBuffer.Buffer);
                cmd.SetGlobalBuffer(BurtTiledLightData.TileLightListBufferId, listBuffer.Buffer);
                cmd.SetGlobalBuffer(BurtTiledLightData.TileLightOffsetBufferId, offsetBuffer.Buffer);
                cmd.SetGlobalVector(BurtTiledLightData.TileLightGridParamsId, new Vector4(layout.TileCountX, layout.TileCountY, layout.TileSize, maxLightsPerTile));
                enabled = true;
            }

            cmd.SetGlobalFloat(BurtTiledLightData.TileLightCountBufferEnabledId, enabled ? 1f : 0f);
        }

        private static void UploadCameraReconstructionGlobals( // 上传 Deferred Lighting 重建世界坐标需要的相机参数。
            BurtRenderGraphContext context, // 接收当前 RenderGraph 执行上下文。
            CommandBuffer cmd, // 接收要写入命令的 CommandBuffer。
            Material material) // 接收当前 Deferred Lighting 材质，避免 fullscreen pass 读到旧的全局相机矩阵。
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

            var cameraWorldPosition = new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f);

            if (material != null)
            {
                material.SetMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix);
                material.SetVector(CameraWorldPositionId, cameraWorldPosition);
                material.SetVector(CameraClipPlanesId, clipPlanes);
                material.SetVector(ScreenSizeId, screenSize);
            }

            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix); // 上传逆 ViewProjection 矩阵。
            cmd.SetGlobalVector(CameraWorldPositionId, cameraWorldPosition); // 上传相机世界坐标，w 固定为 1 方便 shader 识别这是位置。
            cmd.SetGlobalVector(CameraClipPlanesId, clipPlanes); // 上传相机裁剪面参数。
            cmd.SetGlobalVector(ScreenSizeId, screenSize); // 上传屏幕尺寸参数。
        }

        private void UploadMainLightShadowReceiverGlobals(BurtRenderGraphContext context, CommandBuffer cmd, Material material)
        {
            if (cmd == null || material == null)
            {
                return;
            }

            if (context == null || !BurtShadowUtility.ShouldUseMainLightShadow(context.Request, context.Asset))
            {
                DisableMainLightShadowReceiverGlobals(cmd, material);
                return;
            }

            var shadowMapTarget = context.MainLightShadowMapTarget;
            if (!shadowMapTarget.IsValid)
            {
                DisableMainLightShadowReceiverGlobals(cmd, material);
                return;
            }

            var shadowData = BurtShadowUtility.ResolveMainLightShadowData(context.Request, context.Asset);
            if (shadowData == null || shadowData.MainLightIndex < 0)
            {
                DisableMainLightShadowReceiverGlobals(cmd, material);
                return;
            }

            if (!BurtMainLightShadowMatrixUtility.TryGetMainLightShadowCascadeCache(context.Request, shadowData, out var cascadeCache))
            {
                DisableMainLightShadowReceiverGlobals(cmd, material);
                return;
            }

            BurtMainLightShadowMatrixUtility.UploadMainLightShadowReceiverGlobals(cmd, material, shadowMapTarget, cascadeCache.WorldToShadowMatrices, cascadeCache.CascadeSpheres, cascadeCache.CascadeAtlasRects, cascadeCache.CascadeCount, cascadeCache.TileResolution, shadowData);
        }

        private static void DisableMainLightShadowReceiverGlobals(CommandBuffer cmd, Material material)
        {
            BurtMainLightShadowMatrixUtility.ClearMainLightShadowReceiverGlobals(cmd, material);
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

        private bool HasRequiredShaderPass(Material material) // 校验 shader pass index，方便 Hair/Lit 分 pass 时快速定位未导入的 shader。
        {
            if (material != null && shaderPassIndex >= 0 && shaderPassIndex < material.passCount)
            {
                return true;
            }

            if (!hasLoggedMissingShaderPass)
            {
                Debug.LogWarning("BurtRP deferred lighting shader pass missing: " + DeferredLightingShaderName + " pass " + shaderPassIndex + " for " + Name);
                hasLoggedMissingShaderPass = true;
            }

            return false;
        }

        private void UploadAdditionalLightShadowReceiverGlobals(BurtRenderGraphContext context, CommandBuffer cmd, Material material)
        {
            if (cmd == null || material == null)
            {
                return;
            }

            if (context == null || !BurtAdditionalLightShadowUtility.ShouldUseAdditionalLightShadows(context.Request))
            {
                BurtAdditionalLightShadowUtility.ClearAdditionalLightShadowReceiverGlobals(cmd, material);
                return;
            }

            var atlasTarget = context.AdditionalLightShadowAtlasTarget;
            var lightingData = context.Request != null ? context.Request.LightingData : null;
            BurtAdditionalLightShadowUtility.UploadAdditionalLightShadowReceiverGlobals(cmd, material, atlasTarget, lightingData);
        }
    }

    internal sealed class BurtDeferredLitLightingPass : BurtDeferredLightingPass // Lit deferred lighting pass，只处理 Default Lit GBuffer 像素。
    {
        public BurtDeferredLitLightingPass()
            : base("Burt Deferred Lit Lighting", 0, false)
        {
        }
    }

    internal sealed class BurtDeferredHairLightingPass : BurtDeferredLightingPass // Hair deferred lighting pass，只处理 Hair GBuffer 像素。
    {
        public BurtDeferredHairLightingPass()
            : base("Burt Deferred Hair Lighting", 1, true)
        {
        }
    }
}
