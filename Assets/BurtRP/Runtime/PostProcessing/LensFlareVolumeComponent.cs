using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    public sealed class LensFlareDataParameter : VolumeParameter<LensFlareData>
    {
        public LensFlareDataParameter(LensFlareData value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenu("Post Processing/Lens Flare")]
    public sealed class LensFlareVolumeComponent : VolumeComponent
    {
        private const float ActiveEpsilon = 0.0001f;

        [Title("Lens Flare")]
        public BoolParameter enabled = new BoolParameter(true);
        public LensFlareDataParameter data = new LensFlareDataParameter(null);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(LensFlareSettings.DefaultIntensity, 0f, 4f);
        public ClampedFloatParameter scale = new ClampedFloatParameter(LensFlareSettings.DefaultScale, 0f, 2f);
        public ColorParameter tint = new ColorParameter(Color.white, true, false, false);

        public bool IsEnabled()
        {
            return active && HasAnyOverride() && enabled.value && intensity.value > ActiveEpsilon && scale.value > ActiveEpsilon;
        }

        private bool HasAnyOverride()
        {
            return enabled.overrideState ||
                data.overrideState ||
                intensity.overrideState ||
                scale.overrideState ||
                tint.overrideState;
        }
    }

    internal readonly struct LensFlareSettings
    {
        public const float DefaultIntensity = 0.8f;
        public const float DefaultScale = 1f;
        public const float DefaultScaleX1 = 1.6f;
        public const float DefaultScaleY1 = 1.6f;
        public const float DefaultPositionX1 = 1f;
        public const float DefaultPositionY1 = 1f;
        public const float DefaultScaleX2 = 4f;
        public const float DefaultScaleY2 = 4f;
        public const float DefaultPositionX2 = 0.25f;
        public const float DefaultPositionY2 = 0.25f;
        public const float DefaultScaleX3 = 9f;
        public const float DefaultScaleY3 = 9f;
        public const float DefaultPositionX3 = 0.55f;
        public const float DefaultPositionY3 = 0.55f;
        public const float DefaultScaleX4 = 1.65f;
        public const float DefaultScaleY4 = 1.65f;
        public const float DefaultPositionX4 = -0.1f;
        public const float DefaultPositionY4 = -0.1f;
        public const float DefaultScaleX5 = 13f;
        public const float DefaultScaleY5 = 13f;
        public const float DefaultPositionX5 = 0.46f;
        public const float DefaultPositionY5 = 0.46f;
        public const float DefaultLineIntensity = 42f;
        public const float DefaultLineLength = 3.128f;
        public const float DefaultLineWidth = 2.75f;
        public const float DefaultLineCurve = -22.18483f;

        public static readonly Color DefaultColor1 = new Color(0.75f, 0.669f, 0.5742f, 1f);
        public static readonly Color DefaultColor2 = new Color(0.166667f, 0.164503f, 0.162785f, 1f);
        public static readonly Color DefaultColor3 = new Color(0.082465f, 0.116864f, 0.20833f, 1f);
        public static readonly Color DefaultColor4 = new Color(0.134778f, 0.177083f, 0.099522f, 1f);
        public static readonly Color DefaultColor5 = new Color(0.307292f, 0.283174f, 0.273873f, 1f);
        public static readonly LensFlareSettings Default = new LensFlareSettings(false, null, DefaultIntensity, DefaultScale, Color.white);

        public bool Enabled { get; }
        public Texture2D Bokeh0Texture { get; }
        public Texture2D Bokeh1Texture { get; }
        public Texture2D Bokeh2Texture { get; }
        public Texture2D Bokeh3Texture { get; }
        public Texture2D Bokeh4Texture { get; }
        public Texture2D LineTexture { get; }
        public Vector4 Bokeh0ScaleAndPosition { get; }
        public Vector4 Bokeh1ScaleAndPosition { get; }
        public Vector4 Bokeh2ScaleAndPosition { get; }
        public Vector4 Bokeh3ScaleAndPosition { get; }
        public Vector4 Bokeh4ScaleAndPosition { get; }
        public Vector4 Bokeh0Color { get; }
        public Vector4 Bokeh1Color { get; }
        public Vector4 Bokeh2Color { get; }
        public Vector4 Bokeh3Color { get; }
        public Vector4 Bokeh4Color { get; }
        public Vector4 LineParams { get; }
        public Vector4 TotalParams { get; }
        public Vector4 TotalTintColor { get; }
        public Vector4 TextureFlags0 { get; }
        public Vector4 TextureFlags1 { get; }

        public LensFlareSettings(bool enabled, LensFlareData data, float intensity, float scale, Color tint)
        {
            Enabled = enabled;
            Bokeh0Texture = data != null ? data.element1Tex : null;
            Bokeh1Texture = data != null ? data.element2Tex : null;
            Bokeh2Texture = data != null ? data.element3Tex : null;
            Bokeh3Texture = data != null ? data.element4Tex : null;
            Bokeh4Texture = data != null ? data.element5Tex : null;
            LineTexture = data != null ? data.lineTex : null;
            Bokeh0ScaleAndPosition = new Vector4(data != null ? data.scaleX1 : DefaultScaleX1, data != null ? data.scaleY1 : DefaultScaleY1, data != null ? data.positionX1 : DefaultPositionX1, data != null ? data.positionY1 : DefaultPositionY1);
            Bokeh1ScaleAndPosition = new Vector4(data != null ? data.scaleX2 : DefaultScaleX2, data != null ? data.scaleY2 : DefaultScaleY2, data != null ? data.positionX2 : DefaultPositionX2, data != null ? data.positionY2 : DefaultPositionY2);
            Bokeh2ScaleAndPosition = new Vector4(data != null ? data.scaleX3 : DefaultScaleX3, data != null ? data.scaleY3 : DefaultScaleY3, data != null ? data.positionX3 : DefaultPositionX3, data != null ? data.positionY3 : DefaultPositionY3);
            Bokeh3ScaleAndPosition = new Vector4(data != null ? data.scaleX4 : DefaultScaleX4, data != null ? data.scaleY4 : DefaultScaleY4, data != null ? data.positionX4 : DefaultPositionX4, data != null ? data.positionY4 : DefaultPositionY4);
            Bokeh4ScaleAndPosition = new Vector4(data != null ? data.scaleX5 : DefaultScaleX5, data != null ? data.scaleY5 : DefaultScaleY5, data != null ? data.positionX5 : DefaultPositionX5, data != null ? data.positionY5 : DefaultPositionY5);
            Bokeh0Color = ColorToVector(data != null ? data.color1 : DefaultColor1);
            Bokeh1Color = ColorToVector(data != null ? data.color2 : DefaultColor2);
            Bokeh2Color = ColorToVector(data != null ? data.color3 : DefaultColor3);
            Bokeh3Color = ColorToVector(data != null ? data.color4 : DefaultColor4);
            Bokeh4Color = ColorToVector(data != null ? data.color5 : DefaultColor5);
            LineParams = new Vector4(data != null ? data.lineIntensity : DefaultLineIntensity, data != null ? data.lineLength : DefaultLineLength, data != null ? data.lineWidth : DefaultLineWidth, data != null ? data.lineCurve : DefaultLineCurve);
            TotalParams = new Vector4(Mathf.Clamp(SanitizeFinite(intensity, DefaultIntensity), 0f, 4f), Mathf.Clamp(SanitizeFinite(scale, DefaultScale), 0f, 2f), 0f, 0f);
            TotalTintColor = ColorToVector(tint);
            TextureFlags0 = new Vector4(Bokeh0Texture != null ? 1f : 0f, Bokeh1Texture != null ? 1f : 0f, Bokeh2Texture != null ? 1f : 0f, Bokeh3Texture != null ? 1f : 0f);
            TextureFlags1 = new Vector4(Bokeh4Texture != null ? 1f : 0f, LineTexture != null ? 1f : 0f, data != null ? 1f : 0f, 0f);
        }

        private static Vector4 ColorToVector(Color color)
        {
            return new Vector4(
                Mathf.Max(0f, SanitizeFinite(color.r, 1f)),
                Mathf.Max(0f, SanitizeFinite(color.g, 1f)),
                Mathf.Max(0f, SanitizeFinite(color.b, 1f)),
                Mathf.Clamp01(SanitizeFinite(color.a, 1f)));
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}
