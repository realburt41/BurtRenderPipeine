using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Burt.RenderPipeline
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/BurtRP/XGI Probe Streaming Pivot")]
    [MovedFrom(true, "UnityEngine.Rendering", "FunPlus.WorldX.XRender.Runtime", "XGIProbeStreamingPivot")]
    public sealed class BurtGIProbeStreamingPivot : MonoBehaviour
    {
        [Tooltip("Higher-priority pivots drive XGI cell streaming before lower-priority pivots.")]
        public int priority;

        [Tooltip("Stored for XRender compatibility. Current XRender runtime carries this value but does not filter streamed cells with it.")]
        public Vector3Int range;

        private static readonly List<BurtGIProbeStreamingPivot> ActivePivots = new List<BurtGIProbeStreamingPivot>();

        internal readonly struct PivotData
        {
            internal readonly Vector3 Position;
            internal readonly Vector3 Forward;
            internal readonly Vector3Int Range;

            internal PivotData(Vector3 position, Vector3 forward, Vector3Int range)
            {
                Position = position;
                Forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
                Range = range;
            }
        }

        private void OnEnable()
        {
            if (!ActivePivots.Contains(this))
            {
                ActivePivots.Add(this);
            }
        }

        private void OnDisable()
        {
            ActivePivots.Remove(this);
        }

        private void OnDestroy()
        {
            ActivePivots.Remove(this);
        }

        internal static bool TryGetBest(Camera camera, out PivotData pivot)
        {
            var bestPriority = int.MinValue;
            BurtGIProbeStreamingPivot bestPivot = null;
            for (var index = ActivePivots.Count - 1; index >= 0; --index)
            {
                var candidate = ActivePivots[index];
                if (candidate == null)
                {
                    ActivePivots.RemoveAt(index);
                    continue;
                }

                if (!candidate.isActiveAndEnabled || candidate.priority < bestPriority)
                {
                    continue;
                }

                bestPriority = candidate.priority;
                bestPivot = candidate;
            }

            if (bestPivot != null)
            {
                pivot = new PivotData(bestPivot.transform.position, bestPivot.transform.forward, bestPivot.range);
                return true;
            }

            if (BurtXRenderPivot.TryGetTop(out var xrenderPivot))
            {
                pivot = new PivotData(xrenderPivot.Position, xrenderPivot.Forward, Vector3Int.zero);
                return true;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying &&
                SceneView.lastActiveSceneView != null &&
                SceneView.lastActiveSceneView.camera != null)
            {
                var sceneViewCamera = SceneView.lastActiveSceneView.camera;
                pivot = new PivotData(sceneViewCamera.transform.position, sceneViewCamera.transform.forward, Vector3Int.zero);
                return true;
            }
#endif

            if (camera != null)
            {
                pivot = new PivotData(camera.transform.position, camera.transform.forward, Vector3Int.zero);
                return true;
            }

            pivot = default;
            return false;
        }

        internal static string GetDebugStatus(Camera camera)
        {
            var activeCount = 0;
            var bestPriority = int.MinValue;
            BurtGIProbeStreamingPivot bestPivot = null;
            for (var index = ActivePivots.Count - 1; index >= 0; --index)
            {
                var candidate = ActivePivots[index];
                if (candidate == null)
                {
                    ActivePivots.RemoveAt(index);
                    continue;
                }

                if (!candidate.isActiveAndEnabled)
                {
                    continue;
                }

                activeCount++;
                if (candidate.priority < bestPriority)
                {
                    continue;
                }

                bestPriority = candidate.priority;
                bestPivot = candidate;
            }

            if (bestPivot != null)
            {
                var effectiveForward = camera != null ? camera.transform.forward : bestPivot.transform.forward;
                return "Pivot(Name=" + bestPivot.name +
                    ",Priority=" + bestPivot.priority +
                    ",Active=" + activeCount +
                    ",Position=" + bestPivot.transform.position.ToString("F2") +
                    ",PivotForward=" + bestPivot.transform.forward.ToString("F2") +
                    ",EffectiveForward=" + effectiveForward.ToString("F2") +
                    ",Range=" + bestPivot.range + ")";
            }

            if (BurtXRenderPivot.TryGetTop(out _))
            {
                return "XRenderPivot(" + BurtXRenderPivot.GetDebugStatus() + ")";
            }

#if UNITY_EDITOR
            if (!Application.isPlaying &&
                SceneView.lastActiveSceneView != null &&
                SceneView.lastActiveSceneView.camera != null)
            {
                var sceneViewCamera = SceneView.lastActiveSceneView.camera;
                return "SceneView(Name=" + sceneViewCamera.name +
                    ",Position=" + sceneViewCamera.transform.position.ToString("F2") +
                    ",Forward=" + sceneViewCamera.transform.forward.ToString("F2") + ")";
            }
#endif

            return camera != null
                ? "Camera(Name=" + camera.name + ",Position=" + camera.transform.position.ToString("F2") + ")"
                : "None";
        }
    }
}
