using System;
using Burt.RenderPipeline;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Burt.RenderPipeline.Editor
{
    internal static class BurtXGIComponentMenuItems
    {
        [MenuItem("GameObject/Light/Burt XGI Light", false, 2100)]
        private static void CreateXGILight(MenuCommand menuCommand)
        {
            CreateXGILight(menuCommand, "Burt XGI Light");
        }

        [MenuItem("GameObject/Light/XGI Light", false, 2109)]
        private static void CreateXRenderNamedXGILight(MenuCommand menuCommand)
        {
            CreateXGILight(menuCommand, "XGI Light");
        }

        private static void CreateXGILight(MenuCommand menuCommand, string name)
        {
            var existing = UnityEngine.Object.FindObjectOfType<BurtXGILightComponent>();
            if (existing != null)
            {
                throw new Exception("Scene can only have one XGI Light component, there already exists one [" + existing.name + "]");
            }

            var parent = menuCommand.context as GameObject;
            var gameObject = CoreEditorUtils.CreateGameObject(name, parent);
            gameObject.AddComponent<BurtXGILightComponent>();
            Selection.activeGameObject = gameObject;
        }

        [MenuItem("GameObject/Light/Burt XGI Probe Volume", false, 2101)]
        private static void CreateXGIProbeVolume(MenuCommand menuCommand)
        {
            CreateProbeVolume(menuCommand, "Burt XGI Probe Volume");
        }

        [MenuItem("GameObject/Light/Burt XGI Probe Adjust Volume", false, 2102)]
        private static void CreateXGIProbeAdjustVolume(MenuCommand menuCommand)
        {
            CreateProbeAdjustVolume(menuCommand, "Burt XGI Probe Adjust Volume");
        }

        [MenuItem("GameObject/Light/Burt XGI Voxel Light", false, 2103)]
        private static void CreateXGIVoxelLight(MenuCommand menuCommand)
        {
            CreateVoxelLight(menuCommand, "Burt XGI Voxel Light");
        }

        [MenuItem("GameObject/Light/Burt XGI Probe Scene Binding", false, 2104)]
        private static void CreateXGIProbeSceneBinding(MenuCommand menuCommand)
        {
            var parent = menuCommand.context as GameObject;
            var gameObject = CoreEditorUtils.CreateGameObject("Burt XGI Probe Scene Binding", parent);
            var volume = gameObject.AddComponent<BurtGIProbeVolume>();
            InitializeProbeVolume(volume);
            var pool = gameObject.AddComponent<BurtGIVirtualProbePhysicalPool>();
            var streamer = gameObject.AddComponent<BurtGIVirtualProbeCellStreamer>();
            var binding = gameObject.AddComponent<BurtGIProbeSceneBinding>();

            pool.probeVolume = volume;
            streamer.probeVolume = volume;
            streamer.physicalPool = pool;
            binding.probeVolume = volume;
            binding.physicalPool = pool;
            binding.streamer = streamer;

            Selection.activeGameObject = gameObject;
        }

        [MenuItem("GameObject/Light/Burt XRender Pivot", false, 2105)]
        private static void CreateXRenderPivot(MenuCommand menuCommand)
        {
            var parent = menuCommand.context as GameObject;
            var gameObject = CoreEditorUtils.CreateGameObject("Burt XRender Pivot", parent);
            gameObject.AddComponent<BurtXRenderPivot>();
            Selection.activeGameObject = gameObject;
        }

        [MenuItem("GameObject/Light/Burt XGI Probe Streaming Pivot", false, 2106)]
        private static void CreateXGIProbeStreamingPivot(MenuCommand menuCommand)
        {
            var parent = menuCommand.context as GameObject;
            var gameObject = CoreEditorUtils.CreateGameObject("Burt XGI Probe Streaming Pivot", parent);
            gameObject.AddComponent<BurtGIProbeStreamingPivot>();
            Selection.activeGameObject = gameObject;
        }

        [MenuItem("GameObject/Light/XGI Probe Volume", false, 2110)]
        private static void CreateXRenderNamedXGIProbeVolume(MenuCommand menuCommand)
        {
            CreateProbeVolume(menuCommand, "XGI Probe Volume");
        }

        [MenuItem("GameObject/Light/XGI Probe Adjust Volume", false, 2111)]
        private static void CreateXRenderNamedXGIProbeAdjustVolume(MenuCommand menuCommand)
        {
            CreateProbeAdjustVolume(menuCommand, "XGI Probe Adjust Volume");
        }

        [MenuItem("GameObject/Light/XGI Voxel Light", false, 2112)]
        private static void CreateXRenderNamedXGIVoxelLight(MenuCommand menuCommand)
        {
            CreateVoxelLight(menuCommand, "XGIVoxelLight");
        }

        private static void CreateProbeVolume(MenuCommand menuCommand, string name)
        {
            var parent = menuCommand.context as GameObject;
            var gameObject = CoreEditorUtils.CreateGameObject(name, parent);
            InitializeProbeVolume(gameObject.AddComponent<BurtGIProbeVolume>());
            Selection.activeGameObject = gameObject;
        }

        private static void InitializeProbeVolume(BurtGIProbeVolume volume)
        {
            if (volume == null)
            {
                return;
            }

            volume.size = new Vector3(10f, 10f, 10f);
            volume.extent = 5f;
        }

        private static void CreateProbeAdjustVolume(MenuCommand menuCommand, string name)
        {
            var parent = menuCommand.context as GameObject;
            var gameObject = CoreEditorUtils.CreateGameObject(name, parent);
            gameObject.AddComponent<BurtXGIProbeAdjustVolume>();
            Selection.activeGameObject = gameObject;
        }

        private static void CreateVoxelLight(MenuCommand menuCommand, string name)
        {
            var parent = menuCommand.context as GameObject;
            var gameObject = CoreEditorUtils.CreateGameObject(name, parent);
            gameObject.AddComponent<BurtGIVoxelLight>();
            Selection.activeGameObject = gameObject;
        }
    }
}
