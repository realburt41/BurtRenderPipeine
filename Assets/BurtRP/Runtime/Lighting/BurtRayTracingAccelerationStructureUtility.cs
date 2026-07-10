using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Burt.RenderPipeline
{
    internal static class BurtRayTracingAccelerationStructureUtility
    {
        private const string BurtGIRayTracingPassName = "BurtGI";

        private sealed class Entry
        {
            public RayTracingAccelerationStructure accelerationStructure;
            public int frameBuilt;
        }

        private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();

        public static bool TryGetForCamera(Camera camera, out RayTracingAccelerationStructure accelerationStructure)
        {
            accelerationStructure = null;
            if (camera == null || !SystemInfo.supportsRayTracing)
            {
                return false;
            }

            var cameraId = camera.GetInstanceID();
            if (!Entries.TryGetValue(cameraId, out var entry))
            {
                entry = new Entry
                {
                    accelerationStructure = new RayTracingAccelerationStructure(),
                    frameBuilt = -1
                };
                Entries.Add(cameraId, entry);
            }

            if (entry.frameBuilt != Time.frameCount)
            {
                entry.accelerationStructure.ClearInstances();
                var renderers = Object.FindObjectsOfType<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                        (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0 ||
                        !SupportsBurtGIRayTracing(renderer))
                    {
                        continue;
                    }

                    entry.accelerationStructure.AddInstance(renderer, (RayTracingSubMeshFlags[])null, true, false, 0xffu);
                }

                entry.accelerationStructure.Build();
                entry.frameBuilt = Time.frameCount;
            }

            accelerationStructure = entry.accelerationStructure;
            return true;
        }

        private static bool SupportsBurtGIRayTracing(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                return false;
            }

            for (var materialIndex = 0; materialIndex < materials.Length; ++materialIndex)
            {
                var material = materials[materialIndex];
                if (material == null || material.FindPass(BurtGIRayTracingPassName) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static void ReleaseAll()
        {
            foreach (var entry in Entries.Values)
            {
                entry.accelerationStructure?.Release();
            }
            Entries.Clear();
        }
    }
}
