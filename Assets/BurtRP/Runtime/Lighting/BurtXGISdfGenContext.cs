using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public sealed class BurtXGISdfGenContext : IDisposable
    {
        public const int ClipmapBorderSize = 1;
        public const GraphicsFormat SdfGraphicsFormat = GraphicsFormat.R16_SFloat;

        public string name;
        public bool useOccupy;
        public int textureSize;
        public int clipmapCount;
        public RenderTexture sdfTexture;

        public bool IsValid => sdfTexture != null && sdfTexture.IsCreated();
        public int VolumeDepth => Mathf.Max(1, (textureSize + ClipmapBorderSize) * Mathf.Max(1, clipmapCount));

        public bool Configure(string contextName, int size, int clipmaps, bool useOccupancy, bool forceRecreate = false)
        {
            size = Mathf.Max(1, size);
            clipmaps = Mathf.Max(1, clipmaps);
            var needsRecreate = forceRecreate ||
                sdfTexture == null ||
                textureSize != size ||
                clipmapCount != clipmaps ||
                useOccupy != useOccupancy ||
                !sdfTexture.IsCreated();

            name = string.IsNullOrEmpty(contextName) ? "BurtXGI.SdfGen" : contextName;
            textureSize = size;
            clipmapCount = clipmaps;
            useOccupy = useOccupancy;

            if (!needsRecreate)
            {
                return IsValid;
            }

            DestroySdfTexture();
            sdfTexture = CreateSdfTexture();
            return IsValid;
        }

        public void Dispose()
        {
            DestroySdfTexture();
        }

        public void DestroySdfTexture()
        {
            if (sdfTexture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(sdfTexture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(sdfTexture);
            }

            sdfTexture = null;
        }

        public string ResolveStatusLabel()
        {
            if (textureSize <= 0 || clipmapCount <= 0)
            {
                return "Unconfigured";
            }

            if (VolumeDepth > SystemInfo.maxTexture3DSize)
            {
                return "Unsupported(VolumeDepth=" + VolumeDepth + ",MaxTexture3DSize=" + SystemInfo.maxTexture3DSize + ")";
            }

            return IsValid
                ? "Ready(Size=" + textureSize + ",Clipmaps=" + clipmapCount + ",UseOccupy=" + useOccupy + ")"
                : "MissingTexture(Size=" + textureSize + ",Clipmaps=" + clipmapCount + ",UseOccupy=" + useOccupy + ")";
        }

        private RenderTexture CreateSdfTexture()
        {
            var volumeDepth = VolumeDepth;
            if (volumeDepth > SystemInfo.maxTexture3DSize)
            {
                return null;
            }

            var descriptor = new RenderTextureDescriptor
            {
                width = textureSize,
                height = textureSize,
                volumeDepth = volumeDepth,
                enableRandomWrite = true,
                dimension = TextureDimension.Tex3D,
                graphicsFormat = SdfGraphicsFormat,
                msaaSamples = 1,
                depthBufferBits = 0
            };

            var texture = new RenderTexture(descriptor)
            {
                name = name
            };
            texture.Create();
            return texture;
        }
    }
}
