using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 HideFlags、Material、Matrix4x4、Shader 和 Mathf 等 Unity 类型。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 CommandBufferPool 和 MeshTopology。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让 GBuffer Debug Pass 可以访问 RenderGraph 上下文和资源句柄。
{
    internal static class BurtGBufferDebugViewUtility // 定义 GBuffer Debug 的统一解析工具，让 RenderGraph 组装器、Pass 执行和 Editor Overlay 使用同一套模式来源。
    {
        public static bool ShouldUseGBufferDebugView( // 判断当前 request 是否应该插入 GBuffer Debug Pass。
            BurtRenderPipelineAsset asset, // 接收当前 BurtRP 管线资产，用来读取 Renderer Mode 和资产面板上的 GBuffer Debug 模式。
            bool hasLocalGBufferTargets) // 接收当前 RenderGraph 是否真的申请了本地 GBuffer，避免 Overlay 在没有 GBuffer 的路径强行读取。
        {
            if (!hasLocalGBufferTargets) // 如果当前 request 没有 GBuffer 生命周期，就没有可视化的数据源。
            {
                return false; // 返回 false，避免共享 Overlay 或异常 request 读取无效 GBuffer。
            }

            if (asset == null) // 如果资产为空，就无法确认当前是否是 BurtRP Deferred 模式。
            {
                return false; // 返回 false，保持空资产路径安全。
            }

            if (asset.RendererMode != BurtRendererMode.Deferred) // 只有 Deferred 路径会申请和写入真实 GBuffer。
            {
                return false; // Forward 模式下关闭全屏 GBuffer Debug，Forward 的 shader 内 roundtrip debug 仍由 ShadingDebug 处理。
            }

            return ResolveGBufferDebugViewMode(asset) != BurtGBufferDebugViewMode.Disabled; // 资产面板或 Overlay 任一来源选中 GBuffer 模式时就插入 Pass。
        }

        public static BurtGBufferDebugViewMode ResolveGBufferDebugViewMode(BurtRenderPipelineAsset asset) // 解析当前真正要使用的 GBuffer Debug 模式。
        {
            var assetMode = asset != null ? asset.GBufferDebugViewMode : BurtGBufferDebugViewMode.Disabled; // 优先读取 BurtRenderPipelineAsset 上的显式 GBuffer Debug 选择。

            if (assetMode != BurtGBufferDebugViewMode.Disabled) // 如果资产面板已经选了模式，就认为它是最明确的来源。
            {
                return assetMode; // 返回资产面板模式，方便不用 Overlay 时也能调试 GBuffer。
            }

            return ResolveGBufferDebugViewMode(BurtShadingDebugSettings.Mode); // 资产没有开启时，回退读取 SceneView Overlay / Shading Debug 的当前模式。
        }

        public static string ResolveGBufferDebugViewSource(BurtRenderPipelineAsset asset) // 解析当前 GBuffer Debug 模式来自哪里，专门给 RenderGraph Debug 日志显示。
        {
            var assetMode = asset != null ? asset.GBufferDebugViewMode : BurtGBufferDebugViewMode.Disabled; // 先读取资产面板上的模式，因为资产面板优先级最高。

            if (assetMode != BurtGBufferDebugViewMode.Disabled) // 如果资产面板已经显式选择 GBuffer 模式。
            {
                return "Asset"; // 返回 Asset，表示当前调试图由 BurtRenderPipelineAsset Inspector 驱动。
            }

            var overlayMode = ResolveGBufferDebugViewMode(BurtShadingDebugSettings.Mode); // 把当前 Shading Debug Overlay 模式映射成 GBuffer Debug 模式。

            if (overlayMode != BurtGBufferDebugViewMode.Disabled) // 如果 Overlay 当前选择的是某个 GBuffer 分类。
            {
                return "ShadingDebugOverlay"; // 返回 Overlay 来源，方便区分是不是 UI 同步驱动的全屏 GBuffer Pass。
            }

            return "Disabled"; // 两个来源都没有启用时，明确写出 Disabled。
        }

        public static BurtGBufferDebugViewMode ResolveGBufferDebugViewMode(BurtShadingDebugMode shadingDebugMode) // 把 Shading Debug Overlay 的 GBuffer 模式映射成全屏 GBuffer Debug 模式。
        {
            switch (shadingDebugMode) // 逐项映射，避免 enum 数值偶然相近时误判。
            {
                case BurtShadingDebugMode.Albedo: // 便宜的材质基础色调试直接复用 GBuffer BaseColor，避免触发重型 DeferredLightingDebug 编译。
                case BurtShadingDebugMode.PreSkinPosition: // Subsurface PreSkin 已在 GBuffer 写入调试色，这里直接读取 BaseColor 可视化。
                case BurtShadingDebugMode.GBufferBaseColor: // Overlay 选择 GBuffer Base Color 时。
                    return BurtGBufferDebugViewMode.BaseColor; // 显示真实 GBuffer1 解码后的 baseColor。
                case BurtShadingDebugMode.NormalWS: // 便宜的材质法线调试直接复用 GBuffer NormalWS。
                case BurtShadingDebugMode.GBufferNormalWS: // Overlay 选择 GBuffer Direction WS 时。
                    return BurtGBufferDebugViewMode.NormalWS; // 显示真实 GBuffer1 解码后的向量槽。
                case BurtShadingDebugMode.Metallic: // 便宜的材质 metallic 调试直接复用 GBuffer material channel。
                case BurtShadingDebugMode.GBufferMetallic: // Overlay 选择 GBuffer Material Channel 时。
                    return BurtGBufferDebugViewMode.Metallic; // 显示真实 GBuffer2.r 解包出的材质通道。
                case BurtShadingDebugMode.Smoothness: // 便宜的材质 smoothness 调试直接复用 GBuffer smoothness。
                case BurtShadingDebugMode.GBufferSmoothness: // Overlay 选择 GBuffer Smoothness 时。
                    return BurtGBufferDebugViewMode.Smoothness; // 显示真实 GBuffer1.a 的光滑度。
                case BurtShadingDebugMode.Occlusion: // 便宜的材质 AO 调试直接复用 GBuffer occlusion。
                case BurtShadingDebugMode.GBufferOcclusion: // Overlay 选择 GBuffer Occlusion 时。
                    return BurtGBufferDebugViewMode.Occlusion; // 显示真实 GBuffer0.a 的 AO。
                case BurtShadingDebugMode.Reflectance: // 便宜的材质 reflectance 调试直接复用 GBuffer reflectance。
                case BurtShadingDebugMode.GBufferReflectance: // Overlay 选择 GBuffer Reflectance 时。
                    return BurtGBufferDebugViewMode.Reflectance; // 显示真实 GBuffer2.a 的 reflectance。
                case BurtShadingDebugMode.Roughness: // 便宜的材质 roughness 调试直接复用 GBuffer smoothness 反推。
                case BurtShadingDebugMode.GBufferRoughness: // Overlay 选择 GBuffer Roughness 时。
                    return BurtGBufferDebugViewMode.Roughness; // 显示从真实 GBuffer smoothness 还原出的 perceptual roughness。
                case BurtShadingDebugMode.DiffuseColor: // 便宜的 diffuseColor 调试直接复用 GBuffer 重建结果。
                case BurtShadingDebugMode.GBufferDiffuseColor: // Overlay 选择 GBuffer Diffuse Color 时。
                    return BurtGBufferDebugViewMode.DiffuseColor; // 显示从真实 GBuffer 重建 PBRMaterialData 后的 diffuseColor。
                case BurtShadingDebugMode.Emission:
                    return BurtGBufferDebugViewMode.Emission;
                case BurtShadingDebugMode.GBufferHairStrandDirection: // Overlay 选择 Hair strand direction 时。
                    return BurtGBufferDebugViewMode.HairStrandDirection; // 显示 Hair 复用 GBuffer0.rgb 存储的 strand direction。
                case BurtShadingDebugMode.HairScatter:
                case BurtShadingDebugMode.GBufferHairScatter: // Overlay 选择 Hair scatter 时。
                    return BurtGBufferDebugViewMode.HairScatter; // 显示 Hair 复用 GBuffer2.r material channel 存储的 scatter。
                case BurtShadingDebugMode.GBufferHairShift: // Overlay 选择 Hair longitudinal shift scale 时。
                    return BurtGBufferDebugViewMode.HairShift; // 显示 Hair 复用 GBuffer2.r material channel 存储的 shift scale。
                case BurtShadingDebugMode.GBufferClearCoatMask:
                    return BurtGBufferDebugViewMode.ClearCoatMask;
                case BurtShadingDebugMode.GBufferSubsurfaceStrength:
                    return BurtGBufferDebugViewMode.SubsurfaceStrength;
                case BurtShadingDebugMode.GBufferSubsurfaceThickness:
                    return BurtGBufferDebugViewMode.SubsurfaceThickness;
                case BurtShadingDebugMode.GBufferSubsurfaceProfileIndex:
                case BurtShadingDebugMode.SubsurfaceProfileId:
                    return BurtGBufferDebugViewMode.SubsurfaceProfileIndex;
                case BurtShadingDebugMode.GBufferFoliageTransmissionColor:
                    return BurtGBufferDebugViewMode.FoliageTransmissionColor;
                case BurtShadingDebugMode.GBufferFoliageTransmissionWeight:
                    return BurtGBufferDebugViewMode.FoliageTransmissionWeight;
                case BurtShadingDebugMode.GBufferFoliageThickness:
                    return BurtGBufferDebugViewMode.FoliageThickness;
                case BurtShadingDebugMode.GBufferFoliageTransmissionNdotL:
                    return BurtGBufferDebugViewMode.FoliageTransmissionNdotL;
                case BurtShadingDebugMode.GBufferFoliageSpecularScale:
                    return BurtGBufferDebugViewMode.FoliageSpecularScale;
                case BurtShadingDebugMode.GBufferFoliageScreenSpaceShadowIntensity:
                    return BurtGBufferDebugViewMode.FoliageScreenSpaceShadowIntensity;
                case BurtShadingDebugMode.GBufferGrassIsGrass:
                    return BurtGBufferDebugViewMode.GrassIsGrass;
                case BurtShadingDebugMode.GBufferGrassSSSIntensity:
                    return BurtGBufferDebugViewMode.GrassSSSIntensity;
                case BurtShadingDebugMode.GBufferGrassSpecularMultiply:
                    return BurtGBufferDebugViewMode.GrassSpecularMultiply;
                case BurtShadingDebugMode.GBufferGrassScreenSpaceShadowIntensity:
                    return BurtGBufferDebugViewMode.GrassScreenSpaceShadowIntensity;
                case BurtShadingDebugMode.GBufferStencilRaw:
                    return BurtGBufferDebugViewMode.StencilRaw;
                case BurtShadingDebugMode.GBufferStencilShadingModel:
                    return BurtGBufferDebugViewMode.StencilShadingModel;
                case BurtShadingDebugMode.GBufferClearCoatNormalWS:
                    return BurtGBufferDebugViewMode.ClearCoatNormalWS;
                case BurtShadingDebugMode.GBufferClearCoatRoughness:
                    return BurtGBufferDebugViewMode.ClearCoatRoughness;
                case BurtShadingDebugMode.GBufferAnisotropy:
                    return BurtGBufferDebugViewMode.Anisotropy;
                case BurtShadingDebugMode.GBufferTangentWS:
                    return BurtGBufferDebugViewMode.TangentWS;
                default: // 其他 Shading Debug 模式不是全屏 GBuffer 数据源。
                    return BurtGBufferDebugViewMode.Disabled; // 返回 Disabled，让 Deferred 正常渲染或交给其他 Debug Pass。
            }
        }

        public static float ResolveShaderDebugMode(BurtRenderPipelineAsset asset) // 解析 shader 需要的数字模式。
        {
            return Mathf.Max(0f, (int)ResolveGBufferDebugViewMode(asset)); // 把最终模式转成非负 float，匹配 _BurtGBufferDebugMode 的上传格式。
        }
    }

    internal sealed class BurtDebugGBufferPass : BurtRenderPass // 定义 Deferred GBuffer 调试 Pass，负责把 GBuffer 内容可视化到 CameraColor。
    {
        private const string DebugGBufferShaderName = "Hidden/BurtRP/DebugGBuffer"; // 定义 GBuffer 调试 shader 的查找名称，shader 侧需要提供同名隐藏 shader。
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id; // 缓存 GBuffer0 全局纹理 ID，避免每帧重复把字符串转换成整数。
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id; // 缓存 GBuffer1 全局纹理 ID，避免每帧重复把字符串转换成整数。
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id; // 缓存 GBuffer2 全局纹理 ID，避免每帧重复把字符串转换成整数。
        private static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        private static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        private static readonly int GBuffer5Id = BurtRenderGraphResourceRegistry.GBuffer5Id;
        private static readonly int CameraDepthId = BurtRenderGraphResourceRegistry.CameraDepthTextureId; // 缓存 CameraDepth 全局纹理 ID，让 RawDepth 调试模式能读取当前相机深度。
        private static readonly int DebugModeId = Shader.PropertyToID("_BurtGBufferDebugMode"); // 缓存调试模式属性 ID，shader 通过它决定显示哪个 GBuffer 通道。
        private static readonly int DebugYFlipId = Shader.PropertyToID("_BurtGBufferDebugYFlip"); // GBuffer Debug uses raw fullscreen UV and applies this single pre-flip to match the later FinalBlit display direction.
        private Material debugGBufferMaterial; // 缓存运行时 GBuffer 调试材质，避免每帧重复创建 Material。
        private bool hasLoggedMissingShader; // 记录是否已经提示过 shader 缺失，避免 Console 每帧重复刷警告。

        public override string Name => "Burt Debug GBuffer"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源读写关系。
        {
            if (BurtGBufferDebugViewUtility.ResolveGBufferDebugViewMode(builder.Asset) == BurtGBufferDebugViewMode.Disabled) // 如果资产和 Overlay 都没有开启 GBuffer Debug，就不向图里声明依赖。
            {
                return; // 直接结束配置，避免正常 Deferred 图里出现无意义的 GBuffer Debug 资源关系。
            }

            builder.ReadGBuffer0(); // 声明调试 shader 会读取 GBuffer0 的 normal 和 roughness。
            builder.ReadGBuffer1(); // 声明调试 shader 会读取 GBuffer1 的 baseColor 和 occlusion。
            builder.ReadGBuffer2(); // 声明调试 shader 会读取 GBuffer2 的 packed material properties 和 reflectance。
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.ReadGBuffer5();
            builder.ReadCameraDepth(); // 声明调试 shader 会读取 CameraDepth，RawDepth 模式和后续重建调试会用到它。
            builder.WriteCameraColor(); // 声明调试结果会覆盖写回 CameraColor，并在 FinalBlit 前显示到最终窗口。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 GBuffer 调试视图绘制。
        {
            if (BurtGBufferDebugViewUtility.ResolveGBufferDebugViewMode(context != null ? context.Asset : null) == BurtGBufferDebugViewMode.Disabled) // 执行阶段再次检查开关，避免资产或 Overlay 热修改后继续绘制旧调试图。
            {
                return; // 未开启时直接跳过，不改变 CameraColor。
            }

            if (!TryGetRequiredTargets(context, out var cameraColorTarget, out var cameraDepthTarget, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var gbuffer3Target, out var gbuffer4Target, out var gbuffer5Target)) // 读取并验证调试所需的全部 RT。
            {
                return; // 任意目标无效时直接跳过，避免绑定或采样错误资源。
            }

            var material = GetDebugGBufferMaterial(context.Asset); // 获取或创建 GBuffer 调试材质。

            if (material == null) // 如果 shader 缺失或材质创建失败，就不能执行调试绘制。
            {
                return; // 直接跳过，保证 Deferred 正常画面不受调试 shader 缺失影响。
            }

            var debugMode = BurtGBufferDebugViewUtility.ResolveShaderDebugMode(context.Asset); // 把资产或 Overlay 的最终模式转换成 shader 可读取的整数值。
            var debugYFlip = BurtFinalBlitUtility.ResolveFinalBlitYFlip(context.Request); // GBuffer debug writes directly to CameraColor, so match the final display path.
            var cmd = CommandBufferPool.Get(Name); // 从命令缓冲池获取一个 CommandBuffer，并用 Pass 名称作为调试标记。

            cmd.SetRenderTarget(cameraColorTarget.Identifier); // 绑定 CameraColor 作为输出目标，调试图只覆盖颜色不写深度。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier); // 把当前 request 的 GBuffer0 绑定给调试 shader。
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier); // 把当前 request 的 GBuffer1 绑定给调试 shader。
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier); // 把当前 request 的 GBuffer2 绑定给调试 shader。
            cmd.SetGlobalTexture(GBuffer3Id, gbuffer3Target.Identifier);
            cmd.SetGlobalTexture(GBuffer4Id, gbuffer4Target.Identifier);
            cmd.SetGlobalTexture(GBuffer5Id, gbuffer5Target.Identifier);
            cmd.SetGlobalTexture(CameraDepthId, cameraDepthTarget.Identifier); // 把当前 request 的 CameraDepth 绑定给调试 shader。
            BurtDeferredStencilTextureUtility.BindGlobal(cmd, cameraDepthTarget, context.Request != null ? context.Request.Camera : null);
            cmd.SetGlobalFloat(DebugModeId, debugMode); // 上传调试模式，让 shader 选择显示原始 GBuffer 或解码后的材质分量。
            cmd.SetGlobalFloat(DebugYFlipId, debugYFlip); // Upload the display flip used by other direct-to-CameraColor debug views.
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1); // 绘制全屏三角形，把 GBuffer 调试结果写入 CameraColor。

            context.ScriptableContext.ExecuteCommandBuffer(cmd); // 把调试绘制命令提交给 Unity SRP 上下文。
            CommandBufferPool.Release(cmd); // 把 CommandBuffer 放回池中，避免每帧产生额外 GC。
        }

        private static bool TryGetRequiredTargets( // 安全读取 GBuffer 调试需要的全部渲染目标。
            BurtRenderGraphContext context, // 接收当前 RenderGraph 执行上下文。
            out BurtRenderTargetHandle cameraColorTarget, // 输出 CameraColor 句柄，调试结果会写入它。
            out BurtRenderTargetHandle cameraDepthTarget, // 输出 CameraDepth 句柄，RawDepth 模式会采样它。
            out BurtRenderTargetHandle gbuffer0Target, // 输出 GBuffer0 句柄。
            out BurtRenderTargetHandle gbuffer1Target, // 输出 GBuffer1 句柄。
            out BurtRenderTargetHandle gbuffer2Target, // 输出 GBuffer2 句柄。
            out BurtRenderTargetHandle gbuffer3Target,
            out BurtRenderTargetHandle gbuffer4Target,
            out BurtRenderTargetHandle gbuffer5Target)
        {
            cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName); // context 有效时读取 CameraColor，否则返回无效句柄。
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName); // context 有效时读取 CameraDepth，否则返回无效句柄。
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name); // context 有效时读取 GBuffer0，否则返回无效句柄。
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name); // context 有效时读取 GBuffer1，否则返回无效句柄。
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name); // context 有效时读取 GBuffer2，否则返回无效句柄。
            gbuffer3Target = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4Target = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            gbuffer5Target = context != null ? context.GBuffer5Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer5Name);

            return cameraColorTarget.IsValid && cameraDepthTarget.IsValid && gbuffer0Target.IsValid && gbuffer1Target.IsValid && gbuffer2Target.IsValid && gbuffer3Target.IsValid && gbuffer4Target.IsValid && gbuffer5Target.IsValid; // 只有全部目标有效时才允许绘制调试视图。
        }

        private Material GetDebugGBufferMaterial(BurtRenderPipelineAsset asset) // 获取或创建 GBuffer 调试材质。
        {
            if (debugGBufferMaterial != null) // 如果之前已经创建过材质，就复用它。
            {
                return debugGBufferMaterial; // 返回缓存材质，避免每帧创建新对象。
            }

            var shader = asset != null && asset.RuntimeResources != null
                ? asset.RuntimeResources.DebugGBufferShader
                : null;
            if (shader == null)
            {
                shader = Shader.Find(DebugGBufferShaderName); // 仅作为旧资产尚未绑定 Runtime Resources 时的编辑器兼容回退。
            }

            if (shader == null) // 如果 shader 查找失败，说明 shader 文件还没导入或名称不一致。
            {
                if (!hasLoggedMissingShader) // 如果还没有输出过缺失提示，就只输出一次。
                {
                    Debug.LogWarning("BurtRP could not find shader: " + DebugGBufferShaderName); // 输出缺失 shader 警告，方便定位 shader 侧接入进度。
                    hasLoggedMissingShader = true; // 标记已经输出过警告，避免每帧刷屏。
                }

                return null; // 返回空材质，让 Execute 安全跳过。
            }

            debugGBufferMaterial = new Material(shader); // 使用找到的 shader 创建运行时材质。
            debugGBufferMaterial.hideFlags = HideFlags.HideAndDontSave; // 隐藏运行时材质并避免保存到场景或资产。
            return debugGBufferMaterial; // 返回创建好的调试材质。
        }
    }
}
