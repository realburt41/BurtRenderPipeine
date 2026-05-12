using System.Text; // 引入 StringBuilder，用来把 Deferred 材质分类结果追加到 RenderGraph Debug 文本里。
using UnityEngine; // 引入 UnityEngine 命名空间，用来访问 Camera、Renderer、Material、Shader 和 GeometryUtility。
using UnityEngine.Rendering; // 引入 UnityEngine.Rendering 命名空间，用来读取 ShaderTagId 和 shader pass 的 LightMode 标签。

namespace Burt.RenderPipeline // 定义 BurtRP 运行时命名空间，让 RenderGraph Debug 能直接调用这个 Deferred 诊断工具。
{
    internal static class BurtDeferredMaterialDebugUtility // 定义 Deferred 材质分类调试工具，用来解释一个材质在 Deferred 路径里会走哪条绘制通道。
    {
        private const int OpaqueRenderQueueMax = 2500; // 定义 Unity 不透明队列上限，等价于 RenderQueueRange.opaque 的常用边界。
        private const string HairShaderName = "BurtRP/Hair"; // Hair 已从 BurtRP/Lit 拆成独立 shader，调试统计按 shader 名识别。
        private static readonly ShaderTagId LightModeTag = new ShaderTagId("LightMode"); // 定义 shader pass 标签名，Unity 用它标记一个 pass 属于哪种渲染路径。
        private static readonly ShaderTagId BurtGBufferLightMode = new ShaderTagId("BurtGBuffer"); // 定义 BurtRP Deferred GBuffer pass 的 LightMode。
        private static readonly ShaderTagId BurtForwardOnlyLightMode = new ShaderTagId("BurtForwardOnly"); // 定义 Deferred Lighting 后前向兜底 pass 的 LightMode。
        private static readonly ShaderTagId BurtForwardLightMode = new ShaderTagId("BurtForward"); // 定义 BurtRP 常规 Forward pass 的 LightMode。

        public static void AppendDebugState( // 定义向 RenderGraph Debug 输出 Deferred 材质分类状态的入口。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderRequest request, // 接收当前渲染请求，用来读取相机和相机裁剪层。
            BurtRenderPipelineAsset asset) // 接收当前管线资产，用来判断是否处于 Deferred 模式。
        {
            if (builder == null) // 如果没有输出目标，就不能继续追加文本。
            {
                return; // 直接返回，避免调试工具引发空引用。
            }

            builder.AppendLine("Deferred Material State:"); // 单独成段输出，方便和资源状态、Pass 状态分开扫描。

            if (asset == null) // 如果没有管线资产，就无法判断 Deferred 开关。
            {
                builder.AppendLine("  <skipped: no asset>"); // 明确写出跳过原因，避免误以为扫描没有发现材质。

                return; // 没有资产时结束扫描。
            }

            if (asset.RendererMode != BurtRendererMode.Deferred) // 如果当前不是 Deferred 路径，就没有必要扫描 GBuffer/ForwardOnly 分类。
            {
                builder.AppendLine("  <skipped: renderer mode is not Deferred>"); // 明确写出 Forward 模式下不做这项诊断。

                return; // Forward 模式下结束扫描。
            }

            var camera = request != null ? request.Camera : null; // 从 request 安全读取当前相机。

            if (camera == null) // 如果没有相机，就无法根据层级和视锥近似判断当前 request 可能绘制哪些 renderer。
            {
                builder.AppendLine("  <skipped: no camera>"); // 明确写出跳过原因。

                return; // 没有相机时结束扫描。
            }

            var summary = BuildSummary(camera); // 扫描当前已加载场景中的活动 Renderer，并按 BurtRP Deferred 策略做近似分类。

            builder.Append("  Scan=ApproxActiveScene"); // 标记这是调试期的场景近似扫描，不是 Unity CullingResults 的精确内部列表。
            builder.Append(" CameraLayerMask=").Append(camera.cullingMask); // 写入当前相机 cullingMask，方便解释为什么某些层没有被统计。
            builder.Append(" Renderers=").Append(summary.RendererCount); // 写入所有活动 Renderer 数量。
            builder.Append(" LayerMatched=").Append(summary.LayerMatchedRendererCount); // 写入通过相机层级过滤的 Renderer 数量。
            builder.Append(" FrustumMatched=").Append(summary.FrustumMatchedRendererCount); // 写入通过相机视锥 AABB 近似过滤的 Renderer 数量。
            builder.Append(" MaterialSlots=").Append(summary.MaterialSlotCount); // 写入参与分类的材质槽数量。
            builder.AppendLine(); // 结束扫描范围行。

            builder.Append("  OpaqueSlots=").Append(summary.OpaqueSlotCount); // 写入不透明材质槽总数。
            builder.Append(" GBuffer=").Append(summary.OpaqueGBufferSlotCount); // 写入拥有 BurtGBuffer pass 的不透明材质槽数。
            builder.Append(" Hair=").Append(summary.OpaqueHairGBufferSlotCount); // 写入会进 GBuffer 且选择 Hair shading model 的材质槽数。
            builder.Append(" ForwardOnly=").Append(summary.OpaqueForwardOnlySlotCount); // 写入拥有 BurtForwardOnly pass 的不透明材质槽数。
            builder.Append(" MissingDeferredPath=").Append(summary.OpaqueMissingDeferredPathSlotCount); // 写入 Deferred 下既不进 GBuffer 也不进 ForwardOnly 的不透明材质槽数。
            builder.AppendLine(); // 结束不透明分类行。

            builder.Append("  TransparentSlots=").Append(summary.TransparentSlotCount); // 写入透明材质槽总数。
            builder.Append(" Forward=").Append(summary.TransparentForwardSlotCount); // 写入拥有 BurtForward pass、会被透明 Forward Pass 绘制的透明材质槽数。
            builder.Append(" MissingForward=").Append(summary.TransparentMissingForwardSlotCount); // 写入透明但没有 BurtForward pass 的材质槽数。
            builder.AppendLine(); // 结束透明分类行。

            builder.Append("  Policy=Opaque BurtGBuffer -> Deferred Lighting; Opaque BurtForwardOnly -> after Deferred Lighting; Opaque BurtForward only -> not drawn in Deferred"); // 写入当前 Deferred 材质分类规则。
            builder.AppendLine(); // 结束策略说明行。

            if (summary.OpaqueMissingDeferredPathSlotCount > 0) // 如果发现可能在 Deferred 下不可见的不透明材质。
            {
                builder.Append("  FirstMissingDeferredPath="); // 写入第一个风险材质，方便你在场景里快速定位。
                builder.Append(summary.FirstMissingDeferredPathMaterialName); // 写入材质名。
                builder.Append(" Shader=").Append(summary.FirstMissingDeferredPathShaderName); // 写入 shader 名。
                builder.AppendLine(); // 结束风险材质行。
            }
        }

        private static DeferredMaterialSummary BuildSummary(Camera camera) // 根据当前相机构建 Deferred 材质分类摘要。
        {
            var summary = new DeferredMaterialSummary(); // 创建本次扫描的计数容器。
            var renderers = FindActiveRenderers(); // 读取当前已加载场景中的活动 Renderer。
            var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera); // 计算当前相机视锥平面，用 AABB 做近似可见性过滤。

            summary.RendererCount = renderers != null ? renderers.Length : 0; // 记录活动 Renderer 总数。

            if (renderers == null) // 如果 Unity 返回空数组或异常空值。
            {
                return summary; // 直接返回空摘要。
            }

            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++) // 遍历所有活动 Renderer。
            {
                var renderer = renderers[rendererIndex]; // 取出当前 Renderer。

                if (!IsActiveRenderer(renderer)) // 检查 Renderer 和 GameObject 是否处于 Unity 会绘制的活动状态。
                {
                    continue; // 非活动 Renderer 不参与材质分类。
                }

                if (!IsRendererInCameraLayer(renderer, camera)) // 检查 Renderer 所在层是否被当前相机 cullingMask 包含。
                {
                    continue; // 不在当前相机层级范围内的 Renderer 不参与材质分类。
                }

                summary.LayerMatchedRendererCount++; // 记录已经通过相机层级过滤的 Renderer 数量。

                if (!IsRendererInFrustum(renderer, frustumPlanes)) // 检查 Renderer 包围盒是否和当前相机视锥相交。
                {
                    continue; // 视锥外的 Renderer 不参与当前 request 的近似材质分类。
                }

                summary.FrustumMatchedRendererCount++; // 记录通过视锥近似过滤的 Renderer 数量。
                ScanRendererMaterials(renderer, summary); // 扫描这个 Renderer 的材质槽并累计分类结果。
            }

            return summary; // 返回统计完成的摘要。
        }

        private static Renderer[] FindActiveRenderers() // 查找当前场景中活动的 Renderer。
        {
#if UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None); // Unity 2022.2+ 使用新 API，避免旧 FindObjectsOfType 的过时警告。
#else
            return UnityEngine.Object.FindObjectsOfType<Renderer>(); // Unity 2022.1 及更早版本使用旧 API，保持当前工程兼容性。
#endif
        }

        private static bool IsActiveRenderer(Renderer renderer) // 判断 Renderer 本身和所属 GameObject 是否处于可绘制状态。
        {
            if (renderer == null) // 空 Renderer 不能扫描。
            {
                return false; // 返回 false。
            }

            if (!renderer.enabled) // Renderer 组件关闭时 Unity 不会绘制它。
            {
                return false; // 返回 false。
            }

            var gameObject = renderer.gameObject; // 读取 Renderer 所属 GameObject。

            if (gameObject == null) // 极端情况下 Renderer 可能没有有效 GameObject。
            {
                return false; // 返回 false。
            }

            if (!gameObject.activeInHierarchy) // GameObject 未激活时 Unity 不会绘制它。
            {
                return false; // 返回 false。
            }

            return true; // Renderer 和 GameObject 都处于活动状态，可以继续做相机过滤。
        }

        private static bool IsRendererInCameraLayer( // 判断 Renderer 所在层是否会被当前相机渲染。
            Renderer renderer, // 接收待检查的 Renderer。
            Camera camera) // 接收当前渲染相机。
        {
            if (renderer == null || camera == null) // 任意输入为空都不能可靠判断层级。
            {
                return false; // 返回 false。
            }

            var gameObject = renderer.gameObject; // 读取 Renderer 所属 GameObject。

            if (gameObject == null) // 没有 GameObject 时没有 layer 可以判断。
            {
                return false; // 返回 false。
            }

            if ((camera.cullingMask & (1 << gameObject.layer)) == 0) // 如果 Renderer 所在层不在当前相机 cullingMask 中。
            {
                return false; // 返回 false。
            }

            return true; // 相机层级包含该 Renderer。
        }

        private static bool IsRendererInFrustum( // 用包围盒近似判断 Renderer 是否落在当前相机视锥内。
            Renderer renderer, // 接收待检查的 Renderer。
            Plane[] frustumPlanes) // 接收当前相机视锥平面数组。
        {
            if (renderer == null) // 空 Renderer 没有包围盒。
            {
                return false; // 返回 false。
            }

            if (frustumPlanes == null || frustumPlanes.Length == 0) // 如果视锥平面无效，就只能依赖层级过滤。
            {
                return true; // 返回 true，避免因为调试工具拿不到视锥而漏报所有材质。
            }

            return GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds); // 用 Renderer 包围盒近似判断它是否可能被当前相机看到。
        }

        private static void ScanRendererMaterials( // 扫描单个 Renderer 的所有材质槽。
            Renderer renderer, // 接收要扫描的 Renderer。
            DeferredMaterialSummary summary) // 接收要累加的摘要对象。
        {
            var materials = renderer.sharedMaterials; // 使用 sharedMaterials，避免访问 materials 时实例化材质。

            if (materials == null) // 没有材质数组时无法分类。
            {
                return; // 直接返回。
            }

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++) // 遍历材质槽。
            {
                var material = materials[materialIndex]; // 取出当前材质。

                if (material == null) // 空材质槽不会产生有效 BurtRP pass。
                {
                    continue; // 跳过空槽。
                }

                ScanMaterial(material, summary); // 扫描当前材质并累加分类计数。
            }
        }

        private static void ScanMaterial( // 扫描单个材质在 BurtRP Deferred 中的路径归属。
            Material material, // 接收要扫描的材质。
            DeferredMaterialSummary summary) // 接收要累加的摘要对象。
        {
            var shader = material.shader; // 读取材质使用的 shader。

            if (shader == null) // 没有 shader 的材质无法被正常绘制。
            {
                return; // 直接跳过。
            }

            var isOpaque = IsOpaqueMaterial(material); // 判断材质是否属于不透明队列。
            var hasGBuffer = ShaderHasLightMode(shader, BurtGBufferLightMode); // 判断 shader 是否提供 LightMode=BurtGBuffer。
            var hasForwardOnly = ShaderHasLightMode(shader, BurtForwardOnlyLightMode); // 判断 shader 是否提供 LightMode=BurtForwardOnly。
            var hasForward = ShaderHasLightMode(shader, BurtForwardLightMode); // 判断 shader 是否提供 LightMode=BurtForward。

            summary.MaterialSlotCount++; // 每个有效材质槽计入总数。

            if (isOpaque) // 不透明材质由 GBuffer 或 ForwardOnly 决定 Deferred 路径。
            {
                ScanOpaqueMaterial(material, shader, hasGBuffer, hasForwardOnly, summary); // 累加不透明分类。

                return; // 不透明材质扫描结束。
            }

            ScanTransparentMaterial(hasForward, summary); // 透明材质仍由 Burt Draw Transparent 的 BurtForward pass 绘制。
        }

        private static void ScanOpaqueMaterial( // 累加不透明材质的 Deferred 分类。
            Material material, // 接收当前材质，用于记录第一个风险材质名。
            Shader shader, // 接收当前 shader，用于记录第一个风险 shader 名。
            bool hasGBuffer, // 标记 shader 是否有 BurtGBuffer pass。
            bool hasForwardOnly, // 标记 shader 是否有 BurtForwardOnly pass。
            DeferredMaterialSummary summary) // 接收要累加的摘要对象。
        {
            summary.OpaqueSlotCount++; // 累加不透明材质槽数量。

            if (hasGBuffer) // 拥有 GBuffer pass 的不透明材质会进入 Deferred Lighting。
            {
                summary.OpaqueGBufferSlotCount++; // 累加 GBuffer 路径数量。

                if (IsHairMaterial(material)) // Hair 仍走 GBuffer，但只能由独立 BurtRP/Hair shader 写入 Hair shading model。
                {
                    summary.OpaqueHairGBufferSlotCount++; // 累加 Hair 材质数量，方便确认材质 UI 选择是否被扫描到。
                }

                return; // GBuffer 优先级最高，结束分类。
            }

            if (hasForwardOnly) // 没有 GBuffer 但拥有 ForwardOnly 的材质会在 Deferred Lighting 后兜底绘制。
            {
                summary.OpaqueForwardOnlySlotCount++; // 累加 ForwardOnly 路径数量。

                return; // ForwardOnly 分类完成。
            }

            summary.OpaqueMissingDeferredPathSlotCount++; // 没有 GBuffer 和 ForwardOnly 的不透明材质在 Deferred 中可能不可见或只被错误材质覆盖。

            if (string.IsNullOrEmpty(summary.FirstMissingDeferredPathMaterialName)) // 如果还没有记录过第一个风险材质。
            {
                summary.FirstMissingDeferredPathMaterialName = material.name; // 记录材质名，便于定位。
                summary.FirstMissingDeferredPathShaderName = shader.name; // 记录 shader 名，便于判断是否缺少 BurtGBuffer 或 BurtForwardOnly pass。
            }
        }

        private static void ScanTransparentMaterial( // 累加透明材质的 Forward 分类。
            bool hasForward, // 标记 shader 是否有 BurtForward pass。
            DeferredMaterialSummary summary) // 接收要累加的摘要对象。
        {
            summary.TransparentSlotCount++; // 累加透明材质槽数量。

            if (hasForward) // 透明材质当前仍通过 BurtForward pass 绘制。
            {
                summary.TransparentForwardSlotCount++; // 累加可绘制透明材质槽数量。

                return; // 透明材质分类完成。
            }

            summary.TransparentMissingForwardSlotCount++; // 没有 BurtForward 的透明材质不会被当前 Burt Draw Transparent Pass 绘制。
        }

        private static bool IsOpaqueMaterial(Material material) // 判断材质是否属于不透明队列。
        {
            if (material == null) // 空材质没有队列信息。
            {
                return false; // 返回 false，把它视为不可分类。
            }

            var renderQueue = material.renderQueue; // 读取材质当前渲染队列。

            if (renderQueue >= 0) // 大多数情况下 Unity 会返回实际队列值。
            {
                return renderQueue <= OpaqueRenderQueueMax; // 2500 及以下按不透明处理，匹配 DrawRenderers 的 opaque 过滤。
            }

            var queueTag = material.GetTag("Queue", true, "Geometry"); // 队列为 -1 时从 shader Queue 标签兜底读取。

            return !queueTag.StartsWith("Transparent") && !queueTag.StartsWith("Overlay"); // 不是透明或 Overlay 的队列就按不透明处理。
        }

        private static bool IsHairMaterial(Material material) // 判断材质是否使用独立的 BurtRP/Hair shader。
        {
            if (material == null || material.shader == null) // 没有 shader 的材质继续按非 Hair 处理。
            {
                return false; // 返回 false，避免扫描异常材质时报空引用。
            }

            return material.shader.name == HairShaderName; // 不再读取 Lit 的 _ShadingModel，避免 Hair 和 Lit 混用。
        }

        private static bool ShaderHasLightMode( // 判断 shader 是否包含某个 LightMode pass。
            Shader shader, // 接收要检查的 shader。
            ShaderTagId expectedLightMode) // 接收期望的 LightMode 值。
        {
            if (shader == null) // 空 shader 不可能包含 pass。
            {
                return false; // 返回 false。
            }

            for (var passIndex = 0; passIndex < shader.passCount; passIndex++) // 遍历当前 shader 可用的 pass。
            {
                var lightMode = shader.FindPassTagValue(passIndex, LightModeTag); // 读取当前 pass 的 LightMode 标签值。

                if (lightMode.Equals(expectedLightMode)) // 如果 LightMode 和期望值一致。
                {
                    return true; // 说明 shader 支持这条 BurtRP 绘制路径。
                }
            }

            return false; // 遍历完仍没找到就返回 false。
        }

        private sealed class DeferredMaterialSummary // 定义 Deferred 材质分类计数容器。
        {
            public int RendererCount; // 保存当前场景活动 Renderer 总数。
            public int LayerMatchedRendererCount; // 保存通过相机层级过滤的 Renderer 数量。
            public int FrustumMatchedRendererCount; // 保存通过相机视锥近似过滤的 Renderer 数量。
            public int MaterialSlotCount; // 保存参与扫描的材质槽总数。
            public int OpaqueSlotCount; // 保存不透明材质槽数量。
            public int OpaqueGBufferSlotCount; // 保存会写入 GBuffer 的不透明材质槽数量。
            public int OpaqueHairGBufferSlotCount; // 保存 GBuffer 路径中选择 Hair shading model 的材质槽数量。
            public int OpaqueForwardOnlySlotCount; // 保存会在 Deferred Lighting 后前向兜底绘制的不透明材质槽数量。
            public int OpaqueMissingDeferredPathSlotCount; // 保存 Deferred 下缺少绘制路径的不透明材质槽数量。
            public int TransparentSlotCount; // 保存透明材质槽数量。
            public int TransparentForwardSlotCount; // 保存拥有 BurtForward pass 的透明材质槽数量。
            public int TransparentMissingForwardSlotCount; // 保存透明但缺少 BurtForward pass 的材质槽数量。
            public string FirstMissingDeferredPathMaterialName; // 保存第一个缺少 Deferred 路径的不透明材质名。
            public string FirstMissingDeferredPathShaderName; // 保存第一个缺少 Deferred 路径的不透明材质的 shader 名。
        }
    }
}
