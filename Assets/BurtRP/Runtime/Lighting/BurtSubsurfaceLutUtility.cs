using UnityEngine;

namespace Burt.RenderPipeline
{
    internal static class BurtSubsurfaceLutUtility
    {
        public const string TextureShaderName = "_BurtSubsurfacePreIntegratedLut";
        public const string EnabledShaderName = "_BurtSubsurfacePreIntegratedLutEnabled";

        public static readonly int TextureId = Shader.PropertyToID(TextureShaderName);
        public static readonly int EnabledId = Shader.PropertyToID(EnabledShaderName);

        private const int LutSize = 256;
        private const float Pi = 3.14159265359f;
        private static readonly Vector3 DefaultSkinSurfaceAlbedo = new Vector3(0.78f, 0.52f, 0.42f);
        private static readonly Vector3 DefaultSkinDiffuseMeanFreePathMm = new Vector3(1.05f, 0.42f, 0.24f);
        private static Texture2D preIntegratedLut;

        public static Texture2D GetOrCreatePreIntegratedLut()
        {
            if (preIntegratedLut != null)
            {
                return preIntegratedLut;
            }

            var format = SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf)
                ? TextureFormat.RGBAHalf
                : TextureFormat.RGBA32;
            var texture = new Texture2D(LutSize, LutSize, format, false, true)
            {
                name = "Burt Subsurface PreIntegrated LUT",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };

            var pixels = new Color[LutSize * LutSize];
            for (var y = 0; y < LutSize; y++)
            {
                var curvature = y / (float)(LutSize - 1);
                for (var x = 0; x < LutSize; x++)
                {
                    var rawNoL = x / (float)(LutSize - 1) * 2f - 1f;
                    pixels[y * LutSize + x] = EvaluateBurleyApproximation(rawNoL, curvature);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            preIntegratedLut = texture;
            return preIntegratedLut;
        }

        private static Color EvaluateBurleyApproximation(float rawNoL, float curvature)
        {
            var burleyDiffuse = EvaluateBurleyRingIntegral(
                rawNoL,
                Mathf.Max(curvature, 0.001f),
                DefaultSkinSurfaceAlbedo,
                DefaultSkinDiffuseMeanFreePathMm);

            return new Color(
                Mathf.Max(0f, burleyDiffuse.x),
                Mathf.Max(0f, burleyDiffuse.y),
                Mathf.Max(0f, burleyDiffuse.z),
                1f);
        }

        private static Vector3 EvaluateBurleyRingIntegral(
            float rawNoL,
            float curvature,
            Vector3 surfaceAlbedo,
            Vector3 diffuseMeanFreePathMm)
        {
            var cosTheta = Mathf.Clamp(rawNoL, -0.999f, 0.999f);
            var theta = Mathf.Acos(cosTheta);
            var sinTheta = Mathf.Sin(theta);
            var halfPi = Pi * 0.5f;
            var s3d = GetSearchLightDiffuseScalingFactor(surfaceAlbedo);
            var burleyA = Multiply(Divide(s3d, Multiply(diffuseMeanFreePathMm, curvature)), -2f / Pi);
            var burleyB = burleyA / 3f;
            var integralOnRing = 2f * (
                IntegralBurleyDiffusion(Pi, burleyA, burleyB) -
                IntegralBurleyDiffusion(0f, burleyA, burleyB));

            Vector3 integralOnNegativeAngle;
            Vector3 integralOnPositiveAngle;
            if (theta <= halfPi)
            {
                var angleStart = -(halfPi + theta);
                integralOnNegativeAngle =
                    ApproximationRingIntegralOfBurleyDiffusion(0f, 0f, 1f, theta, cosTheta, sinTheta, -burleyA, -burleyB) -
                    ApproximationRingIntegralOfBurleyDiffusion(angleStart, Mathf.Sin(angleStart), Mathf.Cos(angleStart), theta, cosTheta, sinTheta, -burleyA, -burleyB);

                var angleEnd = halfPi - theta;
                integralOnPositiveAngle =
                    ApproximationRingIntegralOfBurleyDiffusion(angleEnd, Mathf.Sin(angleEnd), Mathf.Cos(angleEnd), theta, cosTheta, sinTheta, burleyA, burleyB) -
                    ApproximationRingIntegralOfBurleyDiffusion(0f, 0f, 1f, theta, cosTheta, sinTheta, burleyA, burleyB);
            }
            else
            {
                var angleEnd = halfPi - theta;
                integralOnNegativeAngle =
                    ApproximationRingIntegralOfBurleyDiffusion(angleEnd, Mathf.Sin(angleEnd), Mathf.Cos(angleEnd), theta, cosTheta, sinTheta, -burleyA, -burleyB) -
                    ApproximationRingIntegralOfBurleyDiffusion(-Pi, 0f, -1f, theta, cosTheta, sinTheta, -burleyA, -burleyB);

                var angleStart = halfPi * 3f - theta;
                integralOnPositiveAngle =
                    ApproximationRingIntegralOfBurleyDiffusion(Pi, 0f, -1f, theta, cosTheta, sinTheta, burleyA, burleyB) -
                    ApproximationRingIntegralOfBurleyDiffusion(angleStart, Mathf.Sin(angleStart), Mathf.Cos(angleStart), theta, cosTheta, sinTheta, burleyA, burleyB);
            }

            return Divide(integralOnNegativeAngle + integralOnPositiveAngle, Max(integralOnRing, 0.0001f));
        }

        private static Vector3 ApproximationRingIntegralOfBurleyDiffusion(
            float angle,
            float sinAngle,
            float cosAngle,
            float theta,
            float cosTheta,
            float sinTheta,
            Vector3 burleyA,
            Vector3 burleyB)
        {
            return
                cosTheta * (IntegralCosineWithExponent(angle, sinAngle, cosAngle, burleyA) + IntegralCosineWithExponent(angle, sinAngle, cosAngle, burleyB)) -
                sinTheta * (IntegralSineWithExponent(angle, sinAngle, cosAngle, burleyA) + IntegralSineWithExponent(angle, sinAngle, cosAngle, burleyB));
        }

        private static Vector3 IntegralCosineWithExponent(float angle, float sinAngle, float cosAngle, Vector3 sharpness)
        {
            return Divide(
                Multiply(BurleyUnit(angle, sharpness), Add(Multiply(sharpness, cosAngle), sinAngle)),
                Add(Multiply(sharpness, sharpness), 1f));
        }

        private static Vector3 IntegralSineWithExponent(float angle, float sinAngle, float cosAngle, Vector3 sharpness)
        {
            return Divide(
                Multiply(BurleyUnit(angle, sharpness), Add(Multiply(sharpness, sinAngle), -cosAngle)),
                Add(Multiply(sharpness, sharpness), 1f));
        }

        private static Vector3 IntegralBurleyDiffusion(float angle, Vector3 burleyA, Vector3 burleyB)
        {
            return Divide(BurleyUnit(angle, burleyA), burleyA) + Divide(BurleyUnit(angle, burleyB), burleyB);
        }

        private static Vector3 BurleyUnit(float angle, Vector3 sharpness)
        {
            return new Vector3(
                Mathf.Exp(sharpness.x * angle),
                Mathf.Exp(sharpness.y * angle),
                Mathf.Exp(sharpness.z * angle));
        }

        private static Vector3 GetSearchLightDiffuseScalingFactor(Vector3 surfaceAlbedo)
        {
            var value = surfaceAlbedo - new Vector3(0.33f, 0.33f, 0.33f);
            return new Vector3(3.5f, 3.5f, 3.5f) + 100f * Multiply(Multiply(value, value), Multiply(value, value));
        }

        private static Vector3 Multiply(Vector3 value, float scale)
        {
            return new Vector3(value.x * scale, value.y * scale, value.z * scale);
        }

        private static Vector3 Multiply(Vector3 lhs, Vector3 rhs)
        {
            return new Vector3(lhs.x * rhs.x, lhs.y * rhs.y, lhs.z * rhs.z);
        }

        private static Vector3 Add(Vector3 value, float scalar)
        {
            return new Vector3(value.x + scalar, value.y + scalar, value.z + scalar);
        }

        private static Vector3 Divide(Vector3 lhs, Vector3 rhs)
        {
            return new Vector3(lhs.x / rhs.x, lhs.y / rhs.y, lhs.z / rhs.z);
        }

        private static Vector3 Divide(Vector3 lhs, float rhs)
        {
            return new Vector3(lhs.x / rhs, lhs.y / rhs, lhs.z / rhs);
        }

        private static Vector3 Max(Vector3 value, float minimum)
        {
            return new Vector3(
                Mathf.Max(value.x, minimum),
                Mathf.Max(value.y, minimum),
                Mathf.Max(value.z, minimum));
        }
    }
}
