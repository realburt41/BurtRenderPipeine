using UnityEngine;

namespace Burt.RenderPipeline
{
    public enum BurtSkyLightSourceType
    {
        RenderSettings,
        SpecifiedCubemap,
        ConstantColor,
        CapturedScene
    }

    public enum BurtSkyLightLowerHemisphereMode
    {
        Preserve,
        SolidColor,
        Black
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/Burt Sky Light")]
    public sealed class BurtSkyLight : MonoBehaviour
    {
        [Tooltip("RenderSettings keeps the current ambient/reflection path, SpecifiedCubemap overrides specular, ConstantColor only drives diffuse fallback, CapturedScene is reserved.")]
        public BurtSkyLightSourceType sourceType = BurtSkyLightSourceType.RenderSettings;
        [Tooltip("Global multiplier applied to both diffuse and specular SkyLight outputs.")]
        [Min(0f)] public float intensity = 1f;
        [Tooltip("Multiplier for ambient SH / simple ambient color. In SpecifiedCubemap mode diffuse still uses RenderSettings.ambientProbe.")]
        [Min(0f)] public float diffuseIntensity = 1f;
        [Tooltip("Multiplier for the sky/specular cubemap. ConstantColor disables specular in this first version.")]
        [Min(0f)] public float specularIntensity = 1f;
        [Tooltip("Non-negative color tint applied to diffuse and specular SkyLight output.")]
        public Color tint = Color.white;
        [Tooltip("Used only by SpecifiedCubemap source. The cubemap must already contain suitable mip/prefilter data; BurtRP will not convolve it at runtime.")]
        public Cubemap cubemap;
        [Tooltip("When multiple active BurtSkyLight components exist, the highest priority wins.")]
        public int priority;
        [Tooltip("Controls whether this SkyLight writes BurtRP ambient SH and simple ambient color.")]
        public bool affectDiffuse = true;
        [Tooltip("Controls whether this SkyLight writes the BurtRP sky reflection cubemap and intensity.")]
        public bool affectSpecular = true;
        [Tooltip("Used only by ConstantColor source. This is a low-risk diffuse fallback; specular remains disabled.")]
        public Color constantColor = Color.black;
        [Tooltip("Reserved for a later SH lower-hemisphere policy; not applied in this first version.")]
        public BurtSkyLightLowerHemisphereMode lowerHemisphereMode = BurtSkyLightLowerHemisphereMode.Preserve;
        [Tooltip("Reserved for a later SH lower-hemisphere policy; not applied in this first version.")]
        public Color lowerHemisphereColor = Color.black;

        internal float EffectiveDiffuseIntensity => Mathf.Max(0f, intensity) * Mathf.Max(0f, diffuseIntensity);
        internal float EffectiveSpecularIntensity => Mathf.Max(0f, intensity) * Mathf.Max(0f, specularIntensity);
        internal Color SafeTint => new Color(Mathf.Max(0f, tint.r), Mathf.Max(0f, tint.g), Mathf.Max(0f, tint.b), 1f);

        internal static bool TryGetActive(out BurtSkyLight skyLight)
        {
            skyLight = null;
            var candidates = Object.FindObjectsByType<BurtSkyLight>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (candidates == null || candidates.Length == 0)
            {
                return false;
            }

            var bestPriority = int.MinValue;
            var bestInstanceId = int.MinValue;
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.gameObject == null || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var instanceId = candidate.GetInstanceID();
                if (skyLight != null && (candidate.priority < bestPriority || (candidate.priority == bestPriority && instanceId <= bestInstanceId)))
                {
                    continue;
                }

                skyLight = candidate;
                bestPriority = candidate.priority;
                bestInstanceId = instanceId;
            }

            return skyLight != null;
        }
    }
}
