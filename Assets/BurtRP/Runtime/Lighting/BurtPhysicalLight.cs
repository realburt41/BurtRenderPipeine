using UnityEngine;

namespace Burt.RenderPipeline
{
    public enum BurtPhysicalLightUnit
    {
        Lux = 0,
        Lumen = 1,
        Candela = 2,
        Nits = 3
    }

    public static class BurtPhysicalLightUnitUtility
    {
        private const float MinimumArea = 0.000001f;
        private const float MinimumSpotSolidAngle = 0.01f;

        public static float PointLumenToCandela(float lumen)
        {
            return Mathf.Max(lumen, 0f) / (4f * Mathf.PI);
        }

        public static float PointCandelaToLumen(float candela)
        {
            return Mathf.Max(candela, 0f) * (4f * Mathf.PI);
        }

        public static float SpotLumenToCandela(float lumen, float fullAngleDegrees, bool exact)
        {
            if (!exact)
                return Mathf.Max(lumen, 0f) / Mathf.PI;
            var fullAngleRadians = fullAngleDegrees * Mathf.Deg2Rad;
            var solidAngle = 2f * Mathf.PI * (1f - Mathf.Cos(fullAngleRadians * 0.5f));
            return Mathf.Max(lumen, 0f) / Mathf.Max(solidAngle, MinimumSpotSolidAngle);
        }

        public static float SpotCandelaToLumen(float candela, float fullAngleDegrees, bool exact)
        {
            if (!exact)
                return Mathf.Max(candela, 0f) * Mathf.PI;
            var fullAngleRadians = fullAngleDegrees * Mathf.Deg2Rad;
            var solidAngle = 2f * Mathf.PI * (1f - Mathf.Cos(fullAngleRadians * 0.5f));
            return Mathf.Max(candela, 0f) * solidAngle;
        }

        public static float RectangleLumenToNits(float lumen, Vector2 size)
        {
            var area = Mathf.Max(Mathf.Abs(size.x * size.y), MinimumArea);
            return Mathf.Max(lumen, 0f) / (area * Mathf.PI);
        }

        public static float RectangleNitsToLumen(float nits, Vector2 size)
        {
            var area = Mathf.Max(Mathf.Abs(size.x * size.y), MinimumArea);
            return Mathf.Max(nits, 0f) * area * Mathf.PI;
        }

        public static BurtPhysicalLightUnit NativeUnitFor(LightType type)
        {
            switch (type)
            {
                case LightType.Directional:
                    return BurtPhysicalLightUnit.Lux;
                case LightType.Area:
                    return BurtPhysicalLightUnit.Nits;
                default:
                    return BurtPhysicalLightUnit.Candela;
            }
        }

        public static BurtPhysicalLightUnit PreferredDisplayUnitFor(LightType type)
        {
            switch (type)
            {
                case LightType.Directional:
                    return BurtPhysicalLightUnit.Lux;
                case LightType.Point:
                case LightType.Spot:
                case LightType.Area:
                    return BurtPhysicalLightUnit.Lumen;
                default:
                    return NativeUnitFor(type);
            }
        }

        public static bool IsSupported(LightType type, BurtPhysicalLightUnit unit)
        {
            switch (type)
            {
                case LightType.Directional:
                    return unit == BurtPhysicalLightUnit.Lux;
                case LightType.Area:
                    return unit == BurtPhysicalLightUnit.Lumen || unit == BurtPhysicalLightUnit.Nits;
                case LightType.Point:
                case LightType.Spot:
                    return unit == BurtPhysicalLightUnit.Lumen || unit == BurtPhysicalLightUnit.Candela;
                default:
                    return unit == BurtPhysicalLightUnit.Candela;
            }
        }

        public static float ToNativeIntensity(
            LightType type,
            BurtPhysicalLightUnit unit,
            float intensity,
            float spotAngleDegrees,
            Vector2 areaSize,
            bool exactSpotReflector)
        {
            intensity = Mathf.Max(intensity, 0f);
            switch (type)
            {
                case LightType.Directional:
                    return intensity;
                case LightType.Point:
                    return unit == BurtPhysicalLightUnit.Lumen ? PointLumenToCandela(intensity) : intensity;
                case LightType.Spot:
                    return unit == BurtPhysicalLightUnit.Lumen
                        ? SpotLumenToCandela(intensity, spotAngleDegrees, exactSpotReflector)
                        : intensity;
                case LightType.Area:
                    return unit == BurtPhysicalLightUnit.Lumen ? RectangleLumenToNits(intensity, areaSize) : intensity;
                default:
                    return intensity;
            }
        }

        public static float FromNativeIntensity(
            LightType type,
            BurtPhysicalLightUnit unit,
            float nativeIntensity,
            float spotAngleDegrees,
            Vector2 areaSize,
            bool exactSpotReflector)
        {
            nativeIntensity = Mathf.Max(nativeIntensity, 0f);
            switch (type)
            {
                case LightType.Point:
                    return unit == BurtPhysicalLightUnit.Lumen ? PointCandelaToLumen(nativeIntensity) : nativeIntensity;
                case LightType.Spot:
                    return unit == BurtPhysicalLightUnit.Lumen
                        ? SpotCandelaToLumen(nativeIntensity, spotAngleDegrees, exactSpotReflector)
                        : nativeIntensity;
                case LightType.Area:
                    return unit == BurtPhysicalLightUnit.Lumen ? RectangleNitsToLumen(nativeIntensity, areaSize) : nativeIntensity;
                default:
                    return nativeIntensity;
            }
        }
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    // Additional light data owned by the BRP Light inspector. Keeping it out of
    // Add Component avoids exposing two competing intensity inspectors.
    [AddComponentMenu("")]
    public sealed class BurtPhysicalLight : MonoBehaviour
    {
        [SerializeField]
        private bool usePhysicalLightUnits = true;

        [SerializeField]
        private BurtPhysicalLightUnit unit = BurtPhysicalLightUnit.Candela;

        [SerializeField, Min(0f)]
        private float intensity = 1f;

        [SerializeField]
        private bool exactSpotReflector = true;

        [SerializeField, HideInInspector]
        private int lastAppliedHash;

        private Light cachedLight;

        public bool UsePhysicalLightUnits
        {
            get => usePhysicalLightUnits;
            set => SetUsePhysicalLightUnits(value);
        }

        public BurtPhysicalLightUnit Unit => unit;

        public bool ExactSpotReflector => exactSpotReflector;

        public float Intensity
        {
            get => intensity;
            set
            {
                intensity = Mathf.Max(value, 0f);
                ApplyToUnityLight();
            }
        }

        public float NativeIntensity => ResolveLight() != null
            ? BurtPhysicalLightUnitUtility.ToNativeIntensity(
                cachedLight.type,
                unit,
                intensity,
                cachedLight.spotAngle,
                cachedLight.areaSize,
                exactSpotReflector)
            : 0f;

        public void SetUnit(BurtPhysicalLightUnit newUnit, bool preserveOutput = true)
        {
            var light = ResolveLight();
            if (light == null || !BurtPhysicalLightUnitUtility.IsSupported(light.type, newUnit))
                return;

            var nativeIntensity = preserveOutput
                ? (usePhysicalLightUnits ? NativeIntensity : Mathf.Max(light.intensity, 0f))
                : Mathf.Max(light.intensity, 0f);
            unit = newUnit;
            if (preserveOutput)
            {
                intensity = BurtPhysicalLightUnitUtility.FromNativeIntensity(
                    light.type,
                    unit,
                    nativeIntensity,
                    light.spotAngle,
                    light.areaSize,
                    exactSpotReflector);
            }
            ApplyToUnityLight();
        }

        public void SetUsePhysicalLightUnits(bool enabled, bool preserveOutput = true)
        {
            var light = ResolveLight();
            if (light == null)
            {
                usePhysicalLightUnits = enabled;
                return;
            }

            if (usePhysicalLightUnits == enabled)
            {
                if (enabled)
                    ApplyToUnityLight();
                return;
            }

            if (enabled)
            {
                unit = BurtPhysicalLightUnitUtility.PreferredDisplayUnitFor(light.type);

                if (preserveOutput)
                {
                    intensity = BurtPhysicalLightUnitUtility.FromNativeIntensity(
                        light.type,
                        unit,
                        light.intensity,
                        light.spotAngle,
                        light.areaSize,
                        exactSpotReflector);
                }
            }

            usePhysicalLightUnits = enabled;
            if (enabled)
                ApplyToUnityLight();
            else
                lastAppliedHash = CalculateStateHash(light);
        }

        public void SetExactSpotReflector(bool exact, bool preserveOutput = true)
        {
            var light = ResolveLight();
            if (light == null || exactSpotReflector == exact)
                return;

            var nativeIntensity = usePhysicalLightUnits ? NativeIntensity : Mathf.Max(light.intensity, 0f);
            exactSpotReflector = exact;
            if (preserveOutput && light.type == LightType.Spot && unit == BurtPhysicalLightUnit.Lumen)
            {
                intensity = BurtPhysicalLightUnitUtility.FromNativeIntensity(
                    light.type,
                    unit,
                    nativeIntensity,
                    light.spotAngle,
                    light.areaSize,
                    exactSpotReflector);
            }
            ApplyToUnityLight();
        }

        public void SyncFromUnityLight()
        {
            var light = ResolveLight();
            if (light == null)
                return;

            if (!BurtPhysicalLightUnitUtility.IsSupported(light.type, unit))
                unit = BurtPhysicalLightUnitUtility.NativeUnitFor(light.type);

            intensity = BurtPhysicalLightUnitUtility.FromNativeIntensity(
                light.type,
                unit,
                light.intensity,
                light.spotAngle,
                light.areaSize,
                exactSpotReflector);
            lastAppliedHash = CalculateStateHash(light);
        }

        public void ApplyToUnityLight()
        {
            var light = ResolveLight();
            if (light == null || !usePhysicalLightUnits)
                return;

            if (!BurtPhysicalLightUnitUtility.IsSupported(light.type, unit))
            {
                unit = BurtPhysicalLightUnitUtility.NativeUnitFor(light.type);
                intensity = Mathf.Max(light.intensity, 0f);
            }

            light.intensity = NativeIntensity;
            lastAppliedHash = CalculateStateHash(light);
        }

        private void Reset()
        {
            var light = ResolveLight();
            if (light == null)
                return;
            unit = BurtPhysicalLightUnitUtility.PreferredDisplayUnitFor(light.type);
            intensity = BurtPhysicalLightUnitUtility.FromNativeIntensity(
                light.type,
                unit,
                light.intensity,
                light.spotAngle,
                light.areaSize,
                true);
            exactSpotReflector = true;
            usePhysicalLightUnits = true;
            ApplyToUnityLight();
        }

        private void OnEnable()
        {
            ApplyToUnityLight();
        }

        private void OnValidate()
        {
            intensity = Mathf.Max(intensity, 0f);
            ApplyToUnityLight();
        }

        private void LateUpdate()
        {
            var light = ResolveLight();
            if (light != null && usePhysicalLightUnits && CalculateStateHash(light) != lastAppliedHash)
                ApplyToUnityLight();
        }

        private Light ResolveLight()
        {
            if (cachedLight == null)
                cachedLight = GetComponent<Light>();
            return cachedLight;
        }

        private int CalculateStateHash(Light light)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (int)light.type;
                hash = hash * 31 + unit.GetHashCode();
                hash = hash * 31 + intensity.GetHashCode();
                hash = hash * 31 + exactSpotReflector.GetHashCode();
                hash = hash * 31 + light.spotAngle.GetHashCode();
                hash = hash * 31 + light.areaSize.GetHashCode();
                return hash;
            }
        }
    }
}
