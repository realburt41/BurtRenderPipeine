using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    /// <summary>
    /// BRP owns the Light inspector while its pipeline asset is active. This is
    /// the same integration point used by XRender: Unity's Light remains the
    /// component users edit, while pipeline-specific data is stored alongside it.
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditorForRenderPipeline(typeof(Light), typeof(BurtRenderPipelineAsset))]
    internal sealed class BurtLightEditor : LightEditor
    {
        private static bool generalExpanded = true;
        private static bool shapeExpanded = true;
        private static bool emissionExpanded = true;
        private static bool renderingExpanded;
        private static bool shadowsExpanded = true;

        private BurtPhysicalLight[] physicalLights = Array.Empty<BurtPhysicalLight>();
        private SerializedProperty color;
        private SerializedProperty useColorTemperature;
        private SerializedProperty colorTemperature;
        private SerializedProperty renderingLayerMask;
        private SerializedProperty renderMode;

        protected override void OnEnable()
        {
            settings.OnEnable();
            EnsurePhysicalLightData();

            color = serializedObject.FindProperty("m_Color");
            useColorTemperature = serializedObject.FindProperty("m_UseColorTemperature");
            colorTemperature = serializedObject.FindProperty("m_ColorTemperature");
            renderingLayerMask = serializedObject.FindProperty("m_RenderingLayerMask");
            renderMode = serializedObject.FindProperty("m_RenderMode");

            Undo.undoRedoPerformed += RebuildAfterUndo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RebuildAfterUndo;
        }

        private void RebuildAfterUndo()
        {
            EnsurePhysicalLightData();
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            if (physicalLights.Length != targets.Length || physicalLights.Any(item => item == null))
                EnsurePhysicalLightData();

            serializedObject.Update();
            settings.Update();

            DrawGeneral();
            DrawShape();
            DrawEmission();
            DrawRendering();
            DrawShadows();

            serializedObject.ApplyModifiedProperties();
            settings.ApplyModifiedProperties();

            // Spot angle, area size and light type are Unity Light properties but
            // participate in the physical conversion. Re-apply after they change.
            foreach (var physicalLight in physicalLights)
            {
                if (physicalLight != null && physicalLight.UsePhysicalLightUnits)
                    physicalLight.ApplyToUnityLight();
            }
        }

        private void DrawGeneral()
        {
            generalExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(generalExpanded, "General");
            if (generalExpanded)
            {
                EditorGUILayout.PropertyField(settings.lightType, new GUIContent("Type"));
                if (!settings.lightType.hasMultipleDifferentValues)
                {
                    using (new EditorGUI.DisabledScope(settings.isAreaLightType))
                        settings.DrawLightmapping();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawShape()
        {
            if (settings.lightType.hasMultipleDifferentValues)
                return;

            var lightType = (LightType)settings.lightType.intValue;
            if (lightType != LightType.Spot && lightType != LightType.Rectangle && lightType != LightType.Disc)
                return;

            shapeExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(shapeExpanded, "Shape");
            if (shapeExpanded)
            {
                if (lightType == LightType.Spot)
                    settings.DrawInnerAndOuterSpotAngle();
                else
                    settings.DrawArea();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawEmission()
        {
            emissionExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(emissionExpanded, "Emission");
            if (emissionExpanded)
            {
                if (color != null)
                    EditorGUILayout.PropertyField(color, new GUIContent("Color"));

                if (useColorTemperature != null)
                {
                    EditorGUILayout.PropertyField(useColorTemperature, new GUIContent("Use Color Temperature"));
                    if (!useColorTemperature.hasMultipleDifferentValues && useColorTemperature.boolValue && colorTemperature != null)
                        EditorGUILayout.PropertyField(colorTemperature, new GUIContent("Temperature"));
                }

                DrawPhysicalIntensity();
                settings.DrawBounceIntensity();

                if (!settings.lightType.hasMultipleDifferentValues &&
                    (LightType)settings.lightType.intValue != LightType.Directional)
                {
                    settings.DrawRange();
                }

                settings.DrawCookie();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawPhysicalIntensity()
        {
            if (physicalLights.Length == 0)
            {
                settings.DrawIntensity();
                return;
            }

            var firstPhysicalLight = physicalLights[0];
            var usePhysicalMixed = physicalLights.Any(item => item.UsePhysicalLightUnits != firstPhysicalLight.UsePhysicalLightUnits);

            EditorGUI.showMixedValue = usePhysicalMixed;
            EditorGUI.BeginChangeCheck();
            var usePhysical = EditorGUILayout.Toggle(new GUIContent("Physical Light Units"), firstPhysicalLight.UsePhysicalLightUnits);
            if (EditorGUI.EndChangeCheck())
                ApplyPhysicalChange("Toggle Physical Light Units", item => item.SetUsePhysicalLightUnits(usePhysical, true));
            EditorGUI.showMixedValue = false;

            if (usePhysicalMixed)
            {
                EditorGUILayout.HelpBox("Selected lights do not use the same intensity mode.", MessageType.Info);
                return;
            }

            if (!usePhysical)
            {
                settings.DrawIntensity();
                return;
            }

            var selectedLights = targets.Cast<Light>().ToArray();
            var firstType = selectedLights[0].type;
            if (selectedLights.Any(item => item.type != firstType))
            {
                EditorGUILayout.HelpBox("Physical units cannot be multi-edited across different light types.", MessageType.Info);
                return;
            }

            var supportedUnits = GetSupportedUnits(firstType);
            if (supportedUnits.Length == 0)
            {
                EditorGUILayout.HelpBox("This light type is not supported by BRP physical light units.", MessageType.Warning);
                return;
            }

            var firstUnit = firstPhysicalLight.Unit;
            var unitMixed = physicalLights.Any(item => item.Unit != firstUnit);
            var unitIndex = Math.Max(0, Array.IndexOf(supportedUnits, firstUnit));
            var unitLabels = supportedUnits.Select(GetUnitLabel).ToArray();

            EditorGUI.showMixedValue = unitMixed;
            EditorGUI.BeginChangeCheck();
            var newUnitIndex = EditorGUILayout.Popup(new GUIContent("Unit"), unitIndex, unitLabels);
            if (EditorGUI.EndChangeCheck())
            {
                var newUnit = supportedUnits[Mathf.Clamp(newUnitIndex, 0, supportedUnits.Length - 1)];
                ApplyPhysicalChange("Change Physical Light Unit", item => item.SetUnit(newUnit, true));
                firstUnit = newUnit;
                unitMixed = false;
            }
            EditorGUI.showMixedValue = false;

            var firstIntensity = firstPhysicalLight.Intensity;
            var intensityMixed = physicalLights.Any(item => !Mathf.Approximately(item.Intensity, firstIntensity));
            var intensityLabel = unitMixed ? "Intensity" : $"Intensity ({GetUnitSymbol(firstUnit)})";

            EditorGUI.showMixedValue = intensityMixed;
            EditorGUI.BeginChangeCheck();
            var newIntensity = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent(intensityLabel), firstIntensity));
            if (EditorGUI.EndChangeCheck())
                ApplyPhysicalChange("Change Physical Light Intensity", item => item.Intensity = newIntensity);
            EditorGUI.showMixedValue = false;

            if (firstType == LightType.Spot && !unitMixed && firstUnit == BurtPhysicalLightUnit.Lumen)
            {
                var firstExact = firstPhysicalLight.ExactSpotReflector;
                var exactMixed = physicalLights.Any(item => item.ExactSpotReflector != firstExact);
                EditorGUI.showMixedValue = exactMixed;
                EditorGUI.BeginChangeCheck();
                var exact = EditorGUILayout.Toggle(
                    new GUIContent("Exact Spot Reflector", "Use the spot cone solid angle when converting lumen to candela."),
                    firstExact);
                if (EditorGUI.EndChangeCheck())
                    ApplyPhysicalChange("Change Spot Reflector Conversion", item => item.SetExactSpotReflector(exact, true));
                EditorGUI.showMixedValue = false;
            }
        }

        private void DrawRendering()
        {
            renderingExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(renderingExpanded, "Rendering");
            if (renderingExpanded)
            {
                EditorGUILayout.PropertyField(settings.cullingMask, new GUIContent("Culling Mask"));
                if (renderingLayerMask != null)
                    EditorGUILayout.PropertyField(renderingLayerMask, new GUIContent("Rendering Layer Mask"));
                if (renderMode != null)
                    EditorGUILayout.PropertyField(renderMode, new GUIContent("Render Mode"));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawShadows()
        {
            shadowsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(shadowsExpanded, "Shadows");
            if (shadowsExpanded)
            {
                if (settings.lightType.hasMultipleDifferentValues)
                {
                    EditorGUILayout.HelpBox("Shadows cannot be multi-edited across different light types.", MessageType.Info);
                }
                else
                {
                    settings.DrawShadowsType();
                    if (!settings.shadowsType.hasMultipleDifferentValues &&
                        settings.shadowsType.intValue != (int)LightShadows.None)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            if (settings.isBakedOrMixed)
                            {
                                var lightType = (LightType)settings.lightType.intValue;
                                if (lightType == LightType.Point || lightType == LightType.Spot)
                                    settings.DrawBakedShadowRadius();
                                else if (lightType == LightType.Directional)
                                    settings.DrawBakedShadowAngle();
                            }

                            if (!settings.isCompletelyBaked)
                            {
                                EditorGUILayout.PropertyField(settings.shadowsResolution, new GUIContent("Resolution"));
                                EditorGUILayout.Slider(settings.shadowsStrength, 0f, 1f, new GUIContent("Strength"));
                                EditorGUILayout.Slider(settings.shadowsBias, 0f, 10f, new GUIContent("Depth Bias"));
                                EditorGUILayout.Slider(settings.shadowsNormalBias, 0f, 10f, new GUIContent("Normal Bias"));
                                var nearPlaneMin = Mathf.Min(0.01f * settings.range.floatValue, 0.1f);
                                EditorGUILayout.Slider(settings.shadowsNearPlane, nearPlaneMin, 10f, new GUIContent("Near Plane"));
                            }
                        }
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void EnsurePhysicalLightData()
        {
            var lights = targets.Cast<Light>().ToArray();
            physicalLights = new BurtPhysicalLight[lights.Length];

            for (var index = 0; index < lights.Length; index++)
            {
                var light = lights[index];
                var physicalLight = light.GetComponent<BurtPhysicalLight>();
                if (physicalLight == null)
                    physicalLight = Undo.AddComponent<BurtPhysicalLight>(light.gameObject);

                if ((physicalLight.hideFlags & HideFlags.HideInInspector) == 0)
                {
                    Undo.RecordObject(physicalLight, "Hide BRP Physical Light Data");
                    physicalLight.hideFlags |= HideFlags.HideInInspector;
                    EditorUtility.SetDirty(physicalLight);
                }

                physicalLights[index] = physicalLight;
            }
        }

        private void ApplyPhysicalChange(string undoName, Action<BurtPhysicalLight> change)
        {
            var affectedObjects = physicalLights
                .Where(item => item != null)
                .SelectMany(item => new UnityEngine.Object[] { item, item.GetComponent<Light>() })
                .Where(item => item != null)
                .ToArray();
            Undo.RecordObjects(affectedObjects, undoName);

            foreach (var physicalLight in physicalLights)
            {
                if (physicalLight == null)
                    continue;

                change(physicalLight);
                EditorUtility.SetDirty(physicalLight);
                var light = physicalLight.GetComponent<Light>();
                EditorUtility.SetDirty(light);
                PrefabUtility.RecordPrefabInstancePropertyModifications(physicalLight);
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            }
        }

        private static BurtPhysicalLightUnit[] GetSupportedUnits(LightType lightType)
        {
            switch (lightType)
            {
                case LightType.Directional:
                    return new[] { BurtPhysicalLightUnit.Lux };
                case LightType.Point:
                case LightType.Spot:
                    return new[] { BurtPhysicalLightUnit.Lumen, BurtPhysicalLightUnit.Candela };
                case LightType.Rectangle:
                    return new[] { BurtPhysicalLightUnit.Lumen, BurtPhysicalLightUnit.Nits };
                default:
                    return Array.Empty<BurtPhysicalLightUnit>();
            }
        }

        private static GUIContent GetUnitLabel(BurtPhysicalLightUnit unit)
        {
            switch (unit)
            {
                case BurtPhysicalLightUnit.Lux:
                    return new GUIContent("Lux (lx)");
                case BurtPhysicalLightUnit.Lumen:
                    return new GUIContent("Lumen (lm)");
                case BurtPhysicalLightUnit.Candela:
                    return new GUIContent("Candela (cd)");
                case BurtPhysicalLightUnit.Nits:
                    return new GUIContent("Nits (nt)");
                default:
                    return new GUIContent(unit.ToString());
            }
        }

        private static string GetUnitSymbol(BurtPhysicalLightUnit unit)
        {
            switch (unit)
            {
                case BurtPhysicalLightUnit.Lux:
                    return "lx";
                case BurtPhysicalLightUnit.Lumen:
                    return "lm";
                case BurtPhysicalLightUnit.Candela:
                    return "cd";
                case BurtPhysicalLightUnit.Nits:
                    return "nt";
                default:
                    return unit.ToString();
            }
        }
    }
}
