using Burt.RenderPipeline; // 引入 BurtRP 运行时命名空间，用于给 BurtRenderPipelineAsset 注册自定义 Inspector。
using UnityEditor; // 引入 UnityEditor，用于实现 Editor、SerializedProperty 和 Inspector GUI。
using UnityEngine; // 引入 UnityEngine，用于 HelpBox、GUIContent 等编辑器界面类型。

namespace Burt.RenderPipeline.Editor // 将编辑器扩展放在 BurtRP Editor 命名空间，避免和运行时代码混在一起。
{
    [CustomEditor(typeof(BurtRenderPipelineAsset))] // 指定这个 Inspector 只接管 BurtRenderPipelineAsset 资源。
    internal sealed class BurtRenderPipelineAssetEditor : UnityEditor.Editor // 继承 UnityEditor.Editor 来绘制自定义资源面板。
    {
        private SerializedProperty clearColor; // 缓存默认清屏颜色字段，General 分组使用。

        private SerializedProperty enableDepthPrepass; // 缓存 Depth Prepass 开关字段。
        private SerializedProperty enableDepthDebugView; // 缓存 Depth Debug 覆盖 CameraColor 的开关字段。
        private SerializedProperty depthDebugScale; // 缓存 Depth Debug 的显示缩放字段。

        private SerializedProperty preintegratedFGLut; // 缓存 PBR 预积分 FG LUT 字段。

        private SerializedProperty enableMainLightShadows; // 缓存主光阴影总开关字段。
        private SerializedProperty mainLightShadowResolution; // 缓存主光阴影图分辨率字段。
        private SerializedProperty mainLightShadowDistance; // 缓存主光阴影距离字段。
        private SerializedProperty mainLightShadowDepthBias; // 缓存主光阴影深度偏移字段。
        private SerializedProperty mainLightShadowNormalBias; // 缓存主光阴影法线偏移字段。
        private SerializedProperty mainLightShadowSampleBias; // 缓存主光阴影采样偏移字段。
        private SerializedProperty enableMainLightShadowDebugView; // 缓存 Shadow Debug 覆盖 CameraColor 的开关字段。
        private SerializedProperty mainLightShadowDebugExposure; // 缓存 Shadow Debug 曝光字段。
        private SerializedProperty mainLightShadowDebugYFlipMode; // 缓存 Shadow Debug Y 翻转模式字段。
        private SerializedProperty enableMainLightShadowDebugLog; // 缓存 Shadow Debug 日志字段。

        private SerializedProperty enableUnsupportedShaderDebug; // 缓存不支持 Shader 可视化调试字段。
        private SerializedProperty enableRenderGraphDebug; // 缓存 RenderGraph 调试日志字段。

        private SerializedProperty enableCameraSortDebugLog; // 缓存相机排序调试日志字段。
        private SerializedProperty enableRenderFrameDebugLog; // 缓存 Frame/Stack 分组调试日志字段。

        private static readonly GUIContent ClearColorLabel = new("Clear Color", "默认清屏颜色，供 BurtRP 清屏 Pass 使用。"); // 定义 General 分组显示文本。
        private static readonly GUIContent DepthPrepassLabel = new("Depth Prepass", "开启后先写入 CameraDepth，便于后续深度相关 Pass 使用。"); // 定义 Depth Prepass 显示文本。
        private static readonly GUIContent DepthDebugLabel = new("Depth Debug View", "开启后把 CameraDepth 可视化到 CameraColor。"); // 定义 Depth Debug 显示文本。
        private static readonly GUIContent DepthScaleLabel = new("Depth Debug Scale", "调整深度可视化亮度缩放，数值越大近处深度越明显。"); // 定义 Depth Debug 缩放显示文本。
        private static readonly GUIContent PreintegratedFGLutLabel = new("Preintegrated FG LUT", "用于 IBL 间接高光的 DFG/GGX 预积分查找表。"); // 定义 PBR 预积分 LUT 显示文本。
        private static readonly GUIContent MainLightShadowLabel = new("Enable Shadows", "允许 BurtRP 为主方向光渲染 shadow map。"); // 定义主光阴影总开关显示文本。
        private static readonly GUIContent ShadowResolutionLabel = new("Resolution", "主光阴影图默认分辨率。"); // 定义阴影分辨率显示文本。
        private static readonly GUIContent ShadowDistanceLabel = new("Distance", "主光阴影最大剔除距离。"); // 定义阴影距离显示文本。
        private static readonly GUIContent ShadowDepthBiasLabel = new("Depth Bias", "写入 shadow map 时使用的常量深度偏移。"); // 定义深度偏移显示文本。
        private static readonly GUIContent ShadowNormalBiasLabel = new("Normal Bias", "写入 shadow map 时沿法线方向施加的偏移。"); // 定义法线偏移显示文本。
        private static readonly GUIContent ShadowSampleBiasLabel = new("Sample Bias", "接收端采样 shadow map 前减去的深度偏移。"); // 定义采样偏移显示文本。
        private static readonly GUIContent ShadowDebugViewLabel = new("Shadow Debug View", "开启后把主光 shadow map 直接绘制到 CameraColor。"); // 定义 Shadow Debug 显示文本。
        private static readonly GUIContent ShadowDebugExposureLabel = new("Shadow Debug Exposure", "调节 shadow map 调试图亮度。"); // 定义 Shadow Debug 曝光显示文本。
        private static readonly GUIContent ShadowDebugYFlipLabel = new("Shadow Debug Y Flip", "调节 shadow map 调试图的 Y 翻转策略。"); // 定义 Y 翻转显示文本。
        private static readonly GUIContent ShadowDebugLogLabel = new("Shadow Debug Log", "输出主光阴影诊断日志，排查阴影数据时再开启。"); // 定义 Shadow Debug 日志显示文本。
        private static readonly GUIContent UnsupportedShaderDebugLabel = new("Unsupported Shader Debug", "用 Unity 错误材质标记非 BurtRP Shader，方便发现错误材质。"); // 定义不支持 Shader 调试显示文本。
        private static readonly GUIContent RenderGraphDebugLabel = new("RenderGraph Debug", "输出 RenderGraph 调试信息，可能每帧写入 Console。"); // 定义 RenderGraph 调试显示文本。
        private static readonly GUIContent CameraSortDebugLabel = new("Camera Sort Debug Log", "输出相机 request 排序列表，多相机调试时使用。"); // 定义相机排序调试显示文本。
        private static readonly GUIContent RenderFrameDebugLabel = new("Render Frame Debug Log", "输出 Frame/Stack 分组日志。"); // 定义 Frame/Stack 分组调试显示文本。

        private void OnEnable() // Unity 选中资源或脚本重载后调用，用于绑定所有序列化字段。
        {
            clearColor = FindProperty(nameof(clearColor)); // 绑定 clearColor 私有字段，不改字段名以保持现有同步逻辑稳定。

            enableDepthPrepass = FindProperty(nameof(enableDepthPrepass)); // 绑定深度预写开关。
            enableDepthDebugView = FindProperty(nameof(enableDepthDebugView)); // 绑定深度调试视图开关。
            depthDebugScale = FindProperty(nameof(depthDebugScale)); // 绑定深度调试缩放。

            preintegratedFGLut = FindProperty(nameof(preintegratedFGLut)); // 绑定 PBR 预积分 FG LUT。

            enableMainLightShadows = FindProperty(nameof(enableMainLightShadows)); // 绑定主光阴影总开关。
            mainLightShadowResolution = FindProperty(nameof(mainLightShadowResolution)); // 绑定主光阴影分辨率。
            mainLightShadowDistance = FindProperty(nameof(mainLightShadowDistance)); // 绑定主光阴影距离。
            mainLightShadowDepthBias = FindProperty(nameof(mainLightShadowDepthBias)); // 绑定主光阴影深度偏移。
            mainLightShadowNormalBias = FindProperty(nameof(mainLightShadowNormalBias)); // 绑定主光阴影法线偏移。
            mainLightShadowSampleBias = FindProperty(nameof(mainLightShadowSampleBias)); // 绑定主光阴影采样偏移。
            enableMainLightShadowDebugView = FindProperty(nameof(enableMainLightShadowDebugView)); // 绑定阴影调试视图开关。
            mainLightShadowDebugExposure = FindProperty(nameof(mainLightShadowDebugExposure)); // 绑定阴影调试曝光。
            mainLightShadowDebugYFlipMode = FindProperty(nameof(mainLightShadowDebugYFlipMode)); // 绑定阴影调试 Y 翻转模式。
            enableMainLightShadowDebugLog = FindProperty(nameof(enableMainLightShadowDebugLog)); // 绑定阴影调试日志开关。

            enableUnsupportedShaderDebug = FindProperty(nameof(enableUnsupportedShaderDebug)); // 绑定不支持 Shader 调试开关。
            enableRenderGraphDebug = FindProperty(nameof(enableRenderGraphDebug)); // 绑定 RenderGraph 调试开关。

            enableCameraSortDebugLog = FindProperty(nameof(enableCameraSortDebugLog)); // 绑定相机排序日志开关。
            enableRenderFrameDebugLog = FindProperty(nameof(enableRenderFrameDebugLog)); // 绑定 Frame/Stack 分组日志开关。
        }

        public override void OnInspectorGUI() // 绘制 BurtRenderPipelineAsset 的完整中文分组 Inspector。
        {
            serializedObject.Update(); // 读取资源当前序列化状态，确保 Inspector 显示最新数据。

            DrawGeneralGroup(); // 绘制 General 分组。
            DrawDepthGroup(); // 绘制 Depth 分组。
            DrawPBRGroup(); // 绘制 PBR 分组。
            DrawMainLightShadowGroup(); // 绘制 Main Light Shadows 分组。
            DrawDebugGroup(); // 绘制 Debug 分组。
            DrawCameraDebugGroup(); // 绘制 Camera Debug 分组。

            serializedObject.ApplyModifiedProperties(); // 写回用户在 Inspector 中修改的字段。
        }

        private void DrawGeneralGroup() // 绘制常规渲染设置，当前只包含默认清屏颜色。
        {
            DrawSectionHeader("General / 通用"); // 显示中英文分组标题。
            DrawProperty(clearColor, ClearColorLabel); // 绘制默认清屏颜色字段。
        }

        private void DrawPBRGroup() // 绘制 PBR 设置。
        {
            DrawSectionHeader("PBR / Shading"); // 显示 PBR 分组标题。
            DrawProperty(preintegratedFGLut, PreintegratedFGLutLabel); // 绘制预积分 FG LUT 引用。
            EditorGUILayout.HelpBox("PreintegratedFG.exr 用于 PBR IBL DFG。", MessageType.Info); // 提示 LUT 数据用途。
        }

        private void DrawDepthGroup() // 绘制深度相关设置和 Depth Debug 提示。
        {
            DrawSectionHeader("Depth / 深度"); // 显示深度分组标题。
            DrawProperty(enableDepthPrepass, DepthPrepassLabel); // 绘制 Depth Prepass 开关。
            DrawProperty(enableDepthDebugView, DepthDebugLabel); // 绘制 Depth Debug 开关。
            DrawProperty(depthDebugScale, DepthScaleLabel); // 绘制 Depth Debug 缩放。
            EditorGUILayout.HelpBox("Depth Debug 会把 CameraDepth 可视化并覆盖 CameraColor，开启后正常画面会被调试图替代。", MessageType.Info); // 提示 Depth Debug 覆盖最终颜色。
        }

        private void DrawMainLightShadowGroup() // 绘制主光阴影设置和 Shadow Debug 提示。
        {
            DrawSectionHeader("Main Light Shadows / 主光阴影"); // 显示主光阴影分组标题。
            DrawProperty(enableMainLightShadows, MainLightShadowLabel); // 绘制主光阴影总开关。
            using (new EditorGUI.DisabledScope(enableMainLightShadows != null && !enableMainLightShadows.boolValue)) // 关闭主光阴影时禁用具体参数，避免误以为仍会生效。
            {
                DrawProperty(mainLightShadowResolution, ShadowResolutionLabel); // 绘制阴影图分辨率。
                DrawProperty(mainLightShadowDistance, ShadowDistanceLabel); // 绘制阴影距离。
                DrawProperty(mainLightShadowDepthBias, ShadowDepthBiasLabel); // 绘制写入端深度偏移。
                DrawProperty(mainLightShadowNormalBias, ShadowNormalBiasLabel); // 绘制写入端法线偏移。
                DrawProperty(mainLightShadowSampleBias, ShadowSampleBiasLabel); // 绘制采样端深度偏移。
                DrawProperty(enableMainLightShadowDebugView, ShadowDebugViewLabel); // 绘制 Shadow Debug 开关。
                DrawProperty(mainLightShadowDebugExposure, ShadowDebugExposureLabel); // 绘制 Shadow Debug 曝光。
                DrawProperty(mainLightShadowDebugYFlipMode, ShadowDebugYFlipLabel); // 绘制 Shadow Debug Y 翻转模式。
                DrawProperty(enableMainLightShadowDebugLog, ShadowDebugLogLabel); // 绘制 Shadow Debug 日志开关。
            }

            EditorGUILayout.HelpBox("Shadow Debug 会把主光 shadow map 绘制到 CameraColor，开启后正常画面会被调试图替代。", MessageType.Info); // 提示 Shadow Debug 覆盖最终颜色。
        }

        private void DrawDebugGroup() // 绘制和全局调试相关的开关。
        {
            DrawSectionHeader("Debug / 调试"); // 显示调试分组标题。
            DrawProperty(enableUnsupportedShaderDebug, UnsupportedShaderDebugLabel); // 绘制不支持 Shader 调试开关。
            DrawProperty(enableRenderGraphDebug, RenderGraphDebugLabel); // 绘制 RenderGraph 调试开关。
            EditorGUILayout.HelpBox("RenderGraph Debug 可能在每帧输出日志，建议只在排查渲染图问题时临时开启。", MessageType.Warning); // 提示 RenderGraph Debug 可能刷 Console。
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
    }
}
