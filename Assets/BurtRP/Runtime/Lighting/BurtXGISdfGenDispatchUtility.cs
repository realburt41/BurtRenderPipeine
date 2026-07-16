using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public static class BurtXGISdfGenDispatchUtility
    {
        private const int ThreadGroupSizeXY = 8;

        private static readonly int SdfTemp1Id = Shader.PropertyToID("_BurtXGISdfGenTemp1");
        private static readonly int SdfTemp2Id = Shader.PropertyToID("_BurtXGISdfGenTemp2");
        private static readonly int FillUVTextureSizeId = Shader.PropertyToID("_XGISdfGenFillUVPass_TextureSize");
        private static readonly int FillUVClipmapIndexAndCountId = Shader.PropertyToID("_XGISdfGenFillUVPass_ClipmapIndexAndCount");
        private static readonly int FillUVOccupancyId = Shader.PropertyToID("_XGISdfGenFillUVPass_VoxelOccupy");
        private static readonly int FillUVTemp1Id = Shader.PropertyToID("_RW_XGISdfGenFillUVPass_SdfTemp1");
        private static readonly int JFATextureSizeId = Shader.PropertyToID("_XGISdfGenJFAPass_TextureSize");
        private static readonly int JFATemp1Id = Shader.PropertyToID("_XGISdfGenJFAPass_SdfTemp1");
        private static readonly int JFATemp2Id = Shader.PropertyToID("_RW_XGISdfGenJFAPass_SdfTemp2");
        private static readonly int FinalizeTextureSizeId = Shader.PropertyToID("_XGISdfGenFinalizePass_TextureSize");
        private static readonly int FinalizeClipmapIndexAndCountId = Shader.PropertyToID("_XGISdfGenFinalizePass_ClipmapIndexAndCount");
        private static readonly int FinalizeTemp2Id = Shader.PropertyToID("_XGISdfGenFinalizePass_SdfTemp2");
        private static readonly int FinalizeResultId = Shader.PropertyToID("_RW_XGISdfGenFinalizePass_SdfResult");
        private static readonly int DebugRenderTargetId = Shader.PropertyToID("_RW_XGISdfGenDebugPass_RenderTarget");
        private static readonly int DebugParamsId = Shader.PropertyToID("_XGISdfGenDebugPass_Params");
        private static readonly int SdfApplyTextureId = Shader.PropertyToID("_XGISdfApply_SdfTexture");
        private static readonly int SdfApplyTextureSizeId = Shader.PropertyToID("_XGISdfApply_TextureSize");

        public static bool TryDispatchGenerateFromOccupancy(
            CommandBuffer cmd,
            ComputeShader shader,
            RenderTargetIdentifier occupancyTexture,
            BurtXGISdfGenContext context,
            int clipmapIndex)
        {
            if (cmd == null || shader == null || context == null || !context.IsValid)
            {
                return false;
            }

            clipmapIndex = Mathf.Clamp(clipmapIndex, 0, Mathf.Max(0, context.clipmapCount - 1));
            if (!TryFindKernels(shader, out var fillUVKernel, out var jfaKernel, out var finalizeKernel))
            {
                return false;
            }

            var descriptor = CreateTempDescriptor(context.textureSize);
            cmd.GetTemporaryRT(SdfTemp1Id, descriptor, FilterMode.Point);
            cmd.GetTemporaryRT(SdfTemp2Id, descriptor, FilterMode.Point);

            DispatchFillUV(cmd, shader, fillUVKernel, occupancyTexture, context, clipmapIndex);
            cmd.CopyTexture(new RenderTargetIdentifier(SdfTemp1Id), new RenderTargetIdentifier(SdfTemp2Id));

            DispatchJFA(cmd, shader, jfaKernel, context);

            DispatchFinalize(cmd, shader, finalizeKernel, context, clipmapIndex);

            cmd.ReleaseTemporaryRT(SdfTemp1Id);
            cmd.ReleaseTemporaryRT(SdfTemp2Id);
            return true;
        }

        public static bool HasRequiredKernels(ComputeShader shader)
        {
            return TryFindKernels(shader, out _, out _, out _);
        }

        public static bool TryDispatchDebugSdf(
            CommandBuffer cmd,
            ComputeShader shader,
            RenderTargetIdentifier renderTarget,
            BurtXGISdfGenContext context,
            int width,
            int height,
            BurtXGIToolsSdfDebugLayer debugLayer,
            float initMin,
            float slice01)
        {
            if (cmd == null ||
                shader == null ||
                context == null ||
                !context.IsValid ||
                !shader.HasKernel("VisualizeSdfCS") ||
                width <= 0 ||
                height <= 0)
            {
                return false;
            }

            var kernel = shader.FindKernel("VisualizeSdfCS");
            cmd.SetComputeTextureParam(shader, kernel, DebugRenderTargetId, renderTarget);
            cmd.SetComputeTextureParam(shader, kernel, SdfApplyTextureId, context.sdfTexture);
            cmd.SetComputeVectorParam(
                shader,
                SdfApplyTextureSizeId,
                new Vector4(
                    Mathf.Max(1, context.textureSize),
                    Mathf.Max(1, context.textureSize),
                    Mathf.Max(1, context.textureSize),
                    BurtXGISdfGenContext.ClipmapBorderSize));
            cmd.SetComputeVectorParam(
                shader,
                DebugParamsId,
                new Vector4(
                    Mathf.Max(0.001f, initMin),
                    (float)debugLayer,
                    Mathf.Clamp01(slice01),
                    1f));
            cmd.DispatchCompute(shader, kernel, GroupsXY(width), GroupsXY(height), 1);
            return true;
        }

        private static bool TryFindKernels(ComputeShader shader, out int fillUVKernel, out int jfaKernel, out int finalizeKernel)
        {
            fillUVKernel = -1;
            jfaKernel = -1;
            finalizeKernel = -1;
            if (shader == null ||
                !shader.HasKernel("FillUV") ||
                !shader.HasKernel("JFA") ||
                !shader.HasKernel("Finalize"))
            {
                return false;
            }

            fillUVKernel = shader.FindKernel("FillUV");
            jfaKernel = shader.FindKernel("JFA");
            finalizeKernel = shader.FindKernel("Finalize");
            return fillUVKernel >= 0 && jfaKernel >= 0 && finalizeKernel >= 0;
        }

        private static RenderTextureDescriptor CreateTempDescriptor(int textureSize)
        {
            return new RenderTextureDescriptor
            {
                width = textureSize,
                height = textureSize,
                volumeDepth = textureSize,
                enableRandomWrite = true,
                dimension = TextureDimension.Tex3D,
                graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                msaaSamples = 1,
                depthBufferBits = 0
            };
        }

        private static void DispatchFillUV(
            CommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            RenderTargetIdentifier occupancyTexture,
            BurtXGISdfGenContext context,
            int clipmapIndex)
        {
            var textureSize = context.textureSize;
            cmd.SetComputeVectorParam(shader, FillUVTextureSizeId, new Vector4(textureSize, 1f / textureSize, 0f, 0f));
            cmd.SetComputeVectorParam(shader, FillUVClipmapIndexAndCountId, new Vector4(clipmapIndex, context.clipmapCount, 0f, 0f));
            cmd.SetComputeTextureParam(shader, kernel, FillUVOccupancyId, occupancyTexture);
            cmd.SetComputeTextureParam(shader, kernel, FillUVTemp1Id, new RenderTargetIdentifier(SdfTemp1Id));
            cmd.DispatchCompute(shader, kernel, GroupsXY(textureSize), GroupsXY(textureSize), textureSize);
        }

        private static void DispatchJFA(CommandBuffer cmd, ComputeShader shader, int kernel, BurtXGISdfGenContext context)
        {
            var textureSize = context.textureSize;
            var maxLevel = Mathf.Max(0, Mathf.RoundToInt(Mathf.Log(textureSize, 2f)));
            for (var i = 0; i <= maxLevel; ++i)
            {
                var jumpOffset = 1 << Mathf.Max(0, maxLevel - i);
                cmd.SetComputeVectorParam(shader, JFATextureSizeId, new Vector4(textureSize, 1f / textureSize, jumpOffset, 0f));
                cmd.SetComputeTextureParam(shader, kernel, JFATemp1Id, new RenderTargetIdentifier(SdfTemp1Id));
                cmd.SetComputeTextureParam(shader, kernel, JFATemp2Id, new RenderTargetIdentifier(SdfTemp2Id));
                cmd.DispatchCompute(shader, kernel, GroupsXY(textureSize), GroupsXY(textureSize), textureSize);
                cmd.CopyTexture(new RenderTargetIdentifier(SdfTemp2Id), new RenderTargetIdentifier(SdfTemp1Id));
            }
        }

        private static void DispatchFinalize(
            CommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            BurtXGISdfGenContext context,
            int clipmapIndex)
        {
            var textureSize = context.textureSize;
            cmd.SetComputeVectorParam(shader, FinalizeTextureSizeId, new Vector4(textureSize, 1f / textureSize, 0f, 0f));
            cmd.SetComputeVectorParam(shader, FinalizeClipmapIndexAndCountId, new Vector4(clipmapIndex, context.clipmapCount, BurtXGISdfGenContext.ClipmapBorderSize, 0f));
            cmd.SetComputeTextureParam(shader, kernel, FinalizeTemp2Id, new RenderTargetIdentifier(SdfTemp2Id));
            cmd.SetComputeTextureParam(shader, kernel, FinalizeResultId, context.sdfTexture);
            cmd.DispatchCompute(shader, kernel, GroupsXY(textureSize), GroupsXY(textureSize), textureSize);
        }

        private static int GroupsXY(int textureSize)
        {
            return Mathf.Max(1, (textureSize + ThreadGroupSizeXY - 1) / ThreadGroupSizeXY);
        }
    }
}
