using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Tests
{
    public sealed class BurtLightingDataTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void NoVisibleLightsDoNotCreateMainLightEnergy()
        {
            var data = CreateResetData(0);
            ApplyWithoutAtmosphere(data);
            AssertNoMainLight(data);
        }

        [Test]
        public void PointOnlySceneDoesNotCreatePhantomDirectionalLight()
        {
            var data = CreateResetData(1);
            Resolve(data, LightType.Point, Color.white);
            ApplyWithoutAtmosphere(data);
            AssertNoMainLight(data);
        }

        [Test]
        public void RealDirectionalLightPreservesItsColor()
        {
            var data = CreateResetData(1);
            var color = new Color(2f, .4f, .1f, 1f);
            Resolve(data, LightType.Directional, color);
            ApplyWithoutAtmosphere(data);
            Assert.IsTrue(data.HasMainLight);
            Assert.AreEqual(0, data.MainLightIndex);
            Assert.AreEqual(color, data.MainLightColor);
            Assert.AreEqual(color, data.MainLightColorOuterSpace);
        }

        private static BurtLightingData CreateResetData(int count)
        {
            var data = (BurtLightingData)Activator.CreateInstance(typeof(BurtLightingData), true);
            typeof(BurtLightingData).GetMethod("ResetToDefaults", PrivateInstance).Invoke(data, new object[] { count });
            return data;
        }

        private static void Resolve(BurtLightingData data, LightType type, Color color)
        {
            var lights = new NativeArray<VisibleLight>(1, Allocator.Temp);
            try
            {
                lights[0] = new VisibleLight { lightType = type, finalColor = color, localToWorldMatrix = Matrix4x4.identity };
                typeof(BurtLightingData).GetMethod("ResolveMainLight", PrivateInstance).Invoke(data, new object[] { lights });
            }
            finally { lights.Dispose(); }
        }

        private static void ApplyWithoutAtmosphere(BurtLightingData data)
        {
            typeof(BurtLightingData).GetMethod("ApplyAtmosphereTransmittance", PrivateInstance).Invoke(data, new object[] { false });
        }

        private static void AssertNoMainLight(BurtLightingData data)
        {
            Assert.IsFalse(data.HasMainLight);
            Assert.AreEqual(-1, data.MainLightIndex);
            Assert.AreEqual(Color.black, data.MainLightColor);
            Assert.AreEqual(Color.black, data.MainLightColorOuterSpace);
        }
    }
}
