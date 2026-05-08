using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [CreateAssetMenu(menuName = "Rendering/Burt Render Pipeline Asset", fileName = "BurtRenderPipelineAsset")]
    public sealed class BurtRenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField] private Color clearColor = new(0.02f, 0.02f, 0.025f, 1f);
        public Color ClearColor => clearColor;
        
        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline()
        {
            return new BurtRenderPipeline(this);
        }
    }
}