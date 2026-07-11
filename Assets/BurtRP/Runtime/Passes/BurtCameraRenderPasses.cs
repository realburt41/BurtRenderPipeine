using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Color、ShaderTagId 等 Unity 类型。
using UnityEngine.Rendering; // 引入 UnityEngine.Rendering 命名空间，用来使用 CommandBufferPool、DrawingSettings、FilteringSettings 等 SRP 类型。

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这些 Pass 能直接访问 BurtRenderRequest、BurtCameraData 等类型。
{
    internal static class BurtDrawingSettingsUtility // 集中管理 BurtRP 所有 DrawRenderers Pass 会用到的 ShaderTagId 和 DrawingSettings 创建逻辑。
    {
        private static readonly ShaderTagId BurtForward = new ShaderTagId("BurtForward"); // 定义 BurtRP 前向颜色 Pass 的 LightMode 名称，受支持的 shader 必须提供它。

        private static readonly ShaderTagId BurtForwardOnly = new ShaderTagId("BurtForwardOnly"); // 定义 Deferred 后专用的前向兜底 LightMode，只给不能写 GBuffer 的不透明 shader 使用。

        private static readonly ShaderTagId BurtDepthOnly = new ShaderTagId("BurtDepthOnly"); // 定义 BurtRP Depth Prepass 使用的深度专用 LightMode 名称。
        private static readonly ShaderTagId BurtDepthNormals = new ShaderTagId("BurtDepthNormals");

        private static readonly ShaderTagId BurtGBuffer = new ShaderTagId("BurtGBuffer"); // 定义 Deferred GBuffer 绘制使用的 LightMode 名称，shader 侧需要提供同名 Pass。
        private static readonly ShaderTagId BurtSubsurfaceForward = new ShaderTagId("BurtSubsurfaceForward");

        private static readonly PerObjectData ForwardPerObjectData = // 定义前向颜色绘制需要 Unity 为每个 Renderer 绑定的内置间接光数据。
            PerObjectData.ReflectionProbes | // 请求 Unity 绑定 unity_SpecCube0 / unity_SpecCube0_HDR，让 Reflection Probe 间接高光能生效。
            PerObjectData.Lightmaps | // 请求 Unity 绑定光照贴图相关数据，给后续接入 baked GI 预留正确 per-object 数据。
            PerObjectData.LightProbe | // 请求 Unity 绑定 unity_SHAr 等 SH 数据，让 ShadeSH9 能读到 Light Probe / Ambient Probe。
            PerObjectData.LightProbeProxyVolume | // 请求 Unity 绑定 LPPV 数据，避免使用 Light Probe Proxy Volume 的物体丢失间接光。
            PerObjectData.LightData | // 请求 Unity 绑定基础 per-object 光照数据，保持和 URP 常见配置一致。
            PerObjectData.OcclusionProbe | // 请求 Unity 绑定 probe occlusion 数据，后续如果接入 shadow mask/AO 可直接读取。
            PerObjectData.OcclusionProbeProxyVolume | // 请求 Unity 绑定 LPPV 版本的 occlusion 数据，和 LightProbeProxyVolume 配套。
            PerObjectData.ShadowMask; // 请求 Unity 绑定 shadow mask 数据，给后续 baked shadow / mixed lighting 预留。

        private static readonly ShaderTagId SRPDefaultUnlit = new ShaderTagId("SRPDefaultUnlit"); // 定义 Unity 通用 SRP Unlit LightMode，方便 Unsupported Pass 把它抓出来显示错误材质。

        private static readonly ShaderTagId ForwardBase = new ShaderTagId("ForwardBase"); // 定义 Built-in 管线 ForwardBase LightMode，方便 Unsupported Pass 抓到旧管线材质。

        private static readonly ShaderTagId Always = new ShaderTagId("Always"); // 定义 Built-in 常见 Always LightMode，避免 fallback pass 被 BurtRP 静默接受。

        private static readonly ShaderTagId PrepassBase = new ShaderTagId("PrepassBase"); // 定义旧 Built-in deferred prepass LightMode，方便 Unsupported Pass 暴露不兼容材质。

        private static readonly ShaderTagId Vertex = new ShaderTagId("Vertex"); // 定义旧 Built-in 顶点光照 LightMode，方便 Unsupported Pass 暴露不兼容材质。

        private static readonly ShaderTagId VertexLMRGBM = new ShaderTagId("VertexLMRGBM"); // 定义旧 Built-in lightmap RGBM LightMode，方便 Unsupported Pass 暴露不兼容材质。

        private static readonly ShaderTagId VertexLM = new ShaderTagId("VertexLM"); // 定义另一个旧 Built-in lightmap LightMode，方便 Unsupported Pass 暴露不兼容材质。

        private static readonly ShaderTagId UniversalForward = new ShaderTagId("UniversalForward"); // 定义 URP Forward LightMode，避免 URP shader 被 BurtRP 静默当作可支持材质。

        private static readonly ShaderTagId UniversalForwardOnly = new ShaderTagId("UniversalForwardOnly"); // 定义 URP ForwardOnly LightMode，方便 Unsupported Pass 报告 URP 专用材质。

        private static readonly ShaderTagId LightweightForward = new ShaderTagId("LightweightForward"); // 定义旧 LWRP Forward LightMode，方便 Unsupported Pass 报告旧 SRP 材质。
        private static readonly int UnityWorldToCameraId = Shader.PropertyToID("unity_WorldToCamera");
        private static readonly int UnityCameraToWorldId = Shader.PropertyToID("unity_CameraToWorld");





        private static readonly ShaderTagId[] EditorPreviewShaderTagIds = new ShaderTagId[] // Preview 只允许选择能输出最终颜色的 shading pass；GBuffer/DepthNormals 必须留在完整 Deferred 管线内部。
        {
            BurtForward, // BurtRP 自己的材质预览仍优先走正式前向 Pass。
            BurtForwardOnly, // 允许只实现 ForwardOnly 的 BurtRP 特殊材质在 Preview 中可见。
            SRPDefaultUnlit, // Unity/SRP 默认未标记 Pass 会落到这里，Inspector 预览大量使用它。
            ForwardBase, // Built-in 预览 shader 常见的前向 Pass 名称。
            Always, // Unity 内部预览 shader 常用 Always Pass。
            UniversalForward, // 兼容 URP 风格预览 shader。
            UniversalForwardOnly, // 兼容 URP ForwardOnly 风格预览 shader。
            LightweightForward, // 兼容旧 LWRP 风格预览 shader。
            Vertex, // 兼容 Built-in 顶点光照 fallback。
            VertexLMRGBM, // 兼容 Built-in lightmap RGBM fallback。
            VertexLM, // 兼容 Built-in lightmap fallback。
            PrepassBase, // 兜底旧 Built-in deferred prepass 名称，避免预览物体静默消失。
        };

        private static readonly ShaderTagId[] UnsupportedShaderTagIds = new ShaderTagId[] // 列出 BurtRP 不接管的 LightMode，这些材质会被错误材质明确显示出来。
        {
            SRPDefaultUnlit, // 把通用 SRP Unlit shader 视为不支持，除非它迁移到 BurtForward。
            ForwardBase, // 把 Built-in ForwardBase shader 视为不支持，避免旧材质静默进入 BurtRP。
            Always, // 把 Built-in Always pass 视为不支持，让 fallback 风格 shader 明确变成错误材质。
            PrepassBase, // 把旧 Built-in PrepassBase 视为不支持，因为 BurtRP 不实现这条路径。
            Vertex, // 把旧 Built-in 顶点光照 pass 视为不支持，因为 BurtRP 暂不实现 vertex lighting。
            VertexLMRGBM, // 把旧 Built-in RGBM lightmap pass 视为不支持，因为 BurtRP 暂未接入这条 lightmap 路径。
            VertexLM, // 把另一个旧 Built-in lightmap pass 视为不支持，理由同上。
            UniversalForward, // 把 URP Forward shader 视为不支持，因为 BurtRP 应该使用自己的 LightMode 名称。
            UniversalForwardOnly, // 把 URP ForwardOnly shader 视为不支持，因为 BurtRP 不执行 URP 专用 pass。
            LightweightForward // 把旧 LWRP shader 视为不支持，因为 BurtRP 不执行 LWRP 专用 pass。
        }; // 结束不支持 LightMode 列表。

        private static readonly ShaderTagId BurtRefractionDistortion = new ShaderTagId("BurtRefractionDistortion");

        public static DrawingSettings CreateForwardDrawingSettings(SortingSettings sortingSettings) // 创建 BurtRP 常规前向颜色绘制使用的 DrawingSettings。
        {
            var drawingSettings = new DrawingSettings(BurtForward, sortingSettings); // 只匹配 BurtForward，让主渲染路径严格由 BurtRP 自己的 shader pass 驱动。

            drawingSettings.perObjectData = ForwardPerObjectData; // 让 Unity 在 DrawRenderers 时真正上传 SH、Reflection Probe 等 per-object 间接光数据。

            return drawingSettings; // 返回配置好的前向绘制设置，供调用方 Pass 使用。
        }

        public static DrawingSettings CreateRefractionDistortionDrawingSettings(SortingSettings sortingSettings)
        {
            var drawingSettings = new DrawingSettings(BurtRefractionDistortion, sortingSettings);
            drawingSettings.perObjectData = PerObjectData.None;
            return drawingSettings;
        }

        public static DrawingSettings CreateForwardOnlyDrawingSettings(SortingSettings sortingSettings) // 创建 Deferred 后前向兜底绘制设置，只匹配显式声明 BurtForwardOnly 的 shader。
        {
            var drawingSettings = new DrawingSettings(BurtForwardOnly, sortingSettings); // 只匹配 BurtForwardOnly，避免 Deferred 模式把已经写入 GBuffer 的 BurtForward 物体再画一遍。

            drawingSettings.perObjectData = ForwardPerObjectData; // 兜底前向 shader 仍需要 SH、Reflection Probe 等 per-object 数据，保证和正常 Forward 光照能力一致。

            return drawingSettings; // 返回配置好的绘制设置，供 Deferred ForwardOnly Opaque Pass 使用。
        }

        public static DrawingSettings CreateUnsupportedDrawingSettings( // 创建 Unsupported Shader Debug Pass 使用的 DrawingSettings。
            SortingSettings sortingSettings, // 接收当前相机的排序规则，保证错误材质绘制顺序稳定。
            Material errorMaterial) // 接收用于覆盖不支持材质的错误材质。
        {
            var drawingSettings = new DrawingSettings(UnsupportedShaderTagIds[0], sortingSettings); // 使用第一个不支持 LightMode 作为 DrawingSettings 的主 shader pass 名称。

            for (var shaderTagIndex = 1; shaderTagIndex < UnsupportedShaderTagIds.Length; shaderTagIndex++) // 遍历剩余所有不支持的 LightMode。
            {
                drawingSettings.SetShaderPassName(shaderTagIndex, UnsupportedShaderTagIds[shaderTagIndex]); // 把当前不支持 LightMode 注册到对应 DrawingSettings 槽位。
            }

            drawingSettings.overrideMaterial = errorMaterial; // 强制匹配到的不支持 shader 使用 Unity 错误材质绘制。

            return drawingSettings; // 返回配置好的不支持 shader 绘制设置，供调用方 Pass 使用。
        }

        public static DrawingSettings CreateEditorPreviewDrawingSettings(SortingSettings sortingSettings) // 创建 Unity Editor Preview 专用绘制设置。
        {
            var drawingSettings = new DrawingSettings(EditorPreviewShaderTagIds[0], sortingSettings); // Preview 首选 BurtForward，同时继续注册只会输出最终颜色的兼容 Pass。

            for (var shaderTagIndex = 1; shaderTagIndex < EditorPreviewShaderTagIds.Length; shaderTagIndex++) // 把兼容 LightMode 全部注册进 DrawingSettings。
            {
                drawingSettings.SetShaderPassName(shaderTagIndex, EditorPreviewShaderTagIds[shaderTagIndex]); // 允许 Cubemap/ReflectionProbe 预览使用内置 shader 绘制。
            }

            drawingSettings.perObjectData = ForwardPerObjectData; // 如果预览的是 BurtRP 材质，仍然给它绑定基础 per-object 间接光数据。

            return drawingSettings; // 返回配置好的 Preview 绘制设置。
        }

        public static bool BindCameraColorAndDepth(BurtRenderGraphContext context, string commandBufferName) // DrawRenderers 前显式恢复颜色和深度附件，避免前一个全屏 Pass 只绑定 CameraColor 后丢失深度测试。
        {
            if (context == null) // 没有执行上下文时无法读取 RenderGraph 注册的目标。
            {
                return false;
            }

            var cameraColorTarget = context.CameraColorTarget; // 读取当前 request 的中间颜色 RT。
            var cameraDepthTarget = context.CameraDepthTarget; // 读取当前 request 的深度 RT。
            if (!cameraColorTarget.IsValid || !cameraDepthTarget.IsValid) // 任一目标无效时跳过绘制，避免 DrawRenderers 继承错误 RT 状态。
            {
                return false;
            }

            var cmd = CommandBufferPool.Get(commandBufferName); // 用当前 Pass 名创建临时命令缓冲，Frame Debugger 里能看到这次目标恢复。
            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier); // 同时绑定颜色和深度，让 ZTest/ZWrite 对后续 DrawRenderers 生效。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null); // 恢复 viewport，避免前序 RT 改过尺寸后影响绘制。
            RestoreCameraMatricesForMainDraw(context, cmd);
            BindMainLightShadowMapIfValid(context, cmd); // DrawRenderers 可能在 Deferred Lighting 之后执行，重新绑定当前 request 的 shadow map 避免读到旧全局纹理。
            BindAdditionalLightShadowAtlasIfValid(context, cmd);
            BindPerObjectShadowAtlasIfValid(context, cmd);
            context.ScriptableContext.ExecuteCommandBuffer(cmd); // 立即提交目标绑定，后面的 DrawRenderers 会使用这个状态。
            CommandBufferPool.Release(cmd); // 释放临时命令缓冲，避免每帧 GC。
            return true;
        }

        public static void RestoreCameraMatricesForMainDraw(BurtRenderGraphContext context, CommandBuffer cmd)
        {
            var temporalAA = context != null && context.Request != null ? context.Request.TemporalAA : null;
            if (cmd == null || temporalAA == null || !temporalAA.Enabled)
            {
                return;
            }

            cmd.SetViewProjectionMatrices(temporalAA.ViewMatrix, temporalAA.JitteredProjectionMatrix);
            var worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * temporalAA.ViewMatrix;
            cmd.SetGlobalMatrix(UnityWorldToCameraId, worldToCameraMatrix);
            cmd.SetGlobalMatrix(UnityCameraToWorldId, worldToCameraMatrix.inverse);
        }

        public static bool IsTemporalAAEnabled(BurtRenderGraphContext context)
        {
            var temporalAA = context != null && context.Request != null ? context.Request.TemporalAA : null;
            return temporalAA != null && temporalAA.Enabled;
        }

        public static void BindMainLightShadowMapIfValid(BurtRenderGraphContext context, CommandBuffer cmd) // 把当前 request 的主光 shadow map 绑定到全局纹理槽。
        {
            if (context == null || cmd == null) // 没有执行上下文或命令缓冲时无法安全绑定。
            {
                return; // 直接跳过，调用方会继续使用已有的无阴影默认状态。
            }

            var shadowMapTarget = context.MainLightShadowMapTarget; // 从 RenderGraph 资源表读取当前 request 的主光 shadow map。
            if (!shadowMapTarget.IsValid) // 当前 request 没有生成主光阴影图时不能绑定无效句柄。
            {
                return; // 保持 Setup Lighting 上传的无阴影默认参数。
            }

            BurtMainLightShadowMatrixUtility.BindMainLightShadowMapIfValid(cmd, shadowMapTarget); // 确保 Forward/ForwardOnly/Transparent 采样的是当前 request 的阴影图。
        }

        public static void BindAdditionalLightShadowAtlasIfValid(BurtRenderGraphContext context, CommandBuffer cmd)
        {
            if (context == null || cmd == null)
            {
                return;
            }

            BurtAdditionalLightShadowUtility.BindAdditionalLightShadowAtlasIfValid(cmd, context.AdditionalLightShadowAtlasTarget);
        }

        public static void BindPerObjectShadowAtlasIfValid(BurtRenderGraphContext context, CommandBuffer cmd)
        {
            if (context == null || cmd == null)
            {
                return;
            }

            BurtPerObjectShadowUtility.BindPerObjectShadowAtlasIfValid(cmd, context.PerObjectShadowAtlasTarget);
        }

        public static DrawingSettings CreateDepthDrawingSettings(SortingSettings sortingSettings) // 创建 BurtRP 深度预写使用的 DrawingSettings。
        {
            var drawingSettings = new DrawingSettings(BurtDepthOnly, sortingSettings); // 只匹配 BurtDepthOnly，避免 Depth Prepass 意外执行颜色 pass。

            return drawingSettings; // 返回配置好的深度绘制设置，供调用方 Pass 使用。
        }

        public static DrawingSettings CreateDepthNormalsDrawingSettings(SortingSettings sortingSettings)
        {
            var drawingSettings = new DrawingSettings(BurtDepthNormals, sortingSettings);
            drawingSettings.SetShaderPassName(1, BurtDepthOnly);

            return drawingSettings;
        }

        public static DrawingSettings CreateGBufferDrawingSettings(SortingSettings sortingSettings) // 创建 Deferred GBuffer 绘制设置，只匹配 BurtRP 自己的 GBuffer Pass。
        {
            var drawingSettings = new DrawingSettings(BurtGBuffer, sortingSettings); // 只绘制 LightMode 为 BurtGBuffer 的 shader pass，避免 Forward pass 误写入 GBuffer。

            drawingSettings.perObjectData = PerObjectData.None; // GBuffer 只负责写材质属性，不再请求 SH/ReflectionProbe，避免 Deferred 间接光继续依赖 DrawRenderers 的 per-object 副作用。

            return drawingSettings; // 返回配置好的 GBuffer 绘制设置，供 Draw GBuffer Opaque Pass 使用。
        }

        public static DrawingSettings CreateSubsurfaceForwardDrawingSettings(SortingSettings sortingSettings)
        {
            var drawingSettings = new DrawingSettings(BurtSubsurfaceForward, sortingSettings);
            drawingSettings.perObjectData = PerObjectData.None;
            return drawingSettings;
        }
    }

    internal static class BurtShadowRenderTargetUtility
    {
        public static float ResolveMainLightShadowClearDepth()
        {
            return 1f;
        }

        public static void SetDepthOnlyShadowRenderTarget(CommandBuffer cmd, BurtRenderTargetHandle target)
        {
            if (cmd == null || !target.IsValid)
            {
                return;
            }

            cmd.SetRenderTarget(target.Identifier);
        }

        public static bool HasShadowCasters(CullingResults cullingResults, int visibleLightIndex)
        {
            if (visibleLightIndex < 0 || visibleLightIndex >= cullingResults.visibleLights.Length)
            {
                return false;
            }

            return cullingResults.GetShadowCasterBounds(visibleLightIndex, out _);
        }
    }

    internal static class BurtMainLightShadowMatrixUtility
    {
        public const int MaxCascadeCount = BurtShadowData.MaxMainLightShadowCascadeCount;
        private static readonly int MainLightWorldToShadowId = Shader.PropertyToID("_BurtMainLightWorldToShadow");
        private static readonly int MainLightWorldToShadowRow0Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRow0");
        private static readonly int MainLightWorldToShadowRow1Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRow1");
        private static readonly int MainLightWorldToShadowRow2Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRow2");
        private static readonly int MainLightWorldToShadowRow3Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRow3");
        private static readonly int MainLightWorldToShadowMatricesId = Shader.PropertyToID("_BurtMainLightWorldToShadowMatrices");
        private static readonly int MainLightWorldToShadowRows0Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRows0");
        private static readonly int MainLightWorldToShadowRows1Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRows1");
        private static readonly int MainLightWorldToShadowRows2Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRows2");
        private static readonly int MainLightWorldToShadowRows3Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRows3");
        private static readonly int MainLightShadowCascadeSpheresId = Shader.PropertyToID("_BurtMainLightShadowCascadeSpheres");
        private static readonly int MainLightShadowCascadeAtlasRectsId = Shader.PropertyToID("_BurtMainLightShadowCascadeAtlasRects");
        private static readonly int MainLightShadowCascadeParamsId = Shader.PropertyToID("_BurtMainLightShadowCascadeParams");
        private static readonly int MainLightShadowStrengthId = Shader.PropertyToID("_BurtMainLightShadowStrength");
        private static readonly int MainLightShadowTexelSizeId = Shader.PropertyToID("_BurtMainLightShadowTexelSize");
        private static readonly int MainLightShadowSampleBiasId = Shader.PropertyToID("_BurtMainLightShadowSampleBias");
        private static readonly int MainLightShadowSoftnessId = Shader.PropertyToID("_BurtMainLightShadowSoftness");
        private static readonly int MainLightShadowPCSSParamsId = Shader.PropertyToID("_BurtMainLightShadowPCSSParams");
        private static readonly int MainLightShadowReceiverBiasParamsId = Shader.PropertyToID("_BurtMainLightShadowReceiverBiasParams");

        private static readonly Matrix4x4[] DisabledWorldToShadowMatrices = CreateIdentityMatrixArray();
        private static readonly Vector4[] DisabledCascadeSpheres = CreateDisabledCascadeSpheres();
        private static readonly Vector4[] DisabledCascadeAtlasRects = CreateDisabledCascadeAtlasRects();
        private static readonly Vector4[] WorldToShadowRows0 = new Vector4[MaxCascadeCount];
        private static readonly Vector4[] WorldToShadowRows1 = new Vector4[MaxCascadeCount];
        private static readonly Vector4[] WorldToShadowRows2 = new Vector4[MaxCascadeCount];
        private static readonly Vector4[] WorldToShadowRows3 = new Vector4[MaxCascadeCount];

        public static bool TryGetMainLightShadowMatrices(
            BurtRenderRequest request,
            BurtShadowData shadowData,
            out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix,
            out ShadowSplitData splitData)
        {
            if (TryGetMainLightShadowCascadeCache(request, shadowData, out var cascadeCache))
            {
                viewMatrix = cascadeCache.ViewMatrices[0];
                projectionMatrix = cascadeCache.ProjectionMatrices[0];
                splitData = cascadeCache.SplitDatas[0];
                return true;
            }

            viewMatrix = Matrix4x4.identity;
            projectionMatrix = Matrix4x4.identity;
            splitData = default;
            return false;
        }

        public static bool TryGetMainLightShadowCascadeCache(
            BurtRenderRequest request,
            BurtShadowData shadowData,
            out BurtMainLightShadowCascadeCache cascadeCache)
        {
            cascadeCache = request != null ? request.MainLightShadowCascadeCache : null;
            if (request == null || cascadeCache == null || shadowData == null || !shadowData.HasMainLightShadow || shadowData.MainLightIndex < 0)
            {
                cascadeCache?.Clear();
                return false;
            }

            if (cascadeCache.Matches(shadowData))
            {
                return true;
            }

            cascadeCache.Clear();
            var cascadeCount = BurtShadowUtility.ResolveMainLightShadowCascadeCount(shadowData);
            var tileResolution = BurtShadowUtility.ResolveMainLightShadowTileResolution(shadowData);
            var atlasResolution = BurtShadowUtility.ResolveMainLightShadowAtlasResolution(shadowData);
            if (cascadeCount <= 0 || tileResolution <= 0 || atlasResolution <= 0)
            {
                return false;
            }

            var cascadeSplit = shadowData.CreateMainLightShadowCascadeSplitVector();
            for (var cascadeIndex = 0; cascadeIndex < cascadeCount; cascadeIndex++)
            {
                if (!request.CullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                        shadowData.MainLightIndex,
                        cascadeIndex,
                        cascadeCount,
                        cascadeSplit,
                        tileResolution,
                        shadowData.MainLightShadowNearPlane,
                        out var viewMatrix,
                        out var projectionMatrix,
                        out var splitData))
                {
                    cascadeCache.Clear();
                    return false;
                }

                StabilizeDirectionalShadowProjection(viewMatrix, ref projectionMatrix, tileResolution);

                var cascadeAtlasRect = ResolveCascadeAtlasRect(cascadeIndex, cascadeCount);
                cascadeCache.ViewMatrices[cascadeIndex] = viewMatrix;
                cascadeCache.ProjectionMatrices[cascadeIndex] = projectionMatrix;
                cascadeCache.SplitDatas[cascadeIndex] = splitData;
                cascadeCache.CascadeAtlasRects[cascadeIndex] = cascadeAtlasRect;
                cascadeCache.WorldToShadowMatrices[cascadeIndex] = CreateWorldToShadowMatrix(viewMatrix, projectionMatrix, cascadeAtlasRect);

                var cullingSphere = splitData.cullingSphere;
                var sphereRadius = Mathf.Max(0f, cullingSphere.w);
                cascadeCache.CascadeSpheres[cascadeIndex] = new Vector4(cullingSphere.x, cullingSphere.y, cullingSphere.z, sphereRadius * sphereRadius);
            }

            cascadeCache.Store(shadowData, cascadeCount, tileResolution, atlasResolution);
            return true;
        }

        public static bool TryGetMainLightShadowCascades(
            BurtRenderRequest request,
            BurtShadowData shadowData,
            Matrix4x4[] viewMatrices,
            Matrix4x4[] projectionMatrices,
            Matrix4x4[] worldToShadowMatrices,
            ShadowSplitData[] splitDatas,
            Vector4[] cascadeSpheres,
            Vector4[] cascadeAtlasRects,
            out int cascadeCount,
            out int tileResolution,
            out int atlasResolution)
        {
            cascadeCount = 0;
            tileResolution = 0;
            atlasResolution = 0;
            ResetCascadeOutputs(viewMatrices, projectionMatrices, worldToShadowMatrices, splitDatas, cascadeSpheres, cascadeAtlasRects);

            if (!HasValidCascadeOutputArrays(viewMatrices, projectionMatrices, worldToShadowMatrices, splitDatas, cascadeSpheres, cascadeAtlasRects))
            {
                return false;
            }

            if (!TryGetMainLightShadowCascadeCache(request, shadowData, out var cascadeCache))
            {
                return false;
            }

            cascadeCount = cascadeCache.CascadeCount;
            tileResolution = cascadeCache.TileResolution;
            atlasResolution = cascadeCache.AtlasResolution;
            CopyCascadeOutputs(cascadeCache, viewMatrices, projectionMatrices, worldToShadowMatrices, splitDatas, cascadeSpheres, cascadeAtlasRects);
            return true;
        }

        public static Matrix4x4 CreateWorldToShadowMatrix(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            return CreateWorldToShadowMatrix(viewMatrix, projectionMatrix, new Vector4(0f, 0f, 1f, 1f));
        }

        public static Matrix4x4 CreateWorldToShadowMatrix(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, Vector4 atlasRect)
        {
            var worldToShadow = CreateWorldToShadowMatrixNoAtlas(viewMatrix, projectionMatrix);
            return CreateAtlasSliceTransform(atlasRect) * worldToShadow;
        }

        public static Matrix4x4 CreateWorldToShadowMatrixNoAtlas(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            // ComputeDirectionalShadowMatricesAndCullingPrimitives already returns the projection
            // convention consumed by the shadow caster draw. Match Unity's SRP shadow transform:
            // only reverse the Z row on reversed-Z platforms before forming world-to-shadow.
            if (SystemInfo.usesReversedZBuffer)
            {
                projectionMatrix.m20 = -projectionMatrix.m20;
                projectionMatrix.m21 = -projectionMatrix.m21;
                projectionMatrix.m22 = -projectionMatrix.m22;
                projectionMatrix.m23 = -projectionMatrix.m23;
            }

            var worldToShadow = projectionMatrix * viewMatrix;

            var textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m22 = 0.5f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;
            textureScaleAndBias.m23 = 0.5f;

            return textureScaleAndBias * worldToShadow;
        }

        public static Matrix4x4 CreateAtlasSliceTransform(Vector4 atlasRect)
        {
            var atlasScaleAndBias = Matrix4x4.identity;
            atlasScaleAndBias.m00 = Mathf.Max(0f, atlasRect.z - atlasRect.x);
            atlasScaleAndBias.m11 = Mathf.Max(0f, atlasRect.w - atlasRect.y);
            atlasScaleAndBias.m03 = atlasRect.x;
            atlasScaleAndBias.m13 = atlasRect.y;
            return atlasScaleAndBias;
        }

        public static void SetMainLightWorldToShadow(CommandBuffer cmd, Matrix4x4 matrix)
        {
            if (cmd == null)
            {
                return;
            }

            cmd.SetGlobalMatrix(MainLightWorldToShadowId, matrix);
            cmd.SetGlobalVector(MainLightWorldToShadowRow0Id, matrix.GetRow(0));
            cmd.SetGlobalVector(MainLightWorldToShadowRow1Id, matrix.GetRow(1));
            cmd.SetGlobalVector(MainLightWorldToShadowRow2Id, matrix.GetRow(2));
            cmd.SetGlobalVector(MainLightWorldToShadowRow3Id, matrix.GetRow(3));
        }

        public static void SetMainLightWorldToShadow(CommandBuffer cmd, Material material, Matrix4x4 matrix)
        {
            if (material != null)
            {
                material.SetMatrix(MainLightWorldToShadowId, matrix);
                material.SetVector(MainLightWorldToShadowRow0Id, matrix.GetRow(0));
                material.SetVector(MainLightWorldToShadowRow1Id, matrix.GetRow(1));
                material.SetVector(MainLightWorldToShadowRow2Id, matrix.GetRow(2));
                material.SetVector(MainLightWorldToShadowRow3Id, matrix.GetRow(3));
            }

            SetMainLightWorldToShadow(cmd, matrix);
        }

        public static void UploadMainLightShadowCascadeGlobals(
            CommandBuffer cmd,
            Matrix4x4[] worldToShadowMatrices,
            Vector4[] cascadeSpheres,
            Vector4[] cascadeAtlasRects,
            int cascadeCount,
            int tileResolution,
            BurtShadowData shadowData)
        {
            UploadMainLightShadowCascadeGlobals(cmd, null, worldToShadowMatrices, cascadeSpheres, cascadeAtlasRects, cascadeCount, tileResolution, shadowData);
        }

        public static void UploadMainLightShadowCascadeGlobals(
            CommandBuffer cmd,
            Material material,
            Matrix4x4[] worldToShadowMatrices,
            Vector4[] cascadeSpheres,
            Vector4[] cascadeAtlasRects,
            int cascadeCount,
            int tileResolution,
            BurtShadowData shadowData)
        {
            var safeCascadeCount = Mathf.Clamp(cascadeCount, 0, MaxCascadeCount);
            var safeMatrices = worldToShadowMatrices != null && worldToShadowMatrices.Length >= MaxCascadeCount ? worldToShadowMatrices : DisabledWorldToShadowMatrices;
            var safeSpheres = cascadeSpheres != null && cascadeSpheres.Length >= MaxCascadeCount ? cascadeSpheres : DisabledCascadeSpheres;
            var safeRects = cascadeAtlasRects != null && cascadeAtlasRects.Length >= MaxCascadeCount ? cascadeAtlasRects : DisabledCascadeAtlasRects;
            var cascadeParams = CreateMainLightShadowCascadeParams(shadowData, safeCascadeCount, tileResolution);

            FillWorldToShadowRows(safeMatrices);
            SetMainLightWorldToShadow(cmd, material, safeCascadeCount > 0 ? safeMatrices[0] : Matrix4x4.identity);

            if (material != null)
            {
                material.SetMatrixArray(MainLightWorldToShadowMatricesId, safeMatrices);
                material.SetVectorArray(MainLightWorldToShadowRows0Id, WorldToShadowRows0);
                material.SetVectorArray(MainLightWorldToShadowRows1Id, WorldToShadowRows1);
                material.SetVectorArray(MainLightWorldToShadowRows2Id, WorldToShadowRows2);
                material.SetVectorArray(MainLightWorldToShadowRows3Id, WorldToShadowRows3);
                material.SetVectorArray(MainLightShadowCascadeSpheresId, safeSpheres);
                material.SetVectorArray(MainLightShadowCascadeAtlasRectsId, safeRects);
                material.SetVector(MainLightShadowCascadeParamsId, cascadeParams);
            }

            if (cmd != null)
            {
                cmd.SetGlobalMatrixArray(MainLightWorldToShadowMatricesId, safeMatrices);
                cmd.SetGlobalVectorArray(MainLightWorldToShadowRows0Id, WorldToShadowRows0);
                cmd.SetGlobalVectorArray(MainLightWorldToShadowRows1Id, WorldToShadowRows1);
                cmd.SetGlobalVectorArray(MainLightWorldToShadowRows2Id, WorldToShadowRows2);
                cmd.SetGlobalVectorArray(MainLightWorldToShadowRows3Id, WorldToShadowRows3);
                cmd.SetGlobalVectorArray(MainLightShadowCascadeSpheresId, safeSpheres);
                cmd.SetGlobalVectorArray(MainLightShadowCascadeAtlasRectsId, safeRects);
                cmd.SetGlobalVector(MainLightShadowCascadeParamsId, cascadeParams);
            }
        }

        public static void ClearMainLightShadowCascadeGlobals(CommandBuffer cmd)
        {
            ClearMainLightShadowCascadeGlobals(cmd, null);
        }

        public static void ClearMainLightShadowCascadeGlobals(CommandBuffer cmd, Material material)
        {
            UploadMainLightShadowCascadeGlobals(cmd, material, DisabledWorldToShadowMatrices, DisabledCascadeSpheres, DisabledCascadeAtlasRects, 0, 1, null);
        }

        public static void UploadMainLightShadowReceiverGlobals(
            CommandBuffer cmd,
            Material material,
            BurtRenderTargetHandle shadowMapTarget,
            Matrix4x4[] worldToShadowMatrices,
            Vector4[] cascadeSpheres,
            Vector4[] cascadeAtlasRects,
            int cascadeCount,
            int tileResolution,
            BurtShadowData shadowData)
        {
            UploadMainLightShadowReceiverGlobals(cmd, material, worldToShadowMatrices, cascadeSpheres, cascadeAtlasRects, cascadeCount, tileResolution, shadowData);
            BindMainLightShadowMapIfValid(cmd, shadowMapTarget);
        }

        public static void UploadMainLightShadowReceiverGlobals(
            CommandBuffer cmd,
            Material material,
            Matrix4x4[] worldToShadowMatrices,
            Vector4[] cascadeSpheres,
            Vector4[] cascadeAtlasRects,
            int cascadeCount,
            int tileResolution,
            BurtShadowData shadowData)
        {
            if (shadowData == null || !shadowData.HasMainLightShadow)
            {
                ClearMainLightShadowReceiverGlobals(cmd, material);
                return;
            }

            UploadMainLightShadowCascadeGlobals(cmd, material, worldToShadowMatrices, cascadeSpheres, cascadeAtlasRects, cascadeCount, tileResolution, shadowData);
            UploadMainLightShadowSamplingGlobals(cmd, material, shadowData);
        }

        public static void ClearMainLightShadowReceiverGlobals(CommandBuffer cmd)
        {
            ClearMainLightShadowReceiverGlobals(cmd, null);
        }

        public static void ClearMainLightShadowReceiverGlobals(CommandBuffer cmd, Material material)
        {
            ClearMainLightShadowCascadeGlobals(cmd, material);
            SetVector(cmd, material, MainLightShadowTexelSizeId, Vector4.zero);
            SetFloat(cmd, material, MainLightShadowSampleBiasId, 0f);
            SetFloat(cmd, material, MainLightShadowSoftnessId, 0f);
            SetVector(cmd, material, MainLightShadowPCSSParamsId, Vector4.zero);
            SetVector(cmd, material, MainLightShadowReceiverBiasParamsId, Vector4.zero);
            SetFloat(cmd, material, MainLightShadowStrengthId, 0f);
        }

        public static void BindMainLightShadowMapIfValid(CommandBuffer cmd, BurtRenderTargetHandle shadowMapTarget)
        {
            if (cmd == null || !shadowMapTarget.IsValid)
            {
                return;
            }

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.MainLightShadowMapId, shadowMapTarget.Identifier);
        }

        public static Vector4 CreateMainLightShadowPCSSParams(BurtShadowData shadowData)
        {
            if (shadowData == null || !shadowData.HasMainLightShadow || !shadowData.IsMainLightShadowHard || !shadowData.EnableMainLightShadowPCSS)
            {
                return Vector4.zero;
            }

            return new Vector4(1f, Mathf.Max(0f, shadowData.MainLightShadowPCSSLightSize), Mathf.Max(0f, shadowData.MainLightShadowPCSSBlockerSearchRadius), Mathf.Max(0f, shadowData.MainLightShadowPCSSMaxFilterRadius));
        }

        private static void UploadMainLightShadowSamplingGlobals(CommandBuffer cmd, Material material, BurtShadowData shadowData)
        {
            SetVector(cmd, material, MainLightShadowTexelSizeId, BurtShadowUtility.CreateMainLightShadowTexelSize(shadowData));
            SetFloat(cmd, material, MainLightShadowSampleBiasId, Mathf.Max(0f, shadowData.MainLightShadowSampleBias));
            SetFloat(cmd, material, MainLightShadowSoftnessId, ShouldUseSoftMainLightShadowSampling(shadowData) ? 1f : 0f);
            SetVector(cmd, material, MainLightShadowPCSSParamsId, CreateMainLightShadowPCSSParams(shadowData));
            SetVector(cmd, material, MainLightShadowReceiverBiasParamsId, CreateMainLightShadowReceiverBiasParams(shadowData));
            SetFloat(cmd, material, MainLightShadowStrengthId, shadowData.MainLightShadowStrength);
        }

        private static Vector4 CreateMainLightShadowReceiverBiasParams(BurtShadowData shadowData)
        {
            if (shadowData == null || !shadowData.HasMainLightShadow)
            {
                return Vector4.zero;
            }

            // The shadow map is already rendered with caster-side polygon offset. Applying an
            // unrelated receiver offset moves the lookup away from that depth surface and can
            // erase contact/cast shadows. Keep main-light sampling in the original geometry space.
            return Vector4.zero;
        }

        private static bool ShouldUseSoftMainLightShadowSampling(BurtShadowData shadowData)
        {
            return shadowData != null && shadowData.HasMainLightShadow && (shadowData.IsMainLightShadowSoft || (shadowData.IsMainLightShadowHard && shadowData.EnableMainLightShadowPCSS));
        }

        private static void SetVector(CommandBuffer cmd, Material material, int propertyId, Vector4 value)
        {
            if (material != null)
            {
                material.SetVector(propertyId, value);
            }

            if (cmd != null)
            {
                cmd.SetGlobalVector(propertyId, value);
            }
        }

        private static void SetFloat(CommandBuffer cmd, Material material, int propertyId, float value)
        {
            if (material != null)
            {
                material.SetFloat(propertyId, value);
            }

            if (cmd != null)
            {
                cmd.SetGlobalFloat(propertyId, value);
            }
        }

        private static Vector4 CreateMainLightShadowCascadeParams(BurtShadowData shadowData, int cascadeCount, int tileResolution)
        {
            var blendDistance = shadowData != null ? Mathf.Clamp(shadowData.MainLightShadowCascadeBlendDistance, 0f, 0.5f) : 0f;
            var fadeDistance = shadowData != null ? Mathf.Max(0f, shadowData.MainLightShadowFadeDistance) : 0f;
            return new Vector4(Mathf.Clamp(cascadeCount, 0, MaxCascadeCount), blendDistance, fadeDistance, Mathf.Max(1, tileResolution));
        }

        private static bool HasValidCascadeOutputArrays(
            Matrix4x4[] viewMatrices,
            Matrix4x4[] projectionMatrices,
            Matrix4x4[] worldToShadowMatrices,
            ShadowSplitData[] splitDatas,
            Vector4[] cascadeSpheres,
            Vector4[] cascadeAtlasRects)
        {
            return viewMatrices != null && viewMatrices.Length >= MaxCascadeCount
                && projectionMatrices != null && projectionMatrices.Length >= MaxCascadeCount
                && worldToShadowMatrices != null && worldToShadowMatrices.Length >= MaxCascadeCount
                && splitDatas != null && splitDatas.Length >= MaxCascadeCount
                && cascadeSpheres != null && cascadeSpheres.Length >= MaxCascadeCount
                && cascadeAtlasRects != null && cascadeAtlasRects.Length >= MaxCascadeCount;
        }

        private static void CopyCascadeOutputs(
            BurtMainLightShadowCascadeCache cascadeCache,
            Matrix4x4[] viewMatrices,
            Matrix4x4[] projectionMatrices,
            Matrix4x4[] worldToShadowMatrices,
            ShadowSplitData[] splitDatas,
            Vector4[] cascadeSpheres,
            Vector4[] cascadeAtlasRects)
        {
            if (cascadeCache == null)
            {
                ResetCascadeOutputs(viewMatrices, projectionMatrices, worldToShadowMatrices, splitDatas, cascadeSpheres, cascadeAtlasRects);
                return;
            }

            for (var cascadeIndex = 0; cascadeIndex < MaxCascadeCount; cascadeIndex++)
            {
                if (viewMatrices != null && viewMatrices.Length > cascadeIndex)
                {
                    viewMatrices[cascadeIndex] = cascadeCache.ViewMatrices[cascadeIndex];
                }

                if (projectionMatrices != null && projectionMatrices.Length > cascadeIndex)
                {
                    projectionMatrices[cascadeIndex] = cascadeCache.ProjectionMatrices[cascadeIndex];
                }

                if (worldToShadowMatrices != null && worldToShadowMatrices.Length > cascadeIndex)
                {
                    worldToShadowMatrices[cascadeIndex] = cascadeCache.WorldToShadowMatrices[cascadeIndex];
                }

                if (splitDatas != null && splitDatas.Length > cascadeIndex)
                {
                    splitDatas[cascadeIndex] = cascadeCache.SplitDatas[cascadeIndex];
                }

                if (cascadeSpheres != null && cascadeSpheres.Length > cascadeIndex)
                {
                    cascadeSpheres[cascadeIndex] = cascadeCache.CascadeSpheres[cascadeIndex];
                }

                if (cascadeAtlasRects != null && cascadeAtlasRects.Length > cascadeIndex)
                {
                    cascadeAtlasRects[cascadeIndex] = cascadeCache.CascadeAtlasRects[cascadeIndex];
                }
            }
        }

        private static void ResetCascadeOutputs(
            Matrix4x4[] viewMatrices,
            Matrix4x4[] projectionMatrices,
            Matrix4x4[] worldToShadowMatrices,
            ShadowSplitData[] splitDatas,
            Vector4[] cascadeSpheres,
            Vector4[] cascadeAtlasRects)
        {
            for (var cascadeIndex = 0; cascadeIndex < MaxCascadeCount; cascadeIndex++)
            {
                if (viewMatrices != null && viewMatrices.Length > cascadeIndex)
                {
                    viewMatrices[cascadeIndex] = Matrix4x4.identity;
                }

                if (projectionMatrices != null && projectionMatrices.Length > cascadeIndex)
                {
                    projectionMatrices[cascadeIndex] = Matrix4x4.identity;
                }

                if (worldToShadowMatrices != null && worldToShadowMatrices.Length > cascadeIndex)
                {
                    worldToShadowMatrices[cascadeIndex] = Matrix4x4.identity;
                }

                if (splitDatas != null && splitDatas.Length > cascadeIndex)
                {
                    splitDatas[cascadeIndex] = default;
                }

                if (cascadeSpheres != null && cascadeSpheres.Length > cascadeIndex)
                {
                    cascadeSpheres[cascadeIndex] = new Vector4(0f, 0f, 0f, -1f);
                }

                if (cascadeAtlasRects != null && cascadeAtlasRects.Length > cascadeIndex)
                {
                    cascadeAtlasRects[cascadeIndex] = new Vector4(0f, 0f, 1f, 1f);
                }
            }
        }

        private static void StabilizeDirectionalShadowProjection(Matrix4x4 viewMatrix, ref Matrix4x4 projectionMatrix, int tileResolution)
        {
            if (tileResolution <= 0)
            {
                return;
            }

            var shadowMatrix = projectionMatrix * viewMatrix;
            var shadowOrigin = shadowMatrix.MultiplyPoint(Vector3.zero);
            var texelOrigin = new Vector2(shadowOrigin.x, shadowOrigin.y) * (tileResolution * 0.5f);
            var roundedTexelOrigin = new Vector2(Mathf.Round(texelOrigin.x), Mathf.Round(texelOrigin.y));
            var clipOffset = (roundedTexelOrigin - texelOrigin) * (2f / tileResolution);
            projectionMatrix.m03 += clipOffset.x;
            projectionMatrix.m13 += clipOffset.y;
        }

        private static Vector4 ResolveCascadeAtlasRect(int cascadeIndex, int cascadeCount)
        {
            if (cascadeCount <= 1)
            {
                return new Vector4(0f, 0f, 1f, 1f);
            }

            var tileScale = 0.5f;
            var tileX = cascadeIndex & 1;
            var tileY = cascadeIndex >> 1;
            var minX = tileX * tileScale;
            var minY = tileY * tileScale;
            return new Vector4(minX, minY, minX + tileScale, minY + tileScale);
        }

        private static void FillWorldToShadowRows(Matrix4x4[] matrices)
        {
            for (var cascadeIndex = 0; cascadeIndex < MaxCascadeCount; cascadeIndex++)
            {
                var matrix = matrices != null && matrices.Length > cascadeIndex ? matrices[cascadeIndex] : Matrix4x4.identity;
                WorldToShadowRows0[cascadeIndex] = matrix.GetRow(0);
                WorldToShadowRows1[cascadeIndex] = matrix.GetRow(1);
                WorldToShadowRows2[cascadeIndex] = matrix.GetRow(2);
                WorldToShadowRows3[cascadeIndex] = matrix.GetRow(3);
            }
        }

        private static Matrix4x4[] CreateIdentityMatrixArray()
        {
            var matrices = new Matrix4x4[MaxCascadeCount];
            for (var cascadeIndex = 0; cascadeIndex < matrices.Length; cascadeIndex++)
            {
                matrices[cascadeIndex] = Matrix4x4.identity;
            }

            return matrices;
        }

        private static Vector4[] CreateDisabledCascadeSpheres()
        {
            var spheres = new Vector4[MaxCascadeCount];
            for (var cascadeIndex = 0; cascadeIndex < spheres.Length; cascadeIndex++)
            {
                spheres[cascadeIndex] = new Vector4(0f, 0f, 0f, -1f);
            }

            return spheres;
        }

        private static Vector4[] CreateDisabledCascadeAtlasRects()
        {
            var rects = new Vector4[MaxCascadeCount];
            for (var cascadeIndex = 0; cascadeIndex < rects.Length; cascadeIndex++)
            {
                rects[cascadeIndex] = new Vector4(0f, 0f, 1f, 1f);
            }

            return rects;
        }
    }

    internal sealed class BurtAllocateMainLightShadowMapPass : BurtRenderPass // 定义主光阴影图分配 Pass，负责为当前 request 创建主光 shadow map。
    {
        public override string Name => "Burt Allocate Main Light Shadow Map"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 没有主光阴影，就不声明阴影图写入。
            {
                return; // 直接结束资源声明，避免 Debug 输出无效的 MainLightShadowMap 依赖。
            }

            builder.WriteMainLightShadowMap(); // 声明这个 Pass 会申请并初始化 MainLightShadowMap 资源。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现主光阴影图分配 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            if (!BurtShadowUtility.ShouldUseMainLightShadow(request, context.Asset)) // 如果当前 request 不需要主光阴影，就不申请阴影图。
            {
                return; // 直接结束这个 Pass，避免无意义的临时 RT 分配。
            }

            var shadowData = BurtShadowUtility.ResolveMainLightShadowData(request, context.Asset); // 从 request 中安全读取主光阴影参数。

            var shadowMapTarget = context.MainLightShadowMapTarget; // 从 GraphContext 中取出 MainLightShadowMap 资源句柄。

            if (!shadowMapTarget.IsValid) // 如果 MainLightShadowMap 句柄无效，说明资源表没有注册阴影图。
            {
                return; // 直接结束这个 Pass，避免申请一个后续 Pass 无法找到的 RT。
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateMainLightShadowMapDescriptor(shadowData); // 根据主光阴影数据创建 shadow map RT 描述。

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.MainLightShadowMapId, descriptor, FilterMode.Bilinear); // 使用双线性过滤，让硬件阴影采样器能平滑比较边缘，避免点采样放大阴影条带。

            BurtShadowRenderTargetUtility.SetDepthOnlyShadowRenderTarget(cmd, shadowMapTarget); // 把主光阴影图显式绑定为 depth attachment，为后续 ShadowCaster 绘制做准备。
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);

            cmd.ClearRenderTarget(true, false, Color.clear, BurtShadowRenderTargetUtility.ResolveMainLightShadowClearDepth()); // Match Unity shadow compare semantics: empty atlas pixels must remain lit for BurtRP's current sampler state.

            BurtMainLightShadowMatrixUtility.BindMainLightShadowMapIfValid(cmd, shadowMapTarget); // 把主光阴影图暴露成全局纹理，后续 Lit shader 会通过它采样阴影。

            renderContext.ExecuteCommandBuffer(cmd); // 把申请、绑定和清理 shadow map 的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtDrawMainLightShadowCasterPass : BurtRenderPass
    {
        private const string CastingPunctualLightShadowKeyword = "_CASTING_PUNCTUAL_LIGHT_SHADOW";
        private static readonly int MainLightDirectionId = Shader.PropertyToID("_BurtMainLightDirection");
        private static readonly int ShadowCasterLightPositionId = Shader.PropertyToID("_BurtShadowCasterLightPosition");
        private static readonly int CastingPunctualLightShadowId = Shader.PropertyToID("_BurtCastingPunctualLightShadow");
        private static readonly int MainLightShadowDepthBiasId = Shader.PropertyToID("_BurtMainLightShadowDepthBias");
        private static readonly int MainLightShadowNormalBiasId = Shader.PropertyToID("_BurtMainLightShadowNormalBias");
        private static readonly int UnityLightDirectionId = Shader.PropertyToID("_LightDirection");
        private static readonly int UnityLightPositionId = Shader.PropertyToID("_LightPosition");
        private static readonly int UnityShadowBiasId = Shader.PropertyToID("_ShadowBias");
        private static readonly int WorldSpaceCameraPosId = Shader.PropertyToID("_WorldSpaceCameraPos");
        private static readonly int UnityWorldToCameraId = Shader.PropertyToID("unity_WorldToCamera");
        private static readonly int UnityCameraToWorldId = Shader.PropertyToID("unity_CameraToWorld");

        public override string Name => "Burt Draw Main Light Shadow Caster";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteMainLightShadowMap();
            builder.WriteShadowGlobals();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var renderContext = context.ScriptableContext;
            var request = context.Request;
            if (!BurtShadowUtility.ShouldUseMainLightShadow(request, context.Asset))
            {
                return;
            }

            var camera = request.Camera;
            if (camera == null)
            {
                return;
            }

            var shadowData = BurtShadowUtility.ResolveMainLightShadowData(request, context.Asset);
            if (shadowData == null)
            {
                return;
            }

            var shadowMapTarget = context.MainLightShadowMapTarget;
            if (!shadowMapTarget.IsValid)
            {
                return;
            }

            if (!BurtMainLightShadowMatrixUtility.TryGetMainLightShadowCascadeCache(request, shadowData, out var cascadeCache))
            {
                DisableMainLightShadowReceiverGlobals(renderContext);
                return;
            }

            var cascadeCount = cascadeCache.CascadeCount;
            var tileResolution = cascadeCache.TileResolution;
            var atlasResolution = cascadeCache.AtlasResolution;

            var mainLightDirection = ResolveMainLightDirection(request);
            var worldToShadowMatrices = cascadeCache.WorldToShadowMatrices;
            var cascadeSpheres = cascadeCache.CascadeSpheres;
            var cascadeAtlasRects = cascadeCache.CascadeAtlasRects;
            var cmd = CommandBufferPool.Get(Name);

            try
            {
                BurtShadowRenderTargetUtility.SetDepthOnlyShadowRenderTarget(cmd, shadowMapTarget);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, atlasResolution, atlasResolution);
                cmd.ClearRenderTarget(true, false, Color.clear, BurtShadowRenderTargetUtility.ResolveMainLightShadowClearDepth());
                cmd.SetGlobalVector(MainLightDirectionId, new Vector4(mainLightDirection.x, mainLightDirection.y, mainLightDirection.z, 0f));
                cmd.SetGlobalVector(UnityLightDirectionId, new Vector4(mainLightDirection.x, mainLightDirection.y, mainLightDirection.z, 0f));
                cmd.SetGlobalVector(UnityLightPositionId, Vector4.zero);
                cmd.SetGlobalVector(ShadowCasterLightPositionId, Vector4.zero);
                cmd.SetGlobalFloat(CastingPunctualLightShadowId, 0f);
                SetKeyword(cmd, CastingPunctualLightShadowKeyword, false);
                cmd.SetGlobalDepthBias(1f, 2.5f);
                SetShadowCasterCameraGlobals(cmd, camera);
                BurtMainLightShadowMatrixUtility.UploadMainLightShadowReceiverGlobals(cmd, null, shadowMapTarget, worldToShadowMatrices, cascadeSpheres, cascadeAtlasRects, cascadeCount, tileResolution, shadowData);
                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                for (var cascadeIndex = 0; cascadeIndex < cascadeCount; cascadeIndex++)
                {
                    var atlasRect = cascadeAtlasRects[cascadeIndex];
                    var viewport = new Rect(
                        atlasRect.x * atlasResolution,
                        atlasRect.y * atlasResolution,
                        Mathf.Max(1f, (atlasRect.z - atlasRect.x) * atlasResolution),
                        Mathf.Max(1f, (atlasRect.w - atlasRect.y) * atlasResolution));

                    BurtShadowRenderTargetUtility.SetDepthOnlyShadowRenderTarget(cmd, shadowMapTarget);
                    cmd.SetViewport(viewport);
                    cmd.SetViewProjectionMatrices(cascadeCache.ViewMatrices[cascadeIndex], cascadeCache.ProjectionMatrices[cascadeIndex]);
                    var depthBias = ResolveMainLightShadowDepthBias(shadowData, cascadeCache.ProjectionMatrices[cascadeIndex], tileResolution);
                    var normalBias = ResolveMainLightShadowNormalBias(shadowData, cascadeCache.ProjectionMatrices[cascadeIndex], tileResolution);
                    cmd.SetGlobalFloat(MainLightShadowDepthBiasId, depthBias);
                    cmd.SetGlobalFloat(MainLightShadowNormalBiasId, normalBias);
                    cmd.SetGlobalVector(UnityShadowBiasId, new Vector4(depthBias, normalBias, 0f, 0f));
                    BurtMainLightShadowMatrixUtility.SetMainLightWorldToShadow(cmd, worldToShadowMatrices[cascadeIndex]);
                    renderContext.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    if (BurtShadowRenderTargetUtility.HasShadowCasters(request.CullingResults, shadowData.MainLightIndex))
                    {
                        var shadowDrawingSettings = new ShadowDrawingSettings(request.CullingResults, shadowData.MainLightIndex, BatchCullingProjectionType.Orthographic);
                        shadowDrawingSettings.splitData = cascadeCache.SplitDatas[cascadeIndex];
                        shadowDrawingSettings.useRenderingLayerMaskTest = false;
                        renderContext.DrawShadows(ref shadowDrawingSettings);
                    }
                }
            }
            finally
            {
                CommandBufferPool.Release(cmd);
                ResetMainLightShadowCasterState(context, renderContext, camera);
            }

            UploadMainLightShadowReceiverGlobals(
                renderContext,
                shadowMapTarget,
                worldToShadowMatrices,
                cascadeSpheres,
                cascadeAtlasRects,
                cascadeCount,
                tileResolution,
                shadowData);
        }

        private static void UploadMainLightShadowReceiverGlobals(
            ScriptableRenderContext renderContext,
            BurtRenderTargetHandle shadowMapTarget,
            Matrix4x4[] worldToShadowMatrices,
            Vector4[] cascadeSpheres,
            Vector4[] cascadeAtlasRects,
            int cascadeCount,
            int tileResolution,
            BurtShadowData shadowData)
        {
            if (shadowData == null || !shadowMapTarget.IsValid)
            {
                DisableMainLightShadowReceiverGlobals(renderContext);
                return;
            }

            var cmd = CommandBufferPool.Get("Burt Upload Main Light Shadow Receiver");
            BurtMainLightShadowMatrixUtility.UploadMainLightShadowReceiverGlobals(cmd, null, shadowMapTarget, worldToShadowMatrices, cascadeSpheres, cascadeAtlasRects, cascadeCount, tileResolution, shadowData);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void ResetMainLightShadowCasterState(BurtRenderGraphContext context, ScriptableRenderContext renderContext, Camera camera)
        {
            var cmd = CommandBufferPool.Get("Burt Reset Main Light Shadow Caster State");
            cmd.SetGlobalDepthBias(0f, 0f);
            cmd.SetGlobalFloat(CastingPunctualLightShadowId, 0f);
            cmd.SetGlobalFloat(MainLightShadowDepthBiasId, 0f);
            cmd.SetGlobalFloat(MainLightShadowNormalBiasId, 0f);
            cmd.SetGlobalVector(UnityLightDirectionId, Vector4.zero);
            cmd.SetGlobalVector(UnityLightPositionId, Vector4.zero);
            cmd.SetGlobalVector(ShadowCasterLightPositionId, Vector4.zero);
            cmd.SetGlobalVector(UnityShadowBiasId, Vector4.zero);
            SetKeyword(cmd, CastingPunctualLightShadowKeyword, false);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            BurtDrawingSettingsUtility.RestoreCameraMatricesForMainDraw(context, cmd);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            if (camera != null && !BurtDrawingSettingsUtility.IsTemporalAAEnabled(context))
            {
                renderContext.SetupCameraProperties(camera);
            }
        }

        private static void DisableMainLightShadowReceiverGlobals(ScriptableRenderContext renderContext)
        {
            var cmd = CommandBufferPool.Get("Burt Disable Main Light Shadow Receiver");
            BurtMainLightShadowMatrixUtility.ClearMainLightShadowReceiverGlobals(cmd);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static Vector3 ResolveMainLightDirection(BurtRenderRequest request)
        {
            var lightingData = request != null ? request.LightingData : null;
            var direction = lightingData != null ? lightingData.MainLightDirection : Vector3.forward;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector3.forward;
            }

            return direction.normalized;
        }

        private static float ResolveMainLightShadowNormalBias(BurtShadowData shadowData, Matrix4x4 projectionMatrix, int tileResolution)
        {
            if (shadowData == null || tileResolution <= 0)
            {
                return 0f;
            }

            var normalBias = Mathf.Max(0f, shadowData.MainLightShadowNormalBias);
            if (normalBias <= 0f)
            {
                return 0f;
            }

            var projectionWidth = Mathf.Abs(projectionMatrix.m00) > 0.00001f ? 2f / Mathf.Abs(projectionMatrix.m00) : 0f;
            var projectionHeight = Mathf.Abs(projectionMatrix.m11) > 0.00001f ? 2f / Mathf.Abs(projectionMatrix.m11) : 0f;
            var worldTexelSize = Mathf.Max(projectionWidth, projectionHeight) / Mathf.Max(1, tileResolution);
            return -normalBias * worldTexelSize * ResolveMainLightShadowSoftKernelRadius(shadowData);
        }

        private static float ResolveMainLightShadowDepthBias(BurtShadowData shadowData, Matrix4x4 projectionMatrix, int tileResolution)
        {
            if (shadowData == null || tileResolution <= 0)
            {
                return 0f;
            }

            var depthBias = Mathf.Max(0f, shadowData.MainLightShadowDepthBias);
            if (depthBias <= 0f)
            {
                return 0f;
            }

            var projectionWidth = Mathf.Abs(projectionMatrix.m00) > 0.00001f ? 2f / Mathf.Abs(projectionMatrix.m00) : 0f;
            var projectionHeight = Mathf.Abs(projectionMatrix.m11) > 0.00001f ? 2f / Mathf.Abs(projectionMatrix.m11) : 0f;
            var worldTexelSize = Mathf.Max(projectionWidth, projectionHeight) / Mathf.Max(1, tileResolution);
            return -depthBias * worldTexelSize * ResolveMainLightShadowSoftKernelRadius(shadowData);
        }

        private static float ResolveMainLightShadowSoftKernelRadius(BurtShadowData shadowData)
        {
            // The 13-tap PCF path samples a 5x5-equivalent footprint. Mirror URP's medium
            // soft-shadow bias scaling so neighboring taps do not self-shadow flat receivers.
            return shadowData != null && shadowData.IsMainLightShadowSoft ? 2.5f : 1f;
        }

        private static void SetShadowCasterCameraGlobals(CommandBuffer cmd, Camera camera)
        {
            if (cmd == null || camera == null)
            {
                return;
            }

            var cameraPosition = camera.transform.position;
            cmd.SetGlobalVector(WorldSpaceCameraPosId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f));
            SetWorldToCameraAndCameraToWorldMatrices(cmd, camera.worldToCameraMatrix);
        }

        private static void SetWorldToCameraAndCameraToWorldMatrices(CommandBuffer cmd, Matrix4x4 viewMatrix)
        {
            var worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * viewMatrix;
            cmd.SetGlobalMatrix(UnityWorldToCameraId, worldToCameraMatrix);
            cmd.SetGlobalMatrix(UnityCameraToWorldId, worldToCameraMatrix.inverse);
        }

        private static void SetKeyword(CommandBuffer cmd, string keyword, bool enabled)
        {
            if (cmd == null || string.IsNullOrEmpty(keyword))
            {
                return;
            }

            if (enabled)
            {
                cmd.EnableShaderKeyword(keyword);
            }
            else
            {
                cmd.DisableShaderKeyword(keyword);
            }
        }

    }

    internal static class BurtAdditionalLightShadowUtility
    {
        public const int AtlasTileCountX = 5;
        public const int AtlasTileCountY = 5;
        private const float AdditionalLightShadowReceiverDepthBias = 0.001f;

        private static readonly int AdditionalLightShadowDataId = Shader.PropertyToID("_BurtAdditionalLightShadowData");
        private static readonly int AdditionalLightShadowLightParamsId = Shader.PropertyToID("_BurtAdditionalLightShadowLightParams");
        private static readonly int AdditionalLightShadowSliceAtlasRectsId = Shader.PropertyToID("_BurtAdditionalLightShadowSliceAtlasRects");
        private static readonly int AdditionalLightShadowSliceRows0Id = Shader.PropertyToID("_BurtAdditionalLightShadowSliceRows0");
        private static readonly int AdditionalLightShadowSliceRows1Id = Shader.PropertyToID("_BurtAdditionalLightShadowSliceRows1");
        private static readonly int AdditionalLightShadowSliceRows2Id = Shader.PropertyToID("_BurtAdditionalLightShadowSliceRows2");
        private static readonly int AdditionalLightShadowSliceRows3Id = Shader.PropertyToID("_BurtAdditionalLightShadowSliceRows3");
        private static readonly int AdditionalLightShadowParamsId = Shader.PropertyToID("_BurtAdditionalLightShadowParams");
        private static readonly int AdditionalLightShadowTexelSizeId = Shader.PropertyToID("_BurtAdditionalLightShadowTexelSize");

        private static readonly Matrix4x4[] DisabledSliceWorldToShadowMatrices = CreateIdentitySliceMatrixArray();
        private static readonly Vector4[] DisabledShadowData = new Vector4[BurtLightingData.MaxAdditionalLights];
        private static readonly Vector4[] DisabledShadowLightParams = new Vector4[BurtLightingData.MaxAdditionalLights];
        private static readonly Vector4[] DisabledSliceAtlasRects = CreateDefaultSliceAtlasRectArray();
        private static readonly Vector4[] SliceWorldToShadowRows0 = new Vector4[BurtLightingData.MaxAdditionalLightShadowSlices];
        private static readonly Vector4[] SliceWorldToShadowRows1 = new Vector4[BurtLightingData.MaxAdditionalLightShadowSlices];
        private static readonly Vector4[] SliceWorldToShadowRows2 = new Vector4[BurtLightingData.MaxAdditionalLightShadowSlices];
        private static readonly Vector4[] SliceWorldToShadowRows3 = new Vector4[BurtLightingData.MaxAdditionalLightShadowSlices];
        private static readonly AdditionalShadowCandidate[] AdditionalShadowCandidates = new AdditionalShadowCandidate[BurtLightingData.MaxAdditionalLights];

        private struct AdditionalShadowCandidate
        {
            public int LightIndex;
            public int VisibleLightIndex;
            public LightType LightType;
            public int RequiredSliceCount;
        }

        public static bool ShouldUseAdditionalLightShadows(BurtRenderRequest request)
        {
            var lightingData = request != null ? request.LightingData : null;
            return request != null && request.IsValid && lightingData != null && lightingData.HasShadowedAdditionalLights;
        }

        public static bool TryPrepareAdditionalLightShadowAtlas(BurtRenderRequest request, out BurtLightingData lightingData)
        {
            lightingData = request != null ? request.LightingData : null;
            if (!ShouldUseAdditionalLightShadows(request) || lightingData == null)
            {
                lightingData?.SetAdditionalLightShadowPrepareState(true, false, 0);
                return false;
            }

            var tileResolution = BurtLightingData.DefaultAdditionalLightShadowTileResolution;
            var atlasResolution = tileResolution * AtlasTileCountX;
            var packedSliceIndex = 0;
            var failedCount = 0;
            var candidateCount = 0;
            var additionalLightCount = Mathf.Min(lightingData.AdditionalLightCount, BurtLightingData.MaxAdditionalLights);
            for (var lightIndex = 0; lightIndex < additionalLightCount; lightIndex++)
            {
                if (!IsCandidateShadowSlot(lightingData, lightIndex))
                {
                    ClearPreparedAdditionalLightShadowSlot(lightingData, lightIndex);
                    continue;
                }

                var visibleLightIndex = lightingData.AdditionalLightShadowVisibleLightIndices[lightIndex];
                var visibleLightType = ResolveVisibleLightType(request, visibleLightIndex);
                var requiredSliceCount = ResolveRequiredAdditionalShadowSliceCount(visibleLightType);
                if (requiredSliceCount <= 0)
                {
                    FailPreparedAdditionalLightShadowSlot(lightingData, lightIndex, BurtAdditionalLightShadowStatus.PrepareInvalidVisibleLightIndex);
                    failedCount++;
                    continue;
                }

                AdditionalShadowCandidates[candidateCount++] = new AdditionalShadowCandidate
                {
                    LightIndex = lightIndex,
                    VisibleLightIndex = visibleLightIndex,
                    LightType = visibleLightType,
                    RequiredSliceCount = requiredSliceCount
                };
            }

            var sliceCapacity = ResolveAdditionalShadowSliceCapacity();
            for (var candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                var candidate = AdditionalShadowCandidates[candidateIndex];
                if (packedSliceIndex + candidate.RequiredSliceCount > sliceCapacity)
                {
                    FailAdditionalLightShadowSlotLimitExceeded(lightingData, candidate.LightIndex);
                    failedCount++;
                    continue;
                }

                if (candidate.LightType == LightType.Spot)
                {
                    if (!TryPrepareSpotAdditionalLightShadowSlice(request, lightingData, candidate.LightIndex, candidate.VisibleLightIndex, ref packedSliceIndex))
                    {
                        failedCount++;
                    }

                    continue;
                }

                if (candidate.LightType == LightType.Point)
                {
                    if (!TryPreparePointAdditionalLightShadowSlices(request, lightingData, candidate.LightIndex, candidate.VisibleLightIndex, ref packedSliceIndex, out var pointFailedCount))
                    {
                        failedCount += Mathf.Max(1, pointFailedCount);
                    }
                }
            }

            lightingData.SetAdditionalLightShadowCacheState(packedSliceIndex > 0, tileResolution, atlasResolution, AtlasTileCountX, AtlasTileCountY, packedSliceIndex);
            lightingData.SetAdditionalLightShadowPrepareState(true, packedSliceIndex > 0, failedCount);
            return packedSliceIndex > 0;
        }

        private static int ResolveRequiredAdditionalShadowSliceCount(LightType lightType)
        {
            if (lightType == LightType.Point)
            {
                return BurtLightingData.PointLightShadowFaceCount;
            }

            if (lightType == LightType.Spot)
            {
                return 1;
            }

            return 0;
        }

        private static int ResolveAdditionalShadowSliceCapacity()
        {
            return Mathf.Min(BurtLightingData.MaxAdditionalLightShadowSlices, AtlasTileCountX * AtlasTileCountY);
        }

        private static bool TryPrepareSpotAdditionalLightShadowSlice(
            BurtRenderRequest request,
            BurtLightingData lightingData,
            int lightIndex,
            int visibleLightIndex,
            ref int packedSliceIndex)
        {
            if (!HasFreeAdditionalShadowSlices(packedSliceIndex, 1))
            {
                FailAdditionalLightShadowSlotLimitExceeded(lightingData, lightIndex);
                return false;
            }

            if (!request.CullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
                    visibleLightIndex,
                    out var viewMatrix,
                    out var projectionMatrix,
                    out var splitData))
            {
                FailPreparedAdditionalLightShadowSlot(lightingData, lightIndex, BurtAdditionalLightShadowStatus.PrepareSpotMatrixFailed);
                return false;
            }

            var atlasRect = ResolveAtlasRect(packedSliceIndex);
            var worldToShadowMatrix = BurtMainLightShadowMatrixUtility.CreateWorldToShadowMatrix(viewMatrix, projectionMatrix, atlasRect);
            lightingData.SetAdditionalLightShadowSlice(packedSliceIndex, lightIndex, 0, atlasRect, viewMatrix, projectionMatrix, worldToShadowMatrix, splitData);
            lightingData.SetAdditionalLightShadowSlot(lightIndex, packedSliceIndex, atlasRect, viewMatrix, projectionMatrix, worldToShadowMatrix, splitData);
            var receiverNormalBias = ResolveAdditionalLightShadowReceiverNormalBias(request, lightingData, lightIndex, BurtLightingData.DefaultAdditionalLightShadowTileResolution);
            lightingData.SetAdditionalLightShadowLightParams(lightIndex, packedSliceIndex, 1, 2f, receiverNormalBias);
            packedSliceIndex++;
            return true;
        }

        private static bool TryPreparePointAdditionalLightShadowSlices(
            BurtRenderRequest request,
            BurtLightingData lightingData,
            int lightIndex,
            int visibleLightIndex,
            ref int packedSliceIndex,
            out int failedCount)
        {
            failedCount = 0;
            if (!HasFreeAdditionalShadowSlices(packedSliceIndex, BurtLightingData.PointLightShadowFaceCount))
            {
                FailAdditionalLightShadowSlotLimitExceeded(lightingData, lightIndex);
                failedCount = BurtLightingData.PointLightShadowFaceCount;
                return false;
            }

            var firstSliceIndex = packedSliceIndex;
            var preparedFaceCount = 0;
            var light = request.CullingResults.visibleLights[visibleLightIndex].light;
            var fovBias = ResolvePointAdditionalLightShadowFovBias(light);

            for (var faceIndex = 0; faceIndex < BurtLightingData.PointLightShadowFaceCount; faceIndex++)
            {
                if (!request.CullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                        visibleLightIndex,
                        (CubemapFace)faceIndex,
                        fovBias,
                        out var viewMatrix,
                        out var projectionMatrix,
                        out var splitData))
                {
                    failedCount++;
                    continue;
                }

                StabilizePointShadowViewMatrix(ref viewMatrix);
                splitData = SanitizePointShadowSplitData(splitData, lightingData, lightIndex);

                var sliceIndex = firstSliceIndex + faceIndex;
                var atlasRect = ResolveAtlasRect(sliceIndex);
                var worldToShadowMatrix = BurtMainLightShadowMatrixUtility.CreateWorldToShadowMatrix(viewMatrix, projectionMatrix, atlasRect);
                lightingData.SetAdditionalLightShadowSlice(sliceIndex, lightIndex, faceIndex, atlasRect, viewMatrix, projectionMatrix, worldToShadowMatrix, splitData);
                if (preparedFaceCount == 0)
                {
                    lightingData.SetAdditionalLightShadowSlot(lightIndex, sliceIndex, atlasRect, viewMatrix, projectionMatrix, worldToShadowMatrix, splitData);
                }

                preparedFaceCount++;
            }

            if (preparedFaceCount != BurtLightingData.PointLightShadowFaceCount)
            {
                FailPreparedAdditionalLightShadowSlot(lightingData, lightIndex, BurtAdditionalLightShadowStatus.PreparePointMatrixFailed);
                return false;
            }

            var receiverNormalBias = ResolveAdditionalLightShadowReceiverNormalBias(request, lightingData, lightIndex, BurtLightingData.DefaultAdditionalLightShadowTileResolution);
            lightingData.SetAdditionalLightShadowLightParams(lightIndex, firstSliceIndex, BurtLightingData.PointLightShadowFaceCount, 1f, receiverNormalBias);
            packedSliceIndex += BurtLightingData.PointLightShadowFaceCount;
            return true;
        }

        private static float ResolveAdditionalLightShadowReceiverNormalBias(BurtRenderRequest request, BurtLightingData lightingData, int lightIndex, int tileResolution)
        {
            var light = ResolveUnityLight(request, lightingData, lightIndex);
            var normalBias = light != null ? Mathf.Max(0f, light.shadowNormalBias) : 0f;
            if (normalBias <= 0f)
            {
                return 0f;
            }

            return normalBias * ResolveAdditionalLightShadowWorldTexelSize(light, lightingData, lightIndex, tileResolution);
        }

        public static float ResolveAdditionalLightShadowCasterDepthBias(BurtRenderRequest request, BurtLightingData lightingData, int lightIndex, int tileResolution)
        {
            var light = ResolveUnityLight(request, lightingData, lightIndex);
            var depthBias = light != null ? Mathf.Max(0f, light.shadowBias) : 0f;
            if (depthBias <= 0f)
            {
                return 0f;
            }

            return -depthBias * ResolveAdditionalLightShadowWorldTexelSize(light, lightingData, lightIndex, tileResolution);
        }

        public static float ResolveAdditionalLightShadowCasterNormalBias(BurtRenderRequest request, BurtLightingData lightingData, int lightIndex, int tileResolution)
        {
            var light = ResolveUnityLight(request, lightingData, lightIndex);
            if (light != null && light.type == LightType.Point)
            {
                return 0f;
            }

            var normalBias = light != null ? Mathf.Max(0f, light.shadowNormalBias) : 0f;
            if (normalBias <= 0f)
            {
                return 0f;
            }

            return -normalBias * ResolveAdditionalLightShadowWorldTexelSize(light, lightingData, lightIndex, tileResolution);
        }

        private static float ResolveAdditionalLightShadowWorldTexelSize(Light light, BurtLightingData lightingData, int lightIndex, int tileResolution)
        {
            var safeTileResolution = Mathf.Max(1, tileResolution);
            if (light == null)
            {
                return 1f / safeTileResolution;
            }

            if (light.type == LightType.Point)
            {
                var fovBias = ResolvePointAdditionalLightShadowFovBias(safeTileResolution, light.shadows == LightShadows.Soft);
                var cubeFaceAngle = 90f + fovBias;
                var frustumSize = Mathf.Tan(cubeFaceAngle * 0.5f * Mathf.Deg2Rad) * Mathf.Max(light.range, 0.0001f);
                return frustumSize / safeTileResolution;
            }

            if (light.type == LightType.Spot)
            {
                var frustumSize = Mathf.Tan(light.spotAngle * 0.5f * Mathf.Deg2Rad) * Mathf.Max(light.range, 0.0001f);
                return frustumSize / safeTileResolution;
            }

            if (lightingData != null && lightIndex >= 0 && lightIndex < BurtLightingData.MaxAdditionalLights)
            {
                var projectionMatrix = lightingData.AdditionalLightShadowProjectionMatrices[lightIndex];
                var projectionWidth = Mathf.Abs(projectionMatrix.m00) > 0.00001f ? 2f / Mathf.Abs(projectionMatrix.m00) : 0f;
                var projectionHeight = Mathf.Abs(projectionMatrix.m11) > 0.00001f ? 2f / Mathf.Abs(projectionMatrix.m11) : 0f;
                return Mathf.Max(projectionWidth, projectionHeight) / safeTileResolution;
            }

            return 1f / safeTileResolution;
        }

        private static Light ResolveUnityLight(BurtRenderRequest request, BurtLightingData lightingData, int lightIndex)
        {
            if (request == null || lightingData == null || lightIndex < 0 || lightIndex >= BurtLightingData.MaxAdditionalLights)
            {
                return null;
            }

            var visibleLightIndex = lightingData.AdditionalLightShadowVisibleLightIndices[lightIndex];
            if (visibleLightIndex < 0 || visibleLightIndex >= request.CullingResults.visibleLights.Length)
            {
                return null;
            }

            return request.CullingResults.visibleLights[visibleLightIndex].light;
        }

        private static void ClearPreparedAdditionalLightShadowSlot(BurtLightingData lightingData, int lightIndex)
        {
            if (lightingData == null || lightIndex < 0 || lightIndex >= BurtLightingData.MaxAdditionalLights)
            {
                return;
            }

            var visibleLightIndex = lightingData.AdditionalLightShadowVisibleLightIndices[lightIndex];
            var shadowStrength = lightingData.AdditionalLightShadowData[lightIndex].y;
            var status = lightingData.AdditionalLightShadowStatuses[lightIndex];
            lightingData.DisableAdditionalLightShadowSlot(lightIndex);
            lightingData.AdditionalLightShadowVisibleLightIndices[lightIndex] = visibleLightIndex;
            lightingData.AdditionalLightShadowData[lightIndex] = new Vector4(0f, shadowStrength, 0f, 0f);
            lightingData.SetAdditionalLightShadowStatus(lightIndex, status);
        }

        private static void FailPreparedAdditionalLightShadowSlot(
            BurtLightingData lightingData,
            int lightIndex,
            BurtAdditionalLightShadowStatus status)
        {
            if (lightingData == null || lightIndex < 0 || lightIndex >= BurtLightingData.MaxAdditionalLights)
            {
                return;
            }

            var visibleLightIndex = lightingData.AdditionalLightShadowVisibleLightIndices[lightIndex];
            var shadowData = lightingData.AdditionalLightShadowData[lightIndex];
            lightingData.DisableAdditionalLightShadowSlot(lightIndex, status);
            lightingData.AdditionalLightShadowVisibleLightIndices[lightIndex] = visibleLightIndex;
            lightingData.AdditionalLightShadowData[lightIndex] = new Vector4(0f, shadowData.y, 0f, shadowData.w);
        }

        private static void FailAdditionalLightShadowSlotLimitExceeded(BurtLightingData lightingData, int lightIndex)
        {
            FailPreparedAdditionalLightShadowSlot(lightingData, lightIndex, BurtAdditionalLightShadowStatus.SlotLimitExceeded);
            lightingData?.IncrementAdditionalLightShadowSlotLimitExceededCount();
        }

        public static bool IsActiveShadowSlot(BurtLightingData lightingData, int lightIndex)
        {
            return lightingData != null &&
                lightIndex >= 0 &&
                lightIndex < BurtLightingData.MaxAdditionalLights &&
                lightingData.AdditionalLightShadowData[lightIndex].x > 0.5f &&
                lightingData.AdditionalLightShadowVisibleLightIndices[lightIndex] >= 0;
        }

        private static LightType ResolveVisibleLightType(BurtRenderRequest request, int visibleLightIndex)
        {
            if (request == null || visibleLightIndex < 0 || visibleLightIndex >= request.CullingResults.visibleLights.Length)
            {
                return LightType.Area;
            }

            return request.CullingResults.visibleLights[visibleLightIndex].lightType;
        }

        private static bool HasFreeAdditionalShadowSlices(int firstSliceIndex, int requiredSliceCount)
        {
            return firstSliceIndex >= 0 &&
                requiredSliceCount > 0 &&
                firstSliceIndex + requiredSliceCount <= BurtLightingData.MaxAdditionalLightShadowSlices &&
                firstSliceIndex + requiredSliceCount <= AtlasTileCountX * AtlasTileCountY;
        }

        private static float ResolvePointAdditionalLightShadowFovBias(Light light)
        {
            var shadowFiltering = light != null && light.shadows == LightShadows.Soft;
            return ResolvePointAdditionalLightShadowFovBias(BurtLightingData.DefaultAdditionalLightShadowTileResolution, shadowFiltering);
        }

        public static float ResolvePointAdditionalLightShadowFovBias(int shadowSliceResolution, bool shadowFiltering)
        {
            var safeResolution = Mathf.Max(1, shadowSliceResolution);
            var fovBias = 4f;
            if (safeResolution <= 16)
            {
                fovBias = 43f;
            }
            else if (safeResolution <= 32)
            {
                fovBias = 18.55f;
            }
            else if (safeResolution <= 64)
            {
                fovBias = 8.63f;
            }
            else if (safeResolution <= 128)
            {
                fovBias = 4.13f;
            }
            else if (safeResolution <= 256)
            {
                fovBias = 2.03f;
            }
            else if (safeResolution <= 512)
            {
                fovBias = 1f;
            }
            else if (safeResolution <= 1024)
            {
                fovBias = 0.5f;
            }

            if (!shadowFiltering)
            {
                return fovBias;
            }

            if (safeResolution <= 32)
            {
                fovBias += 9.35f;
            }
            else if (safeResolution <= 64)
            {
                fovBias += 4.07f;
            }
            else if (safeResolution <= 128)
            {
                fovBias += 1.77f;
            }
            else if (safeResolution <= 256)
            {
                fovBias += 0.85f;
            }
            else if (safeResolution <= 512)
            {
                fovBias += 0.39f;
            }
            else if (safeResolution <= 1024)
            {
                fovBias += 0.17f;
            }

            return fovBias;
        }

        private static void StabilizePointShadowViewMatrix(ref Matrix4x4 viewMatrix)
        {
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;
        }

        private static ShadowSplitData SanitizePointShadowSplitData(ShadowSplitData splitData, BurtLightingData lightingData, int lightIndex)
        {
            if (IsValidCullingSphere(splitData.cullingSphere))
            {
                return splitData;
            }

            var lightPositionAndRange = lightingData != null &&
                lightIndex >= 0 &&
                lightIndex < BurtLightingData.MaxAdditionalLights
                    ? lightingData.AdditionalLightPositionAndRange[lightIndex]
                    : Vector4.zero;
            var fallbackX = IsFinite(lightPositionAndRange.x) ? lightPositionAndRange.x : 0f;
            var fallbackY = IsFinite(lightPositionAndRange.y) ? lightPositionAndRange.y : 0f;
            var fallbackZ = IsFinite(lightPositionAndRange.z) ? lightPositionAndRange.z : 0f;
            var fallbackRadius = IsFinite(lightPositionAndRange.w) && lightPositionAndRange.w > 0f ? lightPositionAndRange.w : 0.0001f;
            splitData.cullingSphere = new Vector4(
                fallbackX,
                fallbackY,
                fallbackZ,
                fallbackRadius);
            return splitData;
        }

        private static bool IsValidCullingSphere(Vector4 cullingSphere)
        {
            return IsFinite(cullingSphere.x) &&
                IsFinite(cullingSphere.y) &&
                IsFinite(cullingSphere.z) &&
                IsFinite(cullingSphere.w) &&
                cullingSphere.w > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static int CountActiveShadowSlots(BurtLightingData lightingData)
        {
            var count = 0;
            for (var lightIndex = 0; lightIndex < BurtLightingData.MaxAdditionalLights; lightIndex++)
            {
                if (IsActiveShadowSlot(lightingData, lightIndex))
                {
                    count++;
                }
            }

            return Mathf.Clamp(count, 0, BurtLightingData.MaxAdditionalLights);
        }

        public static void UploadAdditionalLightShadowReceiverGlobals(
            CommandBuffer cmd,
            Material material,
            BurtRenderTargetHandle atlasTarget,
            BurtLightingData lightingData)
        {
            if (lightingData == null || !lightingData.AdditionalLightShadowCacheValid || !atlasTarget.IsValid)
            {
                ClearAdditionalLightShadowReceiverGlobals(cmd, material);
                return;
            }

            var activeShadowSlotCount = CountActiveShadowSlots(lightingData);
            var tileResolution = Mathf.Max(1, lightingData.AdditionalLightShadowTileResolution);
            var shadowParams = new Vector4(
                activeShadowSlotCount,
                tileResolution,
                Mathf.Max(1, lightingData.AdditionalLightShadowAtlasResolution),
                AdditionalLightShadowReceiverDepthBias);
            var atlasResolution = Mathf.Max(1, lightingData.AdditionalLightShadowAtlasResolution);
            var texelSize = new Vector4(1f / atlasResolution, 1f / atlasResolution, atlasResolution, atlasResolution);

            UploadAdditionalLightShadowArrays(
                cmd,
                material,
                lightingData.AdditionalLightShadowData,
                lightingData.AdditionalLightShadowLightParams,
                lightingData.AdditionalLightShadowSliceAtlasRects,
                lightingData.AdditionalLightShadowSliceWorldToShadowMatrices,
                shadowParams,
                texelSize);
            BindAdditionalLightShadowAtlasIfValid(cmd, atlasTarget);
        }

        public static void UploadAdditionalLightShadowReceiverGlobals(CommandBuffer cmd, Material material, BurtLightingData lightingData)
        {
            if (lightingData == null || !lightingData.AdditionalLightShadowCacheValid)
            {
                ClearAdditionalLightShadowReceiverGlobals(cmd, material);
                return;
            }

            var activeShadowSlotCount = CountActiveShadowSlots(lightingData);
            var tileResolution = Mathf.Max(1, lightingData.AdditionalLightShadowTileResolution);
            var shadowParams = new Vector4(
                activeShadowSlotCount,
                tileResolution,
                Mathf.Max(1, lightingData.AdditionalLightShadowAtlasResolution),
                AdditionalLightShadowReceiverDepthBias);
            var atlasResolution = Mathf.Max(1, lightingData.AdditionalLightShadowAtlasResolution);
            var texelSize = new Vector4(1f / atlasResolution, 1f / atlasResolution, atlasResolution, atlasResolution);
            UploadAdditionalLightShadowArrays(
                cmd,
                material,
                lightingData.AdditionalLightShadowData,
                lightingData.AdditionalLightShadowLightParams,
                lightingData.AdditionalLightShadowSliceAtlasRects,
                lightingData.AdditionalLightShadowSliceWorldToShadowMatrices,
                shadowParams,
                texelSize);
        }

        public static void ClearAdditionalLightShadowReceiverGlobals(CommandBuffer cmd)
        {
            ClearAdditionalLightShadowReceiverGlobals(cmd, null);
        }

        public static void ClearAdditionalLightShadowReceiverGlobals(CommandBuffer cmd, Material material)
        {
            UploadAdditionalLightShadowArrays(
                cmd,
                material,
                DisabledShadowData,
                DisabledShadowLightParams,
                DisabledSliceAtlasRects,
                DisabledSliceWorldToShadowMatrices,
                Vector4.zero,
                Vector4.zero);
        }

        public static void BindAdditionalLightShadowAtlasIfValid(CommandBuffer cmd, BurtRenderTargetHandle atlasTarget)
        {
            if (cmd == null || !atlasTarget.IsValid)
            {
                return;
            }

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.AdditionalLightShadowAtlasId, atlasTarget.Identifier);
        }

        public static Vector4 ResolveAtlasRect(int tileIndex)
        {
            var safeTileIndex = Mathf.Clamp(tileIndex, 0, AtlasTileCountX * AtlasTileCountY - 1);
            var tileScaleX = 1f / AtlasTileCountX;
            var tileScaleY = 1f / AtlasTileCountY;
            var tileX = safeTileIndex % AtlasTileCountX;
            var tileY = safeTileIndex / AtlasTileCountX;
            var minX = tileX * tileScaleX;
            var minY = tileY * tileScaleY;
            return new Vector4(minX, minY, minX + tileScaleX, minY + tileScaleY);
        }

        private static bool IsCandidateShadowSlot(BurtLightingData lightingData, int lightIndex)
        {
            if (lightingData == null || lightIndex < 0 || lightIndex >= BurtLightingData.MaxAdditionalLights)
            {
                return false;
            }

            if (lightingData.AdditionalLightShadowData[lightIndex].x <= 0.5f)
            {
                return false;
            }

            if (lightingData.AdditionalLightShadowVisibleLightIndices[lightIndex] < 0)
            {
                return false;
            }

            return lightIndex < lightingData.AdditionalLightCount;
        }

        private static void UploadAdditionalLightShadowArrays(
            CommandBuffer cmd,
            Material material,
            Vector4[] shadowData,
            Vector4[] shadowLightParams,
            Vector4[] sliceAtlasRects,
            Matrix4x4[] sliceWorldToShadowMatrices,
            Vector4 shadowParams,
            Vector4 texelSize)
        {
            var safeShadowData = shadowData != null && shadowData.Length >= BurtLightingData.MaxAdditionalLights ? shadowData : DisabledShadowData;
            var safeLightParams = shadowLightParams != null && shadowLightParams.Length >= BurtLightingData.MaxAdditionalLights ? shadowLightParams : DisabledShadowLightParams;
            var safeSliceAtlasRects = sliceAtlasRects != null && sliceAtlasRects.Length >= BurtLightingData.MaxAdditionalLightShadowSlices ? sliceAtlasRects : DisabledSliceAtlasRects;
            var safeSliceMatrices = sliceWorldToShadowMatrices != null && sliceWorldToShadowMatrices.Length >= BurtLightingData.MaxAdditionalLightShadowSlices ? sliceWorldToShadowMatrices : DisabledSliceWorldToShadowMatrices;

            FillSliceWorldToShadowRows(safeSliceMatrices);
            if (material != null)
            {
                material.SetVectorArray(AdditionalLightShadowDataId, safeShadowData);
                material.SetVectorArray(AdditionalLightShadowLightParamsId, safeLightParams);
                material.SetVectorArray(AdditionalLightShadowSliceAtlasRectsId, safeSliceAtlasRects);
                material.SetVectorArray(AdditionalLightShadowSliceRows0Id, SliceWorldToShadowRows0);
                material.SetVectorArray(AdditionalLightShadowSliceRows1Id, SliceWorldToShadowRows1);
                material.SetVectorArray(AdditionalLightShadowSliceRows2Id, SliceWorldToShadowRows2);
                material.SetVectorArray(AdditionalLightShadowSliceRows3Id, SliceWorldToShadowRows3);
                material.SetVector(AdditionalLightShadowParamsId, shadowParams);
                material.SetVector(AdditionalLightShadowTexelSizeId, texelSize);
            }

            if (cmd != null)
            {
                cmd.SetGlobalVectorArray(AdditionalLightShadowDataId, safeShadowData);
                cmd.SetGlobalVectorArray(AdditionalLightShadowLightParamsId, safeLightParams);
                cmd.SetGlobalVectorArray(AdditionalLightShadowSliceAtlasRectsId, safeSliceAtlasRects);
                cmd.SetGlobalVectorArray(AdditionalLightShadowSliceRows0Id, SliceWorldToShadowRows0);
                cmd.SetGlobalVectorArray(AdditionalLightShadowSliceRows1Id, SliceWorldToShadowRows1);
                cmd.SetGlobalVectorArray(AdditionalLightShadowSliceRows2Id, SliceWorldToShadowRows2);
                cmd.SetGlobalVectorArray(AdditionalLightShadowSliceRows3Id, SliceWorldToShadowRows3);
                cmd.SetGlobalVector(AdditionalLightShadowParamsId, shadowParams);
                cmd.SetGlobalVector(AdditionalLightShadowTexelSizeId, texelSize);
            }
        }

        private static void FillSliceWorldToShadowRows(Matrix4x4[] matrices)
        {
            for (var sliceIndex = 0; sliceIndex < BurtLightingData.MaxAdditionalLightShadowSlices; sliceIndex++)
            {
                var matrix = matrices != null && matrices.Length > sliceIndex ? matrices[sliceIndex] : Matrix4x4.identity;
                SliceWorldToShadowRows0[sliceIndex] = matrix.GetRow(0);
                SliceWorldToShadowRows1[sliceIndex] = matrix.GetRow(1);
                SliceWorldToShadowRows2[sliceIndex] = matrix.GetRow(2);
                SliceWorldToShadowRows3[sliceIndex] = matrix.GetRow(3);
            }
        }

        private static Matrix4x4[] CreateIdentitySliceMatrixArray()
        {
            var matrices = new Matrix4x4[BurtLightingData.MaxAdditionalLightShadowSlices];
            for (var sliceIndex = 0; sliceIndex < matrices.Length; sliceIndex++)
            {
                matrices[sliceIndex] = Matrix4x4.identity;
            }

            return matrices;
        }

        private static Vector4[] CreateDefaultSliceAtlasRectArray()
        {
            var rects = new Vector4[BurtLightingData.MaxAdditionalLightShadowSlices];
            for (var sliceIndex = 0; sliceIndex < rects.Length; sliceIndex++)
            {
                rects[sliceIndex] = new Vector4(0f, 0f, 1f, 1f);
            }

            return rects;
        }
    }

    internal sealed class BurtAllocateAdditionalLightShadowAtlasPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Additional Light Shadow Atlas";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtAdditionalLightShadowUtility.ShouldUseAdditionalLightShadows(builder.Request))
            {
                return;
            }

            builder.WriteAdditionalLightShadowAtlas();
            builder.WriteShadowGlobals();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var request = context.Request;
            if (!BurtAdditionalLightShadowUtility.ShouldUseAdditionalLightShadows(request))
            {
                return;
            }

            var lightingData = request.LightingData;
            lightingData.SetAdditionalLightShadowCacheState(
                false,
                BurtLightingData.DefaultAdditionalLightShadowTileResolution,
                BurtLightingData.DefaultAdditionalLightShadowTileResolution * BurtAdditionalLightShadowUtility.AtlasTileCountX,
                BurtAdditionalLightShadowUtility.AtlasTileCountX,
                BurtAdditionalLightShadowUtility.AtlasTileCountY,
                0);
            var atlasTarget = context.AdditionalLightShadowAtlasTarget;
            lightingData.SetAdditionalLightShadowAtlasState(true, atlasTarget.IsValid);
            if (!atlasTarget.IsValid)
            {
                MarkCandidateShadowSlots(lightingData, BurtAdditionalLightShadowStatus.AtlasInvalid);
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateAdditionalLightShadowAtlasDescriptor(lightingData);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.AdditionalLightShadowAtlasId, descriptor, FilterMode.Bilinear);
            BurtShadowRenderTargetUtility.SetDepthOnlyShadowRenderTarget(cmd, atlasTarget);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.ClearRenderTarget(true, false, Color.clear, BurtShadowRenderTargetUtility.ResolveMainLightShadowClearDepth());
            BurtAdditionalLightShadowUtility.BindAdditionalLightShadowAtlasIfValid(cmd, atlasTarget);
            BurtAdditionalLightShadowUtility.ClearAdditionalLightShadowReceiverGlobals(cmd);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void MarkCandidateShadowSlots(BurtLightingData lightingData, BurtAdditionalLightShadowStatus status)
        {
            if (lightingData == null)
            {
                return;
            }

            var additionalLightCount = Mathf.Min(lightingData.AdditionalLightCount, BurtLightingData.MaxAdditionalLights);
            for (var lightIndex = 0; lightIndex < additionalLightCount; lightIndex++)
            {
                if (lightingData.AdditionalLightShadowData[lightIndex].x > 0.5f)
                {
                    lightingData.SetAdditionalLightShadowStatus(lightIndex, status);
                }
            }
        }
    }

    internal sealed class BurtDrawAdditionalLightShadowCasterPass : BurtRenderPass
    {
        private const string CastingPunctualLightShadowKeyword = "_CASTING_PUNCTUAL_LIGHT_SHADOW";
        private static readonly int ShadowCasterLightPositionId = Shader.PropertyToID("_BurtShadowCasterLightPosition");
        private static readonly int CastingPunctualLightShadowId = Shader.PropertyToID("_BurtCastingPunctualLightShadow");
        private static readonly int MainLightShadowDepthBiasId = Shader.PropertyToID("_BurtMainLightShadowDepthBias");
        private static readonly int MainLightShadowNormalBiasId = Shader.PropertyToID("_BurtMainLightShadowNormalBias");
        private static readonly int UnityLightDirectionId = Shader.PropertyToID("_LightDirection");
        private static readonly int UnityLightPositionId = Shader.PropertyToID("_LightPosition");
        private static readonly int UnityShadowBiasId = Shader.PropertyToID("_ShadowBias");
        private static readonly int UnityWorldToCameraId = Shader.PropertyToID("unity_WorldToCamera");
        private static readonly int UnityCameraToWorldId = Shader.PropertyToID("unity_CameraToWorld");

        public override string Name => "Burt Draw Additional Light Shadow Caster";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtAdditionalLightShadowUtility.ShouldUseAdditionalLightShadows(builder.Request))
            {
                return;
            }

            builder.WriteAdditionalLightShadowAtlas();
            builder.WriteShadowGlobals();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var request = context.Request;
            if (!BurtAdditionalLightShadowUtility.ShouldUseAdditionalLightShadows(request))
            {
                return;
            }

            var camera = request.Camera;
            var atlasTarget = context.AdditionalLightShadowAtlasTarget;
            if (camera == null || !atlasTarget.IsValid)
            {
                request.LightingData?.SetAdditionalLightShadowAtlasState(atlasTarget.IsValid, camera != null && atlasTarget.IsValid);
                return;
            }

            if (!BurtAdditionalLightShadowUtility.TryPrepareAdditionalLightShadowAtlas(request, out var lightingData))
            {
                DisableAdditionalLightShadowReceiverGlobals(context.ScriptableContext);
                return;
            }

            var atlasResolution = Mathf.Max(1, lightingData.AdditionalLightShadowAtlasResolution);
            var tileResolution = Mathf.Max(1, lightingData.AdditionalLightShadowTileResolution);
            var cmd = CommandBufferPool.Get(Name);
            try
            {
                BurtShadowRenderTargetUtility.SetDepthOnlyShadowRenderTarget(cmd, atlasTarget);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, atlasResolution, atlasResolution);
                cmd.ClearRenderTarget(true, false, Color.clear, BurtShadowRenderTargetUtility.ResolveMainLightShadowClearDepth());
                cmd.SetGlobalDepthBias(0f, 0f);
                SetWorldToCameraAndCameraToWorldMatrices(cmd, camera.worldToCameraMatrix);
                BurtAdditionalLightShadowUtility.UploadAdditionalLightShadowReceiverGlobals(cmd, null, atlasTarget, lightingData);
                context.ScriptableContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var activeSliceCount = Mathf.Clamp(lightingData.AdditionalLightShadowActiveSliceCount, 0, BurtLightingData.MaxAdditionalLightShadowSlices);
                for (var sliceIndex = 0; sliceIndex < activeSliceCount; sliceIndex++)
                {
                    var lightIndex = lightingData.AdditionalLightShadowSliceLightIndices[sliceIndex];
                    if (!BurtAdditionalLightShadowUtility.IsActiveShadowSlot(lightingData, lightIndex))
                    {
                        continue;
                    }

                    var atlasRect = lightingData.AdditionalLightShadowSliceAtlasRects[sliceIndex];
                    var viewport = new Rect(
                        atlasRect.x * atlasResolution,
                        atlasRect.y * atlasResolution,
                        Mathf.Max(1f, (atlasRect.z - atlasRect.x) * atlasResolution),
                        Mathf.Max(1f, (atlasRect.w - atlasRect.y) * atlasResolution));
                    var shadowDirection = ResolveAdditionalLightShadowDirection(lightingData, lightIndex, sliceIndex);
                    var lightPosition = lightingData.AdditionalLightPositionAndRange[lightIndex];
                    var depthBias = BurtAdditionalLightShadowUtility.ResolveAdditionalLightShadowCasterDepthBias(request, lightingData, lightIndex, tileResolution);
                    var normalBias = BurtAdditionalLightShadowUtility.ResolveAdditionalLightShadowCasterNormalBias(request, lightingData, lightIndex, tileResolution);

                    BurtShadowRenderTargetUtility.SetDepthOnlyShadowRenderTarget(cmd, atlasTarget);
                    cmd.SetViewport(viewport);
                    cmd.EnableScissorRect(viewport);
                    cmd.SetViewProjectionMatrices(lightingData.AdditionalLightShadowSliceViewMatrices[sliceIndex], lightingData.AdditionalLightShadowSliceProjectionMatrices[sliceIndex]);
                    cmd.SetGlobalDepthBias(1f, 2.5f);
                    cmd.SetGlobalVector(UnityLightDirectionId, new Vector4(shadowDirection.x, shadowDirection.y, shadowDirection.z, 0f));
                    cmd.SetGlobalVector(ShadowCasterLightPositionId, new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, 1f));
                    cmd.SetGlobalVector(UnityLightPositionId, new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, 1f));
                    cmd.SetGlobalFloat(CastingPunctualLightShadowId, 1f);
                    cmd.SetGlobalFloat(MainLightShadowDepthBiasId, depthBias);
                    cmd.SetGlobalFloat(MainLightShadowNormalBiasId, normalBias);
                    cmd.SetGlobalVector(UnityShadowBiasId, new Vector4(depthBias, normalBias, 0f, 0f));
                    SetKeyword(cmd, CastingPunctualLightShadowKeyword, true);
                    context.ScriptableContext.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    var visibleLightIndex = lightingData.AdditionalLightShadowVisibleLightIndices[lightIndex];
                    if (BurtShadowRenderTargetUtility.HasShadowCasters(request.CullingResults, visibleLightIndex))
                    {
                        var shadowDrawingSettings = new ShadowDrawingSettings(request.CullingResults, visibleLightIndex, BatchCullingProjectionType.Perspective);
                        shadowDrawingSettings.splitData = lightingData.AdditionalLightShadowSliceSplitDatas[sliceIndex];
                        context.ScriptableContext.DrawShadows(ref shadowDrawingSettings);
                    }

                    cmd.DisableScissorRect();
                    context.ScriptableContext.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }
            }
            finally
            {
                CommandBufferPool.Release(cmd);
                ResetAdditionalLightShadowCasterState(context, context.ScriptableContext, camera);
            }

            UploadAdditionalLightShadowReceiverGlobals(context.ScriptableContext, atlasTarget, lightingData);
        }

        private static bool IsPointAdditionalLightShadow(BurtLightingData lightingData, int lightIndex)
        {
            return lightingData != null &&
                lightIndex >= 0 &&
                lightIndex < BurtLightingData.MaxAdditionalLights &&
                lightingData.AdditionalLightShadowLightParams[lightIndex].z > 0.5f &&
                lightingData.AdditionalLightShadowLightParams[lightIndex].z < 1.5f;
        }

        private static void SetWorldToCameraAndCameraToWorldMatrices(CommandBuffer cmd, Matrix4x4 viewMatrix)
        {
            if (cmd == null)
            {
                return;
            }

            var worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * viewMatrix;
            cmd.SetGlobalMatrix(UnityWorldToCameraId, worldToCameraMatrix);
            cmd.SetGlobalMatrix(UnityCameraToWorldId, worldToCameraMatrix.inverse);
        }

        private static Vector3 ResolveAdditionalLightShadowDirection(BurtLightingData lightingData, int lightIndex, int sliceIndex)
        {
            if (lightingData != null &&
                lightIndex >= 0 &&
                lightIndex < BurtLightingData.MaxAdditionalLights &&
                sliceIndex >= 0 &&
                sliceIndex < BurtLightingData.MaxAdditionalLightShadowSlices &&
                IsPointAdditionalLightShadow(lightingData, lightIndex))
            {
                return ResolvePointLightShadowFaceDirection(lightingData.AdditionalLightShadowSliceFaceIndices[sliceIndex]);
            }

            var direction = lightingData != null ? lightingData.AdditionalLightDirectionAndSpot[lightIndex] : Vector4.zero;
            var shadowDirection = new Vector3(-direction.x, -direction.y, -direction.z);
            return shadowDirection.sqrMagnitude > 0.0001f ? shadowDirection.normalized : Vector3.forward;
        }

        private static Vector3 ResolvePointLightShadowFaceDirection(int faceIndex)
        {
            switch ((CubemapFace)Mathf.Clamp(faceIndex, 0, BurtLightingData.PointLightShadowFaceCount - 1))
            {
                case CubemapFace.PositiveX:
                    return Vector3.right;
                case CubemapFace.NegativeX:
                    return Vector3.left;
                case CubemapFace.PositiveY:
                    return Vector3.up;
                case CubemapFace.NegativeY:
                    return Vector3.down;
                case CubemapFace.PositiveZ:
                    return Vector3.forward;
                case CubemapFace.NegativeZ:
                    return Vector3.back;
                default:
                    return Vector3.forward;
            }
        }

        private static void UploadAdditionalLightShadowReceiverGlobals(ScriptableRenderContext renderContext, BurtRenderTargetHandle atlasTarget, BurtLightingData lightingData)
        {
            if (lightingData == null || !atlasTarget.IsValid)
            {
                DisableAdditionalLightShadowReceiverGlobals(renderContext);
                return;
            }

            var cmd = CommandBufferPool.Get("Burt Upload Additional Light Shadow Receiver");
            BurtAdditionalLightShadowUtility.UploadAdditionalLightShadowReceiverGlobals(cmd, null, atlasTarget, lightingData);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void ResetAdditionalLightShadowCasterState(BurtRenderGraphContext context, ScriptableRenderContext renderContext, Camera camera)
        {
            var cmd = CommandBufferPool.Get("Burt Reset Additional Light Shadow Caster State");
            cmd.DisableScissorRect();
            cmd.SetGlobalDepthBias(0f, 0f);
            cmd.SetGlobalFloat(CastingPunctualLightShadowId, 0f);
            cmd.SetGlobalFloat(MainLightShadowDepthBiasId, 0f);
            cmd.SetGlobalFloat(MainLightShadowNormalBiasId, 0f);
            cmd.SetGlobalVector(UnityLightDirectionId, Vector4.zero);
            cmd.SetGlobalVector(UnityLightPositionId, Vector4.zero);
            cmd.SetGlobalVector(ShadowCasterLightPositionId, Vector4.zero);
            cmd.SetGlobalVector(UnityShadowBiasId, Vector4.zero);
            SetKeyword(cmd, CastingPunctualLightShadowKeyword, false);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            BurtDrawingSettingsUtility.RestoreCameraMatricesForMainDraw(context, cmd);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            if (camera != null && !BurtDrawingSettingsUtility.IsTemporalAAEnabled(context))
            {
                renderContext.SetupCameraProperties(camera);
            }
        }

        private static void DisableAdditionalLightShadowReceiverGlobals(ScriptableRenderContext renderContext)
        {
            var cmd = CommandBufferPool.Get("Burt Disable Additional Light Shadow Receiver");
            BurtAdditionalLightShadowUtility.ClearAdditionalLightShadowReceiverGlobals(cmd);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void SetKeyword(CommandBuffer cmd, string keyword, bool enabled)
        {
            if (cmd == null || string.IsNullOrEmpty(keyword))
            {
                return;
            }

            if (enabled)
            {
                cmd.EnableShaderKeyword(keyword);
            }
            else
            {
                cmd.DisableShaderKeyword(keyword);
            }
        }
    }

    internal sealed class BurtReleaseAdditionalLightShadowAtlasPass : BurtRenderPass
    {
        public override string Name => "Burt Release Additional Light Shadow Atlas";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtAdditionalLightShadowUtility.ShouldUseAdditionalLightShadows(builder.Request))
            {
                return;
            }

            builder.ReadAdditionalLightShadowAtlas();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtAdditionalLightShadowUtility.ShouldUseAdditionalLightShadows(context.Request))
            {
                return;
            }

            var atlasTarget = context.AdditionalLightShadowAtlasTarget;
            if (!atlasTarget.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.AdditionalLightShadowAtlasId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtSetupLightingPass : BurtRenderPass
    {
        private static readonly int MainLightDirectionId = Shader.PropertyToID("_BurtMainLightDirection");
        private static readonly int MainLightColorId = Shader.PropertyToID("_BurtMainLightColor");
        private static readonly int AmbientLightColorId = Shader.PropertyToID("_BurtAmbientLightColor");
        private static readonly int AdditionalLightCountId = Shader.PropertyToID("_BurtAdditionalLightCount");
        private static readonly int AdditionalLightPositionAndRangeId = Shader.PropertyToID("_BurtAdditionalLightPositionAndRange");
        private static readonly int AdditionalLightColorAndTypeId = Shader.PropertyToID("_BurtAdditionalLightColorAndType");
        private static readonly int AdditionalLightDirectionAndSpotId = Shader.PropertyToID("_BurtAdditionalLightDirectionAndSpot");
        private static readonly int AdditionalLightSpotParamsId = Shader.PropertyToID("_BurtAdditionalLightSpotParams");
        private static readonly int AdditionalLightBufferId = Shader.PropertyToID("_BurtAdditionalLightBuffer");
        private static readonly int AdditionalLightBufferEnabledId = Shader.PropertyToID("_BurtAdditionalLightBufferEnabled");

        public override string Name => "Burt Setup Lighting";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.WriteLightingGlobals();
            builder.WriteShadowGlobals();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var renderContext = context.ScriptableContext;
            var request = context.Request;
            var lightingData = ResolveLightingData(request);
            var asset = context.Asset;
            var shadowData = BurtShadowUtility.ResolveMainLightShadowData(request, asset);
            var mainLightDirection = lightingData.MainLightDirection;
            var mainLightColor = lightingData.MainLightColor;
            var ambientLightColor = lightingData.AmbientLightColor;
            var hasMainLightShadow = BurtMainLightShadowMatrixUtility.TryGetMainLightShadowCascadeCache(request, shadowData, out var cascadeCache);
            var cascadeCount = hasMainLightShadow ? cascadeCache.CascadeCount : 0;
            var tileResolution = hasMainLightShadow ? cascadeCache.TileResolution : 0;

            var cmd = CommandBufferPool.Get(Name);
            PreExposureUtility.UploadGlobals(cmd, PreExposureUtility.ResolveForFrame(request, asset));
            cmd.SetGlobalVector(MainLightDirectionId, new Vector4(mainLightDirection.x, mainLightDirection.y, mainLightDirection.z, 0f));
            cmd.SetGlobalColor(MainLightColorId, mainLightColor);
            cmd.SetGlobalColor(AmbientLightColorId, ambientLightColor);
            cmd.SetGlobalFloat(AdditionalLightCountId, lightingData.AdditionalLightCount);
            cmd.SetGlobalVectorArray(AdditionalLightPositionAndRangeId, lightingData.AdditionalLightPositionAndRange);
            cmd.SetGlobalVectorArray(AdditionalLightColorAndTypeId, lightingData.AdditionalLightColorAndType);
            cmd.SetGlobalVectorArray(AdditionalLightDirectionAndSpotId, lightingData.AdditionalLightDirectionAndSpot);
            cmd.SetGlobalVectorArray(AdditionalLightSpotParamsId, lightingData.AdditionalLightSpotParams);
            UploadAdditionalLightBuffer(cmd, context, lightingData);
            BurtIndirectLightingUtility.UploadGlobalIndirectLighting(cmd, request);

            if (hasMainLightShadow)
            {
                BurtMainLightShadowMatrixUtility.UploadMainLightShadowReceiverGlobals(cmd, null, cascadeCache.WorldToShadowMatrices, cascadeCache.CascadeSpheres, cascadeCache.CascadeAtlasRects, cascadeCount, tileResolution, shadowData);
            }
            else
            {
                BurtMainLightShadowMatrixUtility.ClearMainLightShadowReceiverGlobals(cmd);
            }

            BurtAdditionalLightShadowUtility.UploadAdditionalLightShadowReceiverGlobals(cmd, null, lightingData);
            BurtPerObjectShadowUtility.ClearPerObjectShadowReceiverGlobals(cmd);

            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void UploadAdditionalLightBuffer(CommandBuffer cmd, BurtRenderGraphContext context, BurtLightingData lightingData)
        {
            if (cmd == null || context == null || lightingData == null)
            {
                return;
            }

            lightingData.SetAdditionalLightBufferUploadState(true, false);
            var additionalLightBuffer = context.AdditionalLightBuffer;
            if (!additionalLightBuffer.IsValid || !additionalLightBuffer.HasBuffer)
            {
                cmd.SetGlobalFloat(AdditionalLightBufferEnabledId, 0f);
                return;
            }

            additionalLightBuffer.Buffer.SetData(lightingData.AdditionalLightBufferData);
            cmd.SetGlobalBuffer(AdditionalLightBufferId, additionalLightBuffer.Buffer);
            cmd.SetGlobalFloat(AdditionalLightBufferEnabledId, 1f);
            lightingData.SetAdditionalLightBufferUploadState(true, true);
        }

        private static BurtLightingData ResolveLightingData(BurtRenderRequest request)
        {
            if (request == null || request.LightingData == null)
            {
                return BurtLightingData.Default();
            }

            return request.LightingData;
        }
    }

    internal sealed class BurtDepthPrepass : BurtRenderPass // 定义深度预写 Pass，负责在颜色绘制前先写入 CameraDepth。
    {
        public override string Name => "Burt Depth Prepass"; // 返回这个 Pass 的名称，方便 Frame Debugger 和 RenderGraph Debug 输出识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteCameraDepth(); // 声明这个 Pass 只写 CameraDepth，不写 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现深度预写 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来创建排序设置。

            var cameraDepthTarget = context.CameraDepthTarget; // 从 GraphContext 中取出 CameraDepth 资源句柄。

            if (!cameraDepthTarget.IsValid) // 如果 CameraDepth 无效，说明当前图没有可写入的深度资源。
            {
                return; // 直接结束这个 Pass，避免无效深度预写。
            }

            var sortingSettings = new SortingSettings(camera); // 创建排序设置，Unity 会根据相机信息计算排序参数。

            sortingSettings.criteria = SortingCriteria.CommonOpaque; // 深度预写只处理不透明物体，使用 CommonOpaque 有利于前到后减少 overdraw。

            var drawingSettings = BurtDrawingSettingsUtility.CreateDepthDrawingSettings(sortingSettings); // 创建只匹配 BurtDepthOnly shader pass 的绘制设置。

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque); // 创建过滤设置，只允许渲染队列属于 opaque 范围的物体通过。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 使用 request 的剔除结果绘制可见不透明物体的深度。
        }
    }

    internal sealed class BurtDrawOpaquePass : BurtRenderPass // 定义不透明物体颜色绘制 Pass。
    {
        public override string Name => "Burt Draw Opaque"; // 返回这个 Pass 的名称，后面可以用于调试和性能分析。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            var depthPrepassEnabled = builder.Asset == null || builder.Asset.EnableDepthPrepass; // 判断当前图是否包含 Depth Prepass，asset 为空时沿用 Assembler 的默认开启规则。

            if (depthPrepassEnabled) // 如果前面已经有 Depth Prepass，这个颜色 Pass 就会读取已经写好的 CameraDepth 做深度测试。
            {
                builder.ReadCameraDepth(); // 声明这个 Pass 会读取 Depth Prepass 写好的 CameraDepth。
            }

            builder.ReadLightingGlobals(); // 声明不透明前向着色会读取 Setup Lighting 上传的灯光全局状态。

            builder.ReadShadowGlobals(); // 声明不透明前向着色会读取阴影矩阵、强度、texel size 等阴影全局状态。

            if (BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 真的生成主光阴影图，就把 shadow map 声明为着色输入。
            {
                builder.ReadMainLightShadowMap(); // 声明不透明前向着色会采样 MainLightShadowMap。
            }

            builder.WriteCameraColor(); // 声明这个 Pass 会把不透明物体颜色写入 CameraColor。

            builder.WriteCameraDepth(); // 声明这个 Pass 当前仍可能通过 ZWrite 更新 CameraDepth。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
            if (!BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name))
            {
                return;
            }

            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来创建排序设置。

            var sortingSettings = new SortingSettings(camera); // 创建排序设置，Unity 会根据相机信息计算排序参数。

            sortingSettings.criteria = SortingCriteria.CommonOpaque; // 设置不透明物体排序规则，通常有利于 early-z 和减少 overdraw。

            var drawingSettings = BurtDrawingSettingsUtility.CreateForwardDrawingSettings(sortingSettings); // 创建前向颜色绘制设置，匹配 BurtForward 等颜色 Pass。

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque); // 创建过滤设置，只允许渲染队列属于 opaque 范围的物体通过。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 使用 request 的剔除结果绘制所有可见不透明物体。
        }
    }

    internal sealed class BurtDrawDeferredForwardOnlyOpaquePass : BurtRenderPass // 定义 Deferred 后的前向兜底不透明 Pass，只绘制显式标记为 BurtForwardOnly 的物体。
    {
        public override string Name => "Burt Draw Deferred Forward Only Opaque"; // 返回这个 Pass 的名称，方便在 RenderGraph Debug 和 Frame Debugger 里确认它不是普通 Forward Opaque。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraDepth(); // 声明前向兜底物体会读取 Deferred/GBuffer 阶段已经建立好的 CameraDepth 做深度测试。

            builder.ReadLightingGlobals(); // 声明前向兜底 shader 会读取主光颜色、方向等灯光全局状态。

            builder.ReadShadowGlobals(); // 声明前向兜底 shader 会读取阴影矩阵、强度和 texel size 等阴影全局状态。

            if (BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 真的生成主光阴影图，就声明 shadow map 输入。
            {
                builder.ReadMainLightShadowMap(); // 声明前向兜底 shader 会采样 MainLightShadowMap。
            }

            builder.WriteCameraColor(); // 声明前向兜底结果会写回 Deferred Lighting 已经生成的 CameraColor。

            builder.WriteCameraDepth(); // 声明兜底不透明物体仍可能写入深度，保证后续 Skybox 和 Transparent 能按最终深度测试。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 Deferred 后的前向兜底不透明绘制。
        {
            if (!BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name))
            {
                return;
            }

            var renderContext = context.ScriptableContext; // 从 GraphContext 取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 取出当前渲染请求。

            var camera = request.Camera; // 从 request 取出当前相机，用来创建排序设置。

            var sortingSettings = new SortingSettings(camera); // 创建排序设置，Unity 会根据相机矩阵和位置计算排序参数。

            sortingSettings.criteria = SortingCriteria.CommonOpaque; // 使用不透明排序，尽量保持 early-z 和稳定绘制顺序。

            var drawingSettings = BurtDrawingSettingsUtility.CreateForwardOnlyDrawingSettings(sortingSettings); // 只匹配 LightMode=BurtForwardOnly 的 shader，避免重复绘制 BurtGBuffer/BurtForward 材质。

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque); // 只允许不透明队列通过，透明仍交给后面的 Draw Transparent Pass。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 绘制所有显式声明 BurtForwardOnly 的可见不透明物体。
        }
    }

    internal sealed class BurtDrawEditorPreviewPass : BurtRenderPass // 定义编辑器 Preview 专用绘制 Pass，兼容 Unity 内部资产预览 shader。
    {
        public override string Name => "Burt Draw Editor Preview"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 识别 Preview 是否走专用路径。

        public override void Configure(BurtRenderPassBuilder builder) // 声明 Preview 绘制会使用的资源。
        {
            builder.ReadLightingGlobals(); // BurtRP 材质预览仍可能需要基础灯光全局状态。

            builder.ReadShadowGlobals(); // 读取 Setup Lighting 写入的默认阴影状态，避免 shader 读到上一相机残留。

            if (BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果 Preview 场景真的生成了主光阴影，就声明 shadow map 依赖。
            {
                builder.ReadMainLightShadowMap(); // 让 Preview 中的 BurtRP lit 材质可以保持和 Forward 路径一致。
            }

            builder.WriteCameraColor(); // Preview 结果写入中间颜色，后续仍统一交给 FinalBlit 输出。

            builder.WriteCameraDepth(); // Preview 内部物体需要正常深度测试和写入。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行编辑器 Preview 绘制。
        {
            var renderContext = context.ScriptableContext; // 读取 Unity SRP 上下文，用来提交渲染目标绑定和 DrawRenderers。

            var request = context.Request; // 读取当前 request，用来访问相机和剔除结果。

            var camera = request.Camera; // Preview 相机来自 Unity 编辑器内部。

            var cameraColorTarget = context.CameraColorTarget; // 读取 Preview 中间颜色目标。

            var cameraDepthTarget = context.CameraDepthTarget; // 读取 Preview 中间深度目标。

            if (!cameraColorTarget.IsValid || !cameraDepthTarget.IsValid) // 任一目标无效时不能安全绘制。
            {
                return; // 直接跳过，避免把 Unity 内部预览画到错误目标。
            }

            var cmd = CommandBufferPool.Get(Name); // 重新绑定目标，防止前序内部预览状态影响 DrawRenderers。

            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier); // 使用 BurtRP 当前 request 的颜色和深度目标。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);

            renderContext.ExecuteCommandBuffer(cmd); // 提交绑定命令。

            CommandBufferPool.Release(cmd); // 释放 CommandBuffer，避免每帧分配。

            DrawPreviewRenderers(renderContext, request, camera, RenderQueueRange.opaque, SortingCriteria.CommonOpaque); // 先绘制不透明预览物体。

            DrawPreviewRenderers(renderContext, request, camera, RenderQueueRange.transparent, SortingCriteria.CommonTransparent); // 再绘制透明预览物体。
        }

        private static void DrawPreviewRenderers( // 绘制一段指定队列范围的 Preview renderer。
            ScriptableRenderContext renderContext, // 接收 Unity 渲染上下文。
            BurtRenderRequest request, // 接收当前 request，用来读取剔除结果。
            Camera camera, // 接收当前 Preview 相机。
            RenderQueueRange renderQueueRange, // 接收要绘制的不透明或透明队列范围。
            SortingCriteria sortingCriteria) // 接收对应队列的排序规则。
        {
            var sortingSettings = new SortingSettings(camera); // 基于 Preview 相机创建排序设置。

            sortingSettings.criteria = sortingCriteria; // 使用调用方指定的不透明或透明排序。

            var drawingSettings = BurtDrawingSettingsUtility.CreateEditorPreviewDrawingSettings(sortingSettings); // 使用 Preview 专用的宽松 LightMode 列表。

            var filteringSettings = new FilteringSettings(renderQueueRange); // 只绘制当前队列范围，保证透明物体顺序在不透明之后。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 绘制 Unity 内部资产预览和 BurtRP 材质预览物体。
        }
    }

    internal sealed class BurtDrawSkyboxPass : BurtRenderPass // 定义天空盒绘制 Pass。
    {
        public override string Name => "Burt Draw Skybox"; // 返回这个 Pass 的名称，后面可以用于调试和性能分析。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraDepth(); // 声明这个 Pass 会参考当前深度状态来把天空盒放在场景背景位置。

            builder.WriteCameraColor(); // 声明这个 Pass 在 Skybox 模式下会把天空盒颜色写入 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
            if (!BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name))
            {
                return;
            }

            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来绘制天空盒。

            if (camera == null) // 如果当前相机为空，就没有办法绘制天空盒。
            {
                return; // 直接结束这个 Pass。
            }

            var clearMode = BurtCameraClearUtility.ResolveClearMode(request); // 统一解析清屏模式，让 SceneView 没有 BurtCameraData 时也能识别 Unity 的 Skybox clearFlags。

            if (clearMode != BurtCameraClearMode.Skybox) // 如果当前清屏模式不是 Skybox，就不绘制天空盒。
            {
                return; // 直接结束这个 Pass。
            }

            if (BurtAtmosphereUtility.ShouldUseAtmosphere(request))
            {
                return;
            }

            renderContext.DrawSkybox(camera); // 使用 Unity SRP 上下文绘制当前相机的天空盒。
        }
    }

    internal sealed class BurtDrawRefractionDistortionPass : BurtRenderPass
    {
        private static readonly Color InvalidDistortionClearColor = new Color(0.0f, 0.0f, 0.0f, 65504.0f);

        public override string Name => "Burt Draw Refraction Distortion";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.DrawRenderers;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtRefractionPassUtility.ShouldUseRefraction(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.WriteRefractionDistortion();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtRefractionPassUtility.ShouldUseRefraction(context.Request, context.Asset))
            {
                return;
            }

            var distortionTarget = context.RefractionDistortionTarget;
            var cameraDepthTarget = context.CameraDepthTarget;
            if (!distortionTarget.IsValid || !cameraDepthTarget.IsValid)
            {
                return;
            }

            var request = context.Request;
            var camera = request != null ? request.Camera : null;
            if (camera == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(distortionTarget.Identifier, cameraDepthTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            BurtDrawingSettingsUtility.RestoreCameraMatricesForMainDraw(context, cmd);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.ClearRenderTarget(false, true, InvalidDistortionClearColor);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            var sortingSettings = new SortingSettings(camera);
            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            var drawingSettings = BurtDrawingSettingsUtility.CreateRefractionDistortionDrawingSettings(sortingSettings);
            var filteringSettings = new FilteringSettings(RenderQueueRange.transparent);
            context.ScriptableContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings);
        }
    }

    internal sealed class BurtDrawTransparentPass : BurtRenderPass // 定义透明物体绘制 Pass。
    {
        public override string Name => "Burt Draw Transparent"; // 返回这个 Pass 的名称，后面可以用于调试和性能分析。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraColor(); // 声明透明物体需要读取已有 CameraColor 作为混合背景。

            builder.ReadCameraDepth(); // 声明透明物体需要读取当前 CameraDepth 参与深度测试。

            builder.ReadLightingGlobals(); // 声明透明前向着色会读取 Setup Lighting 上传的灯光全局状态。

            builder.ReadShadowGlobals(); // 声明透明前向着色会读取阴影矩阵、强度、texel size 等阴影全局状态。

            if (BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 真的生成主光阴影图，就把 shadow map 声明为透明着色输入。
            {
                builder.ReadMainLightShadowMap(); // 声明透明前向着色会采样 MainLightShadowMap。
            }

            builder.WriteCameraColor(); // 声明透明混合结果会写回 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
            if (!BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name))
            {
                return;
            }

            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来创建排序设置。

            var sortingSettings = new SortingSettings(camera); // 创建排序设置，Unity 会根据相机信息计算透明排序参数。

            sortingSettings.criteria = SortingCriteria.CommonTransparent; // 设置透明物体排序规则，通常从后往前绘制以保证混合正确。

            var drawingSettings = BurtDrawingSettingsUtility.CreateForwardDrawingSettings(sortingSettings); // 创建前向颜色绘制设置，匹配 BurtForward 等颜色 Pass。

            var filteringSettings = new FilteringSettings(RenderQueueRange.transparent); // 创建过滤设置，只允许渲染队列属于 transparent 范围的物体通过。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 使用 request 的剔除结果绘制所有可见透明物体。
        }
    }

    internal sealed class BurtDrawUnsupportedShadersPass : BurtRenderPass // 用错误材质绘制非 BurtRP shader，让不支持的材质在画面中明显暴露。
    {
        private const string ErrorShaderName = "Hidden/InternalErrorShader"; // 保存 Unity 内置错误 shader 名称。

        private Material errorMaterial; // 缓存运行时错误材质，避免每帧重复创建。

        private bool hasLoggedMissingErrorShader; // 记录缺失错误 shader 的警告是否已经打印，避免 Console 刷屏。

        public override string Name => "Burt Draw Unsupported Shaders"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 标记。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 使用的资源。
        {
            builder.ReadCameraDepth(); // 声明错误材质绘制会读取当前 CameraDepth 做深度测试。

            builder.WriteCameraColor(); // 声明错误材质绘制会把可见像素写入 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行不支持 shader 的调试绘制。
        {
            var renderContext = context.ScriptableContext; // 读取 Unity SRP 上下文，用来提交命令并绘制 renderer。

            var request = context.Request; // 读取当前渲染请求，用来访问剔除结果和相机。

            var camera = request.Camera; // 读取当前相机，用来创建排序设置。

            var cameraColorTarget = context.CameraColorTarget; // 读取接收错误材质输出的 CameraColor 目标。

            var cameraDepthTarget = context.CameraDepthTarget; // 读取控制错误材质深度测试的 CameraDepth 目标。

            if (!cameraColorTarget.IsValid) // 检查 RenderGraph 是否注册了有效颜色目标。
            {
                return; // 没有安全的颜色目标时直接结束，避免错误材质画到无效 RT。
            }

            if (!cameraDepthTarget.IsValid) // 检查 RenderGraph 是否注册了有效深度目标。
            {
                return; // 没有当前深度目标时直接结束，避免错误材质使用错误深度状态。
            }

            var material = GetErrorMaterial(); // 获取缓存的 Unity 错误材质，第一次使用时会创建。

            if (material == null) // 检查错误材质是否创建失败。
            {
                return; // 没有有效 override material 时直接结束，避免 DrawRenderers 报错。
            }

            var cmd = CommandBufferPool.Get(Name); // 从命令缓冲池获取一个以当前 Pass 命名的 CommandBuffer。

            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier); // 在绘制不支持 shader 前重新绑定当前 request 的颜色和深度目标。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            BurtDrawingSettingsUtility.RestoreCameraMatricesForMainDraw(context, cmd);

            renderContext.ExecuteCommandBuffer(cmd); // 把渲染目标绑定命令提交给 Unity 渲染上下文。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回 Unity 池，避免每帧分配。

            var sortingSettings = new SortingSettings(camera); // 基于当前相机创建排序设置。

            sortingSettings.criteria = SortingCriteria.CommonOpaque; // 使用稳定的不透明排序，保证调试覆盖绘制顺序可预测。

            var drawingSettings = BurtDrawingSettingsUtility.CreateUnsupportedDrawingSettings(sortingSettings, material); // 创建会匹配已知非 BurtRP LightMode 的 DrawingSettings。

            var filteringSettings = FilteringSettings.defaultValue; // 使用默认过滤，让任意队列中的不支持 shader 都有机会被报告。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 绘制所有 shader pass 不被 BurtRP 支持的可见 renderer。
        }

        private Material GetErrorMaterial() // 获取或创建用于绘制不支持 shader 的错误材质。
        {
            if (errorMaterial != null) // 检查错误材质是否已经创建。
            {
                return errorMaterial; // 返回缓存的材质实例。
            }

            var shader = Shader.Find(ErrorShaderName); // 通过名称查找 Unity 内置错误 shader。

            if (shader == null) // 检查错误 shader 查找是否失败。
            {
                if (!hasLoggedMissingErrorShader) // 检查这个警告是否已经输出过。
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ErrorShaderName); // 输出一次警告，方便诊断内置错误 shader 缺失。

                    hasLoggedMissingErrorShader = true; // 标记警告已经打印，避免 Console 刷屏。
                }

                return null; // 返回 null，让调用方安全跳过不支持 shader 绘制。
            }

            errorMaterial = new Material(shader); // 使用 Unity 错误 shader 创建运行时材质。

            errorMaterial.hideFlags = HideFlags.HideAndDontSave; // 隐藏运行时材质，并防止它被保存进资源或场景。

            return errorMaterial; // 返回缓存后的运行时错误材质。
        }
    }

    internal sealed class BurtDebugCameraDepthPass : BurtRenderPass // 定义 CameraDepth 调试 Pass，负责把深度 RT 可视化到 CameraColor。
    {
        private const string DebugDepthShaderName = "Hidden/BurtRP/DebugCameraDepth"; // 定义调试深度 shader 的查找名称，必须和 shader 文件里的 Shader 名称一致。

        private static readonly int DepthDebugScaleId = Shader.PropertyToID("_BurtDepthDebugScale"); // 缓存深度调试缩放属性 ID，避免每帧通过字符串查找。

        private static readonly int DepthDebugYFlipId = Shader.PropertyToID("_BurtDepthDebugYFlip"); // 缓存深度调试 Y 预翻转属性 ID，避免每帧通过字符串查找。

        private Material debugDepthMaterial; // 缓存调试深度材质，避免每帧重复创建 Material。

        private bool hasLoggedMissingShader; // 记录是否已经输出过缺失 shader 警告，避免 Console 每帧刷屏。

        public override string Name => "Burt Debug Camera Depth"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraDepth(); // 声明这个 Pass 会读取前面绘制阶段写好的 CameraDepth。

            builder.WriteCameraColor(); // 声明这个 Pass 会把深度可视化结果写回 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现深度可视化 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var asset = context.Asset; // 从 GraphContext 中取出当前管线资产，用来读取调试参数。

            var cameraColorTarget = context.CameraColorTarget; // 从 GraphContext 中取出 CameraColor 资源句柄，调试结果会写到这里。

            var cameraDepthTarget = context.CameraDepthTarget; // 从 GraphContext 中取出 CameraDepth 资源句柄，调试 shader 会采样它。

            if (!cameraColorTarget.IsValid) // 如果 CameraColor 无效，说明当前图没有可写入的颜色目标。
            {
                return; // 直接结束这个 Pass，避免绑定无效颜色目标。
            }

            if (!cameraDepthTarget.IsValid) // 如果 CameraDepth 无效，说明当前图没有可读取的深度目标。
            {
                return; // 直接结束这个 Pass，避免 shader 采样无效深度纹理。
            }

            var material = GetDebugDepthMaterial(); // 获取或创建深度调试材质。

            if (material == null) // 如果材质为空，说明 shader 没有找到或者创建失败。
            {
                return; // 直接结束这个 Pass，避免向 CommandBuffer 提交无效绘制。
            }

            var depthDebugScale = asset != null ? asset.DepthDebugScale : 50f; // 从资产读取深度显示缩放，资产为空时使用默认值。

            material.SetFloat(DepthDebugScaleId, depthDebugScale); // 把深度显示缩放传给 shader，用来增强深度灰度对比。

            var depthDebugYFlip = BurtFinalBlitUtility.ResolveFinalBlitYFlip(context.Request); // 复用最终输出的翻转规则，因为深度调试图写进 CameraColor 后还会经过 FinalBlit。

            material.SetFloat(DepthDebugYFlipId, depthDebugYFlip); // 把预翻转开关传给深度调试 shader，防止最终屏幕里的调试图上下颠倒。

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.SetRenderTarget(cameraColorTarget.Identifier); // 只绑定 CameraColor，因为这个全屏调试 Pass 不需要写入深度。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraDepthTextureId, cameraDepthTarget.Identifier); // 确保 _BurtCameraDepthTexture 指向当前 request 的 CameraDepth。

            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1); // 绘制一个全屏三角形，让 shader 把深度纹理转成灰度图。

            renderContext.ExecuteCommandBuffer(cmd); // 把调试绘制命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }

        private Material GetDebugDepthMaterial() // 定义获取深度调试材质的内部辅助函数。
        {
            if (debugDepthMaterial != null) // 如果之前已经创建过材质，就直接复用它。
            {
                return debugDepthMaterial; // 返回缓存的调试材质。
            }

            var shader = Shader.Find(DebugDepthShaderName); // 通过 shader 名称查找深度调试 shader。

            if (shader == null) // 如果 shader 查找失败，说明 shader 文件没有被 Unity 导入或名称不一致。
            {
                if (!hasLoggedMissingShader) // 如果还没有输出过警告，就输出一次。
                {
                    Debug.LogWarning("BurtRP could not find shader: " + DebugDepthShaderName); // 输出缺失 shader 警告，方便定位资源问题。

                    hasLoggedMissingShader = true; // 标记警告已经输出过，避免每帧重复打印。
                }

                return null; // 返回空材质，调用方会跳过这个 Pass。
            }

            debugDepthMaterial = new Material(shader); // 使用找到的 shader 创建一个运行时材质。

            debugDepthMaterial.hideFlags = HideFlags.HideAndDontSave; // 隐藏运行时材质并避免它被保存到场景或资产中。

            return debugDepthMaterial; // 返回创建好的调试材质。
        }
    }

    internal sealed class BurtDebugMainLightShadowMapPass : BurtRenderPass // 定义主光 shadow map 调试 Pass，负责把阴影图画到 CameraColor。
    {
        private const string DebugShadowShaderName = "Hidden/BurtRP/DebugMainLightShadowMap"; // 定义调试 shadow map shader 的查找名称，必须和 shader 文件里的名称一致。
        private static readonly int ShadowDebugExposureId = Shader.PropertyToID("_BurtMainLightShadowDebugExposure"); // 缓存调试曝光属性 ID，避免每帧字符串查找。
        private static readonly int ShadowDebugYFlipId = Shader.PropertyToID("_BurtMainLightShadowDebugYFlip"); // 缓存 shadow debug 的 Y 预翻转属性 ID，避免每帧字符串查找。
        private Material debugShadowMaterial; // 缓存运行时调试材质，避免每帧重复创建 Material。
        private bool hasLoggedMissingShader; // 记录是否已经输出过缺失 shader 警告，避免 Console 每帧刷屏。
        public override string Name => "Burt Debug Main Light Shadow Map"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 没有有效主光阴影，就不声明 shadow map 依赖。
            {
                return; // 直接结束资源声明，避免 Debug 输出无效资源依赖。
            }

            if (BurtShadingDebugSettings.Mode != BurtShadingDebugMode.MainLightShadow) // Controlled by Shading Debug Overlay instead of Pipeline Asset.
            {
                return; // 直接结束资源声明，保持正常渲染图干净。
            }

            builder.ReadMainLightShadowMap(); // 声明这个 Pass 会读取主光 shadow map。
            builder.WriteCameraColor(); // 声明这个 Pass 会把调试结果写回最终 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行主光 shadow map 可视化。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。
            var request = context.Request; // 从 GraphContext 中取出当前渲染请求，用来再次判断阴影是否有效。
            if (!BurtShadowUtility.ShouldUseMainLightShadow(request, context.Asset)) // 如果执行阶段发现阴影已无效，就跳过调试绘制。
            {
                return; // 直接结束，避免读取未申请的 shadow map。
            }

            var cameraColorTarget = context.CameraColorTarget; // 读取 CameraColor 目标，调试图会覆盖到这里。
            var shadowMapTarget = context.MainLightShadowMapTarget; // 读取 MainLightShadowMap 目标，调试 shader 会采样它。
            if (!cameraColorTarget.IsValid || !shadowMapTarget.IsValid) // 如果颜色目标或 shadow map 无效，就无法显示调试图。
            {
                return; // 直接结束，避免绑定或采样无效资源。
            }

            var material = GetDebugShadowMaterial(); // 获取或创建 shadow map 调试材质。
            if (material == null) // 如果材质创建失败，说明 shader 没找到或导入失败。
            {
                return; // 直接结束，避免提交无效绘制命令。
            }

            const float exposure = 1f; // Pipeline Asset no longer stores shadow debug exposure.
            material.SetFloat(ShadowDebugExposureId, exposure); // 把曝光倍率传给 shader，便于放大或压暗 shadow map 深度显示。
            var debugYFlip = ResolveMainLightShadowDebugYFlip(context.Request); // ????????? shadow map ???? Y ?????
            material.SetFloat(ShadowDebugYFlipId, debugYFlip); // 把解析后的 Y 翻转开关传给调试 shader，让 shader 只负责执行一次采样方向修正。
            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。
            cmd.SetRenderTarget(cameraColorTarget.Identifier); // 绑定 CameraColor 作为绘制目标，因为调试视图只覆盖颜色不写深度。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.MainLightShadowMapId, shadowMapTarget.Identifier); // 确保 shader 采样的是当前 request 的主光 shadow map。
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1); // 绘制全屏三角形，让 shader 把 shadow map 转成灰度图。
            renderContext.ExecuteCommandBuffer(cmd); // 提交调试绘制命令给 ScriptableRenderContext。
            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }

        private static float ResolveMainLightShadowDebugYFlip(BurtRenderRequest request)
        {
            return BurtFinalBlitUtility.ResolveFinalBlitYFlip(request);
        }

        private Material GetDebugShadowMaterial() // 获取或创建 shadow map 调试材质。
        {
            if (debugShadowMaterial != null) // 如果之前已经创建过材质，就直接复用。
            {
                return debugShadowMaterial; // 返回缓存材质。
            }

            var shader = Shader.Find(DebugShadowShaderName); // 按名称查找 shadow map 调试 shader。
            if (shader == null) // 如果 shader 查找失败，说明 shader 文件未导入或名称不一致。
            {
                if (!hasLoggedMissingShader) // 如果还没有输出过警告，就输出一次。
                {
                    Debug.LogWarning("BurtRP could not find shader: " + DebugShadowShaderName); // 输出缺失 shader 警告，方便定位资源问题。
                    hasLoggedMissingShader = true; // 标记已经输出过警告，避免每帧刷屏。
                }
                return null; // 返回空材质，调用方会跳过这个 Pass。
            }

            debugShadowMaterial = new Material(shader); // 使用查找到的 shader 创建运行时材质。
            debugShadowMaterial.hideFlags = HideFlags.HideAndDontSave; // 隐藏运行时材质并避免它被保存到场景或资产中。
            return debugShadowMaterial; // 返回缓存好的调试材质。
        }
    }

    internal sealed class BurtReleaseMainLightShadowMapPass : BurtRenderPass // 定义主光阴影图释放 Pass，负责在当前 request 结束时释放 shadow map 临时 RT。
    {
        public override string Name => "Burt Release Main Light Shadow Map"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 没有主光阴影，就不声明阴影图读取。
            {
                return; // 直接结束资源声明，避免 Debug 输出无效的 MainLightShadowMap 依赖。
            }

            builder.ReadMainLightShadowMap(); // 声明这个 Pass 依赖 MainLightShadowMap，表示它要结束这个阴影资源的生命周期。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现主光阴影图释放 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            if (!BurtShadowUtility.ShouldUseMainLightShadow(request, context.Asset)) // 如果当前 request 不需要主光阴影，就不释放阴影图。
            {
                return; // 直接结束这个 Pass，避免释放一个没有申请过的临时 RT。
            }

            var shadowMapTarget = context.MainLightShadowMapTarget; // 从 GraphContext 中取出 MainLightShadowMap 资源句柄。

            if (!shadowMapTarget.IsValid) // 如果 MainLightShadowMap 句柄无效，说明当前图没有注册这个资源。
            {
                return; // 直接结束这个 Pass，避免释放不存在的临时 RT。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.MainLightShadowMapId); // 释放前面申请的主光阴影图临时 RT，避免资源泄漏到下一个 request。

            renderContext.ExecuteCommandBuffer(cmd); // 把释放 RT 的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtDrawPreImageEffectsGizmosPass : BurtRenderPass
    {
        public override string Name => "Burt Draw Gizmos Pre Image Effects";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Debug;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtEditorGizmoUtility.ShouldRenderGizmos(builder.Request))
            {
                return;
            }

            builder.ReadCameraColor();
            builder.ReadCameraDepth();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
#if UNITY_EDITOR
            if (context == null || !BurtEditorGizmoUtility.ShouldRenderGizmos(context.Request))
            {
                return;
            }

            if (!BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name))
            {
                return;
            }

            context.ScriptableContext.DrawGizmos(context.Request.Camera, GizmoSubset.PreImageEffects);
#endif
        }
    }

    internal sealed class BurtDrawPostImageEffectsGizmosPass : BurtRenderPass
    {
        public override string Name => "Burt Draw Gizmos Post Image Effects";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Debug;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtEditorGizmoUtility.ShouldRenderGizmos(builder.Request))
            {
                return;
            }

            builder.ReadCameraColor();
            builder.ReadCameraDepth();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
#if UNITY_EDITOR
            if (context == null || !BurtEditorGizmoUtility.ShouldRenderGizmos(context.Request))
            {
                return;
            }

            if (!BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name))
            {
                return;
            }

            context.ScriptableContext.DrawGizmos(context.Request.Camera, GizmoSubset.PostImageEffects);
#endif
        }
    }

    internal static class BurtEditorGizmoUtility
    {
        public static void EmitWorldGeometryForSceneView(Camera camera)
        {
#if UNITY_EDITOR
            if (camera == null || camera.cameraType != CameraType.SceneView)
            {
                return;
            }

            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
        }

        public static bool ShouldRenderGizmos(BurtRenderRequest request)
        {
#if UNITY_EDITOR
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return false;
            }

            return Handles.ShouldRenderGizmos();
#else
            return false;
#endif
        }

    }

}
