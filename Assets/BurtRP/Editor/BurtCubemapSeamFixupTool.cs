using System.IO;
using UnityEditor;
using UnityEngine;

namespace Burt.RenderPipeline.Editor
{
    internal static class BurtCubemapSeamFixupTool
    {
        private const int EdgePassCount = 2;
        private const int EdgeFixupWidth = 3;
        private const float EdgeBlendStrength = 0.65f;

        [MenuItem("BurtRP/IBL/Create Seam-Fixed Cubemap From Selection")]
        public static void CreateSeamFixedCubemapFromSelection()
        {
            var source = Selection.activeObject as Cubemap;
            if (source == null)
            {
                Debug.LogWarning("Select a readable Cubemap asset before running BurtRP cubemap seam fixup.");
                return;
            }

            if (!source.isReadable)
            {
                Debug.LogWarning("Cubemap must be readable. Enable Read/Write in the texture import settings, then run seam fixup again: " + AssetDatabase.GetAssetPath(source));
                return;
            }

            var output = CreateFixedCubemap(source);
            var sourcePath = AssetDatabase.GetAssetPath(source);
            var directory = string.IsNullOrEmpty(sourcePath) ? "Assets" : Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrEmpty(directory))
            {
                directory = "Assets";
            }

            var fileName = string.IsNullOrEmpty(source.name) ? "Cubemap" : source.name;
            var outputPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, fileName + "_BurtSeamFixed.cubemap").Replace('\\', '/'));
            AssetDatabase.CreateAsset(output, outputPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = output;
            Debug.Log("Created seam-fixed cubemap: " + outputPath);
        }

        [MenuItem("BurtRP/IBL/Create Seam-Fixed Cubemap From Selection", true)]
        public static bool CanCreateSeamFixedCubemapFromSelection()
        {
            return Selection.activeObject is Cubemap;
        }

        private static Cubemap CreateFixedCubemap(Cubemap source)
        {
            var format = source.format;
            var output = new Cubemap(source.width, format, source.mipmapCount > 1)
            {
                name = source.name + "_BurtSeamFixed",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = source.filterMode,
                anisoLevel = source.anisoLevel
            };

            for (var mip = 0; mip < source.mipmapCount; mip++)
            {
                var size = Mathf.Max(1, source.width >> mip);
                var faces = new Color[6][];
                for (var face = 0; face < 6; face++)
                {
                    faces[face] = source.GetPixels((CubemapFace)face, mip);
                }

                for (var pass = 0; pass < EdgePassCount; pass++)
                {
                    ApplyCubemapSeamFixup(faces, size);
                }

                for (var face = 0; face < 6; face++)
                {
                    output.SetPixels(faces[face], (CubemapFace)face, mip);
                }
            }

            output.Apply(false, false);
            return output;
        }

        private static void ApplyCubemapSeamFixup(Color[][] faces, int size)
        {
            if (size <= 1)
            {
                return;
            }

            var previousFaces = CloneFaces(faces);
            var fixupWidth = Mathf.Min(EdgeFixupWidth, Mathf.Max(1, size / 2));
            for (var face = 0; face < faces.Length; face++)
            {
                var pixels = faces[face];
                var previous = previousFaces[face];
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var leftDistance = x;
                        var rightDistance = size - 1 - x;
                        var bottomDistance = y;
                        var topDistance = size - 1 - y;
                        var edgeDistance = Mathf.Min(Mathf.Min(leftDistance, rightDistance), Mathf.Min(bottomDistance, topDistance));
                        if (edgeDistance >= fixupWidth)
                        {
                            continue;
                        }

                        var uv = PixelCenterToUV(x, y, size);
                        var color = previous[y * size + x];
                        var accum = color;
                        var weight = 1f;

                        AccumulateEdgeSample(previousFaces, face, size, leftDistance, fixupWidth, new Vector2(-(x + 0.5f) / size, uv.y), ref accum, ref weight);
                        AccumulateEdgeSample(previousFaces, face, size, rightDistance, fixupWidth, new Vector2(1f + (size - x - 0.5f) / size, uv.y), ref accum, ref weight);
                        AccumulateEdgeSample(previousFaces, face, size, bottomDistance, fixupWidth, new Vector2(uv.x, -(y + 0.5f) / size), ref accum, ref weight);
                        AccumulateEdgeSample(previousFaces, face, size, topDistance, fixupWidth, new Vector2(uv.x, 1f + (size - y - 0.5f) / size), ref accum, ref weight);
                        AccumulateCornerSample(previousFaces, face, size, leftDistance, bottomDistance, fixupWidth, new Vector2(-(x + 0.5f) / size, -(y + 0.5f) / size), ref accum, ref weight);
                        AccumulateCornerSample(previousFaces, face, size, leftDistance, topDistance, fixupWidth, new Vector2(-(x + 0.5f) / size, 1f + (size - y - 0.5f) / size), ref accum, ref weight);
                        AccumulateCornerSample(previousFaces, face, size, rightDistance, bottomDistance, fixupWidth, new Vector2(1f + (size - x - 0.5f) / size, -(y + 0.5f) / size), ref accum, ref weight);
                        AccumulateCornerSample(previousFaces, face, size, rightDistance, topDistance, fixupWidth, new Vector2(1f + (size - x - 0.5f) / size, 1f + (size - y - 0.5f) / size), ref accum, ref weight);

                        pixels[y * size + x] = accum / Mathf.Max(weight, 0.0001f);
                    }
                }
            }
        }

        private static Color[][] CloneFaces(Color[][] faces)
        {
            var clone = new Color[faces.Length][];
            for (var face = 0; face < faces.Length; face++)
            {
                clone[face] = (Color[])faces[face].Clone();
            }

            return clone;
        }

        private static void AccumulateEdgeSample(Color[][] faces, int face, int size, int distance, int fixupWidth, Vector2 sampleUV, ref Color accum, ref float weight)
        {
            if (distance >= fixupWidth)
            {
                return;
            }

            var sampleWeight = ComputeEdgeWeight(distance, fixupWidth);
            accum += SampleFaceUV(faces, face, sampleUV, size) * sampleWeight;
            weight += sampleWeight;
        }

        private static void AccumulateCornerSample(Color[][] faces, int face, int size, int distanceA, int distanceB, int fixupWidth, Vector2 sampleUV, ref Color accum, ref float weight)
        {
            if (distanceA >= fixupWidth || distanceB >= fixupWidth)
            {
                return;
            }

            var sampleWeight = Mathf.Min(ComputeEdgeWeight(distanceA, fixupWidth), ComputeEdgeWeight(distanceB, fixupWidth)) * 0.75f;
            accum += SampleFaceUV(faces, face, sampleUV, size) * sampleWeight;
            weight += sampleWeight;
        }

        private static float ComputeEdgeWeight(int distance, int fixupWidth)
        {
            return EdgeBlendStrength * saturate(1f - distance / Mathf.Max((float)fixupWidth, 0.0001f));
        }

        private static Vector2 PixelCenterToUV(int x, int y, int size)
        {
            var invSize = 1f / Mathf.Max(size, 1);
            return new Vector2((x + 0.5f) * invSize, (y + 0.5f) * invSize);
        }

        private static Color SampleFaceUV(Color[][] faces, int face, Vector2 uv, int size)
        {
            var direction = FaceUVToDirection(face, uv);
            DirectionToFaceUV(direction, out var sampleFace, out var sampleUV);
            return SampleFaceBilinear(faces[Mathf.Clamp(sampleFace, 0, 5)], sampleUV, size);
        }

        private static Color SampleFaceBilinear(Color[] pixels, Vector2 uv, int size)
        {
            var x = Mathf.Clamp01(uv.x) * (size - 1);
            var y = Mathf.Clamp01(uv.y) * (size - 1);
            var x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, size - 1);
            var y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, size - 1);
            var x1 = Mathf.Min(x0 + 1, size - 1);
            var y1 = Mathf.Min(y0 + 1, size - 1);
            var tx = x - x0;
            var ty = y - y0;
            var a = Color.Lerp(pixels[y0 * size + x0], pixels[y0 * size + x1], tx);
            var b = Color.Lerp(pixels[y1 * size + x0], pixels[y1 * size + x1], tx);
            return Color.Lerp(a, b, ty);
        }

        private static Vector3 FaceUVToDirection(int face, Vector2 uv)
        {
            var st = uv * 2f - Vector2.one;
            switch ((CubemapFace)Mathf.Clamp(face, 0, 5))
            {
                case CubemapFace.PositiveX:
                    return new Vector3(1f, -st.y, -st.x).normalized;
                case CubemapFace.NegativeX:
                    return new Vector3(-1f, -st.y, st.x).normalized;
                case CubemapFace.PositiveY:
                    return new Vector3(st.x, 1f, st.y).normalized;
                case CubemapFace.NegativeY:
                    return new Vector3(st.x, -1f, -st.y).normalized;
                case CubemapFace.PositiveZ:
                    return new Vector3(st.x, -st.y, 1f).normalized;
                default:
                    return new Vector3(-st.x, -st.y, -1f).normalized;
            }
        }

        private static void DirectionToFaceUV(Vector3 direction, out int face, out Vector2 uv)
        {
            var dir = direction.normalized;
            var absDir = new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z));
            if (absDir.x >= absDir.y && absDir.x >= absDir.z)
            {
                var invAxis = 1f / Mathf.Max(absDir.x, 0.000001f);
                if (dir.x >= 0f)
                {
                    face = (int)CubemapFace.PositiveX;
                    uv = new Vector2(-dir.z, -dir.y) * invAxis;
                }
                else
                {
                    face = (int)CubemapFace.NegativeX;
                    uv = new Vector2(dir.z, -dir.y) * invAxis;
                }
            }
            else if (absDir.y >= absDir.z)
            {
                var invAxis = 1f / Mathf.Max(absDir.y, 0.000001f);
                if (dir.y >= 0f)
                {
                    face = (int)CubemapFace.PositiveY;
                    uv = new Vector2(dir.x, dir.z) * invAxis;
                }
                else
                {
                    face = (int)CubemapFace.NegativeY;
                    uv = new Vector2(dir.x, -dir.z) * invAxis;
                }
            }
            else
            {
                var invAxis = 1f / Mathf.Max(absDir.z, 0.000001f);
                if (dir.z >= 0f)
                {
                    face = (int)CubemapFace.PositiveZ;
                    uv = new Vector2(dir.x, -dir.y) * invAxis;
                }
                else
                {
                    face = (int)CubemapFace.NegativeZ;
                    uv = new Vector2(-dir.x, -dir.y) * invAxis;
                }
            }

            uv = uv * 0.5f + Vector2.one * 0.5f;
        }

        private static float saturate(float value)
        {
            return Mathf.Clamp01(value);
        }
    }
}
