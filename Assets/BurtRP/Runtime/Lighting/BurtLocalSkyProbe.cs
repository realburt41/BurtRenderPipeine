using System.Collections.Generic;
using UnityEngine;

namespace Burt.RenderPipeline
{
    [DisallowMultipleComponent]
    public sealed class BurtLocalSkyProbe : MonoBehaviour
    {
        [Tooltip("HDR color cubemap captured at this probe origin.")]
        public Cubemap colorCubemap;

        [Tooltip("Distance cubemap captured from the same origin as the color cubemap.")]
        public Cubemap depthCubemap;

        [Tooltip("Maximum camera/probe offset that can use this depth-parallax trace.")]
        [Min(0.01f)] public float probeOffsetDistanceMax = 20f;

        [Tooltip("Radiance multiplier applied after sampling the color cubemap.")]
        [Min(0f)] public float intensity = 1f;

        [Tooltip("Higher-priority probes win before distance is considered.")]
        public int priority;

        private static readonly List<BurtLocalSkyProbe> ActiveProbes = new List<BurtLocalSkyProbe>();

        internal bool IsTraceReady => isActiveAndEnabled && colorCubemap != null && depthCubemap != null && probeOffsetDistanceMax > 0.01f && intensity > 0f;

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

            var cameraPosition = camera.transform.position;
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

                if (!candidate.IsTraceReady)
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
    }
}
