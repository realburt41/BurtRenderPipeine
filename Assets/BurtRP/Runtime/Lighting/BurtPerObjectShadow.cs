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
        private float normalBias = 1f;

        [SerializeField]
        private bool includeInactiveRenderers;

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
        }

        private void OnDisable()
        {
            BurtPerObjectShadowRegistry.Unregister(this);
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
            }
        }

        internal void CollectRenderers(List<Renderer> renderers)
        {
            if (renderers == null)
            {
                return;
            }

            GetComponentsInChildren(includeInactiveRenderers, renderers);
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
