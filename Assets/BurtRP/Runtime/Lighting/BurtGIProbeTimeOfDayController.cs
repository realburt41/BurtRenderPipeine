using System.Collections.Generic;
using UnityEngine;

namespace Burt.RenderPipeline
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/BurtRP/XGI Probe Time Of Day Controller")]
    public sealed class BurtGIProbeTimeOfDayController : MonoBehaviour
    {
        public enum SourceMode
        {
            ManualHour,
            ManualSlice
        }

        [Tooltip("Highest-priority active controller drives the global XGI probe time slice.")]
        public int priority;

        public SourceMode sourceMode = SourceMode.ManualHour;

        [Range(0f, 24f)]
        public float hour = 15f + 50f / 60f;

        public BurtGIProbeTimeSlice slice = BurtGIProbeTimeSlice.Day;

        public bool updateEveryFrame = true;
        public bool updateInEditMode = true;

        private static readonly List<BurtGIProbeTimeOfDayController> ActiveControllers = new List<BurtGIProbeTimeOfDayController>();

        private void OnEnable()
        {
            if (!ActiveControllers.Contains(this))
            {
                ActiveControllers.Add(this);
            }

            ApplyIfControlling();
        }

        private void OnDisable()
        {
            ActiveControllers.Remove(this);
            ApplyBestController();
        }

        private void OnDestroy()
        {
            ActiveControllers.Remove(this);
            ApplyBestController();
        }

        private void Update()
        {
            if (!updateEveryFrame || (!Application.isPlaying && !updateInEditMode))
            {
                return;
            }

            ApplyIfControlling();
        }

        public void SetHour(float value)
        {
            sourceMode = SourceMode.ManualHour;
            hour = value;
            ApplyIfControlling();
        }

        public void SetSlice(BurtGIProbeTimeSlice value)
        {
            sourceMode = SourceMode.ManualSlice;
            slice = value;
            ApplyIfControlling();
        }

        public void ApplyIfControlling()
        {
            if (TryGetBestController(out var controller) && controller == this)
            {
                Apply();
            }
        }

        public void Apply()
        {
            if (sourceMode == SourceMode.ManualSlice)
            {
                BurtGIProbeVolume.SetActiveTimeSlice(slice);
                return;
            }

            BurtGIProbeVolume.SetActiveTimeSliceForHour(hour);
        }

        private static void ApplyBestController()
        {
            if (TryGetBestController(out var controller))
            {
                controller.Apply();
            }
        }

        private static bool TryGetBestController(out BurtGIProbeTimeOfDayController controller)
        {
            controller = null;
            var bestPriority = int.MinValue;
            for (var index = ActiveControllers.Count - 1; index >= 0; --index)
            {
                var candidate = ActiveControllers[index];
                if (candidate == null)
                {
                    ActiveControllers.RemoveAt(index);
                    continue;
                }

                if (!candidate.isActiveAndEnabled ||
                    (!Application.isPlaying && !candidate.updateInEditMode) ||
                    candidate.priority < bestPriority)
                {
                    continue;
                }

                bestPriority = candidate.priority;
                controller = candidate;
            }

            return controller != null;
        }

        internal static bool HasValidTimeOfDaySource()
        {
            return TryGetBestController(out _);
        }

        internal static string GetDebugStatus()
        {
            var activeCount = 0;
            for (var index = ActiveControllers.Count - 1; index >= 0; --index)
            {
                var candidate = ActiveControllers[index];
                if (candidate == null)
                {
                    ActiveControllers.RemoveAt(index);
                    continue;
                }

                if (candidate.isActiveAndEnabled &&
                    (Application.isPlaying || candidate.updateInEditMode))
                {
                    activeCount++;
                }
            }

            if (!TryGetBestController(out var controller))
            {
                return "Static(ActiveSlice=" + BurtGIProbeVolume.ActiveTimeSlice + ",Controllers=" + activeCount + ")";
            }

            return "Controller(Name=" + controller.name +
                ",Mode=" + controller.sourceMode +
                ",Hour=" + controller.hour.ToString("0.###") +
                ",Slice=" + controller.slice +
                ",ActiveSlice=" + BurtGIProbeVolume.ActiveTimeSlice +
                ",Priority=" + controller.priority +
                ",Controllers=" + activeCount + ")";
        }
    }
}
