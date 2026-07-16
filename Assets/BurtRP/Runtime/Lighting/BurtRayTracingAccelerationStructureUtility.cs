using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Burt.RenderPipeline
{
    public static class BurtRayTracingAccelerationStructureUtility
    {
        private const string BurtGIRayTracingPassName = "BurtGI";

        private sealed class Entry
        {
            public RayTracingAccelerationStructure accelerationStructure;
            public int frameBuilt;
            public int renderersConsidered;
            public int renderersIncluded;
            public int subMeshesEnabled;
            public int subMeshesDisabled;
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
                entry.renderersConsidered = 0;
                entry.renderersIncluded = 0;
                entry.subMeshesEnabled = 0;
                entry.subMeshesDisabled = 0;
                var renderers = Object.FindObjectsOfType<Renderer>();
                foreach (var renderer in renderers)
                {
                    entry.renderersConsidered++;
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                        (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0 ||
                        !TryBuildBurtGIRayTracingSubMeshFlags(renderer, out var subMeshFlags, out var enabledSubMeshes, out var disabledSubMeshes))
                    {
                        continue;
                    }

                    entry.accelerationStructure.AddInstance(renderer, subMeshFlags, true, false, 0xffu);
                    entry.renderersIncluded++;
                    entry.subMeshesEnabled += enabledSubMeshes;
                    entry.subMeshesDisabled += disabledSubMeshes;
                }

                entry.accelerationStructure.Build();
                entry.frameBuilt = Time.frameCount;
            }

            accelerationStructure = entry.accelerationStructure;
            return true;
        }

        public static string ResolveStatusLabel(Camera camera)
        {
            if (camera == null)
            {
                return "CameraMissing";
            }

            if (!SystemInfo.supportsRayTracing)
            {
                return "PlatformNoRayTracing";
            }

            if (!Entries.TryGetValue(camera.GetInstanceID(), out var entry) || entry.frameBuilt < 0)
            {
                return "NotBuilt";
            }

            return "Built(Frame=" + entry.frameBuilt +
                ",Renderers=" + entry.renderersIncluded + "/" + entry.renderersConsidered +
                ",SubMeshes=" + entry.subMeshesEnabled + "/" + (entry.subMeshesEnabled + entry.subMeshesDisabled) + ")";
        }

        private static bool TryBuildBurtGIRayTracingSubMeshFlags(
            Renderer renderer,
            out RayTracingSubMeshFlags[] subMeshFlags,
            out int enabledSubMeshes,
            out int disabledSubMeshes)
        {
            subMeshFlags = null;
            enabledSubMeshes = 0;
            disabledSubMeshes = 0;
            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                return false;
            }

            var mesh = ResolveRendererMesh(renderer);
            var subMeshCount = mesh != null ? mesh.subMeshCount : materials.Length;
            if (subMeshCount <= 0)
            {
                return false;
            }

            subMeshFlags = new RayTracingSubMeshFlags[subMeshCount];
            var hasSupportedSubMesh = false;
            for (var subMeshIndex = 0; subMeshIndex < subMeshCount; ++subMeshIndex)
            {
                var materialIndex = Mathf.Min(subMeshIndex, materials.Length - 1);
                var material = materials[materialIndex];
                if (material == null || material.FindPass(BurtGIRayTracingPassName) < 0)
                {
                    subMeshFlags[subMeshIndex] = RayTracingSubMeshFlags.Disabled;
                    disabledSubMeshes++;
                    continue;
                }

                subMeshFlags[subMeshIndex] = RayTracingSubMeshFlags.Enabled;
                enabledSubMeshes++;
                hasSupportedSubMesh = true;
            }

            return hasSupportedSubMesh;
        }

        private static Mesh ResolveRendererMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                return skinnedMeshRenderer.sharedMesh;
            }

            var meshFilter = renderer.GetComponent<MeshFilter>();
            return meshFilter != null ? meshFilter.sharedMesh : null;
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
