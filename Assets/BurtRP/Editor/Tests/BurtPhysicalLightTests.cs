using NUnit.Framework;
using UnityEngine;

namespace Burt.RenderPipeline.Tests
{
    public sealed class BurtPhysicalLightTests
    {
        private GameObject lightObject;
        private Light light;
        private BurtPhysicalLight physicalLight;

        [TearDown]
        public void TearDown()
        {
            if (lightObject != null)
                Object.DestroyImmediate(lightObject);
        }

        [Test]
        public void PointLightStartsInLumenWithoutChangingNativeOutput()
        {
            CreateLight(LightType.Point, 2f);

            Assert.AreEqual(BurtPhysicalLightUnit.Lumen, physicalLight.Unit);
            Assert.That(physicalLight.Intensity, Is.EqualTo(8f * Mathf.PI).Within(0.0001f));
            Assert.That(light.intensity, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void ChangingPointUnitPreservesNativeOutput()
        {
            CreateLight(LightType.Point, 4f);

            physicalLight.SetUnit(BurtPhysicalLightUnit.Candela, true);

            Assert.AreEqual(BurtPhysicalLightUnit.Candela, physicalLight.Unit);
            Assert.That(physicalLight.Intensity, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(light.intensity, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void ReenablingPhysicalModeUsesLumenAndPreservesEditedLightOutput()
        {
            CreateLight(LightType.Spot, 3f);
            physicalLight.SetUsePhysicalLightUnits(false);
            light.intensity = 7f;

            physicalLight.SetUsePhysicalLightUnits(true, true);

            Assert.AreEqual(BurtPhysicalLightUnit.Lumen, physicalLight.Unit);
            Assert.That(light.intensity, Is.EqualTo(7f).Within(0.0001f));
        }

        [Test]
        public void ExactSpotReflectorTogglePreservesNativeOutput()
        {
            CreateLight(LightType.Spot, 5f);
            var nativeBefore = light.intensity;

            physicalLight.SetExactSpotReflector(false, true);

            Assert.IsFalse(physicalLight.ExactSpotReflector);
            Assert.That(light.intensity, Is.EqualTo(nativeBefore).Within(0.0001f));
        }

        private void CreateLight(LightType type, float nativeIntensity)
        {
            lightObject = new GameObject("BRP Physical Light Test");
            light = lightObject.AddComponent<Light>();
            light.type = type;
            light.intensity = nativeIntensity;
            light.spotAngle = 30f;
            physicalLight = lightObject.AddComponent<BurtPhysicalLight>();
        }

        [Test]
        public void RectangleAreaSizeChangesPreserveLumensAndUpdateNits()
        {
            CreateLight(LightType.Area, 2f);
            physicalLight.AreaSize = new Vector2(2f, 3f);
            physicalLight.SetUnit(BurtPhysicalLightUnit.Lumen, false);
            physicalLight.Intensity = 120f * Mathf.PI;
            Assert.That(light.intensity, Is.EqualTo(20f).Within(0.0001f));

            physicalLight.AreaSize = new Vector2(3f, 4f);
            Assert.AreEqual(new Vector2(3f, 4f), light.areaSize);
            Assert.That(light.intensity, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(physicalLight.Intensity, Is.EqualTo(120f * Mathf.PI).Within(0.0001f));
        }

        [Test]
        public void EditorAuthoredRectangleSizeIsCapturedWithoutChangingOutput()
        {
            CreateLight(LightType.Area, 4f);
            physicalLight.SetUsePhysicalLightUnits(false);
            light.areaSize = new Vector2(2f, 5f);
            physicalLight.CaptureAreaSizeForPlayer();

            var serialized = new UnityEditor.SerializedObject(physicalLight);
            Assert.AreEqual(new Vector2(2f, 5f), serialized.FindProperty("areaSize").vector2Value);
            Assert.That(light.intensity, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void RectangleUnitSwitchPreservesNativeOutputAndDimensions()
        {
            CreateLight(LightType.Area, 4f);
            physicalLight.AreaSize = new Vector2(2f, 5f);
            physicalLight.SetUnit(BurtPhysicalLightUnit.Nits, false);
            physicalLight.Intensity = 4f;
            physicalLight.SetUnit(BurtPhysicalLightUnit.Lumen, true);
            Assert.That(physicalLight.Intensity, Is.EqualTo(40f * Mathf.PI).Within(0.0001f));
            Assert.That(light.intensity, Is.EqualTo(4f).Within(0.0001f));
            physicalLight.SetUnit(BurtPhysicalLightUnit.Nits, true);
            Assert.That(physicalLight.Intensity, Is.EqualTo(4f).Within(0.0001f));
            Assert.AreEqual(new Vector2(2f, 5f), physicalLight.AreaSize);
        }

        [Test]
        public void ReenabledInitializedComponentAppliesItsAuthoredOutput()
        {
            CreateLight(LightType.Point, 4f);
            physicalLight.SetUnit(BurtPhysicalLightUnit.Candela, true);
            physicalLight.Intensity = 7f;
            physicalLight.enabled = false;
            light.intensity = 2f;
            physicalLight.enabled = true;
            Assert.That(light.intensity, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(physicalLight.Intensity, Is.EqualTo(7f).Within(0.0001f));
        }
    }
}
