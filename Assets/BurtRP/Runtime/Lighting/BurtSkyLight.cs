using System.Collections.Generic;
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
        [Tooltip("Yaw rotation in degrees for SpecifiedCubemap sampling, matching XRender's source cubemap angle.")]
        [Range(0f, 360f)] public float cubemapAngle;
        [Tooltip("When multiple active BurtSkyLight components exist, the highest priority wins.")]
        public int priority;
        [Tooltip("Controls whether this SkyLight writes BurtRP ambient SH and simple ambient color.")]
        public bool affectDiffuse = true;
        [Tooltip("Controls whether this SkyLight writes the BurtRP sky reflection cubemap and intensity.")]
        public bool affectSpecular = true;
        [Tooltip("Used only by ConstantColor source. This is a low-risk diffuse fallback; specular remains disabled.")]
        public Color constantColor = Color.black;
        [Tooltip("Controls how BurtRP treats lower hemisphere directions for diffuse SH and sky specular fallback.")]
        public BurtSkyLightLowerHemisphereMode lowerHemisphereMode = BurtSkyLightLowerHemisphereMode.Preserve;
        [Tooltip("Color used by SolidColor lower hemisphere mode. Alpha blends between the original sky and this color.")]
        public Color lowerHemisphereColor = Color.black;

        private static readonly List<BurtSkyLight> ActiveSkyLights = new List<BurtSkyLight>();

        internal float EffectiveDiffuseIntensity => Mathf.Max(0f, intensity) * Mathf.Max(0f, diffuseIntensity);
        internal float EffectiveSpecularIntensity => Mathf.Max(0f, intensity) * Mathf.Max(0f, specularIntensity);
        internal Color SafeTint => new Color(Mathf.Max(0f, tint.r), Mathf.Max(0f, tint.g), Mathf.Max(0f, tint.b), 1f);

        private void OnEnable()
        {
            Register(this);
        }

        private void OnDisable()
        {
            Unregister(this);
        }

        private void OnDestroy()
        {
            Unregister(this);
        }

        internal static bool TryGetActive(out BurtSkyLight skyLight)
        {
            skyLight = null;

            var bestPriority = int.MinValue;
            var bestInstanceId = int.MinValue;
            for (var i = ActiveSkyLights.Count - 1; i >= 0; i--)
            {
                var candidate = ActiveSkyLights[i];
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.gameObject == null || !candidate.gameObject.activeInHierarchy)
                {
                    ActiveSkyLights.RemoveAt(i);
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

        private static void Register(BurtSkyLight skyLight)
        {
            if (skyLight == null || ActiveSkyLights.Contains(skyLight))
            {
                return;
            }

            ActiveSkyLights.Add(skyLight);
        }

        private static void Unregister(BurtSkyLight skyLight)
        {
            if (skyLight == null)
            {
                return;
            }

            ActiveSkyLights.Remove(skyLight);
        }
    }
}
