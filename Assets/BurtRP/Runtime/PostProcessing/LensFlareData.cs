using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Burt.RenderPipeline
{
    [Serializable]
    [HideMonoScript]
    public sealed class LensFlareData : ScriptableObject
    {
        [Title("Bokeh")]
        public Texture2D element1Tex;
        [Range(0f, 20f)] public float scaleX1 = LensFlareSettings.DefaultScaleX1;
        [Range(0f, 20f)] public float scaleY1 = LensFlareSettings.DefaultScaleY1;
        [Range(-1f, 1f)] public float positionX1 = LensFlareSettings.DefaultPositionX1;
        [Range(-1f, 1f)] public float positionY1 = LensFlareSettings.DefaultPositionY1;
        public Color color1 = LensFlareSettings.DefaultColor1;

        public Texture2D element2Tex;
        [Range(0f, 20f)] public float scaleX2 = LensFlareSettings.DefaultScaleX2;
        [Range(0f, 20f)] public float scaleY2 = LensFlareSettings.DefaultScaleY2;
        [Range(-1f, 1f)] public float positionX2 = LensFlareSettings.DefaultPositionX2;
        [Range(-1f, 1f)] public float positionY2 = LensFlareSettings.DefaultPositionY2;
        public Color color2 = LensFlareSettings.DefaultColor2;

        public Texture2D element3Tex;
        [Range(0f, 20f)] public float scaleX3 = LensFlareSettings.DefaultScaleX3;
        [Range(0f, 20f)] public float scaleY3 = LensFlareSettings.DefaultScaleY3;
        [Range(-1f, 1f)] public float positionX3 = LensFlareSettings.DefaultPositionX3;
        [Range(-1f, 1f)] public float positionY3 = LensFlareSettings.DefaultPositionY3;
        public Color color3 = LensFlareSettings.DefaultColor3;

        public Texture2D element4Tex;
        [Range(0f, 20f)] public float scaleX4 = LensFlareSettings.DefaultScaleX4;
        [Range(0f, 20f)] public float scaleY4 = LensFlareSettings.DefaultScaleY4;
        [Range(-1f, 1f)] public float positionX4 = LensFlareSettings.DefaultPositionX4;
        [Range(-1f, 1f)] public float positionY4 = LensFlareSettings.DefaultPositionY4;
        public Color color4 = LensFlareSettings.DefaultColor4;

        public Texture2D element5Tex;
        [Range(0f, 20f)] public float scaleX5 = LensFlareSettings.DefaultScaleX5;
        [Range(0f, 20f)] public float scaleY5 = LensFlareSettings.DefaultScaleY5;
        [Range(-1f, 1f)] public float positionX5 = LensFlareSettings.DefaultPositionX5;
        [Range(-1f, 1f)] public float positionY5 = LensFlareSettings.DefaultPositionY5;
        public Color color5 = LensFlareSettings.DefaultColor5;

        [Title("Line")]
        public Texture2D lineTex;
        [Range(0f, 100f)] public float lineIntensity = LensFlareSettings.DefaultLineIntensity;
        [Range(0f, 10f)] public float lineLength = LensFlareSettings.DefaultLineLength;
        [Range(0f, 10f)] public float lineWidth = LensFlareSettings.DefaultLineWidth;
        [Range(-100f, 100f)] public float lineCurve = LensFlareSettings.DefaultLineCurve;

    }
}
