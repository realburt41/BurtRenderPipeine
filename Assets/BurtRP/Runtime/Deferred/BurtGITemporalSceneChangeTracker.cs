using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    // A content-change hint, not an RGB detector. Sampling noise must not keep
    // the final gather in its fast response mode once the scene is stationary.
    // One instance belongs to one indirect-history camera state.
    internal sealed class BurtGITemporalSceneChangeTracker
    {
        private const int ReactiveRenderCount = 8;
        private readonly Dictionary<int, ulong> materialSignatures = new Dictionary<int, ulong>();
        private readonly List<Material> materials = new List<Material>();
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        private bool hasSignature;
        private ulong previousSignature;
        private int lastRenderIndex = -1;
        private int remainingReactiveRenders;
        private bool currentReactive;

        public bool Evaluate(Camera camera, Renderer[] renderers, int renderIndex)
        {
            if (camera == null) return false;
            if (lastRenderIndex == renderIndex) return currentReactive;
            lastRenderIndex = renderIndex;
            var signature = CaptureSignature(camera, renderers);
            if (hasSignature && signature != previousSignature)
                remainingReactiveRenders = ReactiveRenderCount;
            previousSignature = signature;
            hasSignature = true;
            currentReactive = remainingReactiveRenders > 0;
            if (remainingReactiveRenders > 0) --remainingReactiveRenders;
            return currentReactive;
        }

        public void Reset()
        {
            hasSignature = false;
            lastRenderIndex = -1;
            remainingReactiveRenders = 0;
            currentReactive = false;
            materials.Clear();
            materialSignatures.Clear();
            propertyBlock.Clear();
        }

        private ulong CaptureSignature(Camera camera, Renderer[] renderers)
        {
            ulong hash = 14695981039346656037UL;
            Add(ref hash, camera.cullingMask);
            materialSignatures.Clear();
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled ||
                    (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                materials.Clear();
                renderer.GetSharedMaterials(materials);
                bool contributes = false;
                for (int i = 0; i < materials.Count; ++i)
                    contributes |= materials[i] != null && materials[i].renderQueue <= (int)RenderQueue.GeometryLast;
                if (!contributes) continue;
                Add(ref hash, renderer.GetInstanceID());
                Add(ref hash, renderer.localToWorldMatrix);
                Add(ref hash, renderer.bounds.center);
                Add(ref hash, renderer.bounds.extents);
                bool hasOverrides = renderer.HasPropertyBlock();
                for (int i = 0; i < materials.Count; ++i)
                {
                    var material = materials[i];
                    if (material == null || material.renderQueue > (int)RenderQueue.GeometryLast) continue;
                    Add(ref hash, i);
                    Add(ref hash, GetMaterialSignature(material));
                    if (hasOverrides)
                    {
                        renderer.GetPropertyBlock(propertyBlock);
                        AddOverrides(ref hash, material.shader);
                        renderer.GetPropertyBlock(propertyBlock, i);
                        AddOverrides(ref hash, material.shader);
                    }
                }
            }

            var lights = Object.FindObjectsOfType<Light>();
            System.Array.Sort(lights, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            foreach (var light in lights)
            {
                if (!light.enabled || (light.cullingMask & camera.cullingMask) == 0) continue;
                Add(ref hash, light.GetInstanceID());
                Add(ref hash, light.transform.localToWorldMatrix);
                Add(ref hash, (Vector4)light.color);
                Add(ref hash, light.intensity);
                Add(ref hash, light.range);
                Add(ref hash, light.spotAngle);
                Add(ref hash, light.innerSpotAngle);
                Add(ref hash, (int)light.type);
                Add(ref hash, (int)light.shadows);
                Add(ref hash, light.shadowStrength);
                Add(ref hash, light.cullingMask);
                Add(ref hash, light.useColorTemperature ? 1 : 0);
                Add(ref hash, light.colorTemperature);
                AddTexture(ref hash, light.cookie);
            }
            materials.Clear();
            materialSignatures.Clear();
            return hash;
        }

        private ulong GetMaterialSignature(Material material)
        {
            int id = material.GetInstanceID();
            if (materialSignatures.TryGetValue(id, out var signature)) return signature;
            signature = 14695981039346656037UL;
            Add(ref signature, id);
            Add(ref signature, material.ComputeCRC());
            var shader = material.shader;
            Add(ref signature, shader != null ? shader.GetInstanceID() : 0);
            if (shader != null)
                for (int i = 0; i < shader.GetPropertyCount(); ++i)
                    if (shader.GetPropertyType(i) == ShaderPropertyType.Texture)
                        AddTexture(ref signature, material.GetTexture(shader.GetPropertyNameId(i)));
            materialSignatures.Add(id, signature);
            return signature;
        }

        private void AddOverrides(ref ulong hash, Shader shader)
        {
            if (shader == null || propertyBlock.isEmpty) return;
            for (int i = 0; i < shader.GetPropertyCount(); ++i)
            {
                int id = shader.GetPropertyNameId(i);
                if (!propertyBlock.HasProperty(id)) continue;
                Add(ref hash, id);
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color:
                    case ShaderPropertyType.Vector:
                        Add(ref hash, propertyBlock.GetVector(id));
                        break;
                    case ShaderPropertyType.Texture:
                        AddTexture(ref hash, propertyBlock.GetTexture(id));
                        break;
                    default:
                        Add(ref hash, propertyBlock.GetFloat(id));
                        break;
                }
            }
        }

        private static void AddTexture(ref ulong hash, Texture texture)
        {
            Add(ref hash, texture != null ? texture.GetInstanceID() : 0);
            if (texture != null) Add(ref hash, unchecked((int)texture.updateCount));
        }
        private static void Add(ref ulong hash, ulong value)
        {
            Add(ref hash, unchecked((int)value));
            Add(ref hash, unchecked((int)(value >> 32)));
        }
        private static void Add(ref ulong hash, int value)
        {
            unchecked { hash = (hash ^ (uint)value) * 1099511628211UL; }
        }
        private static void Add(ref ulong hash, float value) => Add(ref hash, value.GetHashCode());
        private static void Add(ref ulong hash, Vector3 value)
        {
            Add(ref hash, value.x); Add(ref hash, value.y); Add(ref hash, value.z);
        }
        private static void Add(ref ulong hash, Vector4 value)
        {
            Add(ref hash, value.x); Add(ref hash, value.y); Add(ref hash, value.z); Add(ref hash, value.w);
        }
        private static void Add(ref ulong hash, Matrix4x4 value)
        {
            for (int i = 0; i < 16; ++i) Add(ref hash, value[i]);
        }
    }
}
