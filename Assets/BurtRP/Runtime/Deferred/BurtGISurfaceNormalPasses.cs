using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    // Hair's GBuffer0 stores strand direction for its BRDF. Screen GI instead
    // needs the material surface normal, including its normal map.
    internal static class BurtGISurfaceNormalUtility
    {
        public const string ResourceName = "BurtGISurfaceNormal";
        public static readonly int TextureId = Shader.PropertyToID("_BurtGISurfaceNormalTexture");
        private static ComputeShader mergeShader;

        public static BurtRenderTargetHandle Get(BurtRenderGraphContext context)
        {
            return context != null && context.ResourceRegistry != null
                ? context.ResourceRegistry.GetRenderTarget(ResourceName)
                : BurtRenderTargetHandle.Invalid(ResourceName);
        }

        public static BurtRenderTargetHandle Resolve(BurtRenderGraphContext context)
        {
            var target = Get(context);
            return target.IsValid ? target : context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(ResourceName);
        }

        public static void Read(BurtRenderPassBuilder builder)
        {
            builder.ReadGBuffer0();
            if (builder.ResourceRegistry.GetRenderTarget(ResourceName).IsValid)
                builder.ReadRenderTarget(ResourceName);
        }

        public static ComputeShader MergeShader
        {
            get
            {
                if (!mergeShader) mergeShader = Resources.Load<ComputeShader>("BurtGISurfaceNormalMerge");
                if (!mergeShader) throw new InvalidOperationException("Missing BurtGISurfaceNormalMerge compute shader.");
                return mergeShader;
            }
        }
    }

    internal sealed class BurtAllocateGISurfaceNormalPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate GI Surface Normal";
        public override void Configure(BurtRenderPassBuilder builder) => builder.WriteRenderTarget(BurtGISurfaceNormalUtility.ResourceName);

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = BurtGISurfaceNormalUtility.Get(context);
            if (!target.IsValid) return;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateGBuffer0Descriptor(context.Request.Camera);
            if (!SystemInfo.IsFormatSupported(descriptor.graphicsFormat, FormatUsage.LoadStore))
                descriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            descriptor.enableRandomWrite = true;
            descriptor.msaaSamples = 1;
            descriptor.sRGB = false;
            var cmd = context.AcquireCommandBuffer(Name);
            cmd.GetTemporaryRT(BurtGISurfaceNormalUtility.TextureId, descriptor, FilterMode.Point);
            cmd.SetRenderTarget(target.Identifier);
            cmd.ClearRenderTarget(false, true, Color.clear);
            context.ExecuteLegacyCommandBuffer(cmd);
            context.ReleaseCommandBuffer(cmd);
        }
    }

    internal sealed class BurtMergeGISurfaceNormalPass : BurtRenderPass
    {
        public override string Name => "Burt Merge GI Surface Normal";
        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.ReadGBuffer0();
            builder.ReadGBuffer2();
            builder.ReadRenderTarget(BurtGISurfaceNormalUtility.ResourceName);
            builder.WriteRenderTarget(BurtGISurfaceNormalUtility.ResourceName);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = BurtGISurfaceNormalUtility.Get(context);
            if (!target.IsValid) return;
            var shader = BurtGISurfaceNormalUtility.MergeShader;
            int kernel = shader.FindKernel("MergeSurfaceNormal");
            var descriptor = BurtRenderTargetDescriptorUtility.CreateGBuffer0Descriptor(context.Request.Camera);
            var cmd = context.AcquireCommandBuffer(Name);
            cmd.SetComputeTextureParam(shader, kernel, "_BurtGBuffer0", context.GBuffer0Target.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, "_BurtGBuffer2", context.GBuffer2Target.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, BurtGISurfaceNormalUtility.TextureId, target.Identifier);
            cmd.SetComputeVectorParam(shader, "_BurtGISurfaceNormalSize", new Vector4(descriptor.width, descriptor.height, 0, 0));
            cmd.DispatchCompute(shader, kernel, (descriptor.width + 7) / 8, (descriptor.height + 7) / 8, 1);
            context.ExecuteLegacyCommandBuffer(cmd);
            context.ReleaseCommandBuffer(cmd);
        }
    }

    internal sealed class BurtReleaseGISurfaceNormalPass : BurtRenderPass
    {
        public override string Name => "Burt Release GI Surface Normal";
        public override void Configure(BurtRenderPassBuilder builder) => builder.ReadRenderTarget(BurtGISurfaceNormalUtility.ResourceName);
        public override void Execute(BurtRenderGraphContext context)
        {
            var cmd = context.AcquireCommandBuffer(Name);
            cmd.ReleaseTemporaryRT(BurtGISurfaceNormalUtility.TextureId);
            context.ExecuteLegacyCommandBuffer(cmd);
            context.ReleaseCommandBuffer(cmd);
        }
    }
}
