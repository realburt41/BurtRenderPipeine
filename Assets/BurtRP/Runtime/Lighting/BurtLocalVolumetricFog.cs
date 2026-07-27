using System.Collections.Generic;
using UnityEngine;

namespace Burt.RenderPipeline
{
    public enum BurtLocalVolumetricFogBlendMode
    {
        Overwrite = 0,
        Additive = 1
    }

    public enum BurtLocalVolumetricFogFalloffMode
    {
        Linear = 0,
        Exponential = 1
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/Burt Local Volumetric Fog")]
    public sealed class BurtLocalVolumetricFog : MonoBehaviour
    {
        [Header("Material")]
        public BurtLocalVolumetricFogBlendMode blendMode = BurtLocalVolumetricFogBlendMode.Additive;

        [ColorUsage(false)] public Color albedo = Color.white;
        public bool useVerticalColorGradient;
        [ColorUsage(false)] public Color albedoTop = Color.white;
        [Range(0f, 1f)] public float extinction = 0.1f;
        [Range(0f, 8f)] public float heightFalloff;

        [Tooltip("Optional repeating 3D density texture. White keeps the analytic density unchanged.")]
        public Texture3D densityTexture;
        public Vector3 textureTiling = Vector3.one;
        public Vector3 textureScrollingSpeed;

        [Header("Oriented Box")]
        [Tooltip("World-space box size before the GameObject rotation is applied.")]
        public Vector3 size = Vector3.one;

        [Tooltip("Normalized fade width measured inward from the local positive X/Y/Z faces. 0 disables that face fade.")]
        public Vector3 positiveFaceFade = Vector3.one * 0.1f;

        [Tooltip("Normalized fade width measured inward from the local negative X/Y/Z faces. 0 disables that face fade.")]
        public Vector3 negativeFaceFade = Vector3.one * 0.1f;

        public BurtLocalVolumetricFogFalloffMode falloffMode = BurtLocalVolumetricFogFalloffMode.Linear;
        public bool invertFade;

        [Header("Distance Fade")]
        [Min(0f)] public float distanceFadeStart = 10000f;
        [Min(0f)] public float distanceFadeEnd = 10000f;

        internal Matrix4x4 VolumeLocalToWorldMatrix => Matrix4x4.TRS(transform.position, transform.rotation, EffectiveSize);
        internal Matrix4x4 WorldToVolumeLocalMatrix => VolumeLocalToWorldMatrix.inverse;
        internal Vector3 EffectiveSize => new Vector3(
            Mathf.Max(Mathf.Abs(size.x), 0.001f),
            Mathf.Max(Mathf.Abs(size.y), 0.001f),
            Mathf.Max(Mathf.Abs(size.z), 0.001f));

        internal Bounds WorldBounds
        {
            get
            {
                var halfSize = EffectiveSize * 0.5f;
                var rotation = transform.rotation;
                var right = rotation * Vector3.right * halfSize.x;
                var up = rotation * Vector3.up * halfSize.y;
                var forward = rotation * Vector3.forward * halfSize.z;
                var extents = new Vector3(
                    Mathf.Abs(right.x) + Mathf.Abs(up.x) + Mathf.Abs(forward.x),
                    Mathf.Abs(right.y) + Mathf.Abs(up.y) + Mathf.Abs(forward.y),
                    Mathf.Abs(right.z) + Mathf.Abs(up.z) + Mathf.Abs(forward.z));
                return new Bounds(transform.position, extents * 2f);
            }
        }

        private void OnEnable()
        {
            Sanitize();
            BurtLocalVolumetricFogRegistry.Register(this);
        }

        private void OnDisable()
        {
            BurtLocalVolumetricFogRegistry.Unregister(this);
        }

        private void OnDestroy()
        {
            BurtLocalVolumetricFogRegistry.Unregister(this);
        }

        private void OnValidate()
        {
            Sanitize();
            if (isActiveAndEnabled)
            {
                BurtLocalVolumetricFogRegistry.Register(this);
            }
        }

        private void Sanitize()
        {
            size = EffectiveSize;
            positiveFaceFade = ClampFade(positiveFaceFade);
            negativeFaceFade = ClampFade(negativeFaceFade);
            extinction = Mathf.Clamp01(extinction);
            heightFalloff = Mathf.Clamp(heightFalloff, 0f, 8f);
            distanceFadeStart = Mathf.Max(0f, distanceFadeStart);
            distanceFadeEnd = Mathf.Max(distanceFadeStart, distanceFadeEnd);
        }

        private static Vector3 ClampFade(Vector3 value)
        {
            return new Vector3(
                Mathf.Clamp01(value.x),
                Mathf.Clamp01(value.y),
                Mathf.Clamp01(value.z));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            Gizmos.matrix = VolumeLocalToWorldMatrix;
            Gizmos.color = new Color(albedo.r, albedo.g, albedo.b, 0.85f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
#endif
    }

    internal static class BurtLocalVolumetricFogRegistry
    {
        private const int FrustumPlaneCount = 6;
        private static readonly List<BurtLocalVolumetricFog> Components = new List<BurtLocalVolumetricFog>();
        private static readonly List<BurtLocalVolumetricFog> Visible = new List<BurtLocalVolumetricFog>();
        private static readonly Plane[] FrustumPlanes = new Plane[FrustumPlaneCount];
        private static Vector3 sortOrigin;

        public static int ActiveCount
        {
            get
            {
                PruneInvalid();
                return Components.Count;
            }
        }

        public static void Register(BurtLocalVolumetricFog component)
        {
            if (component != null && !Components.Contains(component))
            {
                Components.Add(component);
            }
        }

        public static void Unregister(BurtLocalVolumetricFog component)
        {
            if (component != null)
            {
                Components.Remove(component);
            }
        }

        public static int CollectVisible(
            Camera camera,
            float visibleDistance,
            int maxCount,
            List<BurtLocalVolumetricFog> output)
        {
            output.Clear();
            Visible.Clear();
            PruneInvalid();
            if (camera == null || maxCount <= 0)
            {
                return 0;
            }

            GeometryUtility.CalculateFrustumPlanes(camera, FrustumPlanes);
            sortOrigin = camera.transform.position;
            var cullingMask = camera.cullingMask;
            for (var index = 0; index < Components.Count; index++)
            {
                var component = Components[index];
                if (!IsValid(component) || (cullingMask & (1 << component.gameObject.layer)) == 0)
                {
                    continue;
                }

                var bounds = component.WorldBounds;
                var radius = component.EffectiveSize.magnitude;
                var minimumDistance = Vector3.Distance(sortOrigin, component.transform.position)
                    - camera.nearClipPlane
                    - radius;
                if (minimumDistance > visibleDistance || !GeometryUtility.TestPlanesAABB(FrustumPlanes, bounds))
                {
                    continue;
                }

                Visible.Add(component);
            }

            Visible.Sort(CompareDistance);
            var count = Mathf.Min(maxCount, Visible.Count);
            for (var index = 0; index < count; index++)
            {
                output.Add(Visible[index]);
            }

            Visible.Clear();
            return output.Count;
        }

        private static int CompareDistance(BurtLocalVolumetricFog a, BurtLocalVolumetricFog b)
        {
            var distanceA = (a.transform.position - sortOrigin).sqrMagnitude;
            var distanceB = (b.transform.position - sortOrigin).sqrMagnitude;
            var distanceComparison = distanceA.CompareTo(distanceB);
            return distanceComparison != 0
                ? distanceComparison
                : a.GetInstanceID().CompareTo(b.GetInstanceID());
        }

        private static void PruneInvalid()
        {
            for (var index = Components.Count - 1; index >= 0; index--)
            {
                if (!IsValid(Components[index]))
                {
                    Components.RemoveAt(index);
                }
            }
        }

        private static bool IsValid(BurtLocalVolumetricFog component)
        {
            return component != null && component.isActiveAndEnabled && component.gameObject.activeInHierarchy;
        }
    }
}
