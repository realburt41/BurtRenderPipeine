using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    // Explicit authoring operation, not an automatic importer or runtime fix.
    // BC4/readable restriction preserves the original compressed base mip.
    public static class BurtFoliageAlphaCoverageBaker
    {
        public const string DecoderPath = "Assets/BurtRP/Editor/Resources/BurtFoliageAlphaDecode.compute";
        const int Grid = 1024;
        public static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        public static float[] DecodeMip(Texture2D source, int mip)
        {
            int w = Math.Max(1, source.width >> mip), h = Math.Max(1, source.height >> mip);
            var asset = AssetDatabase.LoadAssetAtPath<ComputeShader>(DecoderPath);
            if (asset == null) throw new InvalidOperationException("Missing foliage alpha decoder");
            var shader = UnityEngine.Object.Instantiate(asset);
            var rt = new RenderTexture(w, h, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear) { enableRandomWrite = true };
            try
            {
                if (!rt.Create()) throw new InvalidOperationException("Alpha decode target allocation failed");
                int kernel = shader.FindKernel("DecodeMip");
                shader.SetTexture(kernel, "_Source", source); shader.SetTexture(kernel, "_Output", rt);
                shader.SetInt("_Mip", mip); shader.SetInts("_Size", w, h);
                shader.Dispatch(kernel, (w + 7) / 8, (h + 7) / 8, 1);
                var request = AsyncGPUReadback.Request(rt, 0); request.WaitForCompletion();
                if (request.hasError) throw new InvalidOperationException("Alpha mip GPU readback failed");
                return request.GetData<float>().ToArray();
            }
            finally { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(shader); }
        }
        public static float Coverage(float[] p, int w, int h, int grid, float cutoff)
        {
            int covered = 0;
            for (int y = 0; y < grid; y++) for (int x = 0; x < grid; x++)
            {
                float px = (x + .5f) * w / grid - .5f, py = (y + .5f) * h / grid - .5f;
                int ix = Mathf.FloorToInt(px), iy = Mathf.FloorToInt(py);
                int x0 = (ix + w) % w, x1 = (ix + 1 + w) % w, y0 = (iy + h) % h, y1 = (iy + 1 + h) % h;
                float value = Mathf.Lerp(Mathf.Lerp(p[y0 * w + x0], p[y0 * w + x1], px - ix),
                    Mathf.Lerp(p[y1 * w + x0], p[y1 * w + x1], px - ix), py - iy);
                if (value >= cutoff) covered++;
            }
            return covered / (float)(grid * grid);
        }
        static int MipBytes(int w, int h) => ((w + 3) / 4) * ((h + 3) / 4) * 8;
        public static Texture2D Bake(Texture2D source, float cutoff, string outputAssetPath)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Bake in Edit Mode, outside measured renders");
            if (source == null || !source.isReadable || source.format != TextureFormat.BC4 || source.mipmapCount < 2)
                throw new ArgumentException("Requires a readable BC4 red-channel alpha texture with mips; use a readable copy, not an automatic reimport of the source");
            if (!(cutoff > 0 && cutoff < 1)) throw new ArgumentOutOfRangeException(nameof(cutoff));
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback || !SystemInfo.SupportsTextureFormat(TextureFormat.BC4))
                throw new NotSupportedException("BC4, compute and GPU readback required for validated baking");
            outputAssetPath = outputAssetPath.Replace('\\', '/');
            if (!outputAssetPath.StartsWith("Assets/", StringComparison.Ordinal) || !outputAssetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                || outputAssetPath.Contains("/../") || outputAssetPath.Contains("/./") || !AssetDatabase.IsValidFolder(Path.GetDirectoryName(outputAssetPath).Replace('\\', '/')))
                throw new ArgumentException("Choose a new .asset in an existing Assets folder");
            if (File.Exists(outputAssetPath) || AssetDatabase.LoadMainAssetAtPath(outputAssetPath) != null)
                throw new IOException("Destination exists; original assets are never overwritten");
            var originalBytes = source.GetRawTextureData<byte>().ToArray();
            var report = ScriptableObject.CreateInstance<BurtFoliageAlphaCoverageReport>();
            report.name = "Alpha coverage bake report"; report.source = AssetDatabase.Contains(source) ? source : null;
            report.cutoff = cutoff; report.sourceSha256 = Hash(originalBytes);
            var result = new Texture2D(source.width, source.height, TextureFormat.RFloat, true, true)
            {
                name = Path.GetFileNameWithoutExtension(outputAssetPath), filterMode = source.filterMode,
                wrapModeU = source.wrapModeU, wrapModeV = source.wrapModeV, anisoLevel = source.anisoLevel, mipMapBias = source.mipMapBias
            };
            bool created = false;
            try
            {
                if (result.mipmapCount != source.mipmapCount) throw new ArgumentException("Complete mip chain required");
                int expectedBytes = 0;
                for (int mip = 0; mip < source.mipmapCount; mip++)
                {
                    int w = Math.Max(1, source.width >> mip), h = Math.Max(1, source.height >> mip);
                    expectedBytes += MipBytes(w, h);
                    float[] original = DecodeMip(source, mip);
                    float min = 1, max = 0;
                    foreach (float v in original)
                    {
                        if (float.IsNaN(v) || float.IsInfinity(v) || v < 0 || v > 1) throw new InvalidDataException("Invalid alpha data");
                        min = Math.Min(min, v); max = Math.Max(max, v);
                    }
                    float before = Coverage(original, w, h, Grid, cutoff), scale = 1;
                    if (mip == 0) report.targetCoverage = before;
                    var row = new BurtFoliageAlphaCoverageReport.Mip { level = mip, width = w, height = h, before = before, flat = min == max };
                    float error = Math.Abs(before - report.targetCoverage);
                    var trial = new float[original.Length];
                    if (mip > 0 && !row.flat)
                    {
                        float low = 0, high = 8;
                        int fitGrid = Math.Max(128, Math.Min(Grid, Math.Max(w, h) * 2));
                        for (int step = 0; step < 24; step++)
                        {
                            float candidate = (low + high) * .5f;
                            for (int j = 0; j < trial.Length; j++) trial[j] = Mathf.Clamp01(original[j] * candidate);
                            float coverage = Coverage(trial, w, h, fitGrid, cutoff), e = Math.Abs(coverage - report.targetCoverage);
                            if (e < error || (e == error && Math.Abs(candidate - 1) < Math.Abs(scale - 1))) { error = e; scale = candidate; }
                            if (coverage < report.targetCoverage) low = candidate; else high = candidate;
                        }
                    }
                    for (int j = 0; j < trial.Length; j++) trial[j] = Mathf.Clamp01(original[j] * scale);
                    result.SetPixelData(trial, mip); row.scale = scale; report.mips.Add(row);
                }
                if (originalBytes.Length != expectedBytes) throw new InvalidDataException("Unexpected BC4 layout");
                result.Apply(false, false); EditorUtility.CompressTexture(result, TextureFormat.BC4, TextureCompressionQuality.Best);
                if (result.format != TextureFormat.BC4) throw new InvalidOperationException("BC4 compression failed");
                result.Apply(false, false);
                byte[] baked = result.GetRawTextureData<byte>().ToArray();
                if (baked.Length != originalBytes.Length) throw new InvalidOperationException("Compressed mip layout changed");
                int offset = 0;
                foreach (var row in report.mips)
                {
                    row.after = Coverage(DecodeMip(result, row.level), row.width, row.height, Grid, cutoff);
                    // Compression is part of the result, not an unmeasured final step.
                    // Preserve flat/base mips and reject a worsened measured mip.
                    row.retainedOriginal = row.level == 0 || row.flat || Math.Abs(row.after - report.targetCoverage) > Math.Abs(row.before - report.targetCoverage);
                    int bytes = MipBytes(row.width, row.height);
                    if (row.retainedOriginal) { Buffer.BlockCopy(originalBytes, offset, baked, offset, bytes); row.after = row.before; row.scale = 1; }
                    offset += bytes;
                }
                result.LoadRawTextureData(baked); result.Apply(false, false);
                report.resultSha256 = Hash(baked);
                if (Hash(source.GetRawTextureData<byte>().ToArray()) != report.sourceSha256) throw new InvalidOperationException("Source mutation detected");
                AssetDatabase.CreateAsset(result, outputAssetPath); created = true;
                AssetDatabase.AddObjectToAsset(report, result);
                EditorUtility.SetDirty(result); EditorUtility.SetDirty(report); AssetDatabase.SaveAssetIfDirty(result);
                return result;
            }
            catch
            {
                if (created) AssetDatabase.DeleteAsset(outputAssetPath);
                else { UnityEngine.Object.DestroyImmediate(result); UnityEngine.Object.DestroyImmediate(report); }
                throw;
            }
        }
    }
    public sealed class BurtFoliageAlphaCoverageWindow : EditorWindow
    {
        Texture2D source; float cutoff = .5f;
        [MenuItem("Assets/BurtRP/Bake Foliage Alpha Coverage Copy")]
        static void Open() { var window = GetWindow<BurtFoliageAlphaCoverageWindow>("Foliage Alpha Coverage"); window.source = Selection.activeObject as Texture2D; }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Creates a NEW BC4 red-channel alpha asset. Requires a readable BC4 source. Mip0 is preserved; original texture/materials are untouched. Match the material cutoff and validate distance/atlas coverage before assigning the copy.", MessageType.Info);
            source = (Texture2D)EditorGUILayout.ObjectField("Source alpha (R)", source, typeof(Texture2D), false);
            cutoff = EditorGUILayout.Slider("Material cutoff", cutoff, .01f, .99f);
            using (new EditorGUI.DisabledScope(source == null || EditorApplication.isPlaying))
                if (GUILayout.Button("Bake new asset..."))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Bake alpha coverage copy", source.name + "_Coverage", "asset", "Choose a new texture asset");
                    if (!string.IsNullOrEmpty(path))
                    {
                        try { Selection.activeObject = BurtFoliageAlphaCoverageBaker.Bake(source, cutoff, path); }
                        catch (Exception e) { Debug.LogException(e); }
                    }
                }
        }
    }
}
