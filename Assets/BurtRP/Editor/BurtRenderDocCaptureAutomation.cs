#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Burt.RenderPipeline.Editor
{
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
        private static EditorWindow captureWindow;
        private static BurtShadingDebugMode modeBeforeCapture;
        private static BurtShadingDebugMode modeDuringCapture;
        private static bool restoreShadingDebugMode;

        private static string RequestPath => Path.GetFullPath(RequestRelativePath);
        private static string ResultPath => Path.GetFullPath(ResultRelativePath);

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
                            waitFrames = 4;
                            state = CaptureState.WaitingToBegin;
                        }
                        else if (--waitFrames <= 0)
                        {
                            Finish("ERROR=RenderDoc failed to load");
                        }
                        break;
                    case CaptureState.WaitingToBegin:
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

            if (!TryResolveCaptureMode(requestPayload, out modeDuringCapture, out var modeError))
            {
                Finish("ERROR=" + modeError);
                return;
            }

            modeBeforeCapture = BurtShadingDebugSettings.Mode;
            restoreShadingDebugMode = true;
            BurtShadingDebugSettings.Mode = modeDuringCapture;
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

            waitFrames = 4;
            state = CaptureState.WaitingToBegin;
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
            Finish("OK\nWINDOW=" + captureWindow.GetType().FullName +
                   "\nMODE_BEFORE=" + modeBeforeCapture +
                   "\nMODE_CAPTURE=" + modeDuringCapture +
                   "\nUTC=" + DateTime.UtcNow.ToString("O"));
        }

        private static void Finish(string result)
        {
            if (restoreShadingDebugMode)
            {
                BurtShadingDebugSettings.Mode = modeBeforeCapture;
                result += "\nMODE_RESTORED=" + BurtShadingDebugSettings.Mode;
                restoreShadingDebugMode = false;
                SceneView.RepaintAll();
            }

            File.WriteAllText(ResultPath, result);
            Debug.Log("[BurtRP][RenderDocAutomation] " + result.Replace('\n', ' '));
            captureWindow = null;
            waitFrames = 0;
            state = CaptureState.Idle;
        }

        private static bool TryResolveCaptureMode(
            string requestPayload,
            out BurtShadingDebugMode mode,
            out string error)
        {
            mode = BurtShadingDebugMode.None;
            error = null;
            if (string.IsNullOrWhiteSpace(requestPayload))
            {
                return true;
            }

            const string modePrefix = "MODE=";
            var lines = requestPayload.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (!line.StartsWith(modePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var modeName = line.Substring(modePrefix.Length).Trim();
                if (!Enum.TryParse(modeName, true, out mode))
                {
                    error = "Unknown Burt shading debug capture mode '" + modeName + "'";
                    return false;
                }

                mode = BurtShadingDebugSettings.NormalizeMode(mode);
                return true;
            }

            error = "Capture request must be empty or contain MODE=<BurtShadingDebugMode>";
            return false;
        }
    }
}
#endif
