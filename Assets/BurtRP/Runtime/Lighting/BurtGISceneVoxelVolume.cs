using System.Collections.Generic;
using UnityEngine;

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
}
