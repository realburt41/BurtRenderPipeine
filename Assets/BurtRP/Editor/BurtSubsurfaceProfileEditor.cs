using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Burt.RenderPipeline.Editor
{
    [CustomEditor(typeof(BurtSubsurfaceProfile))]
    internal sealed class BurtSubsurfaceProfileEditor : OdinEditor
    {
        private static readonly GUIContent PreviewSectionLabel = new GUIContent("LUT Preview / Subsurface Profile");
        private static readonly GUIContent LutLabel = new GUIContent("Lut Preview");
        private static readonly GUIContent LutViewModeLabel = new GUIContent("LUT View");
        private static readonly GUIContent CurveLabel = new GUIContent("Burley Diffusion Curve");
        private static readonly string[] LutViewModeNames = { "Profile", "Enhanced" };

        private const int LutSize = 128;
        private const int CurveSamples = 128;
        private const float LutPreviewWhitePercentile = 0.92f;
        private const int LutFormulaVersion = 2;

        private Texture2D lutTexture;
        private int previewHash;
        private int pendingPreviewHash;
        private bool previewDirty;
        private bool previewUpdateRegistered;
        private bool previewFoldout = true;
        private LutViewMode lutViewMode = LutViewMode.Profile;
        private Vector3[] curveValues;
        private Vector3[] curvePoints;
        private Color[] lutPixels;
        private Color[] lutRawPixels;
        private float[] lutLuminanceSamples;
        private float curveDistanceMax;
        private float lutPreviewWhitePoint = 1f;
        private Vector3 previewDiffuseMeanFreePath;

        protected override void OnDisable()
        {
            EditorApplication.update -= DelayedPreviewUpdate;
            DestroyPreviewTexture();
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            base.OnInspectorGUI();
            serializedObject.ApplyModifiedProperties();

            var profile = target as BurtSubsurfaceProfile;
            if (profile == null)
            {
                return;
            }

            if (BurtShaderGUIUtility.BeginSection(PreviewSectionLabel, ref previewFoldout))
            {
                DrawPreview(profile.CreateSettings());
                BurtShaderGUIUtility.EndSection();
            }
        }

        private void DrawPreview(BurtSubsurfaceProfileSettings settings)
        {
            UpdatePreview(settings);
            DrawParameterSummary(settings);
            DrawLutControls();
            DrawLutTexture();
            DrawCurveTexture();
        }

        private void DrawParameterSummary(BurtSubsurfaceProfileSettings settings)
        {
            EditorGUILayout.LabelField("Profile Data", EditorStyles.miniBoldLabel);
            DrawReadOnlyVector("Surface Albedo", ToVector3(settings.SurfaceAlbedo));
            DrawReadOnlyVector("Mean Free Path", ToVector3(settings.MeanFreePathColor) * settings.MeanFreePathDistance);
            DrawReadOnlyVector("Preview DMFP", previewDiffuseMeanFreePath);
            EditorGUILayout.LabelField("Transmission", FormatVector(new Vector3(settings.ExtinctionScale, settings.TransmissionNormalScale, settings.ScatteringDistribution)));
            DrawReadOnlyVector("Transmission Tint", ToVector3(settings.TransmissionTintColor));
            EditorGUILayout.LabelField("IOR", FormatFloat(settings.IOR));
            EditorGUILayout.LabelField("Dual Specular", FormatVector(new Vector3(settings.DualSpecularRoughness0, settings.DualSpecularRoughness1, settings.DualSpecularLobeMix)));
            EditorGUILayout.LabelField("World Unit Scale", FormatFloat(settings.WorldUnitScale));
            EditorGUILayout.LabelField("Screen Radius", FormatFloat(settings.RadiusPixels * settings.MeanFreePathScreenScale) + " px");
            EditorGUILayout.LabelField("5S Blend / Boundary / Tint", FormatVector(new Vector3(settings.Blend, settings.BoundaryBleed, settings.TintStrength)));
            EditorGUILayout.LabelField("5S Distance / Min Strength / Scale", FormatVector(new Vector3(settings.DistanceScale, settings.MinStrength, settings.MeanFreePathScreenScale)));
            EditorGUILayout.Space(4f);
        }

        private void DrawLutControls()
        {
            EditorGUI.BeginChangeCheck();
            lutViewMode = (LutViewMode)EditorGUILayout.Popup(LutViewModeLabel, (int)lutViewMode, LutViewModeNames);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyLutViewMode();
            }

            var hint = lutViewMode == LutViewMode.Display
                ? "Enhanced mode auto-exposes and compresses the LUT so the diffusion shape is easier to inspect."
                : "Profile mode shows the raw pre-integrated LUT clamped to 0-1, matching the runtime profile preview.";
            EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);
        }

        private static void DrawReadOnlyVector(string label, Vector3 value)
        {
            EditorGUILayout.LabelField(label, FormatVector(value));
        }

        private void DrawLutTexture()
        {
            if (lutTexture == null)
            {
                return;
            }

            EditorGUILayout.LabelField(LutLabel, EditorStyles.centeredGreyMiniLabel);
            var availableWidth = Mathf.Max(128f, EditorGUIUtility.currentViewWidth - 46f);
            var size = Mathf.Min(BurtSubsurfaceLutUtility.PreIntegratedLutSize, availableWidth);
            var rowRect = GUILayoutUtility.GetRect(availableWidth, size, GUILayout.ExpandWidth(true));
            var rect = new Rect(rowRect.x + (rowRect.width - size) * 0.5f, rowRect.y, size, size);

            EditorGUI.DrawPreviewTexture(rect, lutTexture, null, ScaleMode.StretchToFill);
            DrawTextureFrame(rect);
            EditorGUILayout.LabelField(GetLutCaption(), EditorStyles.miniLabel);
            EditorGUILayout.Space(6f);
        }

        private void DrawCurveTexture()
        {
            if (curveValues == null || curveValues.Length == 0)
            {
                return;
            }

            EditorGUILayout.LabelField(CurveLabel, EditorStyles.centeredGreyMiniLabel);
            var rect = GUILayoutUtility.GetRect(1f, 118f, GUILayout.ExpandWidth(true));
            DrawCurveBackground(rect);
            DrawCurveChannel(rect, 0, new Color(1f, 0.22f, 0.16f, 1f));
            DrawCurveChannel(rect, 1, new Color(0.22f, 0.78f, 0.28f, 1f));
            DrawCurveChannel(rect, 2, new Color(0.25f, 0.52f, 1f, 1f));
            DrawTextureFrame(rect);
            EditorGUILayout.LabelField("Distance: 0 to " + FormatFloat(curveDistanceMax) + " profile units    RGB curves are normalized for shape.", EditorStyles.miniLabel);
        }

        private void UpdatePreview(BurtSubsurfaceProfileSettings settings)
        {
            var hash = ComputeSettingsHash(settings);
            if (lutTexture != null && curveValues != null && hash == previewHash)
            {
                previewDirty = false;
                return;
            }

            EnsurePreviewTexture();
            if (ShouldDeferPreviewRebuild())
            {
                BurtSubsurfaceLutUtility.MarkEditorInteraction();
                previewDirty = true;
                pendingPreviewHash = hash;
                RegisterPreviewUpdate();
                return;
            }

            RebuildPreview(settings, hash);
        }

        private void RebuildPreview(BurtSubsurfaceProfileSettings settings, int hash)
        {
            previewDiffuseMeanFreePath = GetPreviewDiffuseMeanFreePath(settings);
            GenerateLut(settings);
            GenerateCurve(settings);
            previewHash = hash;
            pendingPreviewHash = 0;
            previewDirty = false;
        }

        private static bool ShouldDeferPreviewRebuild()
        {
            return GUIUtility.hotControl != 0 || EditorGUIUtility.editingTextField;
        }

        private void RegisterPreviewUpdate()
        {
            if (previewUpdateRegistered)
            {
                return;
            }

            previewUpdateRegistered = true;
            EditorApplication.update += DelayedPreviewUpdate;
        }

        private void DelayedPreviewUpdate()
        {
            if (this == null || !previewDirty)
            {
                EditorApplication.update -= DelayedPreviewUpdate;
                previewUpdateRegistered = false;
                return;
            }

            if (ShouldDeferPreviewRebuild())
            {
                BurtSubsurfaceLutUtility.MarkEditorInteraction();
                return;
            }

            EditorApplication.update -= DelayedPreviewUpdate;
            previewUpdateRegistered = false;
            Repaint();
        }

        private void EnsurePreviewTexture()
        {
            if (lutTexture != null)
            {
                return;
            }

            lutTexture = new Texture2D(LutSize, LutSize, TextureFormat.RGBA32, false, true)
            {
                name = "Burt Subsurface Profile Preview",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
        }

        private void DestroyPreviewTexture()
        {
            if (lutTexture == null)
            {
                return;
            }

            Object.DestroyImmediate(lutTexture);
            lutTexture = null;
        }

        private void GenerateLut(BurtSubsurfaceProfileSettings settings)
        {
            var surfaceAlbedo = BurtSubsurfaceLutUtility.GetSurfaceAlbedoForLut(settings);
            EnsureLutScratch();
            for (var y = 0; y < LutSize; y++)
            {
                var curvature = (y + 0.5f) / LutSize;
                for (var x = 0; x < LutSize; x++)
                {
                    var rawNoL = ((x + 0.5f) / LutSize) * 2f - 1f;
                    var value = BurtSubsurfaceLutUtility.EvaluatePreIntegratedLutSample(rawNoL, curvature, surfaceAlbedo, previewDiffuseMeanFreePath);
                    var index = y * LutSize + x;
                    lutRawPixels[index] = value;
                    lutLuminanceSamples[index] = GetLuminance(new Vector3(value.r, value.g, value.b));
                }
            }

            lutPreviewWhitePoint = CalculatePreviewWhitePoint(lutLuminanceSamples);
            ApplyLutViewMode();

            lutTexture.Apply(false, false);
        }

        private void ApplyLutViewMode()
        {
            if (lutTexture == null || lutPixels == null || lutRawPixels == null)
            {
                return;
            }

            for (var i = 0; i < lutPixels.Length; i++)
            {
                lutPixels[i] = lutViewMode == LutViewMode.Display
                    ? ToneMapPreview(lutRawPixels[i], lutPreviewWhitePoint)
                    : ClampRawPreview(lutRawPixels[i]);
            }

            lutTexture.SetPixels(lutPixels);
            lutTexture.Apply(false, false);
        }

        private void EnsureLutScratch()
        {
            var pixelCount = LutSize * LutSize;
            if (lutPixels == null || lutPixels.Length != pixelCount)
            {
                lutPixels = new Color[pixelCount];
            }

            if (lutRawPixels == null || lutRawPixels.Length != pixelCount)
            {
                lutRawPixels = new Color[pixelCount];
            }

            if (lutLuminanceSamples == null || lutLuminanceSamples.Length != pixelCount)
            {
                lutLuminanceSamples = new float[pixelCount];
            }
        }

        private string GetLutCaption()
        {
            var mode = lutViewMode == LutViewMode.Display
                ? "Enhanced tone-mapped"
                : "Profile raw";
            var whitePoint = lutViewMode == LutViewMode.Display
                ? "    Preview white point: " + FormatFloat(lutPreviewWhitePoint)
                : string.Empty;
            var pending = previewDirty && pendingPreviewHash != previewHash
                ? "    Preview pending"
                : string.Empty;
            return "X: raw N dot L (-1 to 1)    Y: curvature scale (0 to 1)    Mode: " + mode + whitePoint + pending;
        }

        private void GenerateCurve(BurtSubsurfaceProfileSettings settings)
        {
            if (curveValues == null || curveValues.Length != CurveSamples)
            {
                curveValues = new Vector3[CurveSamples];
            }

            var surfaceAlbedo = BurtSubsurfaceLutUtility.GetSurfaceAlbedoForLut(settings);
            var scalingFactor = BurtSubsurfaceLutUtility.GetSearchLightDiffuseScalingFactor(surfaceAlbedo);
            curveDistanceMax = Mathf.Max(MaxComponent(previewDiffuseMeanFreePath) * 3f, 1f);

            var maxValue = 0f;
            for (var i = 0; i < curveValues.Length; i++)
            {
                var distance = curveDistanceMax * i / Mathf.Max(curveValues.Length - 1f, 1f);
                var value = BurtSubsurfaceLutUtility.EvaluateBurleyScatteringProfile(distance, surfaceAlbedo, scalingFactor, previewDiffuseMeanFreePath);
                curveValues[i] = value;
                maxValue = Mathf.Max(maxValue, MaxComponent(value));
            }

            if (maxValue <= 0.000001f)
            {
                return;
            }

            for (var i = 0; i < curveValues.Length; i++)
            {
                curveValues[i] /= maxValue;
            }
        }

        private static Vector3 GetPreviewDiffuseMeanFreePath(BurtSubsurfaceProfileSettings settings)
        {
            return BurtSubsurfaceLutUtility.GetEffectiveDiffuseMeanFreePathForLut(settings);
        }

        private static float CalculatePreviewWhitePoint(float[] luminanceSamples)
        {
            if (luminanceSamples == null || luminanceSamples.Length == 0)
            {
                return 1f;
            }

            System.Array.Sort(luminanceSamples);
            var index = Mathf.Clamp(
                Mathf.RoundToInt((luminanceSamples.Length - 1) * LutPreviewWhitePercentile),
                0,
                luminanceSamples.Length - 1);
            return Mathf.Max(luminanceSamples[index], 0.0001f);
        }

        private static Color ToneMapPreview(Color value, float whitePoint)
        {
            var scale = 1f / Mathf.Max(whitePoint, 0.0001f);
            return new Color(
                ToneMapPreviewChannel(value.r * scale),
                ToneMapPreviewChannel(value.g * scale),
                ToneMapPreviewChannel(value.b * scale),
                1f);
        }

        private static float ToneMapPreviewChannel(float value)
        {
            value = Mathf.Max(0f, value);
            return Mathf.Clamp01(value / (1f + value));
        }

        private static Color ClampRawPreview(Color value)
        {
            return new Color(
                Mathf.Clamp01(value.r),
                Mathf.Clamp01(value.g),
                Mathf.Clamp01(value.b),
                1f);
        }

        private static float GetLuminance(Vector3 value)
        {
            return Mathf.Max(0f, value.x) * 0.2126f +
                Mathf.Max(0f, value.y) * 0.7152f +
                Mathf.Max(0f, value.z) * 0.0722f;
        }

        private static void DrawCurveBackground(Rect rect)
        {
            var background = EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.12f, 0.12f, 1f)
                : new Color(0.78f, 0.78f, 0.78f, 1f);
            var grid = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.12f)
                : new Color(0f, 0f, 0f, 0.14f);

            EditorGUI.DrawRect(rect, background);
            Handles.BeginGUI();
            Handles.color = grid;
            for (var i = 1; i < 4; i++)
            {
                var x = Mathf.Lerp(rect.xMin, rect.xMax, i / 4f);
                Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));
            }

            for (var i = 1; i < 3; i++)
            {
                var y = Mathf.Lerp(rect.yMin, rect.yMax, i / 3f);
                Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));
            }

            Handles.EndGUI();
        }

        private void DrawCurveChannel(Rect rect, int channel, Color color)
        {
            if (curvePoints == null || curvePoints.Length != CurveSamples)
            {
                curvePoints = new Vector3[CurveSamples];
            }

            for (var i = 0; i < CurveSamples; i++)
            {
                var t = i / Mathf.Max(CurveSamples - 1f, 1f);
                var value = channel == 0 ? curveValues[i].x : channel == 1 ? curveValues[i].y : curveValues[i].z;
                curvePoints[i] = new Vector3(
                    Mathf.Lerp(rect.xMin, rect.xMax, t),
                    Mathf.Lerp(rect.yMax - 4f, rect.yMin + 4f, Mathf.Clamp01(value)),
                    0f);
            }

            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAPolyLine(2.5f, curvePoints);
            Handles.EndGUI();
        }

        private static void DrawTextureFrame(Rect rect)
        {
            Handles.BeginGUI();
            Handles.color = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.35f)
                : new Color(0f, 0f, 0f, 0.35f);
            Handles.DrawAAPolyLine(
                1.5f,
                new Vector3(rect.xMin, rect.yMin),
                new Vector3(rect.xMax, rect.yMin),
                new Vector3(rect.xMax, rect.yMax),
                new Vector3(rect.xMin, rect.yMax),
                new Vector3(rect.xMin, rect.yMin));
            Handles.EndGUI();
        }

        private static int ComputeSettingsHash(BurtSubsurfaceProfileSettings settings)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + LutFormulaVersion;
                hash = AppendHash(hash, settings.SurfaceAlbedo);
                hash = AppendHash(hash, settings.MeanFreePathColor);
                hash = AppendHash(hash, settings.MeanFreePathDistance);
                hash = AppendHash(hash, settings.WorldUnitScale);
                hash = AppendHash(hash, settings.ExtinctionScale);
                hash = AppendHash(hash, settings.TransmissionNormalScale);
                hash = AppendHash(hash, settings.ScatteringDistribution);
                hash = AppendHash(hash, settings.IOR);
                hash = AppendHash(hash, settings.TransmissionTintColor);
                hash = AppendHash(hash, settings.RadiusPixels);
                return hash;
            }
        }

        private static int AppendHash(int hash, Color value)
        {
            hash = AppendHash(hash, value.r);
            hash = AppendHash(hash, value.g);
            hash = AppendHash(hash, value.b);
            hash = AppendHash(hash, value.a);
            return hash;
        }

        private static int AppendHash(int hash, float value)
        {
            return hash * 31 + Mathf.RoundToInt(value * 10000f);
        }

        private static Vector3 ToVector3(Color value)
        {
            return new Vector3(value.r, value.g, value.b);
        }

        private static float MaxComponent(Vector3 value)
        {
            return Mathf.Max(Mathf.Max(value.x, value.y), value.z);
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + FormatFloat(value.x) + ", " + FormatFloat(value.y) + ", " + FormatFloat(value.z) + ")";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###");
        }

        private enum LutViewMode
        {
            Profile,
            Display
        }
    }
}
