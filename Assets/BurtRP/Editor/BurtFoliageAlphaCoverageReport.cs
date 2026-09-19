using System;
using System.Collections.Generic;
using UnityEngine;

namespace Burt.RenderPipeline.Editor
{
    // Saved as a subasset of the new texture. Never marks the source dirty.
    public sealed class BurtFoliageAlphaCoverageReport : ScriptableObject
    {
        [Serializable] public sealed class Mip
        {
            public int level, width, height;
            public float before, after, scale;
            public bool flat, retainedOriginal;
        }
        public string policy = "Approximate red-channel bilinear-repeat threshold coverage. BC4 mip0 and flat mips retained byte-for-byte; never auto-assigns materials. Not mesh-UV-area weighted or a ghost-free guarantee.";
        public Texture2D source;
        public string sourceSha256, resultSha256;
        public float cutoff, targetCoverage;
        public int measurementGrid = 1024;
        public List<Mip> mips = new List<Mip>();
    }
}
