using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

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
            IsCompatible(radiance) &&
            IsCompatible(geometry) &&
            radiance.width == geometry.width;

        internal int Resolution => IsReady
            ? BurtScreenSpaceGlobalIlluminationPassUtility.NormalizeSceneVoxelRadianceResolution(radiance.width)
            : BurtScreenSpaceGlobalIlluminationPassUtility.SceneVoxelRadianceResolution;

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

        private static bool IsCompatible(RenderTexture texture)
        {
            return texture != null &&
                texture.IsCreated() &&
                texture.dimension == UnityEngine.Rendering.TextureDimension.Tex3D &&
                texture.width == texture.height &&
                texture.width == texture.volumeDepth &&
                BurtScreenSpaceGlobalIlluminationPassUtility.NormalizeSceneVoxelRadianceResolution(texture.width) == texture.width;
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

    [System.Serializable]
    public struct BurtSerializedGIVoxelLight
    {
        [FormerlySerializedAs("m_Position")]
        public Vector3 position;
        [FormerlySerializedAs("m_Rotation")]
        public Quaternion rotation;
        [FormerlySerializedAs("m_Lossyscale")]
        public Vector3 lossyScale;
        [FormerlySerializedAs("m_VoxelMeshType")]
        public BurtGIVoxelLightShape shape;
        [FormerlySerializedAs("m_VoxelDecreaseRatio")]
        public float voxelDecreaseRatio;
        [FormerlySerializedAs("m_EmissiveColor")]
        public Color emissiveColor;
        [FormerlySerializedAs("m_Luminance")]
        public float luminance;
        [FormerlySerializedAs("m_UseEmissiveFactorMap")]
        public bool useEmissiveFactorMap;
    }

    /// <summary>
    /// Injects an emissive proxy directly into BurtGI scene voxelization.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/BurtRP/XGI Voxel Light")]
    [MovedFrom(true, "XRender.Pipeline.Modules.XGI", "FunPlus.WorldX.XRender.Runtime", "XGIVoxelLight")]
    public sealed class BurtGIVoxelLight : MonoBehaviour
    {
        [FormerlySerializedAs("m_VoxelMeshType")]
        [SerializeField] private BurtGIVoxelLightShape shape = BurtGIVoxelLightShape.Cube;
        [FormerlySerializedAs("m_VoxelDecreaseRatio")]
        [SerializeField] [Range(0f, 0.95f)] private float voxelDecreaseRatio;
        [FormerlySerializedAs("m_EmissiveColor")]
        [SerializeField] private Color emissiveColor = Color.white;
        [FormerlySerializedAs("m_Luminance")]
        [SerializeField] [Min(0f)] private float luminance = 1f;
        [FormerlySerializedAs("m_UseEmissiveFactorMap")]
        [SerializeField] private bool useEmissiveFactorMap = true;

        private static readonly List<BurtGIVoxelLight> ActiveLights = new List<BurtGIVoxelLight>();
        private MeshFilter meshFilter;

        internal static IList<BurtGIVoxelLight> Active => ActiveLights;
        internal BurtGIVoxelLightShape Shape => shape;
        internal float VoxelDecreaseRatio => voxelDecreaseRatio;
        internal Color EmissiveColor => emissiveColor;
        internal float Luminance => luminance;
        internal bool UseEmissiveFactorMap => useEmissiveFactorMap;

        public BurtSerializedGIVoxelLight CaptureSerializedData()
        {
            return new BurtSerializedGIVoxelLight
            {
                position = transform.position,
                rotation = transform.rotation,
                lossyScale = transform.lossyScale,
                shape = shape,
                voxelDecreaseRatio = voxelDecreaseRatio,
                emissiveColor = emissiveColor,
                luminance = luminance,
                useEmissiveFactorMap = useEmissiveFactorMap
            };
        }

        public void ApplySerializedData(BurtSerializedGIVoxelLight data)
        {
            transform.SetPositionAndRotation(data.position, data.rotation);
            transform.localScale = ResolveLocalScaleFromLossyScale(transform.parent, data.lossyScale);

            shape = ClampShape(data.shape);
            voxelDecreaseRatio = Mathf.Clamp(data.voxelDecreaseRatio, 0f, 0.95f);
            emissiveColor = data.emissiveColor;
            luminance = Mathf.Max(0f, data.luminance);
            useEmissiveFactorMap = data.useEmissiveFactorMap;
            UpdateMeshType();
        }

        private void OnEnable()
        {
            UpdateMeshType();
            if (!ActiveLights.Contains(this))
            {
                ActiveLights.Add(this);
            }
        }

        private void OnValidate()
        {
            UpdateMeshType();
        }

        private void Reset()
        {
            UpdateMeshType();
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

        private void UpdateMeshType()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshFilter != null)
            {
                meshFilter.hideFlags = HideFlags.NotEditable;
                meshFilter.sharedMesh = GetBuiltinMesh(shape);
            }
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

        private static BurtGIVoxelLightShape ClampShape(BurtGIVoxelLightShape value)
        {
            return (BurtGIVoxelLightShape)Mathf.Clamp((int)value, 0, (int)BurtGIVoxelLightShape.Quad);
        }

        private static Vector3 ResolveLocalScaleFromLossyScale(Transform parent, Vector3 desiredLossyScale)
        {
            if (parent == null)
            {
                return desiredLossyScale;
            }

            var parentScale = parent.lossyScale;
            return new Vector3(
                SafeDivideScale(desiredLossyScale.x, parentScale.x),
                SafeDivideScale(desiredLossyScale.y, parentScale.y),
                SafeDivideScale(desiredLossyScale.z, parentScale.z));
        }

        private static float SafeDivideScale(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 0.000001f ? value / divisor : value;
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
        private static int currentRadianceResolution = BurtScreenSpaceGlobalIlluminationPassUtility.SceneVoxelRadianceResolution;

        internal sealed class ResourceSet
        {
            internal RenderTexture LeafLow;
            internal RenderTexture LeafHigh;
            internal RenderTexture ParentLow;
            internal RenderTexture ParentHigh;
            internal RenderTexture RootLow;
            internal RenderTexture RootHigh;
            internal bool Valid;
            internal int RadianceResolution = BurtScreenSpaceGlobalIlluminationPassUtility.SceneVoxelRadianceResolution;
        }

        public static int CurrentRadianceResolution => currentRadianceResolution;
        public static int LeafResolution => ComputeLeafResolution(currentRadianceResolution);
        public static int ParentResolution => ComputeParentResolution(currentRadianceResolution);
        public static int RootResolution => ComputeRootResolution(currentRadianceResolution);
        public static bool IsValid => valid && AreResourcesValid();
        internal static RenderTexture FallbackLeafLow => EnsureResources() ? leafLow : null;
        internal static RenderTexture FallbackLeafHigh => EnsureResources() ? leafHigh : null;
        internal static RenderTexture FallbackParentLow => EnsureResources() ? parentLow : null;
        internal static RenderTexture FallbackParentHigh => EnsureResources() ? parentHigh : null;
        internal static RenderTexture FallbackRootLow => EnsureResources() ? rootLow : null;
        internal static RenderTexture FallbackRootHigh => EnsureResources() ? rootHigh : null;
        public static string DebugStatus => IsValid
            ? "Valid(" + LeafResolution + "/" + ParentResolution + "/" + RootResolution + ")"
            : "Fallback(" + LeafResolution + "/" + ParentResolution + "/" + RootResolution + ")";

        public static bool EnsureResources()
        {
            return EnsureResources(currentRadianceResolution);
        }

        public static bool EnsureResources(int radianceResolution)
        {
            var normalizedResolution = BurtScreenSpaceGlobalIlluminationPassUtility.NormalizeSceneVoxelRadianceResolution(radianceResolution);
            if (currentRadianceResolution != normalizedResolution)
            {
                currentRadianceResolution = normalizedResolution;
                Release();
            }

            if (AreResourcesValid())
            {
                return true;
            }

            Release();
            leafLow = CreateTexture(ComputeLeafResolution(currentRadianceResolution), "BurtGI Scene Voxel Octree Leaf Low");
            leafHigh = CreateTexture(ComputeLeafResolution(currentRadianceResolution), "BurtGI Scene Voxel Octree Leaf High");
            parentLow = CreateTexture(ComputeParentResolution(currentRadianceResolution), "BurtGI Scene Voxel Octree Parent Low");
            parentHigh = CreateTexture(ComputeParentResolution(currentRadianceResolution), "BurtGI Scene Voxel Octree Parent High");
            rootLow = CreateTexture(ComputeRootResolution(currentRadianceResolution), "BurtGI Scene Voxel Octree Root Low");
            rootHigh = CreateTexture(ComputeRootResolution(currentRadianceResolution), "BurtGI Scene Voxel Octree Root High");
            valid = false;
            return AreResourcesValid();
        }

        public static void MarkBuilt()
        {
            valid = AreResourcesValid();
        }

        public static void Invalidate()
        {
            valid = false;
        }

        public static void BindCompute(CommandBuffer cmd, ComputeShader shader, int kernel)
        {
            if (cmd == null || shader == null)
            {
                return;
            }

            cmd.SetComputeFloatParam(shader, ValidId, IsValid ? 1.0f : 0.0f);
            if (!EnsureResources())
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
            return EnsureResources(resources, namePrefix, currentRadianceResolution);
        }

        public static bool EnsureResources(ResourceSet resources, string namePrefix, int radianceResolution)
        {
            if (resources == null)
            {
                return false;
            }

            var normalizedResolution = BurtScreenSpaceGlobalIlluminationPassUtility.NormalizeSceneVoxelRadianceResolution(radianceResolution);
            if (resources.RadianceResolution != normalizedResolution)
            {
                Release(resources);
                resources.RadianceResolution = normalizedResolution;
            }

            if (AreResourcesValid(resources))
            {
                return true;
            }

            Release(resources);
            resources.RadianceResolution = normalizedResolution;
            resources.LeafLow = CreateTexture(ComputeLeafResolution(normalizedResolution), namePrefix + " Octree Leaf Low");
            resources.LeafHigh = CreateTexture(ComputeLeafResolution(normalizedResolution), namePrefix + " Octree Leaf High");
            resources.ParentLow = CreateTexture(ComputeParentResolution(normalizedResolution), namePrefix + " Octree Parent Low");
            resources.ParentHigh = CreateTexture(ComputeParentResolution(normalizedResolution), namePrefix + " Octree Parent High");
            resources.RootLow = CreateTexture(ComputeRootResolution(normalizedResolution), namePrefix + " Octree Root Low");
            resources.RootHigh = CreateTexture(ComputeRootResolution(normalizedResolution), namePrefix + " Octree Root High");
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
                BindCompute(cmd, shader, kernel);
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

        private static int ComputeLeafResolution(int radianceResolution)
        {
            return Mathf.Max(1, BurtScreenSpaceGlobalIlluminationPassUtility.NormalizeSceneVoxelRadianceResolution(radianceResolution) / NodeWidth);
        }

        private static int ComputeParentResolution(int radianceResolution)
        {
            return Mathf.Max(1, ComputeLeafResolution(radianceResolution) / NodeWidth);
        }

        private static int ComputeRootResolution(int radianceResolution)
        {
            return Mathf.Max(1, ComputeParentResolution(radianceResolution) / NodeWidth);
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
                IsCreated(rootLow) && IsCreated(rootHigh) &&
                leafLow.width == LeafResolution &&
                parentLow.width == ParentResolution &&
                rootLow.width == RootResolution;
        }

        private static bool AreResourcesValid(ResourceSet resources)
        {
            return resources != null &&
                IsCreated(resources.LeafLow) && IsCreated(resources.LeafHigh) &&
                IsCreated(resources.ParentLow) && IsCreated(resources.ParentHigh) &&
                IsCreated(resources.RootLow) && IsCreated(resources.RootHigh) &&
                resources.LeafLow.width == ComputeLeafResolution(resources.RadianceResolution) &&
                resources.ParentLow.width == ComputeParentResolution(resources.RadianceResolution) &&
                resources.RootLow.width == ComputeRootResolution(resources.RadianceResolution);
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
        internal readonly int RadianceResolution;
        internal readonly RenderTexture Radiance;
        internal readonly RenderTexture Geometry;
        internal readonly RenderTexture OccupancyMip;
        internal readonly RenderTexture Lighting;
        internal readonly RenderTexture ProbePageTable;
        internal readonly RenderTexture ProbeIrradianceSHAmbient;
        internal readonly RenderTexture ProbeIrradianceSHDirectional;
        internal readonly GraphicsBuffer ProbeIndexBuffer;
        internal readonly GraphicsBuffer ProbeArgsBuffer;
        internal readonly GraphicsBuffer ProbeArgsParamsBuffer;
        internal readonly BurtGISceneVoxelOctreeUtility.ResourceSet Octree = new BurtGISceneVoxelOctreeUtility.ResourceSet();
        internal readonly BurtXGISdfGenContext SdfContext = new BurtXGISdfGenContext();
        internal string SdfStatus = "Unconfigured";

        internal int ProbeNodeSize => Mathf.Max(1, RadianceResolution >> 2);
        internal int ProbeIndexOffsetForClipmap => Mathf.Max(1, ProbeNodeSize * ProbeNodeSize * ProbeNodeSize);

        internal BurtGISceneVoxelClipmapResources(int cameraId, int level, int radianceResolution)
        {
            Level = level;
            RadianceResolution = BurtScreenSpaceGlobalIlluminationPassUtility.NormalizeSceneVoxelRadianceResolution(radianceResolution);
            var probeNodeSize = ProbeNodeSize;
            Radiance = CreateTexture(
                BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationSceneVoxelRadianceDescriptor(RadianceResolution),
                "BurtGI Scene Voxel Clipmap " + level + " Radiance " + cameraId,
                FilterMode.Bilinear);
            Geometry = CreateTexture(
                BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationSceneVoxelGeometryDescriptor(RadianceResolution),
                "BurtGI Scene Voxel Clipmap " + level + " Geometry " + cameraId,
                FilterMode.Point);
            OccupancyMip = CreateTexture(
                BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationSceneVoxelOccupancyMipDescriptor(RadianceResolution),
                "BurtGI Scene Voxel Clipmap " + level + " Occupancy " + cameraId,
                FilterMode.Point);
            Lighting = CreateTexture(
                BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationSceneVoxelLightingDescriptor(RadianceResolution),
                "BurtGI Scene Voxel Clipmap " + level + " Lighting " + cameraId,
                FilterMode.Bilinear);
            ProbePageTable = CreateTexture(
                CreateProbePageTableDescriptor(probeNodeSize),
                "BurtGI Scene Voxel Clipmap " + level + " Probe PageTable " + cameraId,
                FilterMode.Point);
            ProbeIrradianceSHAmbient = CreateTexture(
                CreateProbeIrradianceAmbientDescriptor(probeNodeSize),
                "BurtGI Scene Voxel Clipmap " + level + " Probe SH Ambient " + cameraId,
                FilterMode.Bilinear);
            ProbeIrradianceSHDirectional = CreateTexture(
                CreateProbeIrradianceDirectionalDescriptor(probeNodeSize),
                "BurtGI Scene Voxel Clipmap " + level + " Probe SH Directional " + cameraId,
                FilterMode.Bilinear);
            ProbeIndexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                8 + ProbeIndexOffsetForClipmap,
                sizeof(uint))
            {
                name = "BurtGI Scene Voxel Clipmap " + level + " Probe Index " + cameraId
            };
            ProbeArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured,
                3,
                sizeof(uint))
            {
                name = "BurtGI Scene Voxel Clipmap " + level + " Probe Args " + cameraId
            };
            ProbeArgsParamsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                10,
                sizeof(uint))
            {
                name = "BurtGI Scene Voxel Clipmap " + level + " Probe Args Params " + cameraId
            };
        }

        internal bool IsValid =>
            IsCreated(Radiance) &&
            IsCreated(Geometry) &&
            IsCreated(OccupancyMip) &&
            IsCreated(Lighting) &&
            IsCreated(ProbePageTable) &&
            IsCreated(ProbeIrradianceSHAmbient) &&
            IsCreated(ProbeIrradianceSHDirectional) &&
            ProbeIndexBuffer != null &&
            ProbeIndexBuffer.IsValid() &&
            ProbeArgsBuffer != null &&
            ProbeArgsBuffer.IsValid() &&
            ProbeArgsParamsBuffer != null &&
            ProbeArgsParamsBuffer.IsValid();

        internal void Release()
        {
            ReleaseTexture(Radiance);
            ReleaseTexture(Geometry);
            ReleaseTexture(OccupancyMip);
            ReleaseTexture(Lighting);
            ReleaseTexture(ProbePageTable);
            ReleaseTexture(ProbeIrradianceSHAmbient);
            ReleaseTexture(ProbeIrradianceSHDirectional);
            ReleaseBuffer(ProbeIndexBuffer);
            ReleaseBuffer(ProbeArgsBuffer);
            ReleaseBuffer(ProbeArgsParamsBuffer);
            SdfContext.Dispose();
            BurtGISceneVoxelOctreeUtility.Release(Octree);
        }

        internal bool ConfigureSdfContext(bool useOccupy)
        {
            var occupancyResolution = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveSceneVoxelOccupancyMipResolution(RadianceResolution);
            var configured = SdfContext.Configure(
                "BurtGI Scene Voxel Clipmap " + Level + " SDF",
                occupancyResolution,
                1,
                useOccupy);
            SdfStatus = SdfContext.ResolveStatusLabel();
            return configured;
        }

        private static RenderTextureDescriptor CreateProbePageTableDescriptor(int probeNodeSize)
        {
            var descriptor = new RenderTextureDescriptor(
                probeNodeSize,
                probeNodeSize,
                RenderTextureFormat.ARGBInt,
                0)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = probeNodeSize,
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
            descriptor.graphicsFormat = GraphicsFormat.R32G32B32A32_UInt;
            return descriptor;
        }

        private static RenderTextureDescriptor CreateProbeIrradianceAmbientDescriptor(int probeNodeSize)
        {
            return new RenderTextureDescriptor(
                probeNodeSize,
                probeNodeSize,
                RenderTextureFormat.ARGBHalf,
                0)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = probeNodeSize,
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
        }

        private static RenderTextureDescriptor CreateProbeIrradianceDirectionalDescriptor(int probeNodeSize)
        {
            return new RenderTextureDescriptor(
                probeNodeSize * 6,
                probeNodeSize,
                RenderTextureFormat.ARGBHalf,
                0)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = probeNodeSize,
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
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

        private static void ReleaseBuffer(GraphicsBuffer buffer)
        {
            if (buffer == null || !buffer.IsValid())
            {
                return;
            }

            buffer.Release();
        }
    }

    internal static class BurtGISceneVoxelClipmapStateUtility
    {
        internal const int ClipmapCount = 6;
        private static readonly int ClipmapCenterExtentId = Shader.PropertyToID("_BurtGISceneVoxelClipmapCenterExtent");
        private static readonly int ClipmapValidMaskId = Shader.PropertyToID("_BurtGISceneVoxelClipmapValidMask");
        private static readonly int[] ClipmapGeometryTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1GeometryReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2GeometryReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3GeometryReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap4GeometryReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap5GeometryReadTexture")
        };
        private static readonly int[] ClipmapRadianceTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1RadianceReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2RadianceReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3RadianceReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap4RadianceReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap5RadianceReadTexture")
        };
        private static readonly int[] ClipmapOccupancyTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OccupancyMipReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OccupancyMipReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OccupancyMipReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap4OccupancyMipReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap5OccupancyMipReadTexture")
        };
        private static readonly int[] ClipmapLightingTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1LightingReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2LightingReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3LightingReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap4LightingReadTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap5LightingReadTexture")
        };
        private static readonly int[] ClipmapOctreeLeafLowTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OctreeLeafLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OctreeLeafLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OctreeLeafLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap4OctreeLeafLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap5OctreeLeafLowTexture")
        };
        private static readonly int[] ClipmapOctreeLeafHighTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OctreeLeafHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OctreeLeafHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OctreeLeafHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap4OctreeLeafHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap5OctreeLeafHighTexture")
        };
        private static readonly int[] ClipmapOctreeParentLowTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OctreeParentLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OctreeParentLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OctreeParentLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap4OctreeParentLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap5OctreeParentLowTexture")
        };
        private static readonly int[] ClipmapOctreeParentHighTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OctreeParentHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OctreeParentHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OctreeParentHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap4OctreeParentHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap5OctreeParentHighTexture")
        };
        private static readonly int[] ClipmapOctreeRootLowTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OctreeRootLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OctreeRootLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OctreeRootLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap4OctreeRootLowTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap5OctreeRootLowTexture")
        };
        private static readonly int[] ClipmapOctreeRootHighTextureIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelClipmap1OctreeRootHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap2OctreeRootHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap3OctreeRootHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap4OctreeRootHighTexture"),
            Shader.PropertyToID("_BurtGISceneVoxelClipmap5OctreeRootHighTexture")
        };
        private static readonly int ProbeApplyPageTableId = Shader.PropertyToID("_BurtGISceneVoxelProbePageTable");
        private static readonly int ProbeApplyIrradianceSHAmbientId = Shader.PropertyToID("_BurtGISceneVoxelProbeIrradianceSHAmbient");
        private static readonly int ProbeApplyIrradianceSHDirectionalId = Shader.PropertyToID("_BurtGISceneVoxelProbeIrradianceSHDirectional");
        private static readonly int ProbeApplyIndexBufferId = Shader.PropertyToID("_BurtGISceneVoxelProbeIndexBuffer");
        private static readonly int ProbeApplyParamsId = Shader.PropertyToID("_BurtGISceneVoxelProbeParams");
        private static readonly int ProbeApplyCenterExtentId = Shader.PropertyToID("_BurtGISceneVoxelProbeCenterExtent");
        private static readonly int ProbeApplyClipmapParamsId = Shader.PropertyToID("_BurtGISceneVoxelProbeClipmapParams");
        private static readonly int ProbeApplyClipmapCenterExtentId = Shader.PropertyToID("_BurtGISceneVoxelProbeClipmapCenterExtent");
        private static readonly int[] ProbeApplyLevelPageTableIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel1PageTable"),
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel2PageTable"),
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel3PageTable"),
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel4PageTable"),
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel5PageTable")
        };
        private static readonly int[] ProbeApplyLevelIrradianceSHAmbientIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel1IrradianceSHAmbient"),
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel2IrradianceSHAmbient"),
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel3IrradianceSHAmbient"),
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel4IrradianceSHAmbient"),
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel5IrradianceSHAmbient")
        };
        private static readonly int[] ProbeApplyLevelIrradianceSHDirectionalIds =
        {
            0,
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel1IrradianceSHDirectional"),
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel2IrradianceSHDirectional"),
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel3IrradianceSHDirectional"),
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel4IrradianceSHDirectional"),
            Shader.PropertyToID("_BurtGISceneVoxelProbeLevel5IrradianceSHDirectional")
        };

        private static RenderTexture fallbackProbePageTable;
        private static GraphicsBuffer fallbackProbeIndexBuffer;

        private sealed class CameraState
        {
            public readonly Bounds[] Bounds = new Bounds[ClipmapCount];
            public readonly BurtGISceneVoxelClipmapResources[] Resources = new BurtGISceneVoxelClipmapResources[ClipmapCount];
            public uint ValidMask;
            public uint UpdateMask;
            public bool Initialized;
            public int RadianceResolution = BurtScreenSpaceGlobalIlluminationPassUtility.SceneVoxelRadianceResolution;
            public readonly BurtXGISdfGenContext BaseSdfContext = new BurtXGISdfGenContext();
            public string BaseSdfStatus = "Unconfigured";
        }

        private static readonly Dictionary<int, CameraState> CameraStates = new Dictionary<int, CameraState>();

        public static void Update(
            Camera camera,
            Vector4 baseCenterExtent,
            float distributionBase,
            Vector3 clipmapForward,
            Vector4 clipmapOffset03,
            Vector4 clipmapUpdateDistance03,
            Vector4 clipmapOffset47,
            Vector4 clipmapUpdateDistance47,
            bool forceUpdate,
            float originUpdateDistance,
            int activeClipmapCount,
            int radianceResolution)
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

            var normalizedResolution = BurtScreenSpaceGlobalIlluminationPassUtility.NormalizeSceneVoxelRadianceResolution(radianceResolution);
            if (state.RadianceResolution != normalizedResolution)
            {
                for (var level = 0; level < ClipmapCount; ++level)
                {
                    state.Resources[level]?.Release();
                    state.Resources[level] = null;
                }

                state.BaseSdfContext.Dispose();
                state.BaseSdfStatus = "Unconfigured";
                state.ValidMask = 0u;
                state.UpdateMask = (1u << ClipmapCount) - 1u;
                state.RadianceResolution = normalizedResolution;
            }

            var activeCount = Mathf.Clamp(activeClipmapCount, 1, ClipmapCount);
            for (var level = activeCount; level < ClipmapCount; ++level)
            {
                state.ValidMask &= ~(1u << level);
                state.UpdateMask &= ~(1u << level);
                state.Bounds[level] = default;
                state.Resources[level]?.Release();
                state.Resources[level] = null;
            }

            for (var level = 0; level < activeCount; ++level)
            {
                var extent = Mathf.Max(0.001f, baseCenterExtent.w * Mathf.Pow(Mathf.Max(1f, distributionBase), level));
                var cellSize = extent * 2f / state.RadianceResolution;
                var offsetVector = level < 4 ? clipmapOffset03 : clipmapOffset47;
                var updateDistanceVector = level < 4 ? clipmapUpdateDistance03 : clipmapUpdateDistance47;
                var vectorIndex = level < 4 ? level : level - 4;
                var levelUpdateDistance = Mathf.Max(0.0001f, ResolveClipmapVectorComponent(updateDistanceVector, vectorIndex, originUpdateDistance));
                var levelOffset = Mathf.Max(0f, ResolveClipmapVectorComponent(offsetVector, vectorIndex, 0f));
                levelOffset = Mathf.Min(levelOffset, levelUpdateDistance * 0.33f);
                var centerSource = new Vector3(baseCenterExtent.x, baseCenterExtent.y, baseCenterExtent.z) +
                    ResolveClipmapForward(clipmapForward) * levelOffset;
                var center = new Vector3(
                    Mathf.Round(centerSource.x / cellSize) * cellSize,
                    Mathf.Round(centerSource.y / cellSize) * cellSize,
                    Mathf.Round(centerSource.z / cellSize) * cellSize);
                var bounds = new Bounds(center, Vector3.one * extent * 2f);
                var movementThreshold = forceUpdate ? 0f : Mathf.Max(cellSize, levelUpdateDistance);
                var changed = forceUpdate || !state.Initialized || state.Bounds[level].size != bounds.size ||
                    Vector3.Distance(state.Bounds[level].center, bounds.center) > movementThreshold;
                if (changed)
                {
                    state.Bounds[level] = bounds;
                    state.ValidMask &= ~(1u << level);
                    state.UpdateMask |= 1u << level;
                }
            }

            state.Initialized = true;
        }

        private static float ResolveClipmapVectorComponent(Vector4 value, int index, float fallback)
        {
            switch (index)
            {
                case 0:
                    return value.x;
                case 1:
                    return value.y;
                case 2:
                    return value.z;
                case 3:
                    return value.w;
                default:
                    return fallback;
            }
        }

        private static Vector3 ResolveClipmapForward(Vector3 clipmapForward)
        {
            clipmapForward.y = 0f;
            return clipmapForward.sqrMagnitude > 0.000001f ? clipmapForward.normalized : Vector3.forward;
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

            if (state.Resources[level] == null || state.Resources[level].RadianceResolution != state.RadianceResolution || !state.Resources[level].IsValid)
            {
                state.Resources[level]?.Release();
                state.Resources[level] = new BurtGISceneVoxelClipmapResources(camera.GetInstanceID(), level, state.RadianceResolution);
            }

            resources = state.Resources[level];
            var bounds = state.Bounds[level];
            centerExtent = new Vector4(bounds.center.x, bounds.center.y, bounds.center.z, bounds.extents.x);
            return resources.IsValid;
        }

        public static bool TryGetBaseSdfContext(Camera camera, int radianceResolution, bool useOccupy, out BurtXGISdfGenContext sdfContext, out string status)
        {
            sdfContext = null;
            status = "Uninitialized";
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return false;
            }

            var occupancyResolution = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveSceneVoxelOccupancyMipResolution(radianceResolution);
            var configured = state.BaseSdfContext.Configure(
                "BurtGI Scene Voxel Base SDF " + camera.GetInstanceID(),
                occupancyResolution,
                1,
                useOccupy);
            state.BaseSdfStatus = state.BaseSdfContext.ResolveStatusLabel();
            sdfContext = state.BaseSdfContext;
            status = state.BaseSdfStatus;
            return configured;
        }

        public static void SetBaseSdfStatus(Camera camera, string status)
        {
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return;
            }

            state.BaseSdfStatus = string.IsNullOrEmpty(status) ? "Unknown" : status;
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

            BindFallbackTraceTextures(cmd, shader, kernel, fallbackGeometry, fallbackOccupancy, fallbackLighting);

            var centerExtents = new Vector4[ClipmapCount];
            for (var level = 0; level < ClipmapCount; ++level)
            {
                centerExtents[level] = baseCenterExtent;
            }

            for (var level = 1; level < ClipmapCount; ++level)
            {
                BindFallbackOctreeTextures(cmd, shader, kernel, level);
            }

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
                        continue;
                    }

                    validMask |= 1u << level;
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapGeometryTextureIds[level], resources.Geometry);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapRadianceTextureIds[level], resources.Radiance);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapOccupancyTextureIds[level], resources.OccupancyMip);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapLightingTextureIds[level], resources.Lighting);
                    BindOctreeTextures(cmd, shader, kernel, level, resources.Octree);
                }
            }
            cmd.SetComputeVectorArrayParam(shader, ClipmapCenterExtentId, centerExtents);
            cmd.SetComputeIntParam(shader, ClipmapValidMaskId, (int)validMask);
        }

        public static void BindTranslucencyTraceCompute(
            CommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            Camera camera,
            Vector4 baseCenterExtent,
            RenderTargetIdentifier fallbackGeometry,
            RenderTargetIdentifier fallbackLighting)
        {
            if (cmd == null || shader == null)
            {
                return;
            }

            BindFallbackTranslucencyTraceTextures(cmd, shader, kernel, fallbackGeometry, fallbackLighting);

            var centerExtents = new Vector4[ClipmapCount];
            for (var level = 0; level < ClipmapCount; ++level)
            {
                centerExtents[level] = baseCenterExtent;
            }

            uint validMask = 1u;
            if (camera != null && CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                for (var level = 1; level < ClipmapCount; ++level)
                {
                    var bounds = state.Bounds[level];
                    centerExtents[level] = new Vector4(bounds.center.x, bounds.center.y, bounds.center.z, bounds.extents.x);
                    var resources = state.Resources[level];
                    var levelValid = (state.ValidMask & (1u << level)) != 0u && resources != null && resources.IsValid;
                    if (!levelValid)
                    {
                        continue;
                    }

                    validMask |= 1u << level;
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapGeometryTextureIds[level], resources.Geometry);
                    cmd.SetComputeTextureParam(shader, kernel, ClipmapLightingTextureIds[level], resources.Lighting);
                }
            }

            cmd.SetComputeVectorArrayParam(shader, ClipmapCenterExtentId, centerExtents);
            cmd.SetComputeIntParam(shader, ClipmapValidMaskId, (int)validMask);
        }

        public static void BindTraceFallbackCompute(CommandBuffer cmd, ComputeShader shader, int kernel)
        {
            if (cmd == null || shader == null)
            {
                return;
            }

            var fallback = new RenderTargetIdentifier(BurtGITranslucencyVolumeFallbackUtility.BlackVolumeRenderTexture);
            BindFallbackTraceTextures(cmd, shader, kernel, fallback, fallback, fallback);
            for (var level = 1; level < ClipmapCount; ++level)
            {
                BindFallbackOctreeTextures(cmd, shader, kernel, level);
            }
            cmd.SetComputeVectorArrayParam(shader, ClipmapCenterExtentId, new Vector4[ClipmapCount]);
            cmd.SetComputeIntParam(shader, ClipmapValidMaskId, 1);
        }

        private static void BindFallbackTraceTextures(
            CommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            RenderTargetIdentifier fallbackGeometry,
            RenderTargetIdentifier fallbackOccupancy,
            RenderTargetIdentifier fallbackLighting)
        {
            var fallbackRadiance = new RenderTargetIdentifier(BurtGITranslucencyVolumeFallbackUtility.BlackVolumeRenderTexture);
            for (var level = 1; level < ClipmapCount; ++level)
            {
                cmd.SetComputeTextureParam(shader, kernel, ClipmapGeometryTextureIds[level], fallbackGeometry);
                cmd.SetComputeTextureParam(shader, kernel, ClipmapRadianceTextureIds[level], fallbackRadiance);
                cmd.SetComputeTextureParam(shader, kernel, ClipmapOccupancyTextureIds[level], fallbackOccupancy);
                cmd.SetComputeTextureParam(shader, kernel, ClipmapLightingTextureIds[level], fallbackLighting);
            }
        }

        private static void BindFallbackTranslucencyTraceTextures(
            CommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            RenderTargetIdentifier fallbackGeometry,
            RenderTargetIdentifier fallbackLighting)
        {
            for (var level = 1; level < ClipmapCount; ++level)
            {
                cmd.SetComputeTextureParam(shader, kernel, ClipmapGeometryTextureIds[level], fallbackGeometry);
                cmd.SetComputeTextureParam(shader, kernel, ClipmapLightingTextureIds[level], fallbackLighting);
            }
        }

        private static void BindOctreeTextures(CommandBuffer cmd, ComputeShader shader, int kernel, int level, BurtGISceneVoxelOctreeUtility.ResourceSet octree)
        {
            if (octree == null || !octree.Valid)
            {
                BindFallbackOctreeTextures(cmd, shader, kernel, level);
                return;
            }

            cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeLeafLowTextureIds[level], octree.LeafLow);
            cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeLeafHighTextureIds[level], octree.LeafHigh);
            cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeParentLowTextureIds[level], octree.ParentLow);
            cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeParentHighTextureIds[level], octree.ParentHigh);
            cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeRootLowTextureIds[level], octree.RootLow);
            cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeRootHighTextureIds[level], octree.RootHigh);
        }

        private static void BindFallbackOctreeTextures(CommandBuffer cmd, ComputeShader shader, int kernel, int level)
        {
            cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeLeafLowTextureIds[level], BurtGISceneVoxelOctreeUtility.FallbackLeafLow);
            cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeLeafHighTextureIds[level], BurtGISceneVoxelOctreeUtility.FallbackLeafHigh);
            cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeParentLowTextureIds[level], BurtGISceneVoxelOctreeUtility.FallbackParentLow);
            cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeParentHighTextureIds[level], BurtGISceneVoxelOctreeUtility.FallbackParentHigh);
            cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeRootLowTextureIds[level], BurtGISceneVoxelOctreeUtility.FallbackRootLow);
            cmd.SetComputeTextureParam(shader, kernel, ClipmapOctreeRootHighTextureIds[level], BurtGISceneVoxelOctreeUtility.FallbackRootHigh);
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

            var resourceStatus = ";L0SDF=" + state.BaseSdfStatus;
            for (var level = 1; level < ClipmapCount; ++level)
            {
                var resources = state.Resources[level];
                resourceStatus += ";L" + level + "=" + (resources != null && resources.IsValid ? (resources.Octree.Valid ? "Ready" : "OctreePending") : "Unallocated");
                if (resources != null)
                {
                    resourceStatus += "/SDF=" + resources.SdfStatus;
                }
            }

            return "Valid=0x" + state.ValidMask.ToString("X") + ";Update=0x" + state.UpdateMask.ToString("X") + resourceStatus;
        }

        public static bool HasValidTraceResources(Camera camera)
        {
            if (BurtGISceneVoxelOctreeUtility.IsValid)
            {
                return true;
            }

            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return false;
            }

            for (var level = 1; level < ClipmapCount; ++level)
            {
                var resources = state.Resources[level];
                if ((state.ValidMask & (1u << level)) != 0u && resources != null && resources.IsValid && resources.Octree.Valid)
                {
                    return true;
                }
            }

            return false;
        }

        public static void UploadProbeApplyGlobals(CommandBuffer cmd, Camera camera)
        {
            if (cmd == null)
            {
                return;
            }

            if (TryResolveProbeApplyResources(camera, out var resources, out var centerExtent))
            {
                cmd.SetGlobalTexture(ProbeApplyPageTableId, resources.ProbePageTable);
                cmd.SetGlobalTexture(ProbeApplyIrradianceSHAmbientId, resources.ProbeIrradianceSHAmbient);
                cmd.SetGlobalTexture(ProbeApplyIrradianceSHDirectionalId, resources.ProbeIrradianceSHDirectional);
                cmd.SetGlobalBuffer(ProbeApplyIndexBufferId, resources.ProbeIndexBuffer);
                cmd.SetGlobalVector(ProbeApplyParamsId, new Vector4(1f, 1f, resources.ProbeNodeSize, resources.ProbeIndexOffsetForClipmap));
                cmd.SetGlobalVector(ProbeApplyCenterExtentId, centerExtent);
                BindProbeApplyClipmapGlobals(cmd, camera, true);
                return;
            }

            cmd.SetGlobalTexture(ProbeApplyPageTableId, FallbackProbePageTable);
            cmd.SetGlobalTexture(ProbeApplyIrradianceSHAmbientId, BurtGITranslucencyVolumeFallbackUtility.BlackVolumeRenderTexture);
            cmd.SetGlobalTexture(ProbeApplyIrradianceSHDirectionalId, BurtGITranslucencyVolumeFallbackUtility.BlackVolumeRenderTexture);
            cmd.SetGlobalBuffer(ProbeApplyIndexBufferId, FallbackProbeIndexBuffer);
            cmd.SetGlobalVector(ProbeApplyParamsId, Vector4.zero);
            cmd.SetGlobalVector(ProbeApplyCenterExtentId, Vector4.zero);
            BindProbeApplyClipmapGlobals(cmd, camera, false);
        }

        internal static bool HasProbeApplyResources(Camera camera)
        {
            return TryResolveProbeApplyResources(camera, out _, out _);
        }

        private static void BindProbeApplyClipmapGlobals(CommandBuffer cmd, Camera camera, bool enabled)
        {
            var clipmapParams = new Vector4[ClipmapCount];
            var clipmapCenterExtents = new Vector4[ClipmapCount];
            for (var level = 1; level < ClipmapCount; ++level)
            {
                cmd.SetGlobalTexture(ProbeApplyLevelPageTableIds[level], FallbackProbePageTable);
                cmd.SetGlobalTexture(ProbeApplyLevelIrradianceSHAmbientIds[level], BurtGITranslucencyVolumeFallbackUtility.BlackVolumeRenderTexture);
                cmd.SetGlobalTexture(ProbeApplyLevelIrradianceSHDirectionalIds[level], BurtGITranslucencyVolumeFallbackUtility.BlackVolumeRenderTexture);
            }

            if (enabled && camera != null && CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                for (var level = 1; level < ClipmapCount; ++level)
                {
                    var resources = state.Resources[level];
                    if ((state.ValidMask & (1u << level)) == 0u || resources == null || !resources.IsValid)
                    {
                        continue;
                    }

                    var bounds = state.Bounds[level];
                    if (bounds.size.sqrMagnitude <= 0.000001f)
                    {
                        continue;
                    }

                    cmd.SetGlobalTexture(ProbeApplyLevelPageTableIds[level], resources.ProbePageTable);
                    cmd.SetGlobalTexture(ProbeApplyLevelIrradianceSHAmbientIds[level], resources.ProbeIrradianceSHAmbient);
                    cmd.SetGlobalTexture(ProbeApplyLevelIrradianceSHDirectionalIds[level], resources.ProbeIrradianceSHDirectional);
                    clipmapParams[level] = new Vector4(1f, resources.ProbeNodeSize, resources.ProbeIndexOffsetForClipmap, 0f);
                    clipmapCenterExtents[level] = new Vector4(bounds.center.x, bounds.center.y, bounds.center.z, bounds.extents.x);
                }
            }

            cmd.SetGlobalVectorArray(ProbeApplyClipmapParamsId, clipmapParams);
            cmd.SetGlobalVectorArray(ProbeApplyClipmapCenterExtentId, clipmapCenterExtents);
        }

        private static bool TryResolveProbeApplyResources(Camera camera, out BurtGISceneVoxelClipmapResources resources, out Vector4 centerExtent)
        {
            resources = null;
            centerExtent = Vector4.zero;
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return false;
            }

            for (var level = 1; level < ClipmapCount; ++level)
            {
                var candidate = state.Resources[level];
                if ((state.ValidMask & (1u << level)) == 0u || candidate == null || !candidate.IsValid)
                {
                    continue;
                }

                var bounds = state.Bounds[level];
                if (bounds.size.sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                resources = candidate;
                centerExtent = new Vector4(bounds.center.x, bounds.center.y, bounds.center.z, bounds.extents.x);
                return true;
            }

            return false;
        }

        private static RenderTexture FallbackProbePageTable
        {
            get
            {
                if (fallbackProbePageTable != null && fallbackProbePageTable.IsCreated())
                {
                    return fallbackProbePageTable;
                }

                if (fallbackProbePageTable != null)
                {
                    fallbackProbePageTable.Release();
                }

                var descriptor = new RenderTextureDescriptor(1, 1, RenderTextureFormat.ARGBInt, 0)
                {
                    dimension = TextureDimension.Tex3D,
                    volumeDepth = 1,
                    enableRandomWrite = true,
                    msaaSamples = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    sRGB = false
                };
                descriptor.graphicsFormat = GraphicsFormat.R32G32B32A32_UInt;

                fallbackProbePageTable = new RenderTexture(descriptor)
                {
                    name = "BurtGI Scene Voxel Probe PageTable Fallback",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                fallbackProbePageTable.Create();
                return fallbackProbePageTable;
            }
        }

        private static GraphicsBuffer FallbackProbeIndexBuffer
        {
            get
            {
                if (fallbackProbeIndexBuffer != null && fallbackProbeIndexBuffer.IsValid())
                {
                    return fallbackProbeIndexBuffer;
                }

                fallbackProbeIndexBuffer?.Release();
                fallbackProbeIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(uint))
                {
                    name = "BurtGI Scene Voxel Probe Index Fallback"
                };
                fallbackProbeIndexBuffer.SetData(new uint[1]);
                return fallbackProbeIndexBuffer;
            }
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

                pair.Value.BaseSdfContext.Dispose();
                pair.Value.BaseSdfStatus = "Unconfigured";
            }

            CameraStates.Clear();
            if (fallbackProbePageTable != null)
            {
                fallbackProbePageTable.Release();
                UnityEngine.Object.DestroyImmediate(fallbackProbePageTable);
                fallbackProbePageTable = null;
            }

            fallbackProbeIndexBuffer?.Release();
            fallbackProbeIndexBuffer = null;
        }
    }

    internal static class BurtGIXGILightGridUtility
    {
        private const int ClipmapCount = BurtGISceneVoxelClipmapStateUtility.ClipmapCount;
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
            cmd.SetRayTracingVectorArrayParam(shader, BoundMinId, state.BoundMin);
            cmd.SetRayTracingVectorArrayParam(shader, BoundMaxId, state.BoundMax);
            cmd.SetRayTracingVectorArrayParam(shader, AxisId, state.Axis);
            cmd.SetRayTracingIntParam(shader, ResolutionId, GridResolution);
            cmd.SetRayTracingIntParam(shader, MaxLightsId, MaxLightsPerCell);
            cmd.SetRayTracingIntParam(shader, ValidId, state.Valid ? 1 : 0);
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
                        if (!LightIntersectsGridCell(light, cellBounds))
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

        private static bool LightIntersectsGridCell(Light light, Bounds cellBounds)
        {
            if (light == null)
            {
                return false;
            }

            var radius = Mathf.Max(light.range, 0.0f);
            if (radius <= 0.0001f || cellBounds.SqrDistance(light.transform.position) > radius * radius)
            {
                return false;
            }

            if (light.type != LightType.Spot)
            {
                return true;
            }

            var center = light.transform.position;
            var direction = light.transform.forward;
            var cellMin = cellBounds.min;
            var cellMax = cellBounds.max;
            var halfSpaceCorner = new Vector3(
                direction.x > 0.0f ? cellMax.x : cellMin.x,
                direction.y > 0.0f ? cellMax.y : cellMin.y,
                direction.z > 0.0f ? cellMax.z : cellMin.z);
            if (Vector3.Dot(halfSpaceCorner - center, direction) < 0.0f)
            {
                return false;
            }

            var cosOuter = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
            var sinOuter = Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - cosOuter * cosOuter));
            var disc = new Vector3(
                Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - direction.x * direction.x)),
                Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - direction.y * direction.y)),
                Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - direction.z * direction.z)));
            var tip = center + direction * radius;
            var coneMin = Vector3.Min(center, tip);
            var coneMax = Vector3.Max(center, tip);
            coneMin = Vector3.Min(coneMin, center + radius * (direction * cosOuter - disc * sinOuter));
            coneMax = Vector3.Max(coneMax, center + radius * (direction * cosOuter + disc * sinOuter));

            return !(cellMax.x < coneMin.x || cellMax.y < coneMin.y || cellMax.z < coneMin.z ||
                cellMin.x > coneMax.x || cellMin.y > coneMax.y || cellMin.z > coneMax.z);
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
