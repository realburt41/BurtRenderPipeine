using System; // 引入基础命名空间，用来给设置类添加 Serializable 特性。
using Sirenix.OdinInspector; // 引入 Odin Inspector 命名空间，用来给后处理设置提供更清晰的 Inspector 分组。
using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 SerializeField 等 Unity 序列化能力。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让后处理设置可以被管线资产和 Pass 直接访问。
{
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtTonemappingMode")]
    public enum TonemappingMode // 定义 BurtRP 的 Tonemapping 模式，数值会被 Volume Profile 序列化，所以新增模式时要保持旧数值稳定。
    {
        None = 0, // 不执行 Tonemapping，只做 No-op Copy 或完全跳过后处理。
        Neutral = 1, // 使用简单中性压缩曲线，适合先验证 HDR 到 LDR 的基础链路。
        [InspectorName("XRender / UE Filmic (ACES)")] ACES = 2 // 使用 XRender 当前对齐 UE Filmic/ACES 的曲线，名称保留 ACES 是为了不破坏旧 Volume 序列化值。
    }

    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtExposureMode")]

    public enum ExposureMode
    {
        ManualEV100 = 0,
        PhysicalCamera = 1,
        Automatic = 2,
        AutomaticHistogram = 3
    }

    internal readonly struct PhysicalExposureSettings
    {
        public const ExposureMode DefaultMode = ExposureMode.ManualEV100;
        public const float DefaultManualEv100 = 0f;
        public const float DefaultIso = 100f;
        public const float DefaultShutterTime = 1f / 60f;
        public const float DefaultAperture = 2.8f;
        public const float DefaultCalibration = 1f;
        public const float DefaultCompensation = 0f;
        public const float DefaultAutoMinEv100 = -10f;
        public const float DefaultAutoMaxEv100 = 20f;
        public const float DefaultAutoMiddleGrey = 0.18f;
        public const float DefaultAutoSpeedUp = 3f;
        public const float DefaultAutoSpeedDown = 1f;
        public const float DefaultAutoLowPercent = 10f;
        public const float DefaultAutoHighPercent = 90f;
        public const float DefaultAutoHistogramMinEv100 = -10f;
        public const float DefaultAutoHistogramMaxEv100 = 20f;
        public const float DefaultAutoAverageLuminance = 1f;
        public const float DefaultAutoAverageLogLuminance = 0f;
        public const float DefaultAutoTargetEv100 = 0f;
        public const int DefaultAutoFrameAge = -1;
        public const int DefaultAutoSampleCount = 0;
        public const string DefaultAutoSampleRejectedReason = "None";
        public static readonly PhysicalExposureSettings Default = new PhysicalExposureSettings(DefaultMode, DefaultManualEv100, DefaultIso, DefaultShutterTime, DefaultAperture, DefaultCalibration, DefaultCompensation);

        public ExposureMode Mode { get; }
        public float EV100 { get; }
        public float ISO { get; }
        public float ShutterTime { get; }
        public float Aperture { get; }
        public float Calibration { get; }
        public float Compensation { get; }
        public float Multiplier { get; }
        public float AutoMinEV100 { get; }
        public float AutoMaxEV100 { get; }
        public float AutoMiddleGrey { get; }
        public float AutoSpeedUp { get; }
        public float AutoSpeedDown { get; }
        public float AutoLowPercent { get; }
        public float AutoHighPercent { get; }
        public float AutoHistogramMinEV100 { get; }
        public float AutoHistogramMaxEV100 { get; }
        public float AutoAverageLuminance { get; }
        public float AutoAverageLogLuminance { get; }
        public float AutoTargetEV100 { get; }
        public bool AutoHasSample { get; }
        public bool AutoReadbackPending { get; }
        public int AutoReadbackAgeFrames { get; }
        public int AutoSampleAgeFrames { get; }
        public int AutoSampleCount { get; }
        public string AutoSampleRejectedReason { get; }

        public PhysicalExposureSettings(
            ExposureMode mode,
            float manualEv100,
            float iso,
            float shutterTime,
            float aperture,
            float calibration,
            float compensation,
            float autoEV100 = DefaultManualEv100,
            float autoMinEV100 = DefaultAutoMinEv100,
            float autoMaxEV100 = DefaultAutoMaxEv100,
            float autoMiddleGrey = DefaultAutoMiddleGrey,
            float autoSpeedUp = DefaultAutoSpeedUp,
            float autoSpeedDown = DefaultAutoSpeedDown,
            float autoLowPercent = DefaultAutoLowPercent,
            float autoHighPercent = DefaultAutoHighPercent,
            float autoHistogramMinEV100 = DefaultAutoHistogramMinEv100,
            float autoHistogramMaxEV100 = DefaultAutoHistogramMaxEv100,
            float autoAverageLuminance = DefaultAutoAverageLuminance,
            float autoAverageLogLuminance = DefaultAutoAverageLogLuminance,
            float autoTargetEV100 = DefaultAutoTargetEv100,
            bool autoHasSample = false,
            bool autoReadbackPending = false,
            int autoReadbackAgeFrames = DefaultAutoFrameAge,
            int autoSampleAgeFrames = DefaultAutoFrameAge,
            int autoSampleCount = DefaultAutoSampleCount,
            string autoSampleRejectedReason = DefaultAutoSampleRejectedReason)
        {
            Mode = NormalizeMode(mode);
            ISO = Mathf.Clamp(SanitizeFinite(iso, DefaultIso), 1f, 204800f);
            ShutterTime = Mathf.Clamp(SanitizeFinite(shutterTime, DefaultShutterTime), 0.000001f, 60f);
            Aperture = Mathf.Clamp(SanitizeFinite(aperture, DefaultAperture), 0.1f, 64f);
            Calibration = Mathf.Clamp(SanitizeFinite(calibration, DefaultCalibration), 0f, 1024f);
            Compensation = Mathf.Clamp(SanitizeFinite(compensation, DefaultCompensation), -16f, 16f);
            var minAutoEV100 = Mathf.Clamp(SanitizeFinite(autoMinEV100, DefaultAutoMinEv100), -16f, 24f);
            var maxAutoEV100 = Mathf.Clamp(SanitizeFinite(autoMaxEV100, DefaultAutoMaxEv100), -16f, 24f);
            if (maxAutoEV100 < minAutoEV100)
            {
                var swapped = minAutoEV100;
                minAutoEV100 = maxAutoEV100;
                maxAutoEV100 = swapped;
            }

            AutoMinEV100 = minAutoEV100;
            AutoMaxEV100 = maxAutoEV100;
            AutoMiddleGrey = Mathf.Clamp(SanitizeFinite(autoMiddleGrey, DefaultAutoMiddleGrey), 0.001f, 1f);
            AutoSpeedUp = Mathf.Clamp(SanitizeFinite(autoSpeedUp, DefaultAutoSpeedUp), 0.02f, 20f);
            AutoSpeedDown = Mathf.Clamp(SanitizeFinite(autoSpeedDown, DefaultAutoSpeedDown), 0.02f, 20f);
            var lowPercent = Mathf.Clamp(SanitizeFinite(autoLowPercent, DefaultAutoLowPercent), 0f, 100f);
            var highPercent = Mathf.Clamp(SanitizeFinite(autoHighPercent, DefaultAutoHighPercent), 0f, 100f);
            if (highPercent < lowPercent)
            {
                var swappedPercent = lowPercent;
                lowPercent = highPercent;
                highPercent = swappedPercent;
            }

            var histogramMinEV100 = Mathf.Clamp(SanitizeFinite(autoHistogramMinEV100, DefaultAutoHistogramMinEv100), -16f, 24f);
            var histogramMaxEV100 = Mathf.Clamp(SanitizeFinite(autoHistogramMaxEV100, DefaultAutoHistogramMaxEv100), -16f, 24f);
            if (histogramMaxEV100 < histogramMinEV100)
            {
                var swappedHistogram = histogramMinEV100;
                histogramMinEV100 = histogramMaxEV100;
                histogramMaxEV100 = swappedHistogram;
            }

            AutoLowPercent = lowPercent;
            AutoHighPercent = highPercent;
            AutoHistogramMinEV100 = histogramMinEV100;
            AutoHistogramMaxEV100 = histogramMaxEV100;
            AutoAverageLuminance = Mathf.Clamp(SanitizeFinite(autoAverageLuminance, DefaultAutoAverageLuminance), 0.000001f, 65504f);
            AutoAverageLogLuminance = Mathf.Clamp(SanitizeFinite(autoAverageLogLuminance, DefaultAutoAverageLogLuminance), -32f, 32f);
            AutoTargetEV100 = Mathf.Clamp(SanitizeFinite(autoTargetEV100, DefaultAutoTargetEv100), AutoMinEV100, AutoMaxEV100);
            AutoHasSample = autoHasSample;
            AutoReadbackPending = autoReadbackPending;
            AutoReadbackAgeFrames = Mathf.Max(DefaultAutoFrameAge, autoReadbackAgeFrames);
            AutoSampleAgeFrames = Mathf.Max(DefaultAutoFrameAge, autoSampleAgeFrames);
            AutoSampleCount = Mathf.Max(0, autoSampleCount);
            AutoSampleRejectedReason = string.IsNullOrEmpty(autoSampleRejectedReason) ? DefaultAutoSampleRejectedReason : autoSampleRejectedReason;

            if (Mode == ExposureMode.PhysicalCamera)
            {
                EV100 = Mathf.Log((Aperture * Aperture) / ShutterTime * (100f / ISO), 2f);
            }
            else if (Mode == ExposureMode.Automatic || Mode == ExposureMode.AutomaticHistogram)
            {
                EV100 = Mathf.Clamp(SanitizeFinite(autoEV100, DefaultManualEv100), AutoMinEV100, AutoMaxEV100);
            }
            else
            {
                EV100 = Mathf.Clamp(SanitizeFinite(manualEv100, DefaultManualEv100), -16f, 24f);
            }

            Multiplier = Mathf.Clamp(Mathf.Pow(2f, -EV100 + Compensation) * Calibration, 0f, 65504f);
        }

        private static ExposureMode NormalizeMode(ExposureMode mode)
        {
            switch (mode)
            {
                case ExposureMode.PhysicalCamera:
                case ExposureMode.Automatic:
                case ExposureMode.AutomaticHistogram:
                    return mode;
                default:
                    return ExposureMode.ManualEV100;
            }
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }

    internal readonly struct TonemappingFilmSettings // 定义一组 UE/XRender Filmic Tonemapping 参数，方便 C# 和 Shader 用同一套默认值。
    {
        public const float DefaultSlope = 0.88f; // 定义默认 Film Slope，对齐 XRender TonemappingComponent 和 UE Film 默认值。
        public const float DefaultToe = 0.55f; // 定义默认 Film Toe，对齐 XRender TonemappingComponent 和 UE Film 默认值。
        public const float DefaultShoulder = 0.26f; // 定义默认 Film Shoulder，对齐 XRender TonemappingComponent 和 UE Film 默认值。
        public const float DefaultBlackClip = 0.0f; // 定义默认 Film Black Clip，对齐 XRender TonemappingComponent 和 UE Film 默认值。
        public const float DefaultWhiteClip = 0.04f; // 定义默认 Film White Clip，对齐 XRender TonemappingComponent 和 UE Film 默认值。
        public const float DefaultBlueCorrection = 0.6f; // 定义默认 Blue Correction，对齐 XRender ColorGradingComponent 中参与 Tonemapping LUT 的默认值。
        public const float DefaultExpandGamut = 1.0f; // 定义默认 Expand Gamut，对齐 XRender ColorGradingComponent 中参与 Tonemapping LUT 的默认值。
        public const float DefaultToneCurveAmount = 1.0f; // 定义默认 Tone Curve Amount，对齐 XRender ColorGradingComponent 中 Tonemapping 曲线全量生效的默认值。
        public static readonly TonemappingFilmSettings Default = new TonemappingFilmSettings(DefaultSlope, DefaultToe, DefaultShoulder, DefaultBlackClip, DefaultWhiteClip, DefaultBlueCorrection, DefaultExpandGamut, DefaultToneCurveAmount); // 提供默认参数集合，供 Volume 缺失或关闭时安全回退。

        public float Slope { get; } // 保存 Film Slope，控制 Tonemapping 中间直线段的斜率。
        public float Toe { get; } // 保存 Film Toe，控制暗部压缩和黑位过渡。
        public float Shoulder { get; } // 保存 Film Shoulder，控制高光肩部压缩。
        public float BlackClip { get; } // 保存 Film Black Clip，控制暗部裁切位置。
        public float WhiteClip { get; } // 保存 Film White Clip，控制高光白位裁切位置。
        public float BlueCorrection { get; } // 保存 Blue Correction，控制 UE/XRender 的蓝色修正矩阵混合强度。
        public float ExpandGamut { get; } // 保存 Expand Gamut，控制 XRender 在 AP1 空间扩展高饱和颜色的强度。
        public float ToneCurveAmount { get; } // 保存 Tone Curve Amount，控制原始 AP1 颜色和 FilmToneMap 结果的混合比例。

        public TonemappingFilmSettings( // 定义完整参数构造函数，避免调用方分散维护默认值。
            float slope, // 接收 Film Slope。
            float toe, // 接收 Film Toe。
            float shoulder, // 接收 Film Shoulder。
            float blackClip, // 接收 Film Black Clip。
            float whiteClip, // 接收 Film White Clip。
            float blueCorrection, // 接收 Blue Correction。
            float expandGamut, // 接收 Expand Gamut。
            float toneCurveAmount) // 接收 Tone Curve Amount。
        {
            Slope = slope; // 保存 Film Slope。
            Toe = toe; // 保存 Film Toe。
            Shoulder = shoulder; // 保存 Film Shoulder。
            BlackClip = blackClip; // 保存 Film Black Clip。
            WhiteClip = whiteClip; // 保存 Film White Clip。
            BlueCorrection = blueCorrection; // 保存 Blue Correction。
            ExpandGamut = expandGamut; // 保存 Expand Gamut。
            ToneCurveAmount = toneCurveAmount; // 保存 Tone Curve Amount。
        }
    }

    internal readonly struct ColorAdjustmentsSettings // 定义 Color Adjustments 的运行时参数包，避免 Pass 直接依赖 Volume 组件字段。
    {
        public const float DefaultSaturation = 1f; // 定义默认饱和度，1 表示不改变颜色离灰轴的距离。
        public const float DefaultContrast = 1f; // 定义默认对比度，1 表示不改变中间调附近的对比关系。
        public const float DefaultGamma = 1f; // 定义默认 Gamma，1 表示不做明暗伽马调整。
        public static readonly Color DefaultColorFilter = Color.white; // 定义默认颜色滤镜，白色表示不额外染色。
        public static readonly ColorAdjustmentsSettings Default = new ColorAdjustmentsSettings(DefaultSaturation, DefaultContrast, DefaultGamma, DefaultColorFilter); // 提供默认参数集合，供 Volume 缺失或关闭时回退。

        public float Saturation { get; } // 保存饱和度参数，后处理 shader 会用它控制颜色鲜艳程度。
        public float Contrast { get; } // 保存对比度参数，后处理 shader 会用它控制明暗差异。
        public float Gamma { get; } // 保存 Gamma 参数，后处理 shader 会用它控制整体明暗曲线。
        public Color ColorFilter { get; } // 保存颜色滤镜参数，后处理 shader 会用它做通道乘法染色。

        public ColorAdjustmentsSettings( // 定义完整参数构造函数，让调用方一次性生成不可变设置。
            float saturation, // 接收饱和度。
            float contrast, // 接收对比度。
            float gamma, // 接收 Gamma。
            Color colorFilter) // 接收颜色滤镜。
        {
            Saturation = saturation; // 保存饱和度。
            Contrast = contrast; // 保存对比度。
            Gamma = gamma; // 保存 Gamma。
            ColorFilter = colorFilter; // 保存颜色滤镜。
        }
    }

    internal readonly struct ColorGradingSettings
    {
        public const float DefaultWhiteTemp = 6500f;
        public const float DefaultWhiteTint = 0f;
        public const float DefaultShadowsMax = 0.09f;
        public const float DefaultHighlightsMin = 0.5f;
        public const float DefaultHighlightsMax = 1f;
        public const float DefaultIntensity = 1f;
        public const float DefaultLutContribution = 0f;
        public const int DefaultLutSize = 16;
        public static readonly Vector4 DefaultColorVector = Vector4.one;
        public static readonly Vector4 DefaultOffsetVector = Vector4.zero;
        public static readonly ColorGradingSettings Default = new ColorGradingSettings(
            false,
            false,
            ColorGradingTemperatureMode.WhiteBalance,
            DefaultWhiteTemp,
            DefaultWhiteTint,
            false,
            DefaultColorVector,
            DefaultColorVector,
            DefaultColorVector,
            DefaultColorVector,
            DefaultOffsetVector,
            DefaultColorVector,
            DefaultColorVector,
            DefaultColorVector,
            DefaultColorVector,
            DefaultOffsetVector,
            DefaultShadowsMax,
            DefaultColorVector,
            DefaultColorVector,
            DefaultColorVector,
            DefaultColorVector,
            DefaultOffsetVector,
            DefaultColorVector,
            DefaultColorVector,
            DefaultColorVector,
            DefaultColorVector,
            DefaultOffsetVector,
            DefaultHighlightsMin,
            DefaultHighlightsMax,
            DefaultIntensity,
            null,
            DefaultLutContribution,
            DefaultLutSize);

        public bool Enabled { get; }
        public bool WhiteBalanceEnabled { get; }
        public ColorGradingTemperatureMode TemperatureMode { get; }
        public float WhiteTemp { get; }
        public float WhiteTint { get; }
        public bool ColorGradingEnabled { get; }
        public Vector4 GlobalSaturation { get; }
        public Vector4 GlobalContrast { get; }
        public Vector4 GlobalGamma { get; }
        public Vector4 GlobalGain { get; }
        public Vector4 GlobalOffset { get; }
        public Vector4 ShadowsSaturation { get; }
        public Vector4 ShadowsContrast { get; }
        public Vector4 ShadowsGamma { get; }
        public Vector4 ShadowsGain { get; }
        public Vector4 ShadowsOffset { get; }
        public float ShadowsMax { get; }
        public Vector4 MidtonesSaturation { get; }
        public Vector4 MidtonesContrast { get; }
        public Vector4 MidtonesGamma { get; }
        public Vector4 MidtonesGain { get; }
        public Vector4 MidtonesOffset { get; }
        public Vector4 HighlightsSaturation { get; }
        public Vector4 HighlightsContrast { get; }
        public Vector4 HighlightsGamma { get; }
        public Vector4 HighlightsGain { get; }
        public Vector4 HighlightsOffset { get; }
        public float HighlightsMin { get; }
        public float HighlightsMax { get; }
        public float Intensity { get; }
        public Texture Lut { get; }
        public float LutContribution { get; }
        public int LutSize { get; }
        public bool HasLut => Lut != null && LutContribution > 0f;

        public ColorGradingSettings(
            bool enabled,
            bool whiteBalanceEnabled,
            ColorGradingTemperatureMode temperatureMode,
            float whiteTemp,
            float whiteTint,
            bool colorGradingEnabled,
            Vector4 globalSaturation,
            Vector4 globalContrast,
            Vector4 globalGamma,
            Vector4 globalGain,
            Vector4 globalOffset,
            Vector4 shadowsSaturation,
            Vector4 shadowsContrast,
            Vector4 shadowsGamma,
            Vector4 shadowsGain,
            Vector4 shadowsOffset,
            float shadowsMax,
            Vector4 midtonesSaturation,
            Vector4 midtonesContrast,
            Vector4 midtonesGamma,
            Vector4 midtonesGain,
            Vector4 midtonesOffset,
            Vector4 highlightsSaturation,
            Vector4 highlightsContrast,
            Vector4 highlightsGamma,
            Vector4 highlightsGain,
            Vector4 highlightsOffset,
            float highlightsMin,
            float highlightsMax,
            float intensity,
            Texture lut,
            float lutContribution,
            int lutSize)
        {
            Enabled = enabled;
            WhiteBalanceEnabled = whiteBalanceEnabled;
            TemperatureMode = temperatureMode;
            WhiteTemp = Mathf.Clamp(SanitizeFinite(whiteTemp, DefaultWhiteTemp), 1500f, 15000f);
            WhiteTint = Mathf.Clamp(SanitizeFinite(whiteTint, DefaultWhiteTint), -1f, 1f);
            ColorGradingEnabled = colorGradingEnabled;
            GlobalSaturation = SanitizeVector(globalSaturation, DefaultColorVector);
            GlobalContrast = SanitizeVector(globalContrast, DefaultColorVector);
            GlobalGamma = SanitizeVector(globalGamma, DefaultColorVector);
            GlobalGain = SanitizeVector(globalGain, DefaultColorVector);
            GlobalOffset = SanitizeVector(globalOffset, DefaultOffsetVector);
            ShadowsSaturation = SanitizeVector(shadowsSaturation, DefaultColorVector);
            ShadowsContrast = SanitizeVector(shadowsContrast, DefaultColorVector);
            ShadowsGamma = SanitizeVector(shadowsGamma, DefaultColorVector);
            ShadowsGain = SanitizeVector(shadowsGain, DefaultColorVector);
            ShadowsOffset = SanitizeVector(shadowsOffset, DefaultOffsetVector);
            ShadowsMax = Mathf.Clamp(SanitizeFinite(shadowsMax, DefaultShadowsMax), -1f, 1f);
            MidtonesSaturation = SanitizeVector(midtonesSaturation, DefaultColorVector);
            MidtonesContrast = SanitizeVector(midtonesContrast, DefaultColorVector);
            MidtonesGamma = SanitizeVector(midtonesGamma, DefaultColorVector);
            MidtonesGain = SanitizeVector(midtonesGain, DefaultColorVector);
            MidtonesOffset = SanitizeVector(midtonesOffset, DefaultOffsetVector);
            HighlightsSaturation = SanitizeVector(highlightsSaturation, DefaultColorVector);
            HighlightsContrast = SanitizeVector(highlightsContrast, DefaultColorVector);
            HighlightsGamma = SanitizeVector(highlightsGamma, DefaultColorVector);
            HighlightsGain = SanitizeVector(highlightsGain, DefaultColorVector);
            HighlightsOffset = SanitizeVector(highlightsOffset, DefaultOffsetVector);
            HighlightsMin = Mathf.Clamp(SanitizeFinite(highlightsMin, DefaultHighlightsMin), -1f, 1f);
            HighlightsMax = Mathf.Clamp(SanitizeFinite(highlightsMax, DefaultHighlightsMax), 1f, 10f);
            Intensity = Mathf.Clamp01(SanitizeFinite(intensity, DefaultIntensity));
            Lut = lut;
            LutContribution = Mathf.Clamp01(SanitizeFinite(lutContribution, DefaultLutContribution));
            LutSize = Mathf.Clamp(lutSize, 2, 64);
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        private static Vector4 SanitizeVector(Vector4 value, Vector4 fallback)
        {
            return new Vector4(
                SanitizeFinite(value.x, fallback.x),
                SanitizeFinite(value.y, fallback.y),
                SanitizeFinite(value.z, fallback.z),
                SanitizeFinite(value.w, fallback.w));
        }
    }

    internal readonly struct RCASSettings
    {
        public const float DefaultSharpness = 0f;
        public static readonly RCASSettings Default = new RCASSettings(false, DefaultSharpness);

        public bool Enabled { get; }
        public float Sharpness { get; }

        public RCASSettings(bool enabled, float sharpness)
        {
            Enabled = enabled;
            Sharpness = Mathf.Clamp01(float.IsNaN(sharpness) || float.IsInfinity(sharpness) ? DefaultSharpness : sharpness);
        }
    }

    internal readonly struct FastApproximateAASettings
    {
        public const float DefaultSubpixel = 0.75f;
        public const float DefaultEdgeThreshold = 0.125f;
        public const float DefaultEdgeThresholdMin = 0.0312f;
        public static readonly FastApproximateAASettings Default = new FastApproximateAASettings(false, DefaultSubpixel, DefaultEdgeThreshold, DefaultEdgeThresholdMin);

        public bool Enabled { get; }
        public float Subpixel { get; }
        public float EdgeThreshold { get; }
        public float EdgeThresholdMin { get; }

        public FastApproximateAASettings(bool enabled, float subpixel, float edgeThreshold, float edgeThresholdMin)
        {
            Enabled = enabled;
            Subpixel = Mathf.Clamp01(SanitizeFinite(subpixel, DefaultSubpixel));
            EdgeThreshold = Mathf.Clamp(SanitizeFinite(edgeThreshold, DefaultEdgeThreshold), 0.0312f, 0.333f);
            EdgeThresholdMin = Mathf.Clamp(SanitizeFinite(edgeThresholdMin, DefaultEdgeThresholdMin), 0f, 0.0833f);
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }

    internal readonly struct SubpixelMorphologicalAASettings
    {
        public const float DefaultThreshold = 0.1f;
        public const float DefaultBlendStrength = 0.65f;
        public const int DefaultMaxSearchSteps = 8;
        public static readonly SubpixelMorphologicalAASettings Default = new SubpixelMorphologicalAASettings(false, DefaultThreshold, DefaultBlendStrength, DefaultMaxSearchSteps);

        public bool Enabled { get; }
        public float Threshold { get; }
        public float BlendStrength { get; }
        public int MaxSearchSteps { get; }

        public SubpixelMorphologicalAASettings(bool enabled, float threshold, float blendStrength, int maxSearchSteps)
        {
            Enabled = enabled;
            Threshold = Mathf.Clamp(SanitizeFinite(threshold, DefaultThreshold), 0.02f, 0.25f);
            BlendStrength = Mathf.Clamp01(SanitizeFinite(blendStrength, DefaultBlendStrength));
            MaxSearchSteps = Mathf.Clamp(maxSearchSteps, 1, 16);
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtBloomQuality")]

    public enum BloomQuality
    {
        Disabled = 0,
        Q1 = 1,
        Q2 = 2,
        Q3 = 3,
        Q4 = 4,
        Q5 = 5
    }

    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtBloomDebugView")]

    public enum BloomDebugView
    {
        Disabled = 0,
        Prefilter = 1,
        FinalBloom = 2,
        Mip1 = 3,
        Mip2 = 4,
        Mip3 = 5,
        Mip4 = 6,
        Mip5 = 7,
        Alpha = 8,
        ThresholdMask = 9
    }

    internal readonly struct BloomSettings // 定义 Bloom 的运行时参数包，让 Pass 不直接依赖 Volume 组件字段。
    {
        public const float DefaultThreshold = 1.0f; // 默认只让 HDR 高光进入 Bloom。
        public const float DefaultIntensity = 0.6f; // 默认强度保持可见但不过曝，便于第一版诊断。
        public const float DefaultScatter = 0.65f; // 默认散布控制上采样叠加权重。
        public const float DefaultSizeScale = 1f; // 默认保持旧 scatter 尺寸语义，新增参数只作为额外 XRender 风格尺寸倍率。
        public const BloomQuality DefaultQuality = BloomQuality.Q4; // 默认对齐当前 5-stage 行为，同时暴露 XRender 风格质量档。
        public const BloomDebugView DefaultDebugView = BloomDebugView.Disabled; // 默认不显示 Bloom 调试纹理。
        public const int DefaultMaxMipCount = 6; // 默认上限允许 Q5 使用 6 级；默认 Q4 仍保持当前 5-stage 视觉。
        public const float DefaultSoftKnee = 0.5f; // 默认软阈值让高光进入 Bloom 时更平滑。
        public const float DefaultFilter1Size = 0.3f; // 对齐 XRender PC Bloom Filter1 的默认直径百分比。
        public const float DefaultFilter2Size = 1f; // 对齐 XRender PC Bloom Filter2 的默认直径百分比。
        public const float DefaultFilter3Size = 2f; // 对齐 XRender PC Bloom Filter3 的默认直径百分比。
        public const float DefaultFilter4Size = 10f; // 对齐 XRender PC Bloom Filter4 的默认直径百分比。
        public const float DefaultFilter5Size = 30f; // 对齐 XRender PC Bloom Filter5 的默认直径百分比。
        public const float DefaultFilter6Size = 64f; // 对齐 XRender PC Bloom Filter6 的默认直径百分比。
        public static readonly Color DefaultFilter1Tint = new Color(0.3465f, 0.3465f, 0.3465f);
        public static readonly Color DefaultFilter2Tint = new Color(0.138f, 0.138f, 0.138f);
        public static readonly Color DefaultFilter3Tint = new Color(0.1176f, 0.1176f, 0.1176f);
        public static readonly Color DefaultFilter4Tint = new Color(0.066f, 0.066f, 0.066f);
        public static readonly Color DefaultFilter5Tint = new Color(0.066f, 0.066f, 0.066f);
        public static readonly Color DefaultFilter6Tint = new Color(0.061f, 0.061f, 0.061f);
        public static readonly BloomSettings Default = new BloomSettings(false, DefaultThreshold, DefaultSoftKnee, DefaultIntensity, DefaultScatter, DefaultSizeScale, DefaultQuality, DefaultMaxMipCount, false, DefaultDebugView, DefaultFilter1Size, DefaultFilter2Size, DefaultFilter3Size, DefaultFilter4Size, DefaultFilter5Size, DefaultFilter6Size, DefaultFilter1Tint, DefaultFilter2Tint, DefaultFilter3Tint, DefaultFilter4Tint, DefaultFilter5Tint, DefaultFilter6Tint); // 关闭 Bloom 的安全默认值。

        public bool Enabled { get; } // 保存 Bloom 是否启用。
        public float Threshold { get; } // 保存亮度阈值。
        public float SoftKnee { get; } // 保存软阈值。
        public float Intensity { get; } // 保存合成强度。
        public float Scatter { get; } // 保存上采样散布强度。
        public float SizeScale { get; } // 保存 XRender 风格的 Bloom 尺寸倍率。
        public BloomQuality Quality { get; } // 保存 XRender 风格 Bloom 质量档，决定最多使用多少 Filter 阶段。
        public int MaxMipCount { get; } // 保存最大 mip 数。
        public bool BloomAlphaChannel { get; } // 保存是否把 Bloom 强度写入 alpha 通道。
        public BloomDebugView DebugView { get; } // 保存 Bloom 调试输出模式。
        public float Filter1Size { get; } // 保存 Filter1 的直径百分比。
        public float Filter2Size { get; } // 保存 Filter2 的直径百分比。
        public float Filter3Size { get; } // 保存 Filter3 的直径百分比。
        public float Filter4Size { get; } // 保存 Filter4 的直径百分比。
        public float Filter5Size { get; } // 保存 Filter5 的直径百分比。
        public float Filter6Size { get; } // 保存 Filter6 的直径百分比。
        public Color Filter1Tint { get; } // 保存 Filter1 的颜色权重。
        public Color Filter2Tint { get; } // 保存 Filter2 的颜色权重。
        public Color Filter3Tint { get; } // 保存 Filter3 的颜色权重。
        public Color Filter4Tint { get; } // 保存 Filter4 的颜色权重。
        public Color Filter5Tint { get; } // 保存 Filter5 的颜色权重。
        public Color Filter6Tint { get; } // 保存 Filter6 的颜色权重。

        public static bool IsQualityEnabled(BloomQuality quality) // 判断 Bloom 质量档是否代表有效 Bloom 阶段。
        {
            return NormalizeQuality(quality) != BloomQuality.Disabled; // 非法枚举值会被归一化为 Disabled。
        }

        public BloomSettings(bool enabled, float threshold, float softKnee, float intensity, float scatter, float sizeScale, BloomQuality quality, int maxMipCount, bool bloomAlphaChannel, BloomDebugView debugView, float filter1Size, float filter2Size, float filter3Size, float filter4Size, float filter5Size, float filter6Size, Color filter1Tint, Color filter2Tint, Color filter3Tint, Color filter4Tint, Color filter5Tint, Color filter6Tint) // 收拢 Bloom 参数。
        {
            Enabled = enabled; // 保存启用状态。
            Threshold = Mathf.Clamp(SanitizeFinite(threshold, DefaultThreshold), -1f, 10f); // 保存阈值，保留 -1 的 XRender bypass 语义。
            SoftKnee = Mathf.Clamp01(SanitizeFinite(softKnee, DefaultSoftKnee)); // 保存软阈值。
            Intensity = Mathf.Clamp(SanitizeFinite(intensity, DefaultIntensity), 0f, 10f); // 保存强度。
            Scatter = Mathf.Clamp01(SanitizeFinite(scatter, DefaultScatter)); // 保存散布。
            SizeScale = Mathf.Clamp(SanitizeFinite(sizeScale, DefaultSizeScale), 0f, 64f); // 保存尺寸倍率。
            Quality = NormalizeQuality(quality); // 保存质量档，避免非法枚举值进入 mip 计算。
            MaxMipCount = Mathf.Clamp(maxMipCount, 1, 8); // 保存最大 mip 数。
            BloomAlphaChannel = bloomAlphaChannel; // 保存 Bloom alpha 输出开关。
            DebugView = NormalizeDebugView(debugView); // 保存 Bloom 调试输出模式。
            Filter1Size = Mathf.Clamp(SanitizeFinite(filter1Size, DefaultFilter1Size), 0f, 4f); // 保存 Filter1 尺寸。
            Filter2Size = Mathf.Clamp(SanitizeFinite(filter2Size, DefaultFilter2Size), 0f, 8f); // 保存 Filter2 尺寸。
            Filter3Size = Mathf.Clamp(SanitizeFinite(filter3Size, DefaultFilter3Size), 0f, 16f); // 保存 Filter3 尺寸。
            Filter4Size = Mathf.Clamp(SanitizeFinite(filter4Size, DefaultFilter4Size), 0f, 32f); // 保存 Filter4 尺寸。
            Filter5Size = Mathf.Clamp(SanitizeFinite(filter5Size, DefaultFilter5Size), 0f, 64f); // 保存 Filter5 尺寸。
            Filter6Size = Mathf.Clamp(SanitizeFinite(filter6Size, DefaultFilter6Size), 0f, 128f); // 保存 Filter6 尺寸。
            Filter1Tint = SanitizeTint(filter1Tint); // 保存 Filter1 tint。
            Filter2Tint = SanitizeTint(filter2Tint); // 保存 Filter2 tint。
            Filter3Tint = SanitizeTint(filter3Tint); // 保存 Filter3 tint。
            Filter4Tint = SanitizeTint(filter4Tint); // 保存 Filter4 tint。
            Filter5Tint = SanitizeTint(filter5Tint); // 保存 Filter5 tint。
            Filter6Tint = SanitizeTint(filter6Tint); // 保存 Filter6 tint。
        }

        private static BloomQuality NormalizeQuality(BloomQuality quality) // 归一化旧资产或脚本传入的 Bloom 质量枚举。
        {
            switch (quality)
            {
                case BloomQuality.Q1:
                case BloomQuality.Q2:
                case BloomQuality.Q3:
                case BloomQuality.Q4:
                case BloomQuality.Q5:
                    return quality; // 有效质量档保持原样。
                default:
                    return BloomQuality.Disabled; // Disabled 和非法枚举值都按关闭处理。
            }
        }

        private static BloomDebugView NormalizeDebugView(BloomDebugView debugView) // 归一化 Bloom 调试模式，避免非法枚举影响最终输出。
        {
            switch (debugView)
            {
                case BloomDebugView.Prefilter:
                case BloomDebugView.FinalBloom:
                case BloomDebugView.Mip1:
                case BloomDebugView.Mip2:
                case BloomDebugView.Mip3:
                case BloomDebugView.Mip4:
                case BloomDebugView.Mip5:
                case BloomDebugView.Alpha:
                case BloomDebugView.ThresholdMask:
                    return debugView;
                default:
                    return BloomDebugView.Disabled;
            }
        }

        private static float SanitizeFinite(float value, float fallback) // 过滤 NaN/Infinity，避免参数热改后进入 shader。
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        private static Color SanitizeTint(Color tint) // 保留 HDR tint 上限，只清掉非法和负值。
        {
            return new Color(
                Mathf.Max(0f, SanitizeFinite(tint.r, 1f)),
                Mathf.Max(0f, SanitizeFinite(tint.g, 1f)),
                Mathf.Max(0f, SanitizeFinite(tint.b, 1f)),
                Mathf.Max(0f, SanitizeFinite(tint.a, 1f)));
        }
    }

    [Serializable] // 标记这个类可以被 Unity 序列化，这样它可以作为 BurtRenderPipelineAsset 的内嵌配置显示在 Inspector 中。
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtPostProcessSettings")]
    public sealed class PostProcessSettings // 定义 BurtRP 后处理框架设置；具体效果参数后续统一放到 Global Volume。
    {
        public static readonly PostProcessSettings Disabled = new PostProcessSettings(false); // 提供关闭配置，避免旧资产字段为空时访问设置出现空引用。

        [TitleGroup("后处理框架", "用于控制 DrawTransparent 之后到 FinalBlit 之前的全屏后处理链路。")] // 使用 Odin 标题组把后处理框架配置集中显示。
        [InfoBox("Asset 只保留后处理框架级开关；Tonemapping、Bloom 等效果参数后续统一从 Global Volume 读取。")] // 用 Odin 信息框说明效果参数不再继续放在 Asset 上。
        [SerializeField, ToggleLeft, LabelText("启用后处理框架")] private bool enablePostProcessing; // 控制 BurtRP 是否在 Forward 图中插入后处理框架 Pass。

        [TitleGroup("后处理框架")] // 继续放在同一个 Odin 分组里，让框架相关开关保持在一起。
        [SerializeField, ToggleLeft, LabelText("启用 No-op Copy 验证 Pass"), ShowIf(nameof(enablePostProcessing))] private bool enableNoOpCopy = true; // 控制没有正式效果时是否仍执行 CameraColor 到 PostProcessColor 再回写 CameraColor 的验证拷贝。

        [TitleGroup("后处理框架 - 调试")] // 单独建立调试分组，避免普通使用时把日志开关和核心开关混在一起。
        [SerializeField, ToggleLeft, LabelText("输出后处理框架日志"), ShowIf(nameof(enablePostProcessing))] private bool enableFrameworkDebugLog; // 控制是否输出后处理 Pass 执行日志，默认关闭以避免每帧刷 Console。

        public bool EnablePostProcessing => enablePostProcessing; // 暴露后处理总开关给 RenderGraph 资源注册和 ForwardGraph 组装逻辑读取。

        public bool EnableNoOpCopy => enableNoOpCopy; // 暴露 No-op Copy 开关，让第一版框架可以单独关闭验证 Pass。

        public bool EnableFrameworkDebugLog => enableFrameworkDebugLog; // 暴露日志开关，Pass 执行时会先检查它再打印诊断信息。

        public bool ShouldRunNoOpCopy => enablePostProcessing && enableNoOpCopy; // ??? No-op Copy ?????? No-op ????????????

        public PostProcessSettings() // 定义 Unity 序列化需要的默认构造函数。
        {
        }

        private PostProcessSettings(bool defaultEnabled) // 定义内部构造函数，只给 Disabled 兜底配置使用。
        {
            enablePostProcessing = defaultEnabled; // 按传入默认值初始化总开关，Disabled 会传入 false。
        }
    }
}
