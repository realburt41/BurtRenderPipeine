using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Validation
{
    internal static class CodexTAAValidationBuilder
    {
        private const string OutputArgument = "-burtTaaValidationOutput";
        private const string RenderScaleArgument = "-burtTaaValidationRenderScale";
        private const string DebugModeArgument = "-burtTaaValidationDebugMode";
        private const string MoveObjectArgument = "-burtTaaValidationMoveObject";
        private const string FastMotionArgument = "-burtTaaValidationFastMotion";
        private const string RigidObjectArgument = "-burtTaaValidationRigidObject";
        private const string DisableGIArgument = "-burtTaaValidationDisableGI";
        private const string DisableSSRArgument = "-burtTaaValidationDisableSSR";
        private const string DisableDepthOfFieldArgument = "-burtTaaValidationDisableDepthOfField";
        private const string DisableGITemporalArgument = "-burtTaaValidationDisableGITemporal";
        private const string DisableGIScreenProbeTemporalArgument = "-burtTaaValidationDisableGIScreenProbeTemporal";
        private const string DisableTAAArgument = "-burtTaaValidationDisableTAA";
        private const string RenderDocArgument = "-burtTaaValidationRenderDoc";
        private static bool renderDocCaptureCompleted;

        public static void RunInEditor()
        {
            EditorSceneManager.OpenScene("Assets/Art/Scene/BurtScene.unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        public static void RunSynchronous()
        {
            EditorSceneManager.OpenScene("Assets/Art/Scene/BurtScene.unity", OpenSceneMode.Single);
            var camera = Camera.main;
            if (camera == null)
            {
                var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                for (var index = 0; index < cameras.Length; ++index)
                {
                    if (cameras[index] != null && cameras[index].enabled && cameras[index].cameraType == CameraType.Game)
                    {
                        camera = cameras[index];
                        break;
                    }
                }
            }

            if (camera == null)
            {
                throw new InvalidOperationException("No enabled Game camera found for TAA validation.");
            }

            var outputDirectory = ResolveOutputDirectory();
            Directory.CreateDirectory(outputDirectory);
            var disabledVolumeComponents = new List<VolumeComponentState>();
            var overriddenBoolParameters = new List<BoolParameterState>();
            if (ResolveOptionalIntArgument(DisableTAAArgument, 0) != 0)
            {
                DisableVolumeComponents<TemporalAAVolumeComponent>(disabledVolumeComponents);
            }
            if (ResolveOptionalIntArgument(DisableGIArgument, 0) != 0)
            {
                DisableVolumeComponents<ScreenSpaceGlobalIlluminationVolumeComponent>(disabledVolumeComponents);
            }

            if (ResolveOptionalIntArgument(DisableSSRArgument, 0) != 0)
            {
                DisableVolumeComponents<ScreenSpaceReflectionVolumeComponent>(disabledVolumeComponents);
            }

            if (ResolveOptionalIntArgument(DisableDepthOfFieldArgument, 0) != 0)
            {
                DisableVolumeComponents<DiaphragmDepthOfFieldVolumeComponent>(disabledVolumeComponents);
            }

            if (ResolveOptionalIntArgument(DisableGITemporalArgument, 0) != 0)
            {
                OverrideGITemporalParameters(overriddenBoolParameters, false);
            }

            if (ResolveOptionalIntArgument(DisableGIScreenProbeTemporalArgument, 0) != 0)
            {
                OverrideGITemporalParameters(overriddenBoolParameters, true);
            }

            var cameraData = camera.GetComponent<BurtCameraData>();
            var requestedRenderScale = ResolveOptionalFloatArgument(RenderScaleArgument, cameraData != null ? cameraData.RenderScale : 1.0f);
            var originalDebugMode = BurtShadingDebugSettings.Mode;
            var requestedDebugMode = ResolveOptionalIntArgument(DebugModeArgument, (int)originalDebugMode);
            BurtShadingDebugSettings.Mode = (BurtShadingDebugMode)requestedDebugMode;
            SerializedObject cameraDataObject = null;
            SerializedProperty renderScaleProperty = null;
            SerializedProperty antialiasingModeProperty = null;
            var originalRenderScale = cameraData != null ? cameraData.RenderScale : 1.0f;
            var originalAntialiasingMode = cameraData != null
                ? cameraData.AntialiasingMode
                : BurtCameraAntialiasingMode.None;
            if (cameraData != null && !Mathf.Approximately(requestedRenderScale, originalRenderScale))
            {
                cameraDataObject = new SerializedObject(cameraData);
                renderScaleProperty = cameraDataObject.FindProperty("renderScale");
                renderScaleProperty.floatValue = Mathf.Clamp(requestedRenderScale, 0.5f, 1.0f);
                cameraDataObject.ApplyModifiedPropertiesWithoutUndo();
            }

            if (cameraData != null && ResolveOptionalIntArgument(DisableTAAArgument, 0) != 0)
            {
                cameraDataObject ??= new SerializedObject(cameraData);
                cameraDataObject.Update();
                antialiasingModeProperty = cameraDataObject.FindProperty("antialiasingMode");
                antialiasingModeProperty.enumValueIndex = (int)BurtCameraAntialiasingMode.None;
                cameraDataObject.ApplyModifiedPropertiesWithoutUndo();
            }

            Debug.Log(
                $"[CodexTAAValidation] Synchronous Camera={camera.name} " +
                $"AA={(cameraData != null ? cameraData.AntialiasingMode.ToString() : "NoBurtCameraData")} " +
                $"RenderScale={(cameraData != null ? cameraData.RenderScale.ToString("0.###") : "n/a")} " +
                $"Debug={BurtShadingDebugSettings.Mode} Output={outputDirectory}");
            LogMotionVectorMaterialCoverage();
            LogVisibleOpaqueRendererCoverage(camera);

            var moveObject = ResolveOptionalIntArgument(MoveObjectArgument, 0) != 0;
            var fastMotion = ResolveOptionalIntArgument(FastMotionArgument, 0) != 0;
            var rigidObject = ResolveOptionalIntArgument(RigidObjectArgument, 0) != 0;
            GameObject validationRigidObject = null;
            Material validationRigidMaterial = null;
            Renderer movingRenderer = null;
            if (moveObject && rigidObject)
            {
                validationRigidObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                validationRigidObject.name = "Codex TAA Validation Rigid Sphere";
                validationRigidObject.hideFlags = HideFlags.HideAndDontSave;
                validationRigidObject.transform.position =
                    camera.transform.position + camera.transform.forward * 5.0f - camera.transform.right * 0.4f;
                validationRigidObject.transform.localScale = Vector3.one * 0.8f;
                movingRenderer = validationRigidObject.GetComponent<Renderer>();
                var litShader = Shader.Find("BurtRP/Lit");
                if (litShader == null)
                {
                    throw new InvalidOperationException("BurtRP/Lit shader was not found for rigid-object validation.");
                }

                validationRigidMaterial = new Material(litShader)
                {
                    name = "Codex TAA Validation Rigid Lit",
                    hideFlags = HideFlags.HideAndDontSave
                };
                movingRenderer.sharedMaterial = validationRigidMaterial;
                movingRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            }
            else if (moveObject)
            {
                movingRenderer = FindValidationMovingRenderer(camera, false);
            }
            var movingSkinnedRenderer = movingRenderer as SkinnedMeshRenderer;
            var movingTransform = movingSkinnedRenderer != null && movingSkinnedRenderer.rootBone != null
                ? movingSkinnedRenderer.rootBone
                : movingRenderer != null ? movingRenderer.transform : null;
            var originalMovingPosition = movingTransform != null ? movingTransform.position : Vector3.zero;
            if (moveObject)
            {
                if (movingRenderer == null)
                {
                    throw new InvalidOperationException("No visible opaque MeshRenderer found for object-motion validation.");
                }

                Debug.Log($"[CodexTAAValidation] MovingRenderer={movingRenderer.name} MovingTransform={movingTransform.name} Material={movingRenderer.sharedMaterial?.name} RigidOnly={rigidObject} " +
                    $"MotionMode={movingRenderer.motionVectorGenerationMode} SkinnedMotion={(movingSkinnedRenderer != null ? movingSkinnedRenderer.skinnedMotionVectors.ToString() : "n/a")} Bounds={movingRenderer.bounds.size}");
            }

            var originalTarget = camera.targetTexture;
            var originalPosition = camera.transform.position;
            var target = new RenderTexture(640, 480, 24, RenderTextureFormat.ARGB32)
            {
                name = "Codex TAA Validation Target",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            target.Create();
            camera.targetTexture = target;

            try
            {
                RenderFrames(camera, 96);
                LogRuntimeTarget("_BurtCameraColorTexture");
                Debug.Log($"[CodexTAAValidation] Camera target={target.width}x{target.height} pixel={camera.pixelWidth}x{camera.pixelHeight} scaledPixel={camera.scaledPixelWidth}x{camera.scaledPixelHeight}");
                CaptureGlobalRenderTexture("_BurtCameraColorTexture", outputDirectory, "taa_source.png");
                CaptureGlobalRenderTexture("_BurtPostProcessColorTexture", outputDirectory, "post_process_color.png");
                CaptureGlobalRenderTexture("_BurtGBuffer0", outputDirectory, "gbuffer0.png");
                CaptureGlobalRenderTexture("_BurtGBuffer1", outputDirectory, "gbuffer1.png");
                CaptureGlobalRenderTexture("_BurtGBuffer2", outputDirectory, "gbuffer2.png");
                CaptureTemporalAAHistories(camera, outputDirectory);
                Capture(camera, target, outputDirectory, "baseline.png");

                var displacement = camera.transform.right * (movingRenderer != null
                    ? Mathf.Clamp(movingRenderer.bounds.extents.magnitude * (fastMotion ? 1.5f : 0.35f), fastMotion ? 0.75f : 0.10f, fastMotion ? 2.0f : 0.35f)
                    : (fastMotion ? 1.5f : 0.20f));
                for (var frame = 1; frame <= 12; ++frame)
                {
                    var t = fastMotion ? 1.0f : frame / 12.0f;
                    if (!fastMotion)
                    {
                        t = t * t * (3.0f - 2.0f * t);
                    }
                    if (movingTransform != null)
                    {
                        movingTransform.position = originalMovingPosition + displacement * t;
                    }
                    else
                    {
                        camera.transform.position = originalPosition + displacement * t;
                    }
                    camera.Render();
                    if (frame == 1 || frame == 6 || frame == 12)
                    {
                        CaptureCurrentTarget(target, outputDirectory, $"moving_{frame:00}.png");
                        CaptureGlobalRenderTexture("_BurtCameraColorTexture", outputDirectory, $"moving_{frame:00}_source.png");
                        CaptureGlobalRenderTexture("_BurtScreenSpaceGlobalIlluminationTexture", outputDirectory, $"moving_{frame:00}_gi_diffuse.png");
                        CaptureGlobalRenderTexture("_BurtGIBackfaceDiffuseIndirectTexture", outputDirectory, $"moving_{frame:00}_gi_backface.png");
                        CaptureGlobalRenderTexture("_BurtGIRoughSpecularIndirectTexture", outputDirectory, $"moving_{frame:00}_gi_rough_specular.png");
                    }
                }

                CaptureCurrentTarget(target, outputDirectory, "motion_end.png");
                var checkpoints = new[] { 0, 1, 2, 4, 8, 16, 32, 64 };
                var elapsed = 0;
                for (var index = 0; index < checkpoints.Length; ++index)
                {
                    var checkpoint = checkpoints[index];
                    RenderFrames(camera, checkpoint - elapsed);
                    elapsed = checkpoint;
                    Capture(camera, target, outputDirectory, $"settle_{checkpoint:00}.png");
                }

                for (var index = 0; index < 8; ++index)
                {
                    Capture(camera, target, outputDirectory, $"static_{index:00}.png");
                }

                File.WriteAllText(Path.Combine(outputDirectory, "complete.txt"), "ok\n");
                Debug.Log("[CodexTAAValidation] Synchronous capture complete.");
            }
            finally
            {
                camera.transform.position = originalPosition;
                if (movingTransform != null)
                {
                    movingTransform.position = originalMovingPosition;
                }
                camera.targetTexture = originalTarget;
                BurtShadingDebugSettings.Mode = originalDebugMode;
                if (renderScaleProperty != null)
                {
                    cameraDataObject.Update();
                    renderScaleProperty.floatValue = originalRenderScale;
                    cameraDataObject.ApplyModifiedPropertiesWithoutUndo();
                }
                if (antialiasingModeProperty != null)
                {
                    cameraDataObject.Update();
                    antialiasingModeProperty.enumValueIndex = (int)originalAntialiasingMode;
                    cameraDataObject.ApplyModifiedPropertiesWithoutUndo();
                }
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                if (validationRigidObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(validationRigidObject);
                }

                if (validationRigidMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(validationRigidMaterial);
                }

                for (var index = 0; index < disabledVolumeComponents.Count; ++index)
                {
                    var state = disabledVolumeComponents[index];
                    if (state.Component != null)
                    {
                        state.Component.active = state.Active;
                    }
                }

                for (var index = 0; index < overriddenBoolParameters.Count; ++index)
                {
                    var state = overriddenBoolParameters[index];
                    if (state.Parameter != null)
                    {
                        state.Parameter.value = state.Value;
                        state.Parameter.overrideState = state.OverrideState;
                    }
                }
            }
        }

        private readonly struct BoolParameterState
        {
            public BoolParameter Parameter { get; }
            public bool Value { get; }
            public bool OverrideState { get; }

            public BoolParameterState(BoolParameter parameter)
            {
                Parameter = parameter;
                Value = parameter != null && parameter.value;
                OverrideState = parameter != null && parameter.overrideState;
            }
        }

        private readonly struct VolumeComponentState
        {
            public VolumeComponent Component { get; }
            public bool Active { get; }

            public VolumeComponentState(VolumeComponent component, bool active)
            {
                Component = component;
                Active = active;
            }
        }

        private static void DisableVolumeComponents<T>(List<VolumeComponentState> states)
            where T : VolumeComponent
        {
            var volumes = UnityEngine.Object.FindObjectsOfType<Volume>();
            var seen = new HashSet<VolumeComponent>();
            for (var index = 0; index < volumes.Length; ++index)
            {
                var volume = volumes[index];
                var profile = volume != null ? volume.profile : null;
                if (profile == null || !profile.TryGet<T>(out var component) || component == null || !seen.Add(component))
                {
                    continue;
                }

                states.Add(new VolumeComponentState(component, component.active));
                component.active = false;
                Debug.Log($"[CodexTAAValidation] Disabled volume component {typeof(T).Name} on {volume.name}.");
            }
        }

        private static void OverrideGITemporalParameters(List<BoolParameterState> states, bool screenProbeTemporal)
        {
            var volumes = UnityEngine.Object.FindObjectsOfType<Volume>();
            var seen = new HashSet<BoolParameter>();
            for (var index = 0; index < volumes.Length; ++index)
            {
                var volume = volumes[index];
                var profile = volume != null ? volume.profile : null;
                if (profile == null || !profile.TryGet<ScreenSpaceGlobalIlluminationVolumeComponent>(out var component) || component == null)
                {
                    continue;
                }

                if (screenProbeTemporal)
                {
                    OverrideBool(component.screenProbeTemporalFilter, false, states, seen);
                    OverrideBool(component.screenProbeTemporalReprojection, false, states, seen);
                }
                else
                {
                    OverrideBool(component.temporalAccumulation, false, states, seen);
                }

                Debug.Log($"[CodexTAAValidation] Disabled GI {(screenProbeTemporal ? "screen-probe" : "main")} temporal accumulation on {volume.name}.");
            }
        }

        private static void OverrideBool(
            BoolParameter parameter,
            bool value,
            List<BoolParameterState> states,
            HashSet<BoolParameter> seen)
        {
            if (parameter == null || !seen.Add(parameter))
            {
                return;
            }

            states.Add(new BoolParameterState(parameter));
            parameter.overrideState = true;
            parameter.value = value;
        }

        private static void LogRuntimeTarget(string propertyName)
        {
            var texture = Shader.GetGlobalTexture(propertyName);
            var renderTexture = texture as RenderTexture;
            Debug.Log(
                $"[CodexTAAValidation] Global {propertyName}=" +
                $"{(texture != null ? texture.name : "null")} " +
                $"size={(texture != null ? texture.width : 0)}x{(texture != null ? texture.height : 0)} " +
                $"created={(renderTexture != null && renderTexture.IsCreated())} " +
                $"dynamic={(renderTexture != null && renderTexture.useDynamicScale)}");
        }

        private static void CaptureGlobalRenderTexture(string propertyName, string outputDirectory, string fileName)
        {
            var source = Shader.GetGlobalTexture(propertyName) as RenderTexture;
            CaptureRenderTexture(source, outputDirectory, fileName);
        }

        private static void CaptureTemporalAAHistories(Camera camera, string outputDirectory)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;
            var utilityType = typeof(BurtCameraData).Assembly.GetType("Burt.RenderPipeline.BurtTemporalAAUtility");
            var ensureMethod = utilityType?.GetMethod("EnsureHistoryTextures", flags);
            if (ensureMethod == null)
            {
                return;
            }

            var arguments = new object[] { camera, false };
            var histories = ensureMethod.Invoke(null, arguments);
            if (histories == null)
            {
                return;
            }

            var instanceFlags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;
            var historyType = histories.GetType();
            var previousColor = historyType.GetProperty("PreviousColor", instanceFlags)?.GetValue(histories) as RenderTexture;
            var currentColor = historyType.GetProperty("CurrentColor", instanceFlags)?.GetValue(histories) as RenderTexture;
            Debug.Log(
                $"[CodexTAAValidation] TAA history previous=" +
                $"{(previousColor != null ? previousColor.width : 0)}x{(previousColor != null ? previousColor.height : 0)} " +
                $"current={(currentColor != null ? currentColor.width : 0)}x{(currentColor != null ? currentColor.height : 0)} " +
                $"previousDescriptor={(previousColor != null ? previousColor.descriptor.width : 0)}x{(previousColor != null ? previousColor.descriptor.height : 0)} " +
                $"currentDescriptor={(currentColor != null ? currentColor.descriptor.width : 0)}x{(currentColor != null ? currentColor.descriptor.height : 0)} " +
                $"previousFormat={(previousColor != null ? previousColor.graphicsFormat.ToString() : "None")} " +
                $"currentFormat={(currentColor != null ? currentColor.graphicsFormat.ToString() : "None")} " +
                $"previousDynamic={(previousColor != null && previousColor.useDynamicScale)} " +
                $"currentDynamic={(currentColor != null && currentColor.useDynamicScale)}");
            CaptureRenderTexture(previousColor, outputDirectory, "taa_history_previous.png");
            CaptureRenderTexture(currentColor, outputDirectory, "taa_history_current.png");
        }

        private static void CaptureRenderTexture(RenderTexture source, string outputDirectory, string fileName)
        {
            if (source == null || !source.IsCreated())
            {
                return;
            }

            var previous = RenderTexture.active;
            RenderTexture.active = source;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false, true);
            try
            {
                image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                image.Apply(false, false);
                File.WriteAllBytes(Path.Combine(outputDirectory, fileName), image.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.active = previous;
            }
        }

        private static void RenderFrames(Camera camera, int count)
        {
            for (var frame = 0; frame < count; ++frame)
            {
                camera.Render();
            }
        }

        private static void Capture(Camera camera, RenderTexture target, string outputDirectory, string fileName)
        {
            if (!renderDocCaptureCompleted && ResolveOptionalIntArgument(RenderDocArgument, 0) != 0)
            {
                renderDocCaptureCompleted = true;
                if (!UnityEditorInternal.RenderDoc.IsLoaded())
                {
                    UnityEditorInternal.RenderDoc.Load();
                }

                var captureWindow = EditorWindow.focusedWindow;
                var createdCaptureWindow = false;
                if (captureWindow == null)
                {
                    captureWindow = ScriptableObject.CreateInstance<SceneView>();
                    captureWindow.Show();
                    createdCaptureWindow = true;
                }

                try
                {
                    Debug.Log($"[CodexTAAValidation] RenderDoc loaded={UnityEditorInternal.RenderDoc.IsLoaded()} window={captureWindow.GetType().Name}");
                    UnityEditorInternal.RenderDoc.BeginCaptureRenderDoc(captureWindow);
                    camera.Render();
                    UnityEditorInternal.RenderDoc.EndCaptureRenderDoc(captureWindow);
                    CaptureCurrentTarget(target, outputDirectory, fileName);
                    return;
                }
                finally
                {
                    if (createdCaptureWindow)
                    {
                        captureWindow.Close();
                    }
                }
            }

            camera.Render();
            CaptureCurrentTarget(target, outputDirectory, fileName);
        }

        private static void CaptureCurrentTarget(RenderTexture target, string outputDirectory, string fileName)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false, true);
            try
            {
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                image.Apply(false, false);
                File.WriteAllBytes(Path.Combine(outputDirectory, fileName), image.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.active = previous;
            }

            Debug.Log($"[CodexTAAValidation] Captured {fileName}");
        }

        private static string ResolveOutputDirectory()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; ++index)
            {
                if (arguments[index] == OutputArgument)
                {
                    return Path.GetFullPath(arguments[index + 1]);
                }
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "CodexTAAValidation", "Capture"));
        }

        private static float ResolveOptionalFloatArgument(string argumentName, float fallback)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; ++index)
            {
                if (arguments[index] == argumentName &&
                    float.TryParse(arguments[index + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    return value;
                }
            }

            return fallback;
        }

        private static int ResolveOptionalIntArgument(string argumentName, int fallback)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; ++index)
            {
                if (arguments[index] == argumentName && int.TryParse(arguments[index + 1], out var value))
                {
                    return value;
                }
            }

            return fallback;
        }

        private static void LogMotionVectorMaterialCoverage()
        {
            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || !seen.Add(material.GetInstanceID()))
                    {
                        continue;
                    }

                    var passes = new System.Text.StringBuilder();
                    for (var pass = 0; pass < material.passCount; ++pass)
                    {
                        if (pass > 0) passes.Append('|');
                        passes.Append(material.GetPassName(pass));
                    }

                    Debug.Log($"[CodexTAAValidation] Material={material.name} Shader={material.shader.name} Queue={material.renderQueue} Passes={passes}");
                }
            }
        }

        private static void LogVisibleOpaqueRendererCoverage(Camera camera)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    !GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                {
                    continue;
                }

                var material = renderer.sharedMaterial;
                if (material == null || material.renderQueue > 2500 || material.shader == null ||
                    !material.shader.name.StartsWith("BurtRP/", StringComparison.Ordinal))
                {
                    continue;
                }

                var bounds = renderer.bounds;
                var min = bounds.min;
                var max = bounds.max;
                var viewportMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var viewportMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                var anyInFront = false;
                for (var corner = 0; corner < 8; ++corner)
                {
                    var world = new Vector3(
                        (corner & 1) != 0 ? max.x : min.x,
                        (corner & 2) != 0 ? max.y : min.y,
                        (corner & 4) != 0 ? max.z : min.z);
                    var viewport = camera.WorldToViewportPoint(world);
                    if (viewport.z <= 0f)
                    {
                        continue;
                    }

                    anyInFront = true;
                    viewportMin = Vector2.Min(viewportMin, viewport);
                    viewportMax = Vector2.Max(viewportMax, viewport);
                }

                if (!anyInFront)
                {
                    continue;
                }

                var hasGBufferPass =
                    material.FindPass("Burt Multipass Fur GBuffer") >= 0 ||
                    material.FindPass("Burt Lit GBuffer") >= 0 ||
                    material.FindPass("Burt Fabric GBuffer") >= 0 ||
                    material.FindPass("Burt Hair GBuffer") >= 0 ||
                    material.FindPass("Burt Grass GBuffer") >= 0 ||
                    material.FindPass("Burt Foliage GBuffer") >= 0 ||
                    material.FindPass("Burt Trunk GBuffer") >= 0 ||
                    material.FindPass("Burt InteriorMapping GBuffer") >= 0;
                Debug.Log(
                    $"[CodexTAAValidation] Renderer={renderer.name} Material={material.name} " +
                    $"Shader={material.shader.name} Queue={material.renderQueue} " +
                    $"Viewport=({viewportMin.x:0.###},{viewportMin.y:0.###})-({viewportMax.x:0.###},{viewportMax.y:0.###}) " +
                    $"GBufferPass={hasGBufferPass}");
            }
        }

        private static Renderer FindValidationMovingRenderer(Camera camera, bool rigidOnly)
        {
            Renderer best = null;
            var bestScore = float.PositiveInfinity;
            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                {
                    continue;
                }

                if (rigidOnly && renderer is SkinnedMeshRenderer)
                {
                    continue;
                }

                var material = renderer.sharedMaterial;
                if (material == null || material.renderQueue > 2500 || material.shader == null ||
                    !material.shader.name.StartsWith("BurtRP/", StringComparison.Ordinal))
                {
                    continue;
                }

                var viewport = camera.WorldToViewportPoint(renderer.bounds.center);
                if (viewport.z <= 0f || viewport.x <= 0.05f || viewport.x >= 0.95f || viewport.y <= 0.05f || viewport.y >= 0.95f)
                {
                    continue;
                }

                var radius = renderer.bounds.extents.magnitude;
                if (radius <= 0.01f || radius > 8f)
                {
                    continue;
                }

                var centerDistance = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f).sqrMagnitude;
                var score = centerDistance + Mathf.Abs(radius - 0.75f) * 0.02f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = renderer;
                }
            }

            return best;
        }

        public static void Build()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            var buildDirectory = Path.Combine(projectRoot, "Temp", "CodexTAAValidation", "Player");
            Directory.CreateDirectory(buildDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Art/Scene/BurtScene.unity" },
                locationPathName = Path.Combine(buildDirectory, "BurtTAAValidation.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"TAA validation player build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            }

            UnityEngine.Debug.Log($"[CodexTAAValidation] Player built: {options.locationPathName}");
        }
    }
}
