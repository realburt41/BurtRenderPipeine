using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Burt.RenderPipeline.Editor
{
    internal static class BurtMaterialRefreshMenu
    {
        private const string RefreshSelectedPath = "Assets/BurtRP/Refresh Selected Materials";
        private const string RefreshFolderPath = "Assets/BurtRP/Refresh Materials In Folder";

        [MenuItem(RefreshSelectedPath, false, 2200)]
        private static void RefreshSelectedMaterials()
        {
            RefreshMaterials(CollectSelectedMaterials(includeFolders: false));
        }

        [MenuItem(RefreshSelectedPath, true)]
        private static bool CanRefreshSelectedMaterials()
        {
            return CollectSelectedMaterials(includeFolders: false).Count > 0;
        }

        [MenuItem(RefreshFolderPath, false, 2201)]
        private static void RefreshMaterialsInFolder()
        {
            RefreshMaterials(CollectSelectedMaterials(includeFolders: true));
        }

        [MenuItem(RefreshFolderPath, true)]
        private static bool CanRefreshMaterialsInFolder()
        {
            return CollectSelectedMaterials(includeFolders: true).Count > 0;
        }

        private static HashSet<Material> CollectSelectedMaterials(bool includeFolders)
        {
            HashSet<Material> materials = new HashSet<Material>();
            foreach (Object selectedObject in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selectedObject);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    if (includeFolders)
                    {
                        CollectMaterialsFromFolder(path, materials);
                    }

                    continue;
                }

                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null)
                {
                    materials.Add(material);
                }
            }

            return materials;
        }

        private static void CollectMaterialsFromFolder(string folderPath, HashSet<Material> materials)
        {
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
            foreach (string guid in materialGuids)
            {
                string materialPath = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material != null)
                {
                    materials.Add(material);
                }
            }
        }

        private static void RefreshMaterials(HashSet<Material> materials)
        {
            int changedCount = 0;
            foreach (Material material in materials)
            {
                if (ApplyKnownShaderState(material))
                {
                    EditorUtility.SetDirty(material);
                    changedCount++;
                }
            }

            if (changedCount > 0)
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"BurtRP refreshed {changedCount} material(s).");
        }

        private static bool ApplyKnownShaderState(Material material)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            switch (material.shader.name)
            {
                case "BurtRP/Lit":
                    BurtLitShaderGUI.ValidateMaterialState(material);
                    return true;
                case "BurtRP/Hair":
                    BurtHairShaderGUI.ValidateMaterialState(material);
                    return true;
                case "BurtRP/UnlitColor":
                    BurtUnlitShaderGUI.ValidateMaterialState(material);
                    return true;
                default:
                    return false;
            }
        }
    }
}
