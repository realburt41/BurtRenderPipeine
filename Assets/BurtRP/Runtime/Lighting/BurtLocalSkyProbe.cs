using System.Collections.Generic;
using UnityEngine;

namespace Burt.RenderPipeline
{
    public enum BurtLocalSkyProbeShape
    {
        Sphere,
        Box
    }

    [DisallowMultipleComponent]
    public sealed class BurtLocalSkyProbe : MonoBehaviour
    {
        internal const float DefaultCameraSelectionForwardDistance = 2f;

        [Tooltip("XRender ambient probe shape used when selecting the best local sky probe.")]
        public BurtLocalSkyProbeShape shape = BurtLocalSkyProbeShape.Sphere;

        [Tooltip("HDR color cubemap captured at this probe origin.")]
        public Cubemap colorCubemap;

        [Tooltip("Distance cubemap captured from the same origin as the color cubemap.")]
        public Cubemap depthCubemap;

        [Tooltip("Maximum camera/probe offset that can use this depth-parallax trace.")]
        [Min(0.01f)] public float probeOffsetDistanceMax = 100f;

        [Tooltip("Maximum distance used by the XRender local sky probe sample-vector lerp path.")]
        [Min(0.01f)] public float probeSampleLerpDistanceMax = 50f;

        [Tooltip("Radiance multiplier applied after sampling the color cubemap.")]
        [Min(0f)] public float intensity = 1f;

        [Tooltip("Higher-priority probes win before distance is considered.")]
        public int priority;

        [Tooltip("Show the XRender-style local sky probe debug volume in the Scene view.")]
        public bool showDebugSphere = true;

        private static readonly List<BurtLocalSkyProbe> ActiveProbes = new List<BurtLocalSkyProbe>();
        private static bool globalShowDebugSphere = true;
        private static float cameraSelectionForwardDistance = DefaultCameraSelectionForwardDistance;

        internal bool IsTraceReady => isActiveAndEnabled && colorCubemap != null && depthCubemap != null && probeOffsetDistanceMax > 0.01f && probeSampleLerpDistanceMax > 0.01f && intensity > 0f;
        internal static float CameraSelectionForwardDistance => cameraSelectionForwardDistance;

        internal static void SetGlobalDebugVisibility(bool visible)
        {
            globalShowDebugSphere = visible;
        }

        internal static void SetCameraSelectionForwardDistance(float distance)
        {
            cameraSelectionForwardDistance = Mathf.Max(0f, distance);
        }

        private void OnEnable()
        {
            if (!ActiveProbes.Contains(this))
            {
                ActiveProbes.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveProbes.Remove(this);
        }

        internal static bool TryGetBestForCamera(Camera camera, out BurtLocalSkyProbe probe)
        {
            probe = null;
            if (camera == null)
            {
                return false;
            }

            var cameraTransform = camera.transform;
            var cameraPosition = cameraTransform.position + cameraTransform.forward * cameraSelectionForwardDistance;
            if (TryChooseBestForCameraPosition(cameraPosition, BurtLocalSkyProbeShape.Box, out probe))
            {
                return true;
            }

            return TryChooseBestForCameraPosition(cameraPosition, BurtLocalSkyProbeShape.Sphere, out probe);
        }

        private static bool TryChooseBestForCameraPosition(Vector3 cameraPosition, BurtLocalSkyProbeShape shape, out BurtLocalSkyProbe probe)
        {
            probe = null;
            var bestPriority = int.MinValue;
            var bestDistanceSq = float.PositiveInfinity;
            for (var index = ActiveProbes.Count - 1; index >= 0; --index)
            {
                var candidate = ActiveProbes[index];
                if (candidate == null)
                {
                    ActiveProbes.RemoveAt(index);
                    continue;
                }

                if (!candidate.IsTraceReady || candidate.shape != shape || !Contains(candidate, cameraPosition))
                {
                    continue;
                }

                var distanceSq = (candidate.transform.position - cameraPosition).sqrMagnitude;
                if (candidate.priority < bestPriority || (candidate.priority == bestPriority && distanceSq >= bestDistanceSq))
                {
                    continue;
                }

                probe = candidate;
                bestPriority = candidate.priority;
                bestDistanceSq = distanceSq;
            }

            return probe != null;
        }

        private static bool Contains(BurtLocalSkyProbe probe, Vector3 position)
        {
            if (probe.shape == BurtLocalSkyProbeShape.Box)
            {
                var positionLocalSpace = probe.transform.InverseTransformPoint(position);
                return new Bounds(Vector3.zero, Vector3.one).Contains(positionLocalSpace);
            }

            var distanceSq = (probe.transform.position - position).sqrMagnitude;
            var maxDistance = Mathf.Max(0.01f, probe.probeOffsetDistanceMax);
            return distanceSq <= maxDistance * maxDistance;
        }

        private void OnDrawGizmos()
        {
            if (globalShowDebugSphere && showDebugSphere)
            {
                DrawDebugVolume(false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugSphere)
            {
                DrawDebugVolume(true);
            }
        }

        private void DrawDebugVolume(bool selected)
        {
            var ready = colorCubemap != null && depthCubemap != null && intensity > 0f;
            Gizmos.color = ready
                ? new Color(0.42f, 0.58f, 1f, selected ? 0.95f : 0.55f)
                : new Color(1f, 0.45f, 0.22f, selected ? 0.95f : 0.55f);

            if (shape == BurtLocalSkyProbeShape.Box)
            {
                var probeTransform = transform;
                var previousMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(probeTransform.position, probeTransform.rotation, probeTransform.lossyScale);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                Gizmos.matrix = previousMatrix;
                return;
            }

            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, probeOffsetDistanceMax));
        }
    }
}
