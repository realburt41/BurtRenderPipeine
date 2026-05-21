using Burt.RenderPipeline; // 引入 BurtRP 运行时命名空间，用于给 BurtRenderPipelineAsset 注册自定义 Inspector。
using UnityEditor; // 引入 UnityEditor，用于实现 Editor、SerializedProperty 和 Inspector GUI。
using UnityEngine; // 引入 UnityEngine，用于 HelpBox、GUIContent 等编辑器界面类型。

namespace Burt.RenderPipeline.Editor // 将编辑器扩展放在 BurtRP Editor 命名空间，避免和运行时代码混在一起。
{
    [CustomEditor(typeof(BurtRenderPipelineAsset))] // 指定这个 Inspector 只接管 BurtRenderPipelineAsset 资源。
    internal sealed class BurtRenderPipelineAssetEditor : UnityEditor.Editor // 继承 UnityEditor.Editor 来绘制自定义资源面板。
    {
        private SerializedProperty clearColor; // 缓存默认清屏颜色字段，General 分组使用。
        private SerializedProperty rendererMode; // 缓存渲染路径模式字段，用来在 Inspector 中切换 Forward / Deferred。

        private SerializedProperty enableDepthPrepass; // 缓存 Depth Prepass 开关字段。
        private SerializedProperty enableDepthDebugView; // 缓存 Depth Debug 覆盖 CameraColor 的开关字段。
        private SerializedProperty depthDebugScale; // 缓存 Depth Debug 的显示缩放字段。

        private SerializedProperty preintegratedFGLut; // 缓存 PBR 预积分 FG LUT 字段。
        private SerializedProperty enableScreenSpaceSubsurface;
        private SerializedProperty screenSpaceSubsurfaceProfile;
        private SerializedProperty screenSpaceSubsurfaceProfiles;
        private SerializedProperty screenSpaceSubsurfaceRadiusPixels;
        private SerializedProperty screenSpaceSubsurfaceDepthSigma;
        private SerializedProperty screenSpaceSubsurfaceNormalSigma;
        private SerializedProperty screenSpaceSubsurfaceBlend;
        private SerializedProperty screenSpaceSubsurfaceDistanceScale;
        private SerializedProperty screenSpaceSubsurfaceBoundaryBleed;
        private SerializedProperty screenSpaceSubsurfaceTintStrength;
        private SerializedProperty screenSpaceSubsurfaceMinStrength;

        private SerializedProperty postProcessSettings; // 缓存后处理框架设置字段，具体效果参数会从 Global Volume 读取。
        private SerializedProperty postProcessVolumeLayerMask; // 缓存后处理 Volume 查询层字段，Global Volume 需要通过它参与后处理。
        private SerializedProperty enableUnsupportedShaderDebug; // 缓存不支持 Shader 可视化调试字段。
        private SerializedProperty enableRenderGraphDebug; // 缓存 RenderGraph 调试日志字段。
        private SerializedProperty enableRenderGraphDebugConsoleLog; // 缓存 RenderGraph 长日志 Console 输出字段。

        private SerializedProperty enableCameraSortDebugLog; // 缓存相机排序调试日志字段。
        private SerializedProperty enableRenderFrameDebugLog; // 缓存 Frame/Stack 分组调试日志字段。

        private static readonly GUIContent ClearColorLabel = new("Clear Color", "默认清屏颜色，供 BurtRP 清屏 Pass 使用。"); // 定义 General 分组显示文本。
        private static readonly GUIContent RendererModeLabel = new("Renderer Mode", "选择 BurtRP 主渲染路径；Deferred 当前仍是实验路径，默认保持 Forward。"); // 定义渲染路径模式显示文本。
        private static readonly GUIContent DepthPrepassLabel = new("Depth Prepass", "开启后先写入 CameraDepth，便于后续深度相关 Pass 使用。"); // 定义 Depth Prepass 显示文本。
        private static readonly GUIContent DepthDebugLabel = new("Depth Debug View", "开启后把 CameraDepth 可视化到 CameraColor。"); // 定义 Depth Debug 显示文本。
        private static readonly GUIContent DepthScaleLabel = new("Depth Debug Scale", "调整深度可视化亮度缩放，数值越大近处深度越明显。"); // 定义 Depth Debug 缩放显示文本。
        private static readonly GUIContent PreintegratedFGLutLabel = new("Preintegrated FG LUT", "用于 IBL 间接高光的 DFG/GGX 预积分查找表。"); // 定义 PBR 预积分 LUT 显示文本。
        private static readonly GUIContent EnableScreenSpaceSubsurfaceLabel = new("Enable Screen Space SSS", "开启 Deferred 屏幕空间 4S/5S 次表面散射。");
        private static readonly GUIContent ScreenSpaceSubsurfaceProfileLabel = new("SSS Default Profile (Slot 0)", "默认 SSS profile 文件，也是材质 Profile Index 为 0 时使用的 profile；未挂时使用下方 inline fallback。");
        private static readonly GUIContent ScreenSpaceSubsurfaceProfilesLabel = new("SSS Profile List (Slots 1-7)", "材质 Profile Index 1 到 7 会依次读取这里的 profile。列表为空或槽位为空时回退到 Slot 0。");
        private static readonly GUIContent ScreenSpaceSubsurfaceRadiusPixelsLabel = new("Fallback Radius Pixels", "未指定 profile 时使用的屏幕空间扩散半径。");
        private static readonly GUIContent ScreenSpaceSubsurfaceDepthSigmaLabel = new("Fallback Depth Sigma", "未指定 profile 时使用的深度边界保护。");
        private static readonly GUIContent ScreenSpaceSubsurfaceNormalSigmaLabel = new("Fallback Normal Sigma", "未指定 profile 时使用的法线边界保护。");
        private static readonly GUIContent ScreenSpaceSubsurfaceBlendLabel = new("Fallback Blend", "未指定 profile 时使用的最终混合强度。");
        private static readonly GUIContent ScreenSpaceSubsurfaceDistanceScaleLabel = new("Fallback Distance Scale", "未指定 profile 时使用的远距离衰减强度。");
        private static readonly GUIContent ScreenSpaceSubsurfaceBoundaryBleedLabel = new("Fallback Boundary Bleed", "未指定 profile 时使用的边界防串色强度。");
        private static readonly GUIContent ScreenSpaceSubsurfaceTintStrengthLabel = new("Fallback Tint Strength", "未指定 profile 时使用的材质 tint 混合强度。");
        private static readonly GUIContent ScreenSpaceSubsurfaceMinStrengthLabel = new("Fallback Min Strength", "未指定 profile 时过滤低强度次表面像素的阈值。");
        private static readonly GUIContent PostProcessSettingsLabel = new("Post Process Settings", "后处理框架设置，具体效果参数从 Global Volume 读取。"); // 定义后处理设置显示文本。
        private static readonly GUIContent PostProcessVolumeLayerMaskLabel = new("Post Process Volume Layer Mask", "后处理 Global Volume 查询层，Tonemapping 等效果参数从匹配的 Volume Profile 读取。"); // 定义后处理 Volume 层显示文本。
        private static readonly GUIContent UnsupportedShaderDebugLabel = new("Unsupported Shader Debug", "用 Unity 错误材质标记非 BurtRP Shader，方便发现错误材质。"); // 定义不支持 Shader 调试显示文本。
        private static readonly GUIContent RenderGraphDebugLabel = new("RenderGraph Debug Capture", "缓存最近一次 RenderGraph 调试信息，供下方按钮复制到剪切板。"); // 定义 RenderGraph 捕获显示文本。
        private static readonly GUIContent RenderGraphDebugConsoleLogLabel = new("RenderGraph Console Log", "把捕获到的完整 RenderGraph Debug 继续输出到 Console；默认关闭以避免刷屏。"); // 定义 RenderGraph Console 输出显示文本。
        private static readonly GUIContent CameraSortDebugLabel = new("Camera Sort Debug Log", "输出相机 request 排序列表，多相机调试时使用。"); // 定义相机排序调试显示文本。
        private static readonly GUIContent RenderFrameDebugLabel = new("Render Frame Debug Log", "输出 Frame/Stack 分组日志。"); // 定义 Frame/Stack 分组调试显示文本。

        private void OnEnable() // Unity 选中资源或脚本重载后调用，用于绑定所有序列化字段。
        {
            clearColor = FindProperty(nameof(clearColor)); // 绑定 clearColor 私有字段，不改字段名以保持现有同步逻辑稳定。
            rendererMode = FindProperty(nameof(rendererMode)); // 绑定 Renderer Mode 字段，让用户可以在资产上选择 Forward 或 Deferred。

            enableDepthPrepass = FindProperty(nameof(enableDepthPrepass)); // 绑定深度预写开关。
            enableDepthDebugView = FindProperty(nameof(enableDepthDebugView)); // 绑定深度调试视图开关。
            depthDebugScale = FindProperty(nameof(depthDebugScale)); // 绑定深度调试缩放。

            preintegratedFGLut = FindProperty(nameof(preintegratedFGLut)); // 绑定 PBR 预积分 FG LUT。
            enableScreenSpaceSubsurface = FindProperty(nameof(enableScreenSpaceSubsurface));
            screenSpaceSubsurfaceProfile = FindProperty(nameof(screenSpaceSubsurfaceProfile));
            screenSpaceSubsurfaceProfiles = FindProperty(nameof(screenSpaceSubsurfaceProfiles));
            screenSpaceSubsurfaceRadiusPixels = FindProperty(nameof(screenSpaceSubsurfaceRadiusPixels));
            screenSpaceSubsurfaceDepthSigma = FindProperty(nameof(screenSpaceSubsurfaceDepthSigma));
            screenSpaceSubsurfaceNormalSigma = FindProperty(nameof(screenSpaceSubsurfaceNormalSigma));
            screenSpaceSubsurfaceBlend = FindProperty(nameof(screenSpaceSubsurfaceBlend));
            screenSpaceSubsurfaceDistanceScale = FindProperty(nameof(screenSpaceSubsurfaceDistanceScale));
            screenSpaceSubsurfaceBoundaryBleed = FindProperty(nameof(screenSpaceSubsurfaceBoundaryBleed));
            screenSpaceSubsurfaceTintStrength = FindProperty(nameof(screenSpaceSubsurfaceTintStrength));
            screenSpaceSubsurfaceMinStrength = FindProperty(nameof(screenSpaceSubsurfaceMinStrength));

            postProcessSettings = FindProperty(nameof(postProcessSettings)); // 绑定后处理设置，让现有自定义 Inspector 也能显示新配置。
            postProcessVolumeLayerMask = FindProperty(nameof(postProcessVolumeLayerMask)); // 绑定后处理 Volume 查询层，让 Global Volume 可以按 LayerMask 过滤。
            enableUnsupportedShaderDebug = FindProperty(nameof(enableUnsupportedShaderDebug)); // 绑定不支持 Shader 调试开关。
            enableRenderGraphDebug = FindProperty(nameof(enableRenderGraphDebug)); // 绑定 RenderGraph 调试开关。
            enableRenderGraphDebugConsoleLog = FindProperty(nameof(enableRenderGraphDebugConsoleLog)); // 绑定 RenderGraph Console 输出开关。

            enableCameraSortDebugLog = FindProperty(nameof(enableCameraSortDebugLog)); // 绑定相机排序日志开关。
            enableRenderFrameDebugLog = FindProperty(nameof(enableRenderFrameDebugLog)); // 绑定 Frame/Stack 分组日志开关。
        }

        public override void OnInspectorGUI() // 绘制 BurtRenderPipelineAsset 的完整中文分组 Inspector。
        {
            serializedObject.Update(); // 读取资源当前序列化状态，确保 Inspector 显示最新数据。

            DrawGeneralGroup(); // 绘制 General 分组。
            DrawDepthGroup(); // 绘制 Depth 分组。
            DrawPBRGroup(); // 绘制 PBR 分组。
            DrawSubsurfaceGroup();
            DrawPostProcessGroup(); // 绘制 Post Processing 分组。
            DrawDebugGroup(); // 绘制 Debug 分组。
            DrawCameraDebugGroup(); // 绘制 Camera Debug 分组。

            serializedObject.ApplyModifiedProperties(); // 写回用户在 Inspector 中修改的字段。
        }

        private void DrawGeneralGroup() // 绘制常规渲染设置，当前只包含默认清屏颜色。
        {
            DrawSectionHeader("General / 通用"); // 显示中英文分组标题。
            DrawProperty(rendererMode, RendererModeLabel); // 绘制渲染路径选择，默认 Forward，Deferred 用于后续实验验证。
            DrawProperty(clearColor, ClearColorLabel); // 绘制默认清屏颜色字段。
            EditorGUILayout.HelpBox("Deferred 目前只接入 GBuffer 资源生命周期，画面仍临时复用 Forward 输出。", MessageType.Info); // 提示 Deferred 当前阶段不会立刻变成正式延迟渲染。
        }

        private void DrawPBRGroup() // 绘制 PBR 设置。
        {
            DrawSectionHeader("PBR / Shading"); // 显示 PBR 分组标题。
            DrawProperty(preintegratedFGLut, PreintegratedFGLutLabel); // 绘制预积分 FG LUT 引用。
            EditorGUILayout.HelpBox("PreintegratedFG.exr 用于 PBR IBL DFG。", MessageType.Info); // 提示 LUT 数据用途。
        }

        private void DrawSubsurfaceGroup()
        {
            DrawSectionHeader("Deferred 4S/5S / 次表面");
            DrawProperty(enableScreenSpaceSubsurface, EnableScreenSpaceSubsurfaceLabel);

            using (new EditorGUI.DisabledScope(enableScreenSpaceSubsurface == null || !enableScreenSpaceSubsurface.boolValue))
            {
                DrawProperty(screenSpaceSubsurfaceProfile, ScreenSpaceSubsurfaceProfileLabel);
                DrawProfileListProperty();

                var usingProfile = screenSpaceSubsurfaceProfile != null && screenSpaceSubsurfaceProfile.objectReferenceValue != null;
                using (new EditorGUI.DisabledScope(usingProfile))
                {
                    DrawProperty(screenSpaceSubsurfaceRadiusPixels, ScreenSpaceSubsurfaceRadiusPixelsLabel);
                    DrawProperty(screenSpaceSubsurfaceDepthSigma, ScreenSpaceSubsurfaceDepthSigmaLabel);
                    DrawProperty(screenSpaceSubsurfaceNormalSigma, ScreenSpaceSubsurfaceNormalSigmaLabel);
                    DrawProperty(screenSpaceSubsurfaceBlend, ScreenSpaceSubsurfaceBlendLabel);
                    DrawProperty(screenSpaceSubsurfaceDistanceScale, ScreenSpaceSubsurfaceDistanceScaleLabel);
                    DrawProperty(screenSpaceSubsurfaceBoundaryBleed, ScreenSpaceSubsurfaceBoundaryBleedLabel);
                    DrawProperty(screenSpaceSubsurfaceTintStrength, ScreenSpaceSubsurfaceTintStrengthLabel);
                    DrawProperty(screenSpaceSubsurfaceMinStrength, ScreenSpaceSubsurfaceMinStrengthLabel);
                }
            }

            EditorGUILayout.HelpBox("材质里的 Subsurface Profile Index 会查这张表：0 使用 Default Profile，1-7 使用 Profile List 的第 1-7 个槽。", MessageType.Info);
        }

        private void DrawProfileListProperty()
        {
            if (screenSpaceSubsurfaceProfiles == null)
            {
                return;
            }

            if (screenSpaceSubsurfaceProfiles.arraySize > BurtSubsurfaceProfilePalette.MaxProfiles - 1)
            {
                screenSpaceSubsurfaceProfiles.arraySize = BurtSubsurfaceProfilePalette.MaxProfiles - 1;
            }

            DrawProperty(screenSpaceSubsurfaceProfiles, ScreenSpaceSubsurfaceProfilesLabel);
        }

        private void DrawPostProcessGroup() // 绘制后处理框架设置；Tonemapping、Bloom 等具体效果参数走 Global Volume。
        {
            DrawSectionHeader("Post Processing / 后处理"); // 显示后处理分组标题。
            DrawPropertyWithChildren(postProcessSettings, PostProcessSettingsLabel); // 绘制后处理设置对象和子字段，让框架开关能直接在现有 Inspector 里编辑。
            DrawProperty(postProcessVolumeLayerMask, PostProcessVolumeLayerMaskLabel); // 绘制 Volume 查询层，用户可以决定哪些 Global Volume 会影响 BurtRP 后处理。
            EditorGUILayout.HelpBox("当前后处理链路是 CameraColor -> PostProcessColor -> CameraColor；Tonemapping 参数从 Global Volume 读取，FinalBlit 仍然负责最终输出方向。", MessageType.Info); // 提示当前 Tonemapping 的配置来源和 FinalBlit 的职责。
        }

        private void DrawDepthGroup() // 绘制深度相关设置和 Depth Debug 提示。
        {
            DrawSectionHeader("Depth / 深度"); // 显示深度分组标题。
            DrawProperty(enableDepthPrepass, DepthPrepassLabel); // 绘制 Depth Prepass 开关。
            DrawProperty(enableDepthDebugView, DepthDebugLabel); // 绘制 Depth Debug 开关。
            DrawProperty(depthDebugScale, DepthScaleLabel); // 绘制 Depth Debug 缩放。
            EditorGUILayout.HelpBox("Depth Debug 会把 CameraDepth 可视化并覆盖 CameraColor，开启后正常画面会被调试图替代。", MessageType.Info); // 提示 Depth Debug 覆盖最终颜色。
        }

        private void DrawDebugGroup() // 绘制和全局调试相关的开关。
        {
            DrawSectionHeader("Debug / 调试"); // 显示调试分组标题。
            DrawProperty(enableUnsupportedShaderDebug, UnsupportedShaderDebugLabel); // 绘制不支持 Shader 调试开关。
            DrawProperty(enableRenderGraphDebug, RenderGraphDebugLabel); // 绘制 RenderGraph 调试开关。
            using (new EditorGUI.DisabledScope(enableRenderGraphDebug == null || !enableRenderGraphDebug.boolValue)) // 只有开启捕获时，Console 长日志开关才有意义。
            {
                DrawProperty(enableRenderGraphDebugConsoleLog, RenderGraphDebugConsoleLogLabel); // 绘制是否继续把完整 dump 打到 Console 的开关。
            }

            DrawRenderGraphClipboardButtons(); // 绘制 RenderGraph Debug 剪切板按钮，避免用户手动从 Console 复制长日志。
            EditorGUILayout.HelpBox("RenderGraph Debug 现在默认只缓存最近一次 dump；需要长日志时再打开 Console Log，平时建议用按钮复制到剪切板。", MessageType.Info); // 提示新工作流：按钮复制优先，Console 输出按需开启。
        }

        private void DrawRenderGraphClipboardButtons() // 绘制 RenderGraph Debug 剪切板相关按钮。
        {
            var pipelineAsset = target as BurtRenderPipelineAsset; // 把当前 Inspector 目标转换成 BurtRenderPipelineAsset，便于调用复制方法。

            if (pipelineAsset == null) // 如果转换失败，说明 Inspector 目标异常。
            {
                return; // 直接跳过按钮，避免空引用。
            }

            EditorGUILayout.LabelField("Last RenderGraph Debug", pipelineAsset.LatestRenderGraphDebugDumpSummary); // 显示最近一次缓存摘要，帮助确认复制的是哪一帧、哪个相机。
            EditorGUILayout.LabelField(pipelineAsset.GetLatestRenderGraphDebugDumpSummary(BurtRenderRequestType.SceneView)); // 单独显示 SceneView 最近缓存，避免误把它当成 Preview/Reflection。
            EditorGUILayout.LabelField(pipelineAsset.GetLatestRenderGraphDebugDumpSummary(BurtRenderRequestType.Preview)); // 单独显示 Preview 最近缓存，排查 Cubemap/ReflectionProbe Inspector Preview。
            EditorGUILayout.LabelField(pipelineAsset.GetLatestRenderGraphDebugDumpSummary(BurtRenderRequestType.Reflection)); // 单独显示 Reflection 最近缓存，排查 ReflectionProbe 捕获/刷新。

            using (new EditorGUILayout.HorizontalScope()) // 把复制按钮放在同一行，减少 Inspector 占用高度。
            {
                using (new EditorGUI.DisabledScope(!pipelineAsset.HasLatestRenderGraphDebugDump)) // 没有缓存时禁用“复制最近一次”按钮。
                {
                    if (GUILayout.Button("复制最近一次到剪切板")) // 用户点击后立即复制当前缓存。
                    {
                        CopyLatestRenderGraphDebugDump(pipelineAsset); // 执行复制，并输出短提示。
                    }
                }

                if (GUILayout.Button("下一帧复制到剪切板")) // 用户点击后请求下一次渲染图生成时自动复制。
                {
                    RequestCopyNextRenderGraphDebugDump(pipelineAsset); // 设置一次性复制请求，并请求编辑器刷新视图。
                }
            }

            using (new EditorGUILayout.HorizontalScope()) // 定向复制最近一次指定 request 类型，避免 SceneView 覆盖真正要看的 Preview/Reflection。
            {
                DrawCopyLatestRenderGraphDebugDumpButton(pipelineAsset, BurtRenderRequestType.SceneView, "复制 SceneView"); // 复制最近一次 SceneView dump。
                DrawCopyLatestRenderGraphDebugDumpButton(pipelineAsset, BurtRenderRequestType.Preview, "复制 Preview"); // 复制最近一次 Inspector/Asset Preview dump。
                DrawCopyLatestRenderGraphDebugDumpButton(pipelineAsset, BurtRenderRequestType.Reflection, "复制 Reflection"); // 复制最近一次 ReflectionProbe 捕获 dump。
            }

            using (new EditorGUILayout.HorizontalScope()) // 定向等待下一次指定 request 类型，解决“下一帧”被 SceneView 抢先消费的问题。
            {
                if (GUILayout.Button("下一帧复制 Preview")) // 用户要排查 Inspector Preview 时点击。
                {
                    RequestCopyNextRenderGraphDebugDump(pipelineAsset, BurtRenderRequestType.Preview); // 只等待 Preview request，不让 SceneView 抢先复制。
                }

                if (GUILayout.Button("下一帧复制 Reflection")) // 用户要排查 ReflectionProbe 捕获时点击。
                {
                    RequestCopyNextRenderGraphDebugDump(pipelineAsset, BurtRenderRequestType.Reflection); // 只等待 Reflection request。
                }
            }

            using (new EditorGUI.DisabledScope(!pipelineAsset.HasLatestRenderGraphDebugDump)) // 没有缓存时禁用清空按钮。
            {
                if (GUILayout.Button("清空缓存的 RenderGraph Debug")) // 用户点击后清掉最近一次长文本缓存。
                {
                    pipelineAsset.ClearLatestRenderGraphDebugDump(); // 清空运行时静态缓存。
                }
            }
        }

        private static void DrawCopyLatestRenderGraphDebugDumpButton( // 绘制一个“复制指定 request 类型最近 dump”的按钮。
            BurtRenderPipelineAsset pipelineAsset, // 当前 Inspector 对应的管线资产。
            BurtRenderRequestType requestType, // 要复制的 request 类型。
            string buttonText) // 按钮显示文本。
        {
            using (new EditorGUI.DisabledScope(!pipelineAsset.HasLatestRenderGraphDebugDumpForRequestType(requestType))) // 没有对应类型缓存时禁用按钮。
            {
                if (GUILayout.Button(buttonText)) // 用户点击后复制指定类型缓存。
                {
                    CopyLatestRenderGraphDebugDump(pipelineAsset, requestType); // 执行按类型复制。
                }
            }
        }

        private static void CopyLatestRenderGraphDebugDump(BurtRenderPipelineAsset pipelineAsset) // 复制最近一次 RenderGraph Debug 到剪切板。
        {
            if (pipelineAsset.CopyLatestRenderGraphDebugDumpToClipboard()) // 尝试把缓存文本写入系统剪切板。
            {
                Debug.Log("[BurtRP][RenderGraphClipboard] 已复制最近一次 RenderGraph Debug 到剪切板：" + pipelineAsset.LatestRenderGraphDebugDumpSummary); // 输出短确认，不打印完整 dump。

                return; // 复制成功后结束。
            }

            EditorUtility.DisplayDialog("BurtRP RenderGraph Debug", "当前还没有缓存 RenderGraph Debug。请先打开 Capture 等一帧，或点击“下一帧复制到剪切板”。", "OK"); // 没有缓存时给出明确操作提示。
        }

        private static void CopyLatestRenderGraphDebugDump( // 复制指定 request 类型最近一次 RenderGraph Debug 到剪切板。
            BurtRenderPipelineAsset pipelineAsset, // 当前 Inspector 对应的管线资产。
            BurtRenderRequestType requestType) // 要复制的 request 类型。
        {
            if (pipelineAsset.CopyLatestRenderGraphDebugDumpToClipboard(requestType)) // 尝试复制指定类型缓存。
            {
                Debug.Log("[BurtRP][RenderGraphClipboard] 已复制 " + requestType + " RenderGraph Debug 到剪切板：" + pipelineAsset.GetLatestRenderGraphDebugDumpSummary(requestType)); // 输出短确认。

                return; // 复制成功后结束。
            }

            EditorUtility.DisplayDialog("BurtRP RenderGraph Debug", "当前还没有缓存 " + requestType + " RenderGraph Debug。请先打开 Capture 等一帧，或点击对应的“下一帧复制”。", "OK"); // 没有缓存时给出明确操作提示。
        }

        private static void RequestCopyNextRenderGraphDebugDump(BurtRenderPipelineAsset pipelineAsset) // 请求下一帧自动复制 RenderGraph Debug。
        {
            pipelineAsset.RequestCopyNextRenderGraphDebugDumpToClipboard(); // 设置运行时一次性复制请求。
            EditorApplication.QueuePlayerLoopUpdate(); // 请求 Unity 编辑器尽快跑一次 PlayerLoop，让渲染器有机会生成 dump。
            SceneView.RepaintAll(); // 请求 SceneView 重绘，确保只打开 SceneView 时也能触发一次渲染。
            Debug.Log("[BurtRP][RenderGraphClipboard] 已请求下一帧 RenderGraph Debug 复制到剪切板。"); // 输出短提示，说明按钮已经生效。
        }

        private static void RequestCopyNextRenderGraphDebugDump( // 请求下一次指定 request 类型自动复制 RenderGraph Debug。
            BurtRenderPipelineAsset pipelineAsset, // 当前 Inspector 对应的管线资产。
            BurtRenderRequestType requestType) // 要等待的 request 类型。
        {
            pipelineAsset.RequestCopyNextRenderGraphDebugDumpToClipboard(requestType); // 设置按类型过滤的一次性复制请求。
            EditorApplication.QueuePlayerLoopUpdate(); // 请求 Unity 编辑器尽快跑一次 PlayerLoop。
            SceneView.RepaintAll(); // 仍然刷新 SceneView；Preview/Reflection 会在对应窗口或 Probe 刷新时命中。
            Debug.Log("[BurtRP][RenderGraphClipboard] 已请求下一次 " + requestType + " RenderGraph Debug 复制到剪切板。"); // 输出短提示。
        }

        private void DrawCameraDebugGroup() // 绘制相机调试相关设置。
        {
            DrawSectionHeader("Camera Debug / 相机调试"); // 显示相机调试分组标题。
            DrawProperty(enableCameraSortDebugLog, CameraSortDebugLabel); // 绘制相机排序日志开关。
            DrawProperty(enableRenderFrameDebugLog, RenderFrameDebugLabel); // 绘制 Frame/Stack 分组日志开关。
            EditorGUILayout.HelpBox("Render Frame Debug 只输出分组诊断，不改变画面。", MessageType.Info); // 提示 Frame Debug 只做诊断。
        }

        private SerializedProperty FindProperty(string propertyName) // 按字段名查找 SerializedProperty，并在缺失时输出错误方便定位字段改名。
        {
            var property = serializedObject.FindProperty(propertyName); // 使用 Unity 序列化系统读取私有 SerializeField 字段。
            if (property == null) // 字段缺失时给出明确错误，而不是让 Inspector 静默漏项。
            {
                Debug.LogError($"BurtRenderPipelineAssetEditor 找不到序列化字段：{propertyName}", target); // 输出带资源上下文的错误日志。
            }

            return property; // 返回找到的字段，调用方会安全跳过空字段。
        }

        private static void DrawSectionHeader(string title) // 绘制统一的分组标题，让 Inspector 结构更清晰。
        {
            EditorGUILayout.Space(8f); // 分组之间留出间距，提升可读性。
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel); // 使用 Unity 内置粗体标题，保持 Editor 原生风格。
        }

        private static void DrawProperty(SerializedProperty property, GUIContent label) // 安全绘制单个字段，字段缺失时自动跳过。
        {
            if (property == null) // 如果运行时代码字段被改名或删除，避免 Inspector 抛空引用异常。
            {
                return; // 跳过缺失字段，错误已由 FindProperty 输出。
            }

            EditorGUILayout.PropertyField(property, label); // 绘制字段并保留 Unity 默认 Undo、多对象编辑和 prefab 逻辑。
        }

        private static void DrawPropertyWithChildren(SerializedProperty property, GUIContent label) // 安全绘制带子字段的对象属性，给内嵌配置类使用。
        {
            if (property == null) // 如果运行时代码字段被改名或删除，避免 Inspector 抛空引用异常。
            {
                return; // 跳过缺失字段，错误已由 FindProperty 输出。
            }

            EditorGUILayout.PropertyField(property, label, true); // 绘制对象本身和所有子字段，确保 BurtPostProcessSettings 可以直接展开编辑。
        }
    }
}
