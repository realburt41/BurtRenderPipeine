using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [DisallowMultipleComponent]
    public sealed class BurtGISceneVoxelVolume : MonoBehaviour
    {
        [Tooltip("World-space half extent represented by the supplied voxel textures.")]
        [Min(0.01f)] public float extent = 12f;

        [Tooltip("RGB radiance with confidence in alpha. The texture must be a 64 cubed 3D RenderTexture.")]
        public RenderTexture radiance;

        [Tooltip("World normal encoded in RGB with occupancy in alpha. The texture must be a 64 cubed 3D RenderTexture.")]
        public RenderTexture geometry;

        [Tooltip("Higher-priority volumes win before distance is considered.")]
        public int priority;

        [Tooltip("Use this volume's center and extent as the radiance-cache clipmap base level while the camera is inside it.")]
        public bool driveRadianceCacheClipmap = true;

        [Tooltip("Extent multiplier between radiance-cache clipmap levels when this volume drives the cache.")]
        [Min(1f)] public float radianceCacheClipmapDistributionBase = 2f;

        private static readonly List<BurtGISceneVoxelVolume> ActiveVolumes = new List<BurtGISceneVoxelVolume>();

        internal bool IsReady => isActiveAndEnabled &&
            extent > 0.01f &&
            IsCompatible(radiance, BurtScreenSpaceGlobalIlluminationPassUtility.SceneVoxelRadianceResolution) &&
            IsCompatible(geometry, BurtScreenSpaceGlobalIlluminationPassUtility.SceneVoxelRadianceResolution);

        internal Vector4 CenterExtent
        {
            get
            {
                var center = transform.position;
                return new Vector4(center.x, center.y, center.z, extent);
            }
        }

        private void OnEnable()
        {
            if (!ActiveVolumes.Contains(this))
            {
                ActiveVolumes.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveVolumes.Remove(this);
        }

        internal static bool TryGetBestForCamera(Camera camera, out BurtGISceneVoxelVolume volume)
        {
            volume = null;
            if (camera == null)
            {
                return false;
            }

            var cameraPosition = camera.transform.position;
            var bestPriority = int.MinValue;
            var bestDistanceSq = float.PositiveInfinity;
            for (var index = ActiveVolumes.Count - 1; index >= 0; --index)
            {
                var candidate = ActiveVolumes[index];
                if (candidate == null)
                {
                    ActiveVolumes.RemoveAt(index);
                    continue;
                }

                if (!candidate.IsReady || !Contains(candidate, cameraPosition))
                {
                    continue;
                }

                var distanceSq = (candidate.transform.position - cameraPosition).sqrMagnitude;
                if (candidate.priority < bestPriority || (candidate.priority == bestPriority && distanceSq >= bestDistanceSq))
                {
                    continue;
                }

                volume = candidate;
                bestPriority = candidate.priority;
                bestDistanceSq = distanceSq;
            }

            return volume != null;
        }

        private static bool Contains(BurtGISceneVoxelVolume volume, Vector3 position)
        {
            var delta = position - volume.transform.position;
            return Mathf.Abs(delta.x) <= volume.extent &&
                Mathf.Abs(delta.y) <= volume.extent &&
                Mathf.Abs(delta.z) <= volume.extent;
        }

        private static bool IsCompatible(RenderTexture texture, int resolution)
        {
            return texture != null &&
                texture.IsCreated() &&
                texture.dimension == UnityEngine.Rendering.TextureDimension.Tex3D &&
                texture.width == resolution &&
                texture.height == resolution &&
                texture.volumeDepth == resolution;
        }
    }

    internal static class BurtGISceneVoxelVolumeUtility
    {
        public static Vector4 ResolveCenterExtent(Camera camera, Vector4 fallback)
        {
            return BurtGISceneVoxelVolume.TryGetBestForCamera(camera, out var volume)
                ? volume.CenterExtent
                : fallback;
        }

        public static bool TryResolveRadianceCacheClipmap(Camera camera, out Vector4 centerExtent, out float distributionBase)
        {
            centerExtent = Vector4.zero;
            distributionBase = 2f;
            if (!BurtGISceneVoxelVolume.TryGetBestForCamera(camera, out var volume) || !volume.driveRadianceCacheClipmap)
            {
                return false;
            }

            centerExtent = volume.CenterExtent;
            distributionBase = Mathf.Max(1f, volume.radianceCacheClipmapDistributionBase);
            return true;
        }
    }

    public enum BurtGIVoxelLightShape
    {
        Cube,
        Sphere,
        Capsule,
        Cylinder,
        Plane,
        Quad
    }

    /// <summary>
    /// Injects an emissive proxy directly into BurtGI scene voxelization.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BurtGIVoxelLight : MonoBehaviour
    {
        [SerializeField] private BurtGIVoxelLightShape shape = BurtGIVoxelLightShape.Cube;
        [SerializeField] [Range(0f, 0.95f)] private float voxelDecreaseRatio;
        [SerializeField] private Color emissiveColor = Color.white;
        [SerializeField] [Min(0f)] private float luminance = 1f;

        private static readonly List<BurtGIVoxelLight> ActiveLights = new List<BurtGIVoxelLight>();

        internal static IList<BurtGIVoxelLight> Active => ActiveLights;
        internal BurtGIVoxelLightShape Shape => shape;
        internal float VoxelDecreaseRatio => voxelDecreaseRatio;
        internal Color EmissiveColor => emissiveColor;
        internal float Luminance => luminance;

        private void OnEnable()
        {
            if (!ActiveLights.Contains(this))
            {
                ActiveLights.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveLights.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveLights.Remove(this);
        }

        private void OnDrawGizmosSelected()
        {
            var previousColor = Gizmos.color;
            var previousMatrix = Gizmos.matrix;
            Gizmos.color = emissiveColor.linear * luminance;
            Gizmos.matrix = transform.localToWorldMatrix;
            var mesh = GetBuiltinMesh(shape);
            if (mesh != null)
            {
                Gizmos.DrawWireMesh(mesh);
            }

            Gizmos.color = previousColor;
            Gizmos.matrix = previousMatrix;
        }

        internal static Mesh GetBuiltinMesh(BurtGIVoxelLightShape lightShape)
        {
            switch (lightShape)
            {
                case BurtGIVoxelLightShape.Cube: return Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                case BurtGIVoxelLightShape.Sphere: return Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
                case BurtGIVoxelLightShape.Capsule: return Resources.GetBuiltinResource<Mesh>("New-Capsule.fbx");
                case BurtGIVoxelLightShape.Cylinder: return Resources.GetBuiltinResource<Mesh>("New-Cylinder.fbx");
                case BurtGIVoxelLightShape.Plane: return Resources.GetBuiltinResource<Mesh>("New-Plane.fbx");
                case BurtGIVoxelLightShape.Quad: return Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                default: return null;
            }
        }
    }

    /// <summary>
    /// GPU-resident 4x4x4 occupancy hierarchy for the runtime scene voxel volume.
    /// Each node stores a 64-bit child mask split across two R32 textures.
    /// </summary>
    internal static class BurtGISceneVoxelOctreeUtility
    {
        private const int NodeWidth = 4;
        private static readonly int LeafLowId = Shader.PropertyToID("_BurtGISceneVoxelOctreeLeafLowTexture");
        private static readonly int LeafHighId = Shader.PropertyToID("_BurtGISceneVoxelOctreeLeafHighTexture");
        private static readonly int ParentLowId = Shader.PropertyToID("_BurtGISceneVoxelOctreeParentLowTexture");
        private static readonly int ParentHighId = Shader.PropertyToID("_BurtGISceneVoxelOctreeParentHighTexture");
        private static readonly int RootLowId = Shader.PropertyToID("_BurtGISceneVoxelOctreeRootLowTexture");
        private static readonly int RootHighId = Shader.PropertyToID("_BurtGISceneVoxelOctreeRootHighTexture");
        private static readonly int ValidId = Shader.PropertyToID("_BurtGISceneVoxelOctreeValid");

        private static RenderTexture leafLow;
        private static RenderTexture leafHigh;
        private static RenderTexture parentLow;
        private static RenderTexture parentHigh;
        private static RenderTexture rootLow;
        private static RenderTexture rootHigh;
        private static bool valid;

        internal sealed class ResourceSet
        {
            internal RenderTexture LeafLow;
            internal RenderTexture LeafHigh;
            internal RenderTexture ParentLow;
            internal RenderTexture ParentHigh;
            internal RenderTexture RootLow;
            internal RenderTexture RootHigh;
            internal bool Valid;
        }

        public static int LeafResolution => Mathf.Max(1, BurtScreenSpaceGlobalIlluminationPassUtility.SceneVoxelRadianceResolution / NodeWidth);
        public static int ParentResolution => Mathf.Max(1, LeafResolution / NodeWidth);
        public static int RootResolution => Mathf.Max(1, ParentResolution / NodeWidth);
        public static bool IsValid => valid && AreResourcesValid();
        public static string DebugStatus => IsValid
            ? "Valid(" + LeafResolution + "/" + ParentResolution + "/" + RootResolution + ")"
            : "Fallback(" + LeafResolution + "/" + ParentResolution + "/" + RootResolution + ")";

        public static bool EnsureResources()
        {
            if (AreResourcesValid())
            {
                return true;
            }

            Release();
            leafLow = CreateTexture(LeafResolution, "BurtGI Scene Voxel Octree Leaf Low");
            leafHigh = CreateTexture(LeafResolution, "BurtGI Scene Voxel Octree Leaf High");
            parentLow = CreateTexture(ParentResolution, "BurtGI Scene Voxel Octree Parent Low");
            parentHigh = CreateTexture(ParentResolution, "BurtGI Scene Voxel Octree Parent High");
            rootLow = CreateTexture(RootResolution, "BurtGI Scene Voxel Octree Root Low");
            rootHigh = CreateTexture(RootResolution, "BurtGI Scene Voxel Octree Root High");
            valid = false;
            return AreResourcesValid();
        }

        public static void MarkBuilt()
        {
            valid = AreResourcesValid();
        }

        public static void BindCompute(CommandBuffer cmd, ComputeShader shader, int kernel)
        {
            if (cmd == null || shader == null)
            {
                return;
            }

            cmd.SetComputeFloatParam(shader, ValidId, IsValid ? 1.0f : 0.0f);
            if (!AreResourcesValid())
            {
                return;
            }

            cmd.SetComputeTextureParam(shader, kernel, LeafLowId, leafLow);
            cmd.SetComputeTextureParam(shader, kernel, LeafHighId, leafHigh);
            cmd.SetComputeTextureParam(shader, kernel, ParentLowId, parentLow);
            cmd.SetComputeTextureParam(shader, kernel, ParentHighId, parentHigh);
            cmd.SetComputeTextureParam(shader, kernel, RootLowId, rootLow);
            cmd.SetComputeTextureParam(shader, kernel, RootHighId, rootHigh);
        }

        public static bool EnsureResources(ResourceSet resources, string namePrefix)
        {
            if (resources == null)
            {
                return false;
            }

            if (AreResourcesValid(resources))
            {
                return true;
            }

            Release(resources);
            resources.LeafLow = CreateTexture(LeafResolution, namePrefix + " Octree Leaf Low");
            resources.LeafHigh = CreateTexture(LeafResolution, namePrefix + " Octree Leaf High");
            resources.ParentLow = CreateTexture(ParentResolution, namePrefix + " Octree Parent Low");
            resources.ParentHigh = CreateTexture(ParentResolution, namePrefix + " Octree Parent High");
            resources.RootLow = CreateTexture(RootResolution, namePrefix + " Octree Root Low");
            resources.RootHigh = CreateTexture(RootResolution, namePrefix + " Octree Root High");
            resources.Valid = false;
            return AreResourcesValid(resources);
        }

        public static void MarkBuilt(ResourceSet resources)
        {
            if (resources != null)
            {
                resources.Valid = AreResourcesValid(resources);
            }
        }

        public static void BindCompute(CommandBuffer cmd, ComputeShader shader, int kernel, ResourceSet resources)
        {
            if (cmd == null || shader == null)
            {
                return;
            }

            cmd.SetComputeFloatParam(shader, ValidId, resources != null && resources.Valid && AreResourcesValid(resources) ? 1.0f : 0.0f);
            if (!AreResourcesValid(resources))
            {
                return;
            }

            cmd.SetComputeTextureParam(shader, kernel, LeafLowId, resources.LeafLow);
            cmd.SetComputeTextureParam(shader, kernel, LeafHighId, resources.LeafHigh);
            cmd.SetComputeTextureParam(shader, kernel, ParentLowId, resources.ParentLow);
            cmd.SetComputeTextureParam(shader, kernel, ParentHighId, resources.ParentHigh);
            cmd.SetComputeTextureParam(shader, kernel, RootLowId, resources.RootLow);
            cmd.SetComputeTextureParam(shader, kernel, RootHighId, resources.RootHigh);
        }

        public static void Release()
        {
            ReleaseTexture(ref leafLow);
            ReleaseTexture(ref leafHigh);
            ReleaseTexture(ref parentLow);
            ReleaseTexture(ref parentHigh);
            ReleaseTexture(ref rootLow);
            ReleaseTexture(ref rootHigh);
            valid = false;
        }

        public static void Release(ResourceSet resources)
        {
            if (resources == null)
            {
                return;
            }

            ReleaseTexture(ref resources.LeafLow);
            ReleaseTexture(ref resources.LeafHigh);
            ReleaseTexture(ref resources.ParentLow);
            ReleaseTexture(ref resources.ParentHigh);
            ReleaseTexture(ref resources.RootLow);
            ReleaseTexture(ref resources.RootHigh);
            resources.Valid = false;
        }

        private static RenderTexture CreateTexture(int resolution, string name)
        {
            var descriptor = new RenderTextureDescriptor(resolution, resolution, GraphicsFormat.R32_UInt, 0)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = resolution,
                msaaSamples = 1,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true
            };
            var texture = new RenderTexture(descriptor)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            return texture;
        }

        private static bool AreResourcesValid()
        {
            return IsCreated(leafLow) && IsCreated(leafHigh) &&
                IsCreated(parentLow) && IsCreated(parentHigh) &&
                IsCreated(rootLow) && IsCreated(rootHigh);
        }

        private static bool AreResourcesValid(ResourceSet resources)
        {
            return resources != null &&
                IsCreated(resources.LeafLow) && IsCreated(resources.LeafHigh) &&
                IsCreated(resources.ParentLow) && IsCreated(resources.ParentHigh) &&
                IsCreated(resources.RootLow) && IsCreated(resources.RootHigh);
        }

        private static bool IsCreated(RenderTexture texture)
        {
            return texture != null && texture.IsCreated();
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            Object.DestroyImmediate(texture);
            texture = null;
        }
    }

    internal sealed class BurtGISceneVoxelClipmapResources
    {
        internal readonly int Level;
        internal readonly RenderTexture Radiance;
        internal readonly RenderTexture Geometry;
        internal readonly RenderTexture OccupancyMip;
        internal readonly RenderTexture Lighting;
        internal readonly BurtGISceneVoxelOctreeUtility.ResourceSet Octree = new BurtGISceneVoxelOctreeUtility.ResourceSet();

        internal BurtGISceneVoxelClipmapResources(int cameraId, int level)
        {
            Level = level;
            Radiance = CreateTexture(
                BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationSceneVoxelRadianceDescriptor(),
                "BurtGI Scene Voxel Clipmap " + level + " Radiance " + cameraId,
                FilterMode.Bilinear);
            Geometry = CreateTexture(
                BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationSceneVoxelGeometryDescriptor(),
                "BurtGI Scene Voxel Clipmap " + level + " Geometry " + cameraId,
                FilterMode.Point);
            OccupancyMip = CreateTexture(
                BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationSceneVoxelOccupancyMipDescriptor(),
                "BurtGI Scene Voxel Clipmap " + level + " Occupancy " + cameraId,
                FilterMode.Point);
            Lighting = CreateTexture(
                BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationSceneVoxelLightingDescriptor(),
                "BurtGI Scene Voxel Clipmap " + level + " Lighting " + cameraId,
                FilterMode.Bilinear);
        }

        internal bool IsValid => IsCreated(Radiance) && IsCreated(Geometry) && IsCreated(OccupancyMip) && IsCreated(Lighting);

        internal void Release()
        {
            ReleaseTexture(Radiance);
            ReleaseTexture(Geometry);
            ReleaseTexture(OccupancyMip);
            ReleaseTexture(Lighting);
            BurtGISceneVoxelOctreeUtility.Release(Octree);
        }

        private static RenderTexture CreateTexture(RenderTextureDescriptor descriptor, string name, FilterMode filterMode)
        {
            descriptor.msaaSamples = 1;
            descriptor.enableRandomWrite = true;
            var texture = new RenderTexture(descriptor)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = filterMode,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            return texture;
        }

        private static bool IsCreated(RenderTexture texture)
        {
            return texture != null && texture.IsCreated();
        }

        private static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            Object.DestroyImmediate(texture);
        }
    }

    internal static class BurtGISceneVoxelClipmapStateUtility
    {
        private const int ClipmapCount = 4;
        private static readonly int ClipmapCenterExtentId = Shader.PropertyToID("_BurtGISceneVoxelClipmapCenterExtent");
        private static readonly int ClipmapValidMaskId = Shader.PropertyToID("_BurtGISceneVoxelClipmapValidMask");
        private static readonly int[] ClipmapGeometryTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1GeometryReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2GeometryReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3GeometryReadTexture")
        };
        private static readonly int[] ClipmapRadianceTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1RadianceReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2RadianceReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3RadianceReadTexture")
        };
        private static readonly int[] ClipmapOccupancyTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OccupancyMipReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OccupancyMipReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OccupancyMipReadTexture")
        };
        private static readonly int[] ClipmapLightingTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1LightingReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2LightingReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3LightingReadTexture")
        };
        private static readonly int[] ClipmapOctreeLeafLowTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OctreeLeafLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OctreeLeafLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OctreeLeafLowTexture")
        };
        private static readonly int[] ClipmapOctreeLeafHighTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OctreeLeafHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OctreeLeafHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OctreeLeafHighTexture")
        };
        private static readonly int[] ClipmapOctreeParentLowTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OctreeParentLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OctreeParentLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OctreeParentLowTexture")
        };
        private static readonly int[] ClipmapOctreeParentHighTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OctreeParentHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OctreeParentHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OctreeParentHighTexture")
        };
        private static readonly int[] ClipmapOctreeRootLowTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OctreeRootLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OctreeRootLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OctreeRootLowTexture")
        };
        private static readonly int[] ClipmapOctreeRootHighTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OctreeRootHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OctreeRootHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OctreeRootHighTexture")
        };
        private sealed class CameraState
        {
            public readonly Bounds[] Bounds = new Bounds[ClipmapCount];
            public readonly BurtGISceneVoxelClipmapResources[] Resources = new BurtGISceneVoxelClipmapResources[ClipmapCount];
            public uint ValidMask;
            public uint UpdateMask;
            public bool Initialized;
        }

        private static readonly Dictionary<int, CameraState> CameraStates = new Dictionary<int, CameraState>();

        public static void Update(Camera camera, Vector4 baseCenterExtent, float distributionBase)
        {
            if (camera == null)
            {
                return;
            }

            if (!CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                state = new CameraState();
                CameraStates.Add(camera.GetInstanceID(), state);
            }

            for (var level = 0; level < ClipmapCount; ++level)
            {
                var extent = Mathf.Max(0.001f, baseCenterExtent.w * Mathf.Pow(Mathf.Max(1f, distributionBase), level));
                var cellSize = extent * 2f / BurtScreenSpaceGlobalIlluminationPassUtility.SceneVoxelRadianceResolution;
                var center = new Vector3(
                    Mathf.Round(baseCenterExtent.x / cellSize) * cellSize,
                    Mathf.Round(baseCenterExtent.y / cellSize) * cellSize,
                    Mathf.Round(baseCenterExtent.z / cellSize) * cellSize);
                var bounds = new Bounds(center, Vector3.one * extent * 2f);
                var changed = !state.Initialized || state.Bounds[level].size != bounds.size ||
                    Vector3.Distance(state.Bounds[level].center, bounds.center) > cellSize;
                if (changed)
                {
                    state.Bounds[level] = bounds;
                    state.ValidMask &= ~(1u << level);
                    state.UpdateMask |= 1u << level;
                }
            }

            state.Initialized = true;
        }

        public static void MarkGenerated(Camera camera, int level)
        {
            if (camera == null || level < 0 || level >= ClipmapCount || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return;
            }

            state.ValidMask |= 1u << level;
            state.UpdateMask &= ~(1u << level);
        }

        public static bool NeedsUpdate(Camera camera, int level)
        {
            return camera != null && level >= 0 && level < ClipmapCount &&
                CameraStates.TryGetValue(camera.GetInstanceID(), out var state) &&
                (state.UpdateMask & (1u << level)) != 0u;
        }

        public static void Invalidate(Camera camera)
        {
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return;
            }

            state.ValidMask = 0u;
            state.UpdateMask |= (1u << ClipmapCount) - 1u;
        }

        public static bool TryGetBounds(Camera camera, int level, out Bounds bounds)
        {
            bounds = default;
            return camera != null && level >= 0 && level < ClipmapCount &&
                CameraStates.TryGetValue(camera.GetInstanceID(), out var state) && state.Initialized &&
                (bounds = state.Bounds[level]).size.sqrMagnitude > 0.000001f;
        }

        public static bool TryGetResources(Camera camera, int level, out BurtGISceneVoxelClipmapResources resources, out Vector4 centerExtent)
        {
            resources = null;
            centerExtent = Vector4.zero;
            if (camera == null || level <= 0 || level >= ClipmapCount || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return false;
            }

            if (state.Resources[level] == null || !state.Resources[level].IsValid)
            {
                state.Resources[level]?.Release();
                state.Resources[level] = new BurtGISceneVoxelClipmapResources(camera.GetInstanceID(), level);
            }

            resources = state.Resources[level];
            var bounds = state.Bounds[level];
            centerExtent = new Vector4(bounds.center.x, bounds.center.y, bounds.center.z, bounds.extents.x);
            return resources.IsValid;
        }

        public static void BindTraceCompute(
            CommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            Camera camera,
            Vector4 baseCenterExtent,
            RenderTargetIdentifier fallbackGeometry,
            RenderTargetIdentifier fallbackOccupancy,
            RenderTargetIdentifier fallbackLighting)
        {
            if (cmd == null || shader == null)
            {
                return;
            }

            var centerExtents = new[] { baseCenterExtent, baseCenterExtent, baseCenterExtent };
            uint validMask = 1u;
            if (camera != null && CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                for (var level = 1; level < ClipmapCount; ++level)
                {
                    var bounds = state.Bounds[level];
                    centerExtents[level] = new Vector4(bounds.center.x, bounds.center.y, bounds.center.z, bounds.extents.x);
                    var resources = state.Resources[level];
                    var levelValid = (state.ValidMask & (1u << level)) != 0u && resources != null && resources.IsValid && resources.Octree.Valid;
                    if (!levelValid)
                    {
                        cmd.SetComputeTextureParam(shader, kernel, ClipmapGeometryTextureIds[level], fallbackGeometry);
                        cmd.SetComputeTextureParam(shader, kernel, ClipmapOccupancyTextureIds[level], fallbackOccupancy);
                        cmd.SetComputeTextureParam(shader, kernel, ClipmapLightingTextureIds[level], fallbackLighting);
                        continue;
                    }

                    validMask |= 1u << level;
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapGeometryTextureIds[level], resources.Geometry);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapOccupancyTextureIds[level], resources.OccupancyMip);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapLightingTextureIds[level], resources.Lighting);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeLeafLowTextureIds[level], resources.Octree.LeafLow);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeLeafHighTextureIds[level], resources.Octree.LeafHigh);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeParentLowTextureIds[level], resources.Octree.ParentLow);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeParentHighTextureIds[level], resources.Octree.ParentHigh);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeRootLowTextureIds[level], resources.Octree.RootLow);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeRootHighTextureIds[level], resources.Octree.RootHigh);
                }
            }
            else
            {
                for (var level = 1; level < ClipmapCount; ++level)
                {
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapGeometryTextureIds[level], fallbackGeometry);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapOccupancyTextureIds[level], fallbackOccupancy);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapLightingTextureIds[level], fallbackLighting);
                }
            }

            cmd.SetComputeVectorArrayParam(shader, ClipmapCenterExtentId, centerExtents);
            cmd.SetComputeIntParam(shader, ClipmapValidMaskId, (int)validMask);
        }

        public static void BindRadianceCacheTraceCompute(
            CommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            Camera camera,
            Vector4 baseCenterExtent,
            RenderTargetIdentifier fallbackRadiance,
            RenderTargetIdentifier fallbackGeometry,
            RenderTargetIdentifier fallbackOccupancy,
            RenderTargetIdentifier fallbackLighting)
        {
            BindTraceCompute(cmd, shader, kernel, camera, baseCenterExtent, fallbackGeometry, fallbackOccupancy, fallbackLighting);
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                for (var level = 1; level < ClipmapCount; ++level)
                {
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapRadianceTextureIds[level], fallbackRadiance);
                }

                return;
            }

            for (var level = 1; level < ClipmapCount; ++level)
            {
                var resources = state.Resources[level];
                var levelValid = (state.ValidMask & (1u << level)) != 0u && resources != null && resources.IsValid && resources.Octree.Valid;
                if (levelValid)
                {
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapRadianceTextureIds[level], resources.Radiance);
                }
                else
                {
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapRadianceTextureIds[level], fallbackRadiance);
                }
            }
        }

        public static string GetDebugStatus(Camera camera)
        {
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return "Uninitialized";
            }

            var resourceStatus = "";
            for (var level = 1; level < ClipmapCount; ++level)
            {
                var resources = state.Resources[level];
                resourceStatus += ";L" + level + "=" + (resources != null && resources.IsValid ? (resources.Octree.Valid ? "Ready" : "OctreePending") : "Unallocated");
            }

            return "Valid=0x" + state.ValidMask.ToString("X") + ";Update=0x" + state.UpdateMask.ToString("X") + resourceStatus;
        }

        public static void ReleaseAll()
        {
            foreach (var pair in CameraStates)
            {
                var resources = pair.Value.Resources;
                for (var level = 0; level < resources.Length; ++level)
                {
                    resources[level]?.Release();
                    resources[level] = null;
                }
            }

            CameraStates.Clear();
        }
    }

    internal static class BurtGIXGILightGridUtility
    {
        private const int ClipmapCount = 4;
        private const int GridResolution = 32;
        private const int MaxLightsPerCell = 32;
        private const int MaxSceneLights = 32;
        private const int LightDataRows = 4;
        private const int RebuildPeriodFrames = 8;

        private static readonly int LightDataId = Shader.PropertyToID("_BurtGIXGILightData");
        private static readonly int GridCountId = Shader.PropertyToID("_BurtGIXGILightGridCount");
        private static readonly int GridListId = Shader.PropertyToID("_BurtGIXGILightGridList");
        private static readonly int BoundMinId = Shader.PropertyToID("_BurtGIXGILightGridBoundMin");
        private static readonly int BoundMaxId = Shader.PropertyToID("_BurtGIXGILightGridBoundMax");
        private static readonly int AxisId = Shader.PropertyToID("_BurtGIXGILightGridAxis");
        private static readonly int ResolutionId = Shader.PropertyToID("_BurtGIXGILightGridResolution");
        private static readonly int MaxLightsId = Shader.PropertyToID("_BurtGIXGILightGridMaxLights");
        private static readonly int ValidId = Shader.PropertyToID("_BurtGIXGILightGridValid");

        private sealed class CameraState
        {
            public readonly Vector4[] LightData = new Vector4[MaxSceneLights * LightDataRows];
            public readonly uint[] GridCounts = new uint[ClipmapCount * GridResolution * GridResolution];
            public readonly uint[] GridLists = new uint[ClipmapCount * GridResolution * GridResolution * MaxLightsPerCell];
            public readonly Vector4[] BoundMin = new Vector4[ClipmapCount];
            public readonly Vector4[] BoundMax = new Vector4[ClipmapCount];
            public readonly Vector4[] Axis = new Vector4[ClipmapCount];
            public readonly Bounds[] PreviousBounds = new Bounds[ClipmapCount];
            public readonly bool[] PreviousBoundsValid = new bool[ClipmapCount];
            public GraphicsBuffer LightDataBuffer;
            public GraphicsBuffer GridCountBuffer;
            public GraphicsBuffer GridListBuffer;
            public int LightCount;
            public int LastBuildFrame = -RebuildPeriodFrames;
            public bool HasBuilt;
            public bool Valid;
        }

        private static readonly Dictionary<int, CameraState> CameraStates = new Dictionary<int, CameraState>();
        private static readonly List<Light> CandidateLights = new List<Light>(MaxSceneLights);

        public static void BindRayTracing(CommandBuffer cmd, RayTracingShader shader, Camera camera)
        {
            if (cmd == null || shader == null)
            {
                return;
            }

            var state = PrepareState(camera);
            if (state == null)
            {
                return;
            }

            cmd.SetGlobalBuffer(LightDataId, state.LightDataBuffer);
            cmd.SetGlobalBuffer(GridCountId, state.GridCountBuffer);
            cmd.SetGlobalBuffer(GridListId, state.GridListBuffer);
            cmd.SetGlobalVectorArray(BoundMinId, state.BoundMin);
            cmd.SetGlobalVectorArray(BoundMaxId, state.BoundMax);
            cmd.SetGlobalVectorArray(AxisId, state.Axis);
            cmd.SetGlobalInt(ResolutionId, GridResolution);
            cmd.SetGlobalInt(MaxLightsId, MaxLightsPerCell);
            cmd.SetGlobalInt(ValidId, state.Valid ? 1 : 0);
        }

        public static void BindCompute(CommandBuffer cmd, ComputeShader shader, int kernel, Camera camera)
        {
            if (cmd == null || shader == null)
            {
                return;
            }

            var state = PrepareState(camera);
            if (state == null)
            {
                return;
            }

            cmd.SetComputeBufferParam(shader, kernel, LightDataId, state.LightDataBuffer);
            cmd.SetComputeBufferParam(shader, kernel, GridCountId, state.GridCountBuffer);
            cmd.SetComputeBufferParam(shader, kernel, GridListId, state.GridListBuffer);
            cmd.SetComputeVectorArrayParam(shader, BoundMinId, state.BoundMin);
            cmd.SetComputeVectorArrayParam(shader, BoundMaxId, state.BoundMax);
            cmd.SetComputeVectorArrayParam(shader, AxisId, state.Axis);
            cmd.SetComputeIntParam(shader, ResolutionId, GridResolution);
            cmd.SetComputeIntParam(shader, MaxLightsId, MaxLightsPerCell);
            cmd.SetComputeIntParam(shader, ValidId, state.Valid ? 1 : 0);
        }

        public static string GetDebugStatus(Camera camera)
        {
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return "Uninitialized";
            }

            return "Valid=" + state.Valid + ";Lights=" + state.LightCount + ";Grid=" + GridResolution + "x" + GridResolution + ";MaxCell=" + MaxLightsPerCell + ";LastBuild=" + state.LastBuildFrame;
        }

        public static void ReleaseAll()
        {
            foreach (var pair in CameraStates)
            {
                Release(pair.Value);
            }

            CameraStates.Clear();
            CandidateLights.Clear();
        }

        private static CameraState GetOrCreateState(Camera camera)
        {
            if (camera == null)
            {
                return null;
            }

            var cameraId = camera.GetInstanceID();
            if (!CameraStates.TryGetValue(cameraId, out var state))
            {
                state = new CameraState();
                state.LightDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxSceneLights * LightDataRows, sizeof(float) * 4);
                state.GridCountBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, state.GridCounts.Length, sizeof(uint));
                state.GridListBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, state.GridLists.Length, sizeof(uint));
                CameraStates.Add(cameraId, state);
            }

            return state;
        }

        private static CameraState PrepareState(Camera camera)
        {
            var state = GetOrCreateState(camera);
            if (state != null && NeedsRebuild(camera, state))
            {
                Build(camera, state);
            }

            return state;
        }

        private static bool NeedsRebuild(Camera camera, CameraState state)
        {
            if (!state.HasBuilt || Time.frameCount - state.LastBuildFrame >= RebuildPeriodFrames)
            {
                return true;
            }

            for (var level = 0; level < ClipmapCount; ++level)
            {
                var hasBounds = BurtGISceneVoxelClipmapStateUtility.TryGetBounds(camera, level, out var bounds);
                if (hasBounds != state.PreviousBoundsValid[level])
                {
                    return true;
                }

                if (hasBounds && (bounds.center != state.PreviousBounds[level].center || bounds.size != state.PreviousBounds[level].size))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Build(Camera camera, CameraState state)
        {
            state.LastBuildFrame = Time.frameCount;
            state.Valid = false;
            state.LightCount = 0;
            System.Array.Clear(state.LightData, 0, state.LightData.Length);
            System.Array.Clear(state.GridCounts, 0, state.GridCounts.Length);
            System.Array.Clear(state.GridLists, 0, state.GridLists.Length);
            System.Array.Clear(state.BoundMin, 0, state.BoundMin.Length);
            System.Array.Clear(state.BoundMax, 0, state.BoundMax.Length);
            System.Array.Clear(state.Axis, 0, state.Axis.Length);
            if (!BurtGISceneVoxelClipmapStateUtility.TryGetBounds(camera, ClipmapCount - 1, out var outerBounds))
            {
                Upload(state);
                return;
            }

            CandidateLights.Clear();
            var sceneLights = Object.FindObjectsOfType<Light>();
            for (var lightIndex = 0; lightIndex < sceneLights.Length; ++lightIndex)
            {
                var light = sceneLights[lightIndex];
                if (light == null || !light.isActiveAndEnabled || light.intensity <= 0.0001f ||
                    (light.type != LightType.Point && light.type != LightType.Spot))
                {
                    continue;
                }

                if (outerBounds.SqrDistance(light.transform.position) > light.range * light.range)
                {
                    continue;
                }

                CandidateLights.Add(light);
            }

            CandidateLights.Sort((left, right) =>
            {
                return left.GetInstanceID().CompareTo(right.GetInstanceID());
            });

            var lightCount = Mathf.Min(CandidateLights.Count, MaxSceneLights);
            for (var lightIndex = 0; lightIndex < lightCount; ++lightIndex)
            {
                PackLight(CandidateLights[lightIndex], state.LightData, lightIndex);
            }

            state.LightCount = lightCount;
            for (var level = 0; level < ClipmapCount; ++level)
            {
                var hasBounds = BurtGISceneVoxelClipmapStateUtility.TryGetBounds(camera, level, out var bounds);
                state.PreviousBoundsValid[level] = hasBounds;
                if (!hasBounds)
                {
                    continue;
                }

                state.PreviousBounds[level] = bounds;
                var min = bounds.min;
                var max = bounds.max;
                var axis = ResolveShortestAxis(bounds.size);
                state.BoundMin[level] = new Vector4(min.x, min.y, min.z, 0f);
                state.BoundMax[level] = new Vector4(max.x, max.y, max.z, 0f);
                state.Axis[level] = new Vector4(axis, 0f, 0f, 0f);
                BuildLevelGrid(state, level, bounds, axis, lightCount);
            }

            state.Valid = lightCount > 0;
            state.HasBuilt = true;
            Upload(state);
        }

        private static void BuildLevelGrid(CameraState state, int level, Bounds bounds, int axis, int lightCount)
        {
            var min = bounds.min;
            var size = bounds.size;
            for (var y = 0; y < GridResolution; ++y)
            {
                for (var x = 0; x < GridResolution; ++x)
                {
                    var cellIndex = level * GridResolution * GridResolution + x + y * GridResolution;
                    var cellBounds = ResolveGridCellBounds(min, size, axis, x, y);
                    var count = 0;
                    for (var lightIndex = 0; lightIndex < lightCount && count < MaxLightsPerCell; ++lightIndex)
                    {
                        var light = CandidateLights[lightIndex];
                        if (light.type != LightType.Directional && cellBounds.SqrDistance(light.transform.position) > light.range * light.range)
                        {
                            continue;
                        }

                        state.GridLists[cellIndex * MaxLightsPerCell + count] = (uint)lightIndex;
                        ++count;
                    }

                    state.GridCounts[cellIndex] = (uint)count;
                }
            }
        }

        private static Bounds ResolveGridCellBounds(Vector3 min, Vector3 size, int axis, int x, int y)
        {
            var cellMin = min;
            var cellSize = size;
            var step0 = axis == 0 ? size.y / GridResolution : size.x / GridResolution;
            var step1 = axis == 2 ? size.y / GridResolution : size.z / GridResolution;
            if (axis == 0)
            {
                cellMin.y += x * step0;
                cellMin.z += y * step1;
                cellSize.y = step0;
                cellSize.z = step1;
            }
            else if (axis == 1)
            {
                cellMin.x += x * step0;
                cellMin.z += y * step1;
                cellSize.x = step0;
                cellSize.z = step1;
            }
            else
            {
                cellMin.x += x * step0;
                cellMin.y += y * step1;
                cellSize.x = step0;
                cellSize.y = step1;
            }

            return new Bounds(cellMin + cellSize * 0.5f, cellSize);
        }

        private static void PackLight(Light light, Vector4[] target, int lightIndex)
        {
            var baseIndex = lightIndex * LightDataRows;
            var type = light.type == LightType.Directional ? 0f : light.type == LightType.Point ? 1f : 2f;
            var color = light.color.linear * light.intensity;
            var direction = light.type == LightType.Directional ? -light.transform.forward : light.transform.forward;
            var outerCos = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
            var innerAngle = Mathf.Min(light.innerSpotAngle, light.spotAngle);
            var innerCos = Mathf.Cos(innerAngle * 0.5f * Mathf.Deg2Rad);
            target[baseIndex] = new Vector4(light.transform.position.x, light.transform.position.y, light.transform.position.z, light.type == LightType.Directional ? 0f : Mathf.Max(light.range, 0.001f));
            target[baseIndex + 1] = new Vector4(color.r, color.g, color.b, type);
            target[baseIndex + 2] = new Vector4(direction.x, direction.y, direction.z, 0f);
            target[baseIndex + 3] = new Vector4(innerCos, outerCos, 1f / Mathf.Max(innerCos - outerCos, 0.001f), 0f);
        }

        private static int ResolveShortestAxis(Vector3 size)
        {
            return size.x < size.y && size.x < size.z ? 0 : size.y < size.z ? 1 : 2;
        }

        private static void Upload(CameraState state)
        {
            state.LightDataBuffer.SetData(state.LightData);
            state.GridCountBuffer.SetData(state.GridCounts);
            state.GridListBuffer.SetData(state.GridLists);
        }

        private static void Release(CameraState state)
        {
            state.LightDataBuffer?.Release();
            state.GridCountBuffer?.Release();
            state.GridListBuffer?.Release();
        }
    }
}
