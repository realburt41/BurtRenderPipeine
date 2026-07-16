using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Burt.RenderPipeline.Editor
{
    internal sealed class BurtXGIProbeBakingWindow : EditorWindow
    {
        private const string MenuPath = "BurtRP/XGI/Probe Baking Window";

        private BurtXGIProbeBakingConfig activeConfig;
        private BurtXGIProbeBakingPlatform platform = BurtXGIProbeBakingPlatform.PC;
        private readonly List<BurtXGIProbeBakingConfig> configs = new List<BurtXGIProbeBakingConfig>();
        private Vector2 scrollPosition;
        private string validationReport = string.Empty;
        private string progressReport = string.Empty;
        private string resultReport = string.Empty;
        private float progress;
        private UnityEditor.Editor activeConfigEditor;
        private bool xgiToolsDebugFoldout = true;
        private static string xgiToolsResourceReport = string.Empty;

        [MenuItem(MenuPath, false, 2490)]
        private static void Open()
        {
            var window = GetWindow<BurtXGIProbeBakingWindow>("Burt XGI Bake");
            window.minSize = new Vector2(420f, 520f);
            window.RefreshConfigs();
            window.Show();
        }

        public static void ValidateXGIToolsApplyFromCommandLine()
        {
            var report = ValidateXGIToolsApply(out var hasIssue);
            if (hasIssue)
            {
                Debug.LogError(report);
                EditorApplication.Exit(3);
                return;
            }

            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        private void OnEnable()
        {
            RefreshConfigs();
            BurtXGIProbeBakeAPI.OnBakeProgress += OnBakeProgress;
            BurtXGIProbeBakeAPI.OnBakeCompleted += OnBakeCompleted;
        }

        private void OnDisable()
        {
            BurtXGIProbeBakeAPI.OnBakeProgress -= OnBakeProgress;
            BurtXGIProbeBakeAPI.OnBakeCompleted -= OnBakeCompleted;
            DestroyCachedEditor();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is BurtXGIProbeBakingConfig selectedConfig)
            {
                SetActiveConfig(selectedConfig);
                Repaint();
            }
        }

        private void OnGUI()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scroll.scrollPosition;
                DrawHeader();
                DrawConfigSelection();
                DrawValidation();
                DrawBakeControls();
                DrawProgress();
                DrawXGIToolsDebug();
                DrawActiveConfigInspector();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Burt XGI Probe Baking", EditorStyles.boldLabel);
            EditorGUILayout.Space();
        }

        private void DrawConfigSelection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Config", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                platform = (BurtXGIProbeBakingPlatform)EditorGUILayout.EnumPopup("Platform", platform);
                if (EditorGUI.EndChangeCheck())
                {
                    PickFirstConfigForPlatform();
                }

                EditorGUI.BeginChangeCheck();
                var nextConfig = (BurtXGIProbeBakingConfig)EditorGUILayout.ObjectField(
                    "Active Config",
                    activeConfig,
                    typeof(BurtXGIProbeBakingConfig),
                    false);
                if (EditorGUI.EndChangeCheck())
                {
                    SetActiveConfig(nextConfig);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Use Selection"))
                    {
                        SetActiveConfig(Selection.activeObject as BurtXGIProbeBakingConfig);
                    }

                    if (GUILayout.Button("Refresh"))
                    {
                        RefreshConfigs();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create Config"))
                    {
                        CreateConfigAsset();
                    }

                    if (GUILayout.Button("Create Scene Configs"))
                    {
                        CreateSceneConfigAssets();
                    }

                    if (GUILayout.Button("Select Asset") && activeConfig != null)
                    {
                        Selection.activeObject = activeConfig;
                        EditorGUIUtility.PingObject(activeConfig);
                    }
                }

                EditorGUILayout.LabelField("Known Configs", configs.Count.ToString());
                if (activeConfig != null)
                {
                    EditorGUILayout.LabelField("Baked Asset", activeConfig.bakedDataAsset != null ? activeConfig.bakedDataAsset.name : "<none>");
                    EditorGUILayout.LabelField("Time Slice Assets", CountTimeSliceAssets(activeConfig).ToString());
                }
            }
        }

        private void DrawValidation()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scene Validation", EditorStyles.boldLabel);
                if (GUILayout.Button("Validate Scene"))
                {
                    ValidateScene();
                }

                if (!string.IsNullOrEmpty(validationReport))
                {
                    EditorGUILayout.TextArea(validationReport, GUILayout.MinHeight(120f));
                }
            }
        }

        private void DrawBakeControls()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(Application.isPlaying || BurtXGIProbeBakeAPI.IsRunning || activeConfig == null))
                {
                    if (GUILayout.Button(activeConfig != null && activeConfig.useTimeSliceData && activeConfig.bakeAllTimeSlices
                            ? "Bake All Time Slices"
                            : "Bake Active Config"))
                    {
                        StartBake(useConfigAllSlices: activeConfig != null && activeConfig.useTimeSliceData && activeConfig.bakeAllTimeSlices);
                    }

                    if (GUILayout.Button("Bake Active Config Only"))
                    {
                        StartBake(useConfigAllSlices: false);
                    }

                    if (GUILayout.Button("Bake All Time Slices"))
                    {
                        StartBake(useConfigAllSlices: true);
                    }
                }

                using (new EditorGUI.DisabledScope(!BurtXGIProbeBakeAPI.IsRunning))
                {
                    if (GUILayout.Button("Cancel"))
                    {
                        BurtXGIProbeBakeAPI.Cancel();
                    }
                }
            }
        }

        private void DrawProgress()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
                var statusRect = GUILayoutUtility.GetRect(1f, 18f);
                EditorGUI.ProgressBar(statusRect, progress, Mathf.RoundToInt(progress * 100f) + "%");
                if (!string.IsNullOrEmpty(progressReport))
                {
                    EditorGUILayout.TextArea(progressReport, GUILayout.MinHeight(80f));
                }

                if (!string.IsNullOrEmpty(resultReport))
                {
                    EditorGUILayout.TextArea(resultReport, GUILayout.MinHeight(48f));
                }
            }
        }

        private void DrawActiveConfigInspector()
        {
            if (activeConfig == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Active Config Inspector", EditorStyles.boldLabel);
                UnityEditor.Editor.CreateCachedEditor(activeConfig, typeof(BurtXGIProbeBakingConfigEditor), ref activeConfigEditor);
                activeConfigEditor?.OnInspectorGUI();
            }
        }

        private void DrawXGIToolsDebug()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                xgiToolsDebugFoldout = EditorGUILayout.Foldout(xgiToolsDebugFoldout, "XGI Tools Debug", true, EditorStyles.foldoutHeader);
                if (!xgiToolsDebugFoldout)
                {
                    return;
                }

                var debugComponent = BurtXGIToolsDebugComponent.instance;
                debugComponent.OnAfterDeserialize();

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Current Debug Component", debugComponent, typeof(BurtXGIToolsDebugComponent), false);
                }

                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(debugComponent, "Edit Burt XGI Tools Debug");
                DrawXGIToolsBase(debugComponent);
                DrawXGIToolsProbe(debugComponent);
                DrawXGIToolsVoxel(debugComponent);
                DrawXGIToolsSdf(debugComponent);
                DrawXGIToolsRtx(debugComponent);
                DrawXGIToolsDebugActions(debugComponent);
                if (EditorGUI.EndChangeCheck())
                {
                    debugComponent.OnBeforeSerialize();
                    EditorUtility.SetDirty(debugComponent);
                    SceneView.RepaintAll();
                }
            }
        }

        private static void DrawXGIToolsBase(BurtXGIToolsDebugComponent debugComponent)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Base", EditorStyles.boldLabel);
            debugComponent.followCamera = EditorGUILayout.Toggle("Follow Scene Camera", debugComponent.followCamera);
            debugComponent.followCameraOffset = EditorGUILayout.Slider("Follow Camera Offset", debugComponent.followCameraOffset, 0f, 50f);
        }

        private static void DrawXGIToolsProbe(BurtXGIToolsDebugComponent debugComponent)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Probe", EditorStyles.boldLabel);
            debugComponent.drawCells = EditorGUILayout.Toggle("Draw Cells", debugComponent.drawCells);
            debugComponent.drawBricks = EditorGUILayout.Toggle("Draw Bricks", debugComponent.drawBricks);
            if (debugComponent.drawCells || debugComponent.drawBricks)
            {
                EditorGUI.indentLevel++;
                debugComponent.realtimeSubdivision = EditorGUILayout.Toggle("Realtime Update", debugComponent.realtimeSubdivision);
                if (debugComponent.realtimeSubdivision)
                {
                    debugComponent.subdivisionCellUpdatePerFrame = EditorGUILayout.IntSlider("Cells Per Frame", debugComponent.subdivisionCellUpdatePerFrame, 1, 64);
                    debugComponent.subdivisionDelayInSeconds = EditorGUILayout.Slider("Update Delay", debugComponent.subdivisionDelayInSeconds, 0f, 10f);
                }

                debugComponent.subdivisionViewCullingDistance = EditorGUILayout.Slider("Draw Distance", debugComponent.subdivisionViewCullingDistance, 1f, 4000f);
                EditorGUI.indentLevel--;
            }

            debugComponent.drawProbes = EditorGUILayout.Toggle("Draw Probes", debugComponent.drawProbes);
            if (debugComponent.drawProbes)
            {
                EditorGUI.indentLevel++;
                debugComponent.drawProbesDepthTest = EditorGUILayout.Toggle("Depth Test", debugComponent.drawProbesDepthTest);
                debugComponent.drawProbeSize = EditorGUILayout.Slider("Probe Size", debugComponent.drawProbeSize, 0.05f, 3f);
                debugComponent.drawProbeCullingDistance = EditorGUILayout.Slider("Probe Distance", debugComponent.drawProbeCullingDistance, 1f, 4000f);
                debugComponent.minSubdivToVisualize = EditorGUILayout.IntSlider(
                    "Min Subdiv To Visualize",
                    debugComponent.minSubdivToVisualize,
                    0,
                    debugComponent.maxSubdivToVisualize);
                debugComponent.maxSubdivToVisualize = EditorGUILayout.IntSlider(
                    "Max Subdiv To Visualize",
                    debugComponent.maxSubdivToVisualize,
                    debugComponent.minSubdivToVisualize,
                    BurtXGIToolsDebugComponent.MaxProbeSubdivisionLevel);
                debugComponent.drawProbesDebugLayer = (BurtXGIToolsProbeDebugLayer)EditorGUILayout.EnumPopup("Debug Layer", debugComponent.drawProbesDebugLayer);
                EditorGUI.indentLevel--;
            }

            debugComponent.drawVirtualOffset = EditorGUILayout.Toggle("Draw Virtual Offset", debugComponent.drawVirtualOffset);
            if (debugComponent.drawVirtualOffset)
            {
                EditorGUI.indentLevel++;
                debugComponent.drawVirtualOffsetSize = EditorGUILayout.Slider("Virtual Offset Size", debugComponent.drawVirtualOffsetSize, 0.001f, 1f);
                EditorGUI.indentLevel--;
            }

            debugComponent.drawRuntimeInfo = EditorGUILayout.Toggle("Draw Runtime Info", debugComponent.drawRuntimeInfo);
            if (debugComponent.drawRuntimeInfo)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    "Applies the XGIProbe runtime streaming and memory overlay using the active SceneView camera.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }

        private static void DrawXGIToolsVoxel(BurtXGIToolsDebugComponent debugComponent)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Voxel", EditorStyles.boldLabel);
            debugComponent.drawVoxel = EditorGUILayout.Toggle("Draw Voxel", debugComponent.drawVoxel);
            if (debugComponent.drawVoxel)
            {
                EditorGUI.indentLevel++;
                debugComponent.drawVoxelByTrace = EditorGUILayout.Toggle("Trace Voxel", debugComponent.drawVoxelByTrace);
                debugComponent.drawVoxelDebugIndirectProbe = EditorGUILayout.Toggle("Draw Probe", debugComponent.drawVoxelDebugIndirectProbe);
                debugComponent.drawVoxelDebugCameraOffset = EditorGUILayout.Slider("Camera Offset", debugComponent.drawVoxelDebugCameraOffset, 0f, 800f);
                debugComponent.drawVoxelDebugCullingDistance = EditorGUILayout.Slider("Culling Distance", debugComponent.drawVoxelDebugCullingDistance, 0.01f, 4000f);
                debugComponent.drawVoxelsDebugLayer = (BurtXGIToolsVoxelDebugLayer)EditorGUILayout.EnumPopup("Debug Layer", debugComponent.drawVoxelsDebugLayer);
                debugComponent.voxelDebugMipLevel = EditorGUILayout.IntSlider("Mip Level", debugComponent.voxelDebugMipLevel, 0, 8);
                debugComponent.drawVoxelProbeSizeWS = EditorGUILayout.Slider("Probe Size", debugComponent.drawVoxelProbeSizeWS, 0.01f, 2f);
                EditorGUI.indentLevel--;
            }

            debugComponent.clipmapCount = EditorGUILayout.IntSlider("Clipmap Count", debugComponent.clipmapCount, 1, 8);
            debugComponent.voxelSize = (BurtXGIToolsVoxelSize)EditorGUILayout.EnumPopup("Voxel Size", debugComponent.voxelSize);
            debugComponent.voxelSizeWS = EditorGUILayout.Slider("Voxel Size WS", debugComponent.voxelSizeWS, 0.01f, 4f);
            debugComponent.materialBudget = (SceneVoxelMaterialMemoryBudget)EditorGUILayout.EnumPopup("Material Budget", debugComponent.materialBudget);
            debugComponent.materialGenMethod = (SceneVoxelMaterialGenerateMethod)EditorGUILayout.EnumPopup("Material Gen Method", debugComponent.materialGenMethod);
            debugComponent.lightingType = (SceneVoxelLightingType)EditorGUILayout.EnumPopup("Lighting Type", debugComponent.lightingType);
            debugComponent.voxelAlwaysUpdate = EditorGUILayout.Toggle("Always Update", debugComponent.voxelAlwaysUpdate);
            debugComponent.voxelDrawVegetation = EditorGUILayout.Toggle("Draw Vegetation", debugComponent.voxelDrawVegetation);
            debugComponent.voxelDrawGrass = EditorGUILayout.Toggle("Draw Grass", debugComponent.voxelDrawGrass);
            debugComponent.voxelLightingDirectionalShadow = EditorGUILayout.Toggle("Directional Shadow", debugComponent.voxelLightingDirectionalShadow);
            debugComponent.voxelLightingPunctualLightShadow = EditorGUILayout.Toggle("Punctual Shadow", debugComponent.voxelLightingPunctualLightShadow);
            debugComponent.voxelLightingSkyLight = EditorGUILayout.Toggle("Sky Light", debugComponent.voxelLightingSkyLight);
            DrawClipmapArray("Clipmap Offset", debugComponent.clipmapOffset, 0f, 5000f);
            DrawClipmapArray("Clipmap Update Distance", debugComponent.clipmapUpdateDistance, 0f, 5000f);
        }

        private static void DrawXGIToolsDebugActions(BurtXGIToolsDebugComponent debugComponent)
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(debugComponent == null || !IsAnyXGIDebugRequested(debugComponent)))
                {
                    if (GUILayout.Button("Apply XGI Debug"))
                    {
                        ApplyVoxelDebug(debugComponent);
                    }
                }

                if (GUILayout.Button("Clear BurtGI Debug"))
                {
                    BurtShadingDebugOverlayUtility.SetMode(BurtShadingDebugMode.None);
                }
            }

            if (GUILayout.Button("Validate XGI Resources"))
            {
                xgiToolsResourceReport = BurtScreenSpaceGlobalIlluminationDiagnosticsUtility.ResolveXGIResourceStatusReport();
            }

            if (!string.IsNullOrEmpty(xgiToolsResourceReport))
            {
                EditorGUILayout.TextArea(xgiToolsResourceReport, GUILayout.MinHeight(72f));
            }

            var xgiComponent = FindSceneXGILightComponent();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Target XGI Light", xgiComponent, typeof(BurtXGILightComponent), true);
            }

            if (xgiComponent == null)
            {
                EditorGUILayout.HelpBox("Apply XGI Debug needs a Burt XGI Light Component in the active scene.", MessageType.Info);
                if (GUILayout.Button("Create Burt XGI Light Component"))
                {
                    CreateSceneXGILightComponent();
                }
            }
        }

        private static void DrawXGIToolsSdf(BurtXGIToolsDebugComponent debugComponent)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("SDF", EditorStyles.boldLabel);
            debugComponent.drawSdf = EditorGUILayout.Toggle("Draw SDF", debugComponent.drawSdf);
            if (!debugComponent.drawSdf)
            {
                return;
            }

            EditorGUI.indentLevel++;
            debugComponent.drawSdfDebugUseOccupy = EditorGUILayout.Toggle("Use Occupancy", debugComponent.drawSdfDebugUseOccupy);
            debugComponent.drawSdfDebugLayer = (BurtXGIToolsSdfDebugLayer)EditorGUILayout.EnumPopup("Debug Layer", debugComponent.drawSdfDebugLayer);
            EditorGUI.indentLevel--;
            EditorGUILayout.HelpBox(
                "SDF debug resource status: " + BurtScreenSpaceGlobalIlluminationDiagnosticsUtility.ResolveXGISdfDebugStatusLabel() +
                ". XRender samples XGISdfGenContext.sdfTexture here; BRP now generates SDF for persistent scene voxel clipmaps and on-demand base debug, then samples the base SDF into the Scene Voxel debug view.",
                MessageType.Info);
        }

        private static void DrawXGIToolsRtx(BurtXGIToolsDebugComponent debugComponent)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("RTX", EditorStyles.boldLabel);
            debugComponent.drawRTX = EditorGUILayout.Toggle("Draw RTX", debugComponent.drawRTX);
            if (!debugComponent.drawRTX)
            {
                return;
            }

            EditorGUI.indentLevel++;
            debugComponent.rtxRange = EditorGUILayout.Slider("Range", debugComponent.rtxRange, 0.1f, 2000f);
            debugComponent.rtxEnableLODCulling = EditorGUILayout.Toggle("LOD Culling", debugComponent.rtxEnableLODCulling);
            debugComponent.rtxUpdateDistance = EditorGUILayout.Slider("Update Distance", debugComponent.rtxUpdateDistance, 0f, 1000f);
            debugComponent.rtxMesh = (Mesh)EditorGUILayout.ObjectField("Mesh", debugComponent.rtxMesh, typeof(Mesh), false);
            debugComponent.rtxMaterial = (Material)EditorGUILayout.ObjectField("Material", debugComponent.rtxMaterial, typeof(Material), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Instance"))
                {
                    debugComponent.RtxAddInstance();
                }

                if (GUILayout.Button("Remove Instance"))
                {
                    debugComponent.RtxRemoveInstance();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add AABB"))
                {
                    debugComponent.RtxAddAABB();
                }

                if (GUILayout.Button("Remove AABB"))
                {
                    debugComponent.RtxRemoveAABB();
                }
            }
            EditorGUI.indentLevel--;
        }

        private static void ApplyVoxelDebug(BurtXGIToolsDebugComponent debugComponent)
        {
            if (debugComponent == null)
            {
                return;
            }

            if (!debugComponent.drawVoxel && !debugComponent.drawSdf)
            {
                if (debugComponent.drawRuntimeInfo)
                {
                    BurtShadingDebugOverlayUtility.SetMode(BurtShadingDebugMode.GIProbeRuntimeInfo);
                }

                SceneView.RepaintAll();
                return;
            }

            var xgiComponent = FindSceneXGILightComponent();
            if (xgiComponent == null)
            {
                CreateSceneXGILightComponent();
                xgiComponent = FindSceneXGILightComponent();
            }

            if (xgiComponent == null)
            {
                return;
            }

            Undo.RecordObject(xgiComponent, "Apply Burt XGI Tools Voxel Debug");
            xgiComponent.overrideConfig = true;
            xgiComponent.sceneVoxelDebugExpandView = debugComponent.drawVoxelByTrace || debugComponent.drawVoxelDebugIndirectProbe;
            xgiComponent.sceneVoxelDebugExpandViewDistance = debugComponent.drawVoxelDebugCullingDistance;
            xgiComponent.sceneVoxelDebugShowMipmapID = debugComponent.voxelDebugMipLevel;
            xgiComponent.sceneVoxelDebugLayer = debugComponent.drawVoxelsDebugLayer;
            xgiComponent.sceneVoxelDebugByTrace = debugComponent.drawVoxelByTrace;
            xgiComponent.sceneVoxelDebugDrawProbe = debugComponent.drawVoxelDebugIndirectProbe;
            xgiComponent.sceneVoxelDebugProbeSizeWS = debugComponent.drawVoxelProbeSizeWS;
            xgiComponent.sceneVoxelAlwaysUpdate = debugComponent.voxelAlwaysUpdate;
            xgiComponent.sceneVoxelFollowCamera = debugComponent.followCamera;
            xgiComponent.sceneVoxelCameraForward = debugComponent.followCameraOffset;
            xgiComponent.sceneVoxelOrigin = debugComponent.Position;
            xgiComponent.sceneVoxelClipMapCount = Mathf.Clamp(debugComponent.clipmapCount, 1, 6);
            xgiComponent.sceneVoxelClipMapResolution = ResolveToolsVoxelResolution(debugComponent.voxelSize);
            xgiComponent.sceneVoxelClipMapFirstWorldExtent = ResolveToolsVoxelFirstWorldExtent(debugComponent.voxelSize, debugComponent.voxelSizeWS);
            xgiComponent.sceneVoxelMaterialBudget = debugComponent.materialBudget;
            xgiComponent.sceneVoxelMaterialGenerateMethod = debugComponent.materialGenMethod;
            xgiComponent.sceneVoxelDrawVegetation = debugComponent.voxelDrawVegetation;
            xgiComponent.sceneVoxelDrawGrass = debugComponent.voxelDrawGrass;
            xgiComponent.sceneVoxelLightingType = debugComponent.lightingType;
            xgiComponent.sceneVoxelLightingDirectionalShadow = debugComponent.voxelLightingDirectionalShadow;
            xgiComponent.sceneVoxelLightingPunctualShadow = debugComponent.voxelLightingPunctualLightShadow;
            xgiComponent.sceneVoxelLightingSkyLight = debugComponent.voxelLightingSkyLight;
            xgiComponent.sceneVoxelClipMapOffset03 = ResolveClipmapVector(debugComponent.clipmapOffset);
            xgiComponent.sceneVoxelClipMapUpdateDistance03 = ResolveClipmapVector(debugComponent.clipmapUpdateDistance);
            xgiComponent.sceneVoxelClipMapOffset47 = ResolveClipmapVector(debugComponent.clipmapOffset, 4);
            xgiComponent.sceneVoxelClipMapUpdateDistance47 = ResolveClipmapVector(debugComponent.clipmapUpdateDistance, 4);
            EditorUtility.SetDirty(xgiComponent);
            BurtShadingDebugOverlayUtility.SetMode(BurtShadingDebugMode.ScreenSpaceGlobalIlluminationSceneVoxelOccupancy);
        }

        private static bool IsAnyXGIDebugRequested(BurtXGIToolsDebugComponent debugComponent)
        {
            return debugComponent != null &&
                (debugComponent.drawCells ||
                 debugComponent.drawBricks ||
                 debugComponent.drawProbes ||
                 debugComponent.drawVirtualOffset ||
                 debugComponent.drawRuntimeInfo ||
                 debugComponent.drawVoxel ||
                 debugComponent.drawSdf);
        }

        internal static string ValidateXGIToolsApply(out bool hasIssue)
        {
            hasIssue = false;
            var previousDebugMode = BurtShadingDebugSettings.Mode;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var failures = new List<string>();

                var runtimeInfoObject = new GameObject("Burt XGI Tools Runtime Info Validation");
                var runtimeInfoComponent = runtimeInfoObject.AddComponent<BurtXGIToolsDebugComponent>();
                runtimeInfoComponent.drawRuntimeInfo = true;
                ApplyVoxelDebug(runtimeInfoComponent);
                AddFailureIfFalse(failures, BurtShadingDebugSettings.Mode == BurtShadingDebugMode.GIProbeRuntimeInfo, "RuntimeInfoShadingDebugMode");
                BurtShadingDebugSettings.Mode = previousDebugMode;

                var debugObject = new GameObject("Burt XGI Tools Apply Validation");
                debugObject.transform.position = new Vector3(12f, 3f, -4f);
                var debugComponent = debugObject.AddComponent<BurtXGIToolsDebugComponent>();
                debugComponent.drawVoxel = true;
                debugComponent.drawVoxelByTrace = true;
                debugComponent.drawVoxelDebugIndirectProbe = true;
                debugComponent.drawVoxelDebugCullingDistance = 123f;
                debugComponent.drawVoxelProbeSizeWS = 0.75f;
                debugComponent.followCamera = false;
                debugComponent.followCameraOffset = 7.5f;
                debugComponent.clipmapCount = 8;
                debugComponent.voxelSize = BurtXGIToolsVoxelSize._128;
                debugComponent.voxelSizeWS = 0.5f;
                debugComponent.voxelAlwaysUpdate = true;
                debugComponent.voxelDebugMipLevel = 2;
                debugComponent.drawVoxelsDebugLayer = BurtXGIToolsVoxelDebugLayer.Lighting_Indirect;
                debugComponent.materialBudget = SceneVoxelMaterialMemoryBudget.High;
                debugComponent.materialGenMethod = SceneVoxelMaterialGenerateMethod.Atomic;
                debugComponent.lightingType = SceneVoxelLightingType.Indirect;
                debugComponent.voxelDrawVegetation = false;
                debugComponent.voxelDrawGrass = true;
                debugComponent.voxelLightingDirectionalShadow = false;
                debugComponent.voxelLightingPunctualLightShadow = false;
                debugComponent.voxelLightingSkyLight = false;
                debugComponent.clipmapOffset = new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f };
                debugComponent.clipmapUpdateDistance = new[] { 11f, 12f, 13f, 14f, 15f, 16f, 17f, 18f };

                ApplyVoxelDebug(debugComponent);
                var xgiComponent = FindSceneXGILightComponent();
                if (xgiComponent == null)
                {
                    failures.Add("NoBurtXGILightComponent");
                }
                else
                {
                    AddFailureIfFalse(failures, xgiComponent.overrideConfig, "OverrideConfig");
                    AddFailureIfFalse(failures, xgiComponent.sceneVoxelDebugExpandView, "SceneVoxelDebugExpandView");
                    AddFailureIfFalse(failures, xgiComponent.sceneVoxelDebugByTrace, "SceneVoxelDebugByTrace");
                    AddFailureIfFalse(failures, xgiComponent.sceneVoxelDebugDrawProbe, "SceneVoxelDebugDrawProbe");
                    AddFailureIfNotEqual(failures, 123f, xgiComponent.sceneVoxelDebugExpandViewDistance, "SceneVoxelDebugExpandViewDistance");
                    AddFailureIfNotEqual(failures, 0.75f, xgiComponent.sceneVoxelDebugProbeSizeWS, "SceneVoxelDebugProbeSizeWS");
                    AddFailureIfNotEqual(failures, 2, xgiComponent.sceneVoxelDebugShowMipmapID, "SceneVoxelDebugShowMipmapID");
                    AddFailureIfFalse(failures, xgiComponent.sceneVoxelDebugLayer == BurtXGIToolsVoxelDebugLayer.Lighting_Indirect, "SceneVoxelDebugLayer");
                    AddFailureIfFalse(failures, xgiComponent.sceneVoxelAlwaysUpdate, "SceneVoxelAlwaysUpdate");
                    AddFailureIfFalse(failures, !xgiComponent.sceneVoxelFollowCamera, "SceneVoxelFollowCamera");
                    AddFailureIfNotEqual(failures, 7.5f, xgiComponent.sceneVoxelCameraForward, "SceneVoxelCameraForward");
                    AddFailureIfNotEqual(failures, debugComponent.Position, xgiComponent.sceneVoxelOrigin, "SceneVoxelOrigin");
                    AddFailureIfNotEqual(failures, 6, xgiComponent.sceneVoxelClipMapCount, "SceneVoxelClipMapCount");
                    AddFailureIfNotEqual(failures, 64, xgiComponent.sceneVoxelClipMapResolution, "SceneVoxelClipMapResolution");
                    AddFailureIfNotEqual(failures, 32f, xgiComponent.sceneVoxelClipMapFirstWorldExtent, "SceneVoxelClipMapFirstWorldExtent");
                    AddFailureIfFalse(failures, xgiComponent.sceneVoxelMaterialBudget == SceneVoxelMaterialMemoryBudget.High, "SceneVoxelMaterialBudget");
                    AddFailureIfFalse(failures, xgiComponent.sceneVoxelLightingType == SceneVoxelLightingType.Indirect, "SceneVoxelLightingType");
                    AddFailureIfFalse(failures, !xgiComponent.sceneVoxelDrawVegetation, "SceneVoxelDrawVegetation");
                    AddFailureIfFalse(failures, xgiComponent.sceneVoxelDrawGrass, "SceneVoxelDrawGrass");
                    AddFailureIfFalse(failures, !xgiComponent.sceneVoxelLightingDirectionalShadow, "SceneVoxelLightingDirectionalShadow");
                    AddFailureIfFalse(failures, !xgiComponent.sceneVoxelLightingPunctualShadow, "SceneVoxelLightingPunctualShadow");
                    AddFailureIfFalse(failures, !xgiComponent.sceneVoxelLightingSkyLight, "SceneVoxelLightingSkyLight");
                    AddFailureIfNotEqual(failures, new Vector4(1f, 2f, 3f, 4f), xgiComponent.sceneVoxelClipMapOffset03, "SceneVoxelClipMapOffset03");
                    AddFailureIfNotEqual(failures, new Vector4(11f, 12f, 13f, 14f), xgiComponent.sceneVoxelClipMapUpdateDistance03, "SceneVoxelClipMapUpdateDistance03");
                    AddFailureIfNotEqual(failures, new Vector4(5f, 6f, 7f, 8f), xgiComponent.sceneVoxelClipMapOffset47, "SceneVoxelClipMapOffset47");
                    AddFailureIfNotEqual(failures, new Vector4(15f, 16f, 17f, 18f), xgiComponent.sceneVoxelClipMapUpdateDistance47, "SceneVoxelClipMapUpdateDistance47");
                }

                AddFailureIfFalse(failures, BurtShadingDebugSettings.Mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationSceneVoxelOccupancy, "ShadingDebugMode");
                hasIssue = failures.Count > 0;
                return "Burt XGI tools apply validation completed.\n" +
                    "Failures=" + (failures.Count > 0 ? string.Join("|", failures) : "<none>") + "\n" +
                    "XGILight=" + DescribeXGILightForToolsValidation(xgiComponent);
            }
            finally
            {
                BurtShadingDebugSettings.Mode = previousDebugMode;
            }
        }

        private static void AddFailureIfFalse(List<string> failures, bool condition, string label)
        {
            if (!condition)
            {
                failures.Add(label);
            }
        }

        private static void AddFailureIfNotEqual(List<string> failures, int expected, int actual, string label)
        {
            if (expected != actual)
            {
                failures.Add(label + "(" + actual + "!=" + expected + ")");
            }
        }

        private static void AddFailureIfNotEqual(List<string> failures, float expected, float actual, string label)
        {
            if (Mathf.Abs(expected - actual) > 0.0001f)
            {
                failures.Add(label + "(" + actual.ToString("0.###") + "!=" + expected.ToString("0.###") + ")");
            }
        }

        private static void AddFailureIfNotEqual(List<string> failures, Vector3 expected, Vector3 actual, string label)
        {
            if ((expected - actual).sqrMagnitude > 0.000001f)
            {
                failures.Add(label + "(" + actual + "!=" + expected + ")");
            }
        }

        private static void AddFailureIfNotEqual(List<string> failures, Vector4 expected, Vector4 actual, string label)
        {
            if ((expected - actual).sqrMagnitude > 0.000001f)
            {
                failures.Add(label + "(" + actual + "!=" + expected + ")");
            }
        }

        private static string DescribeXGILightForToolsValidation(BurtXGILightComponent component)
        {
            if (component == null)
            {
                return "<none>";
            }

            return "Override=" + component.overrideConfig +
                ",Follow=" + component.sceneVoxelFollowCamera +
                ",Origin=" + component.sceneVoxelOrigin +
                ",Clipmaps=" + component.sceneVoxelClipMapCount +
                ",Resolution=" + component.sceneVoxelClipMapResolution +
                ",Extent=" + component.sceneVoxelClipMapFirstWorldExtent.ToString("0.###") +
                ",DebugLayer=" + component.sceneVoxelDebugLayer;
        }

        private static Vector4 ResolveClipmapVector(float[] values, int offset = 0)
        {
            return new Vector4(
                ResolveArrayValue(values, offset + 0),
                ResolveArrayValue(values, offset + 1),
                ResolveArrayValue(values, offset + 2),
                ResolveArrayValue(values, offset + 3));
        }

        private static float ResolveArrayValue(float[] values, int index)
        {
            return values != null && (uint)index < (uint)values.Length ? values[index] : 0f;
        }

        private static int ResolveToolsVoxelResolution(BurtXGIToolsVoxelSize voxelSize)
        {
            return Mathf.Clamp((int)voxelSize, 16, 64);
        }

        private static float ResolveToolsVoxelFirstWorldExtent(BurtXGIToolsVoxelSize voxelSize, float voxelSizeWS)
        {
            var normalizedVoxelSize = Mathf.Clamp((int)voxelSize, 4, 512);
            var normalizedVoxelSizeWS = Mathf.Max(voxelSizeWS, 0.3f);
            return Mathf.Clamp(normalizedVoxelSize * normalizedVoxelSizeWS * 0.5f, 1f, 1000f);
        }

        private static BurtXGILightComponent FindSceneXGILightComponent()
        {
            var components = UnityEngine.Object.FindObjectsOfType<BurtXGILightComponent>(true);
            BurtXGILightComponent best = null;
            for (var index = 0; index < components.Length; index++)
            {
                var component = components[index];
                if (component == null || !component.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (best == null || component.priority > best.priority)
                {
                    best = component;
                }
            }

            return best;
        }

        private static void CreateSceneXGILightComponent()
        {
            var gameObject = new GameObject("Burt XGI Light Component");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Burt XGI Light Component");
            gameObject.AddComponent<BurtXGILightComponent>();
            Selection.activeObject = gameObject;
        }

        private static void DrawClipmapArray(string label, float[] values, float min, float max)
        {
            if (values == null)
            {
                return;
            }

            EditorGUILayout.LabelField(label);
            EditorGUI.indentLevel++;
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = EditorGUILayout.Slider("[" + index + "]", values[index], min, max);
            }
            EditorGUI.indentLevel--;
        }

        private void RefreshConfigs()
        {
            configs.Clear();
            var guids = AssetDatabase.FindAssets("t:BurtXGIProbeBakingConfig");
            for (var index = 0; index < guids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[index]);
                var config = AssetDatabase.LoadAssetAtPath<BurtXGIProbeBakingConfig>(path);
                if (config != null)
                {
                    configs.Add(config);
                }
            }

            if (activeConfig == null)
            {
                SetActiveConfig(Selection.activeObject as BurtXGIProbeBakingConfig);
                if (activeConfig == null)
                {
                    PickFirstConfigForPlatform();
                }
            }

            Repaint();
        }

        private void PickFirstConfigForPlatform()
        {
            for (var index = 0; index < configs.Count; index++)
            {
                if (configs[index] != null && configs[index].platform == platform)
                {
                    SetActiveConfig(configs[index]);
                    return;
                }
            }

            SetActiveConfig(configs.Count > 0 ? configs[0] : null);
        }

        private void SetActiveConfig(BurtXGIProbeBakingConfig config)
        {
            if (activeConfig == config)
            {
                return;
            }

            activeConfig = config;
            if (activeConfig != null)
            {
                platform = activeConfig.platform;
            }

            validationReport = string.Empty;
            DestroyCachedEditor();
        }

        private void ValidateScene()
        {
            var validation = BurtXGIProbeBakeAPI.ValidateScene(activeConfig);
            validationReport = validation.report;
            Repaint();
        }

        private void StartBake(bool useConfigAllSlices)
        {
            progress = 0f;
            progressReport = string.Empty;
            resultReport = string.Empty;
            if (useConfigAllSlices)
            {
                BurtXGIProbeBakeAPI.BakeAllTimeSlicesAsync(activeConfig, OnBakeProgress, OnBakeCompleted);
            }
            else
            {
                BurtXGIProbeBakeAPI.BakeAsync(activeConfig, OnBakeProgress, OnBakeCompleted);
            }

            ValidateScene();
        }

        private void OnBakeProgress(BurtXGIProbeBakeAPI.BakeProgress bakeProgress)
        {
            progress = bakeProgress.progress;
            progressReport = bakeProgress.stepName + Environment.NewLine + bakeProgress.description;
            Repaint();
        }

        private void OnBakeCompleted(BurtXGIProbeBakeAPI.BakeResult result)
        {
            resultReport = "Result=" + result.status +
                " Elapsed=" + result.elapsedSeconds.ToString("0.###") + "s" +
                (string.IsNullOrEmpty(result.error) ? string.Empty : Environment.NewLine + result.error);
            RefreshConfigs();
            Repaint();
        }

        private void CreateConfigAsset()
        {
            var scene = SceneManager.GetActiveScene();
            var config = CreateOrFindConfigForScene(scene, platform);
            RefreshConfigs();
            SetActiveConfig(config);
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        private void CreateSceneConfigAssets()
        {
            var scene = SceneManager.GetActiveScene();
            var pcConfig = CreateOrFindConfigForScene(scene, BurtXGIProbeBakingPlatform.PC);
            var mobileConfig = CreateOrFindConfigForScene(scene, BurtXGIProbeBakingPlatform.Mobile);
            RefreshConfigs();
            SetActiveConfig(platform == BurtXGIProbeBakingPlatform.Mobile ? mobileConfig : pcConfig);
            Selection.activeObject = activeConfig;
            EditorGUIUtility.PingObject(activeConfig);
        }

        private static BurtXGIProbeBakingConfig CreateOrFindConfigForScene(Scene scene, BurtXGIProbeBakingPlatform targetPlatform)
        {
            if (BurtXGIProbeBakingConfig.TryGetBakingConfigForScene(scene, targetPlatform, out var existingConfig))
            {
                return existingConfig;
            }

            var directory = ResolveSceneConfigDirectory(scene);
            EnsureAssetFolder(directory);
            var config = CreateInstance<BurtXGIProbeBakingConfig>();
            config.platform = targetPlatform;
            config.CaptureSceneMetadata(scene);
            var assetName = BurtXGIProbeBakingConfig.GetBakingConfigName(ResolveSceneName(scene), targetPlatform);
            var path = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + assetName + ".asset");
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static string ResolveSceneConfigDirectory(Scene scene)
        {
            return BurtXGIProbeBakingConfig.GetBakingConfigDirectory(ResolveSceneName(scene));
        }

        private static string ResolveSceneName(Scene scene)
        {
            return BurtXGIProbeBakingConfig.ResolveSceneName(scene);
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parts = folder.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static int CountTimeSliceAssets(BurtXGIProbeBakingConfig config)
        {
            if (config == null || config.timeSliceBakedDataAssets == null)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < config.timeSliceBakedDataAssets.Count; index++)
            {
                if (config.timeSliceBakedDataAssets[index]?.asset != null)
                {
                    count++;
                }
            }

            return count;
        }

        private void DestroyCachedEditor()
        {
            if (activeConfigEditor == null)
            {
                return;
            }

            DestroyImmediate(activeConfigEditor);
            activeConfigEditor = null;
        }
    }
}
