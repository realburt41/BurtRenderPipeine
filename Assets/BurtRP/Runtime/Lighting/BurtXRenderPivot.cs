using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Burt.RenderPipeline
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/BurtRP/XRender Pivot")]
    [MovedFrom(true, "UnityEngine.Rendering", "FunPlus.WorldX.XRender.Runtime", "XRenderPivot")]
    public sealed class BurtXRenderPivot : MonoBehaviour
    {
        [Tooltip("Matches XRenderPivot semantics: lower values win for the top pivot.")]
        public int priority;

        private static readonly List<BurtXRenderPivot> ActivePivots = new List<BurtXRenderPivot>();

        internal readonly struct PivotData
        {
            internal readonly Vector3 Position;
            internal readonly Vector3 Forward;
            internal readonly int Priority;

            internal PivotData(Vector3 position, Vector3 forward, int priority)
            {
                Position = position;
                Forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
                Priority = priority;
            }
        }

        private void OnEnable()
        {
            if (!ActivePivots.Contains(this))
            {
                ActivePivots.Add(this);
            }
        }

        private void OnDisable()
        {
            // XRender keeps disabled pivots registered for pet-combine flows.
        }

        private void OnDestroy()
        {
            ActivePivots.Remove(this);
        }

        internal static bool TryGetTop(out PivotData pivot)
        {
            var bestPriority = int.MaxValue;
            BurtXRenderPivot bestPivot = null;
            for (var index = ActivePivots.Count - 1; index >= 0; --index)
            {
                var candidate = ActivePivots[index];
                if (candidate == null)
                {
                    ActivePivots.RemoveAt(index);
                    continue;
                }

                if (candidate.priority >= bestPriority)
                {
                    continue;
                }

                bestPriority = candidate.priority;
                bestPivot = candidate;
            }

            if (bestPivot != null)
            {
                pivot = new PivotData(bestPivot.transform.position, bestPivot.transform.forward, bestPivot.priority);
                return true;
            }

            pivot = default;
            return false;
        }

        internal static bool TryGetTopPosition(out Vector3 position)
        {
            if (TryGetTop(out var pivot))
            {
                position = pivot.Position;
                return true;
            }

            position = default;
            return false;
        }

        internal static string GetDebugStatus()
        {
            var activeCount = 0;
            var bestPriority = int.MaxValue;
            BurtXRenderPivot bestPivot = null;
            for (var index = ActivePivots.Count - 1; index >= 0; --index)
            {
                var candidate = ActivePivots[index];
                if (candidate == null)
                {
                    ActivePivots.RemoveAt(index);
                    continue;
                }

                activeCount++;
                if (candidate.priority >= bestPriority)
                {
                    continue;
                }

                bestPriority = candidate.priority;
                bestPivot = candidate;
            }

            if (bestPivot == null)
            {
                return "None(Active=0)";
            }

            return "Pivot(Name=" + bestPivot.name +
                ",Priority=" + bestPivot.priority +
                ",Active=" + activeCount +
                ",Position=" + bestPivot.transform.position.ToString("F2") +
                ",Forward=" + bestPivot.transform.forward.ToString("F2") + ")";
        }
    }
}
