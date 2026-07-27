using UnityEngine;

namespace Burt.RenderPipeline
{
    public enum BurtLightFalloffMode
    {
        // Keep inverse-square at zero so components serialized before this field
        // existed retain XRender's default physical falloff.
        InverseSquared = 0,
        Linear = 1
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("Rendering/Burt Volumetric Light")]
    public sealed class BurtVolumetricLight : MonoBehaviour
    {
        [Tooltip("Controls whether this Unity Light contributes to BurtRP Volumetric Fog. Without this component, lights affect volumetric fog by default.")]
        public bool affectVolumetric = true;

        [Tooltip("Per-light multiplier for volumetric fog scattering, matching XRender's volumetric scattering intensity scale semantics.")]
        [Min(0f)] public float scatteringIntensityScale = 1f;

        [Tooltip("Point and spot lights only. Volumetric scattering fades in outside this radius to avoid over-bright fog at the light origin.")]
        [Min(0f)] public float nearCutoffDistance;

        [Tooltip("Point and spot lights. Inverse Squared matches XRender's default physical falloff; Linear uses max(0, 1 - distance/range). This affects both surface and volumetric lighting.")]
        public BurtLightFalloffMode falloffMode = BurtLightFalloffMode.InverseSquared;

        internal float EffectiveScatteringIntensityScale => affectVolumetric ? Mathf.Max(0f, scatteringIntensityScale) : 0f;

        internal float EffectiveNearCutoffDistance => affectVolumetric ? Mathf.Max(0f, nearCutoffDistance) : 0f;

        internal bool UseInverseSquaredFalloff => falloffMode != BurtLightFalloffMode.Linear;
    }
}
