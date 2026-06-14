using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class BurtPerObjectShadow : MonoBehaviour
    {
        public const int MinSliceResolution = 64;
        public const int MaxSliceResolution = 4096;
        public const float DefaultNormalBias = 5f;
        internal const uint MainLightRenderingLayerMask = 0xFFu;
        internal const uint PerObjectShadowRenderingLayerMask = 1u << 22;

        [SerializeField]
        private int priority;

        [SerializeField]
        private int sliceResolution = 1024;

        [SerializeField]
        private float padding = 0.02f;

        [SerializeField]
        [Range(0f, 1f)]
        private float strength = 1f;

        [SerializeField]
        private float receiverDepthBias = 0.0005f;

        [SerializeField]
        private float normalBias = DefaultNormalBias;

        [SerializeField]
        private bool includeInactiveRenderers;

        private readonly Dictionary<Renderer, uint> originalRenderingLayerMasks = new Dictionary<Renderer, uint>();
        private readonly Dictionary<BurtMultipassRenderer, int> originalMultipassRenderingLayerMasks = new Dictionary<BurtMultipassRenderer, int>();
        private readonly List<Renderer> rendererScratch = new List<Renderer>();
        private readonly List<BurtMultipassRenderer> multipassRendererScratch = new List<BurtMultipassRenderer>();
        private readonly List<Renderer> restoreScratch = new List<Renderer>();
        private readonly List<BurtMultipassRenderer> restoreMultipassScratch = new List<BurtMultipassRenderer>();

        public int Priority => priority;

        public int SliceResolution => Mathf.Clamp(sliceResolution, MinSliceResolution, MaxSliceResolution);

        public float Padding => Mathf.Max(0f, padding);

        public float Strength => Mathf.Clamp01(strength);

        public float ReceiverDepthBias => Mathf.Max(0f, receiverDepthBias);

        public float NormalBias => Mathf.Max(0f, normalBias);

        public bool IncludeInactiveRenderers => includeInactiveRenderers;

        public bool IsRenderable => isActiveAndEnabled && gameObject != null && gameObject.activeInHierarchy && Strength > 0f;

        private void OnEnable()
        {
            BurtPerObjectShadowRegistry.Register(this);
            SyncRendererLayerOverrides();
        }

        private void OnDisable()
        {
            RestoreRendererLayerOverrides();
            BurtPerObjectShadowRegistry.Unregister(this);
        }

        private void OnDestroy()
        {
            RestoreRendererLayerOverrides();
        }

        private void OnValidate()
        {
            sliceResolution = Mathf.Clamp(sliceResolution, MinSliceResolution, MaxSliceResolution);
            padding = Mathf.Max(0f, padding);
            strength = Mathf.Clamp01(strength);
            receiverDepthBias = Mathf.Max(0f, receiverDepthBias);
            normalBias = Mathf.Max(0f, normalBias);

            if (isActiveAndEnabled)
            {
                BurtPerObjectShadowRegistry.Register(this);
                SyncRendererLayerOverrides();
            }
            else
            {
                RestoreRendererLayerOverrides();
            }
        }

        private void Update()
        {
            SyncRendererLayerOverrides();
        }

        private void OnTransformChildrenChanged()
        {
            SyncRendererLayerOverrides();
        }

        internal void CollectRenderers(List<Renderer> renderers)
        {
            if (renderers == null)
            {
                return;
            }

            GetComponentsInChildren(includeInactiveRenderers, renderers);
        }

        internal void CollectMultipassRenderers(List<BurtMultipassRenderer> renderers)
        {
            if (renderers == null)
            {
                return;
            }

            GetComponentsInChildren(includeInactiveRenderers, renderers);
        }

        private void SyncRendererLayerOverrides()
        {
            if (!IsRenderable)
            {
                RestoreRendererLayerOverrides();
                return;
            }

            rendererScratch.Clear();
            GetComponentsInChildren(includeInactiveRenderers, rendererScratch);

            restoreScratch.Clear();
            foreach (var entry in originalRenderingLayerMasks)
            {
                var renderer = entry.Key;
                if (renderer == null || !rendererScratch.Contains(renderer))
                {
                    restoreScratch.Add(renderer);
                }
            }

            for (var index = 0; index < restoreScratch.Count; index++)
            {
                RestoreRendererLayerOverride(restoreScratch[index]);
            }

            for (var index = 0; index < rendererScratch.Count; index++)
            {
                var renderer = rendererScratch[index];
                if (renderer == null)
                {
                    continue;
                }

                var currentMask = renderer.renderingLayerMask;
                if (!originalRenderingLayerMasks.TryGetValue(renderer, out var originalMask))
                {
                    originalMask = currentMask;
                    originalRenderingLayerMasks.Add(renderer, originalMask);
                }

                var perObjectMask = (originalMask & ~MainLightRenderingLayerMask) | PerObjectShadowRenderingLayerMask;
                if (currentMask != perObjectMask)
                {
                    renderer.renderingLayerMask = perObjectMask;
                }
            }

            SyncMultipassRendererLayerOverrides();
        }

        private void RestoreRendererLayerOverrides()
        {
            restoreScratch.Clear();
            foreach (var entry in originalRenderingLayerMasks)
            {
                restoreScratch.Add(entry.Key);
            }

            for (var index = 0; index < restoreScratch.Count; index++)
            {
                RestoreRendererLayerOverride(restoreScratch[index]);
            }

            restoreScratch.Clear();
            RestoreMultipassRendererLayerOverrides();
        }

        private void RestoreRendererLayerOverride(Renderer renderer)
        {
            if (renderer != null && originalRenderingLayerMasks.TryGetValue(renderer, out var originalMask))
            {
                renderer.renderingLayerMask = originalMask;
            }

            originalRenderingLayerMasks.Remove(renderer);
        }

        private void SyncMultipassRendererLayerOverrides()
        {
            multipassRendererScratch.Clear();
            GetComponentsInChildren(includeInactiveRenderers, multipassRendererScratch);

            restoreMultipassScratch.Clear();
            foreach (var entry in originalMultipassRenderingLayerMasks)
            {
                var multipassRenderer = entry.Key;
                if (multipassRenderer == null || !multipassRendererScratch.Contains(multipassRenderer))
                {
                    restoreMultipassScratch.Add(multipassRenderer);
                }
            }

            for (var index = 0; index < restoreMultipassScratch.Count; index++)
            {
                RestoreMultipassRendererLayerOverride(restoreMultipassScratch[index]);
            }

            for (var index = 0; index < multipassRendererScratch.Count; index++)
            {
                var multipassRenderer = multipassRendererScratch[index];
                if (multipassRenderer == null)
                {
                    continue;
                }

                var currentMask = multipassRenderer.m_RenderingLayerMask;
                if (!originalMultipassRenderingLayerMasks.TryGetValue(multipassRenderer, out var originalMask))
                {
                    originalMask = currentMask;
                    originalMultipassRenderingLayerMasks.Add(multipassRenderer, originalMask);
                }

                var perObjectMask = (originalMask & ~(int)MainLightRenderingLayerMask) | (int)PerObjectShadowRenderingLayerMask;
                if (currentMask != perObjectMask)
                {
                    multipassRenderer.m_RenderingLayerMask = perObjectMask;
                }
            }
        }

        private void RestoreMultipassRendererLayerOverrides()
        {
            restoreMultipassScratch.Clear();
            foreach (var entry in originalMultipassRenderingLayerMasks)
            {
                restoreMultipassScratch.Add(entry.Key);
            }

            for (var index = 0; index < restoreMultipassScratch.Count; index++)
            {
                RestoreMultipassRendererLayerOverride(restoreMultipassScratch[index]);
            }

            restoreMultipassScratch.Clear();
        }

        private void RestoreMultipassRendererLayerOverride(BurtMultipassRenderer multipassRenderer)
        {
            if (multipassRenderer != null && originalMultipassRenderingLayerMasks.TryGetValue(multipassRenderer, out var originalMask))
            {
                multipassRenderer.m_RenderingLayerMask = originalMask;
            }

            originalMultipassRenderingLayerMasks.Remove(multipassRenderer);
        }
    }

    internal static class BurtPerObjectShadowRegistry
    {
        private static readonly List<BurtPerObjectShadow> Components = new List<BurtPerObjectShadow>();
        private static readonly List<BurtPerObjectShadow> SortedComponents = new List<BurtPerObjectShadow>();

        public static int ActiveCount
        {
            get
            {
                PruneInvalid();
                var count = 0;
                for (var index = 0; index < Components.Count; index++)
                {
                    if (IsValidComponent(Components[index]))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public static void Register(BurtPerObjectShadow component)
        {
            if (component == null)
            {
                return;
            }

            if (!Components.Contains(component))
            {
                Components.Add(component);
            }
        }

        public static void Unregister(BurtPerObjectShadow component)
        {
            if (component == null)
            {
                return;
            }

            Components.Remove(component);
        }

        public static int CollectActive(List<BurtPerObjectShadow> output, int maxCount, Camera camera)
        {
            if (output == null)
            {
                return 0;
            }

            output.Clear();
            PruneInvalid();
            SortedComponents.Clear();

            for (var index = 0; index < Components.Count; index++)
            {
                var component = Components[index];
                if (!IsValidComponent(component) || !IsInCameraLayer(component, camera))
                {
                    continue;
                }

                SortedComponents.Add(component);
            }

            SortedComponents.Sort(CompareComponents);

            var count = Mathf.Min(Mathf.Max(0, maxCount), SortedComponents.Count);
            for (var index = 0; index < count; index++)
            {
                output.Add(SortedComponents[index]);
            }

            return output.Count;
        }

        private static void PruneInvalid()
        {
            for (var index = Components.Count - 1; index >= 0; index--)
            {
                if (Components[index] == null)
                {
                    Components.RemoveAt(index);
                }
            }
        }

        private static bool IsValidComponent(BurtPerObjectShadow component)
        {
            return component != null && component.IsRenderable;
        }

        private static bool IsInCameraLayer(BurtPerObjectShadow component, Camera camera)
        {
            if (component == null || camera == null)
            {
                return component != null;
            }

            return (camera.cullingMask & (1 << component.gameObject.layer)) != 0;
        }

        private static int CompareComponents(BurtPerObjectShadow left, BurtPerObjectShadow right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var priorityCompare = right.Priority.CompareTo(left.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            return left.GetInstanceID().CompareTo(right.GetInstanceID());
        }
    }
}
