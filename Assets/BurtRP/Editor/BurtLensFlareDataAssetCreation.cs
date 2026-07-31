using UnityEditor;
using UnityEngine;

namespace Burt.RenderPipeline.Editor
{
    internal static class BurtLensFlareDataAssetCreation
    {
        private const string PrimeFlarePath = "Assets/BurtRP/Prime_Flare.TGA";
        private const string RainbowRingPath = "Assets/BurtRP/Rainbow_Ring.TGA";
        private const string CircleBokehPath = "Assets/BurtRP/Circle_Bokeh.TGA";
        private const string FlareLinePath = "Assets/BurtRP/Flare_Line.TGA";

        [MenuItem("Assets/Create/BurtRP/Post Processing/Lens Flare Data", false, 1)]
        private static void CreateLensFlareData()
        {
            var asset = ScriptableObject.CreateInstance<LensFlareData>();
            var circleBokeh = AssetDatabase.LoadAssetAtPath<Texture2D>(CircleBokehPath);
            asset.element1Tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PrimeFlarePath);
            asset.element2Tex = AssetDatabase.LoadAssetAtPath<Texture2D>(RainbowRingPath);
            asset.element3Tex = circleBokeh;
            asset.element4Tex = circleBokeh;
            asset.element5Tex = circleBokeh;
            asset.lineTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FlareLinePath);

            ProjectWindowUtil.CreateAsset(asset, "LensFlareData.asset");
        }
    }
}
