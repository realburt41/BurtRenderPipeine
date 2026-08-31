#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    // Keeps RenderDoc diagnostics responsive to external shader edits during capture.
    /// <summary>
    /// Dormant editor bridge used by automated RenderDoc validation. A capture
    /// is requested by creating Library/BurtRenderDocCapture.request; no scene
    /// or project asset is modified by the capture cycle.
    /// </summary>
    [InitializeOnLoad]
    internal static class BurtRenderDocCaptureAutomation
    {
        private const string RequestRelativePath = "Library/BurtRenderDocCapture.request";
        private const string ResultRelativePath = "Library/BurtRenderDocCapture.result";

        private enum CaptureState
        {
            Idle,
            WaitingForLoad,
            WaitingToBegin,
            Capturing
        }

        private static CaptureState state;
        private static int waitFrames;
        private static double waitUntilTime;
        private static EditorWindow captureWindow;
        private static string activeRequestId;
        private static BurtShadingDebugMode modeBeforeCapture;
        private static BurtShadingDebugMode modeDuringCapture;
        private static bool restoreShadingDebugMode;
        private static bool useTemporaryLocalExposure;
        private static bool overrideIntegrateType;
        private static ScreenProbeIntegrateType integrateTypeDuringCapture;
        private static readonly List<BurtXGILightComponent> overriddenXGILights = new List<BurtXGILightComponent>();
        private static readonly List<ScreenProbeIntegrateType> overriddenXGIIntegrateTypes = new List<ScreenProbeIntegrateType>();
        private static GameObject temporaryIntegrateVolumeObject;
        private static VolumeProfile temporaryIntegrateVolumeProfile;
        private static GameObject temporaryVolumeObject;
        private static VolumeProfile temporaryVolumeProfile;

        private static string RequestPath => Path.GetFullPath(RequestRelativePath);
        private static string ResultPath => Path.GetFullPath(ResultRelativePath);
        private static int CaptureWarmupFrames => 4;
        private static double CaptureWarmupSeconds => useTemporaryLocalExposure ? 2.0 : 0.0;

        static BurtRenderDocCaptureAutomation()
        {
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                switch (state)
                {
                    case CaptureState.Idle:
                        if (File.Exists(RequestPath))
                            StartRequest();
                        break;
                    case CaptureState.WaitingForLoad:
                        if (RenderDoc.IsLoaded())
                        {
                            PrepareWaitingToBegin();
                        }
                        else if (--waitFrames <= 0)
                        {
                            Finish("ERROR=RenderDoc failed to load");
                        }
                        break;
                    case CaptureState.WaitingToBegin:
                        SceneView.RepaintAll();
                        EditorApplication.QueuePlayerLoopUpdate();
                        if (EditorApplication.timeSinceStartup < waitUntilTime)
                            break;
                        if (--waitFrames <= 0)
                            BeginCapture();
                        break;
                    case CaptureState.Capturing:
                        if (--waitFrames <= 0)
                            EndCapture();
                        break;
                }
            }
            catch (Exception exception)
            {
                Finish("ERROR=" + exception);
            }
        }

        private static void StartRequest()
        {
            var requestPayload = File.ReadAllText(RequestPath).Trim();
            File.Delete(RequestPath);
            File.Delete(ResultPath);

            if (!TryResolveCaptureRequest(
                    requestPayload,
                    out activeRequestId,
                    out modeDuringCapture,
                    out useTemporaryLocalExposure,
                    out overrideIntegrateType,
                    out integrateTypeDuringCapture,
                    out var modeError))
            {
                Finish("ERROR=" + modeError);
                return;
            }

            modeBeforeCapture = BurtShadingDebugSettings.Mode;
            restoreShadingDebugMode = true;
            BurtShadingDebugSettings.Mode = modeDuringCapture;
            if (overrideIntegrateType)
                ApplyTemporaryIntegrateType(integrateTypeDuringCapture);
            if (useTemporaryLocalExposure)
                CreateTemporaryLocalExposureVolume();
            SceneView.RepaintAll();

            if (!RenderDoc.IsInstalled())
            {
                Finish("ERROR=RenderDoc is not installed");
                return;
            }
            if (!RenderDoc.IsSupported())
            {
                Finish("ERROR=RenderDoc is not supported by the active graphics API");
                return;
            }

            if (!RenderDoc.IsLoaded())
            {
                RenderDoc.Load();
                waitFrames = 300;
                state = CaptureState.WaitingForLoad;
                return;
            }

            PrepareWaitingToBegin();
        }

        private static void BeginCapture()
        {
            captureWindow = SceneView.lastActiveSceneView != null
                ? (EditorWindow)SceneView.lastActiveSceneView
                : EditorWindow.focusedWindow;
            if (captureWindow == null)
            {
                Finish("ERROR=No editor window is available for RenderDoc capture");
                return;
            }

            RenderDoc.BeginCaptureRenderDoc(captureWindow);
            captureWindow.Repaint();
            SceneView.RepaintAll();
            waitFrames = 6;
            state = CaptureState.Capturing;
        }

        private static void EndCapture()
        {
            RenderDoc.EndCaptureRenderDoc(captureWindow);
            var exposureResult = string.Empty;
            var sceneView = captureWindow as SceneView;
            if (sceneView != null && sceneView.camera != null &&
                AutoExposureDebugUtility.TryGetSnapshot(sceneView.camera, out var exposureSnapshot))
            {
                exposureResult =
                    "\nEXPOSURE_GLOBAL=" + exposureSnapshot.GlobalExposureScale.ToString("R") +
                    "\nEXPOSURE_LOCAL_AVERAGE=" + exposureSnapshot.AverageLocalExposure.ToString("R") +
                    "\nPRE_EXPOSURE=" + exposureSnapshot.PreExposure.ToString("R");
            }

            Finish("OK\nWINDOW=" + captureWindow.GetType().FullName +
                   "\nMODE_BEFORE=" + modeBeforeCapture +
                   "\nMODE_CAPTURE=" + modeDuringCapture +
                   "\nINTEGRATE_CAPTURE=" + (overrideIntegrateType ? integrateTypeDuringCapture.ToString() : "Unchanged") +
                   "\nLOCAL_EXPOSURE=" + (useTemporaryLocalExposure ? "1" : "0") +
                   exposureResult +
                   "\nUTC=" + DateTime.UtcNow.ToString("O"));
        }

        private static void Finish(string result)
        {
            if (!string.IsNullOrEmpty(activeRequestId))
                result = "REQUEST_ID=" + activeRequestId + "\n" + result;

            if (restoreShadingDebugMode)
            {
                BurtShadingDebugSettings.Mode = modeBeforeCapture;
                result += "\nMODE_RESTORED=" + BurtShadingDebugSettings.Mode;
                restoreShadingDebugMode = false;
                SceneView.RepaintAll();
            }

            DestroyTemporaryLocalExposureVolume();
            RestoreTemporaryIntegrateType();

            File.WriteAllText(ResultPath, result);
            if (!string.IsNullOrEmpty(activeRequestId))
                File.WriteAllText(GetRequestResultPath(activeRequestId), result);
            Debug.Log("[BurtRP][RenderDocAutomation] " + result.Replace('\n', ' '));
            captureWindow = null;
            waitFrames = 0;
            waitUntilTime = 0.0;
            activeRequestId = null;
            state = CaptureState.Idle;
        }

        private static string GetRequestResultPath(string requestId)
        {
            return Path.GetFullPath("Library/BurtRenderDocCapture." + requestId + ".result");
        }

        private static void PrepareWaitingToBegin()
        {
            waitFrames = CaptureWarmupFrames;
            waitUntilTime = EditorApplication.timeSinceStartup + CaptureWarmupSeconds;
            state = CaptureState.WaitingToBegin;
        }

        private static bool TryResolveCaptureRequest(
            string requestPayload,
            out string requestId,
            out BurtShadingDebugMode mode,
            out bool localExposure,
            out bool hasIntegrateType,
            out ScreenProbeIntegrateType integrateType,
            out string error)
        {
            requestId = null;
            mode = BurtShadingDebugMode.None;
            localExposure = false;
            hasIntegrateType = false;
            integrateType = ScreenProbeIntegrateType.SimpleIntegrate;
            error = null;
            if (string.IsNullOrWhiteSpace(requestPayload))
            {
                return true;
            }

            const string modePrefix = "MODE=";
            const string requestIdPrefix = "REQUEST_ID=";
            const string localExposurePrefix = "LOCAL_EXPOSURE=";
            const string integratePrefix = "INTEGRATE=";
            var lines = requestPayload.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.StartsWith(requestIdPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var value = line.Substring(requestIdPrefix.Length).Trim();
                    if (!TryNormalizeRequestId(value, out requestId))
                    {
                        error = "REQUEST_ID must contain 1-64 ASCII letters, digits, '-' or '_'";
                        return false;
                    }
                    continue;
                }

                if (line.StartsWith(modePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var modeName = line.Substring(modePrefix.Length).Trim();
                    if (!Enum.TryParse(modeName, true, out mode))
                    {
                        error = "Unknown Burt shading debug capture mode '" + modeName + "'";
                        return false;
                    }

                    mode = BurtShadingDebugSettings.NormalizeMode(mode);
                    continue;
                }

                if (line.StartsWith(localExposurePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var value = line.Substring(localExposurePrefix.Length).Trim();
                    localExposure = string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
                    if (!localExposure &&
                        !string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
                    {
                        error = "LOCAL_EXPOSURE must be 0/1, false/true, or off/on";
                        return false;
                    }
                    continue;
                }

                if (line.StartsWith(integratePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var value = line.Substring(integratePrefix.Length).Trim();
                    if (!Enum.TryParse(value, true, out integrateType))
                    {
                        error = "Unknown Screen Probe integrate type '" + value + "'";
                        return false;
                    }

                    hasIntegrateType = true;
                    continue;
                }

                error = "Unknown capture request option '" + line + "'";
                return false;
            }

            return true;
        }

        private static bool TryNormalizeRequestId(string value, out string requestId)
        {
            requestId = null;
            if (string.IsNullOrEmpty(value) || value.Length > 64)
                return false;

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                var valid = character >= 'a' && character <= 'z' ||
                            character >= 'A' && character <= 'Z' ||
                            character >= '0' && character <= '9' ||
                            character == '-' || character == '_';
                if (!valid)
                    return false;
            }

            requestId = value;
            return true;
        }

        private static void ApplyTemporaryIntegrateType(ScreenProbeIntegrateType integrateType)
        {
            RestoreTemporaryIntegrateType();
            overrideIntegrateType = true;
            var lights = Resources.FindObjectsOfTypeAll<BurtXGILightComponent>();
            for (var i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light == null || EditorUtility.IsPersistent(light))
                    continue;

                overriddenXGILights.Add(light);
                overriddenXGIIntegrateTypes.Add(light.screenProbeIntegrateType);
                light.screenProbeIntegrateType = integrateType;
            }

            temporaryIntegrateVolumeObject = new GameObject("Burt RenderDoc Integrate Type Override")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            temporaryIntegrateVolumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            temporaryIntegrateVolumeProfile.name = "Burt RenderDoc Integrate Type Override Profile";
            temporaryIntegrateVolumeProfile.hideFlags = HideFlags.HideAndDontSave;
            var screenSpaceGI = temporaryIntegrateVolumeProfile.Add<ScreenSpaceGlobalIlluminationVolumeComponent>(true);
            screenSpaceGI.SetAllOverridesTo(false);
            screenSpaceGI.active = true;
            screenSpaceGI.screenProbeIntegrateType.overrideState = true;
            screenSpaceGI.screenProbeIntegrateType.value = integrateType;

            var volume = temporaryIntegrateVolumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = float.MaxValue;
            volume.weight = 1f;
            volume.sharedProfile = temporaryIntegrateVolumeProfile;
        }

        private static void RestoreTemporaryIntegrateType()
        {
            for (var i = 0; i < overriddenXGILights.Count && i < overriddenXGIIntegrateTypes.Count; i++)
            {
                if (overriddenXGILights[i] != null)
                    overriddenXGILights[i].screenProbeIntegrateType = overriddenXGIIntegrateTypes[i];
            }

            overriddenXGILights.Clear();
            overriddenXGIIntegrateTypes.Clear();
            if (temporaryIntegrateVolumeObject != null)
                UnityEngine.Object.DestroyImmediate(temporaryIntegrateVolumeObject);
            if (temporaryIntegrateVolumeProfile != null)
                UnityEngine.Object.DestroyImmediate(temporaryIntegrateVolumeProfile);
            temporaryIntegrateVolumeObject = null;
            temporaryIntegrateVolumeProfile = null;
            overrideIntegrateType = false;
        }

        private static void CreateTemporaryLocalExposureVolume()
        {
            DestroyTemporaryLocalExposureVolume();
            useTemporaryLocalExposure = true;
            temporaryVolumeObject = new GameObject("Burt RenderDoc Local Exposure Validation")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            temporaryVolumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            temporaryVolumeProfile.name = "Burt RenderDoc Local Exposure Validation Profile";
            temporaryVolumeProfile.hideFlags = HideFlags.HideAndDontSave;
            var localExposure = temporaryVolumeProfile.Add<LocalExposureVolumeComponent>(true);
            localExposure.SetAllOverridesTo(true);
            localExposure.active = true;
            localExposure.enabled.value = true;

            var volume = temporaryVolumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = float.MaxValue;
            volume.weight = 1f;
            volume.sharedProfile = temporaryVolumeProfile;
        }

        private static void DestroyTemporaryLocalExposureVolume()
        {
            if (temporaryVolumeObject != null)
                UnityEngine.Object.DestroyImmediate(temporaryVolumeObject);
            if (temporaryVolumeProfile != null)
                UnityEngine.Object.DestroyImmediate(temporaryVolumeProfile);
            temporaryVolumeObject = null;
            temporaryVolumeProfile = null;
            useTemporaryLocalExposure = false;
        }
    }
}
#endif
