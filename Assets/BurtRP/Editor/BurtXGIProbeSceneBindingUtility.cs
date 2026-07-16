using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Burt.RenderPipeline.Editor
{
    internal static class BurtXGIProbeSceneBindingUtility
    {
        internal static BurtGIProbeSceneBinding CreateOrRefresh(
            Scene scene,
            BurtXGIProbeBakingConfig config,
            BurtXGIProbeBakedDataAsset asset,
            bool selectObject,
            bool registerUndo)
        {
            if (!scene.IsValid())
            {
                return null;
            }

            var binding = FindSceneBinding(scene);
            if (binding == null)
            {
                var go = new GameObject("Burt XGI Probe Scene Binding");
                SceneManager.MoveGameObjectToScene(go, scene);
                if (registerUndo)
                {
                    Undo.RegisterCreatedObjectUndo(go, "Create Burt XGI Probe Scene Binding");
                }

                binding = go.AddComponent<BurtGIProbeSceneBinding>();
            }
            else if (registerUndo)
            {
                Undo.RecordObject(binding, "Refresh Burt XGI Probe Scene Binding");
            }

            EnsureBindingComponents(scene, binding, registerUndo);
            var sceneGuid = ResolveSceneGuid(scene);
            if (!string.IsNullOrEmpty(sceneGuid))
            {
                binding.sceneGuid = sceneGuid;
            }
            else if (config != null && !string.IsNullOrEmpty(config.sceneGuid))
            {
                binding.sceneGuid = config.sceneGuid;
            }

            ApplyConfigAndAsset(binding, config, asset);
            ApplyOwnedProbeVolumeBounds(binding, config, asset);
            binding.ApplyConfiguration();
            MarkBindingDirty(binding, scene);
            if (selectObject && binding != null)
            {
                Selection.activeObject = binding.gameObject;
            }

            return binding;
        }

        internal static BurtGIProbeSceneBinding CreateOrRefreshForActiveScene(
            BurtXGIProbeBakingConfig config,
            BurtXGIProbeBakedDataAsset asset,
            bool selectObject,
            bool registerUndo)
        {
            return CreateOrRefresh(SceneManager.GetActiveScene(), config, asset, selectObject, registerUndo);
        }

        internal static BurtGIProbeSceneBinding FindSceneBinding(Scene scene)
        {
            var bindings = Object.FindObjectsOfType<BurtGIProbeSceneBinding>(true);
            for (var index = 0; index < bindings.Length; index++)
            {
                var binding = bindings[index];
                if (binding != null && binding.gameObject.scene == scene)
                {
                    return binding;
                }
            }

            return null;
        }

        internal static void EnsureBindingComponents(Scene scene, BurtGIProbeSceneBinding binding, bool registerUndo)
        {
            if (binding == null)
            {
                return;
            }

            binding.probeVolume ??= FindSceneComponent<BurtGIProbeVolume>(scene);
            binding.physicalPool ??= binding.GetComponent<BurtGIVirtualProbePhysicalPool>();
            binding.streamer ??= binding.GetComponent<BurtGIVirtualProbeCellStreamer>();

            if (binding.probeVolume == null)
            {
                binding.probeVolume = binding.GetComponent<BurtGIProbeVolume>() ??
                    AddComponent<BurtGIProbeVolume>(binding.gameObject, registerUndo);
            }

            binding.physicalPool ??= AddComponent<BurtGIVirtualProbePhysicalPool>(binding.gameObject, registerUndo);
            binding.streamer ??= AddComponent<BurtGIVirtualProbeCellStreamer>(binding.gameObject, registerUndo);
            binding.physicalPool.probeVolume = binding.probeVolume;
            binding.streamer.probeVolume = binding.probeVolume;
            binding.streamer.physicalPool = binding.physicalPool;
        }

        private static void ApplyOwnedProbeVolumeBounds(
            BurtGIProbeSceneBinding binding,
            BurtXGIProbeBakingConfig config,
            BurtXGIProbeBakedDataAsset asset)
        {
            if (binding == null || binding.probeVolume == null ||
                binding.probeVolume.gameObject != binding.gameObject)
            {
                return;
            }

            var bounds = asset != null && HasValidBounds(asset.globalBounds)
                ? asset.globalBounds
                : config != null && HasValidBounds(config.globalBounds)
                    ? config.globalBounds
                    : default;
            if (!HasValidBounds(bounds))
            {
                return;
            }

            binding.probeVolume.mode = BurtGIProbeVolumeMode.Scene;
            binding.probeVolume.transform.position = bounds.center;
            binding.probeVolume.transform.rotation = Quaternion.identity;
            binding.probeVolume.size = Vector3.Max(bounds.size, Vector3.one * 0.01f);
            binding.probeVolume.extent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.01f);
        }

        private static bool HasValidBounds(Bounds bounds)
        {
            var size = bounds.size;
            return size.x > 0.0001f && size.y > 0.0001f && size.z > 0.0001f;
        }

        private static void ApplyConfigAndAsset(
            BurtGIProbeSceneBinding binding,
            BurtXGIProbeBakingConfig config,
            BurtXGIProbeBakedDataAsset asset)
        {
            if (binding == null)
            {
                return;
            }

            if (asset != null && config == null)
            {
                if (asset.hasTimeSliceSH)
                {
                    RegisterTimeSliceAsset(binding.timeSliceBakedDataAssets, asset);
                }
                else
                {
                    binding.bakedDataAsset = asset;
                }

                return;
            }

            if (config == null)
            {
                return;
            }

            if (config.platform == BurtXGIProbeBakingPlatform.Mobile)
            {
                binding.mobileBakingConfig = config;
                SyncConfigTimeSliceAssets(binding.mobileTimeSliceBakedDataAssets, config);
                if (asset != null)
                {
                    if (asset.hasTimeSliceSH)
                    {
                        RegisterTimeSliceAsset(binding.mobileTimeSliceBakedDataAssets, asset);
                    }
                    else
                    {
                        binding.mobileBakedDataAsset = asset;
                    }
                }
            }
            else
            {
                binding.pcBakingConfig = config;
                binding.bakingConfig ??= config;
                SyncConfigTimeSliceAssets(binding.pcTimeSliceBakedDataAssets, config);
                if (asset != null)
                {
                    if (asset.hasTimeSliceSH)
                    {
                        RegisterTimeSliceAsset(binding.pcTimeSliceBakedDataAssets, asset);
                    }
                    else
                    {
                        binding.pcBakedDataAsset = asset;
                    }
                }
            }

            if (asset != null && !asset.hasTimeSliceSH)
            {
                binding.bakedDataAsset ??= asset;
            }
        }

        private static void SyncConfigTimeSliceAssets(
            List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> list,
            BurtXGIProbeBakingConfig config)
        {
            if (list == null)
            {
                return;
            }

            list.Clear();
            if (config == null)
            {
                return;
            }

            var assets = config.GetTimeSliceBakedDataAssets();
            for (var index = 0; index < assets.Count; index++)
            {
                var entry = assets[index];
                if (entry?.asset == null)
                {
                    continue;
                }

                RegisterTimeSliceAsset(list, entry.asset);
            }
        }

        private static void RegisterTimeSliceAsset(
            List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> list,
            BurtXGIProbeBakedDataAsset asset)
        {
            if (list == null || asset == null)
            {
                return;
            }

            for (var index = 0; index < list.Count; index++)
            {
                var entry = list[index];
                if (entry == null || entry.timeSlice != asset.timeSliceType)
                {
                    continue;
                }

                entry.asset = asset;
                return;
            }

            list.Add(new BurtGIVirtualProbeCellStreamer.TimeSliceBakedData
            {
                timeSlice = asset.timeSliceType,
                asset = asset
            });
        }

        private static T AddComponent<T>(GameObject target, bool registerUndo) where T : Component
        {
            return registerUndo ? Undo.AddComponent<T>(target) : target.AddComponent<T>();
        }

        private static void MarkBindingDirty(BurtGIProbeSceneBinding binding, Scene scene)
        {
            if (binding == null)
            {
                return;
            }

            EditorUtility.SetDirty(binding);
            if (binding.probeVolume != null)
            {
                EditorUtility.SetDirty(binding.probeVolume);
            }

            if (binding.physicalPool != null)
            {
                EditorUtility.SetDirty(binding.physicalPool);
            }

            if (binding.streamer != null)
            {
                EditorUtility.SetDirty(binding.streamer);
            }

            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static string ResolveSceneGuid(Scene scene)
        {
            return scene.IsValid() && !string.IsNullOrEmpty(scene.path)
                ? AssetDatabase.AssetPathToGUID(scene.path)
                : string.Empty;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            var components = Object.FindObjectsOfType<T>(true);
            for (var index = 0; index < components.Length; index++)
            {
                var component = components[index];
                if (component != null && component.gameObject.scene == scene)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
