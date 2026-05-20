using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Camera、Mathf、RenderTextureDescriptor 和 RenderTextureFormat。
using UnityEngine.Experimental.Rendering; // 引入 GraphicsFormat，用来显式申请带 stencil 的 depth/stencil RT。
using UnityEngine.Rendering;

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让 RenderTarget 描述工具和 Pass/Graph 代码保持同一模块可见性。
{
    internal static class BurtRenderTargetDescriptorUtility // 定义渲染目标描述工具类，用来集中创建 BurtRP 自己管理的 RT 描述。
    {
        public static RenderTextureDescriptor CreateCameraColorDescriptor(Camera camera) // 定义创建中间相机颜色 RT 描述的函数。
        {
            var width = 1; // 定义默认宽度为 1，避免相机尺寸异常时创建 0 宽 RT。

            var height = 1; // 定义默认高度为 1，避免相机尺寸异常时创建 0 高 RT。

            var colorFormat = RenderTextureFormat.Default; // 定义默认颜色格式，保证普通 LDR 相机可以得到平台推荐的 backbuffer 格式。

            if (camera != null) // 如果当前 request 有有效相机，就优先从相机读取目标尺寸和 HDR 设置。
            {
                width = Mathf.Max(1, camera.pixelWidth); // 使用相机像素宽度，并强制最小为 1。

                height = Mathf.Max(1, camera.pixelHeight); // 使用相机像素高度，并强制最小为 1。

                if (camera.allowHDR) // 如果相机允许 HDR，就让中间颜色 RT 使用平台推荐的 HDR 格式。
                {
                    colorFormat = RenderTextureFormat.DefaultHDR; // 使用 Unity 的默认 HDR 格式，避免过早把高亮颜色截断到 LDR。
                }

                if (camera.targetTexture != null) // 如果相机输出到 RenderTexture，就尽量匹配目标纹理的尺寸和颜色格式。
                {
                    width = Mathf.Max(1, camera.targetTexture.width); // 使用 targetTexture 宽度，并强制最小为 1。

                    height = Mathf.Max(1, camera.targetTexture.height); // 使用 targetTexture 高度，并强制最小为 1。

                    colorFormat = camera.targetTexture.format; // 使用 targetTexture 的格式，减少 FinalBlit 写回时的格式转换差异。
                }
            }

            var descriptor = new RenderTextureDescriptor(width, height, colorFormat, 0); // 创建颜色专用 RT 描述，深度位数为 0，因为深度由 CameraDepth 独立管理。

            descriptor.msaaSamples = 1; // 当前阶段先关闭 MSAA，避免中间颜色 RT 与独立深度 RT 的采样数不匹配。

            descriptor.useMipMap = false; // 相机颜色中间 RT 不需要 mipmap，关闭后可以减少显存和生成开销。

            descriptor.autoGenerateMips = false; // 中间颜色 RT 不生成 mipmap，避免 Unity 在 FinalBlit 前做额外工作。

            return descriptor; // 返回创建好的颜色 RT 描述，供分配 Pass 使用。
        }

        public static RenderTextureDescriptor CreatePostProcessColorDescriptor(Camera camera) // 定义创建后处理中间颜色 RT 描述的函数。
        {
            var descriptor = CreateCameraColorDescriptor(camera); // 后处理中间颜色需要和 CameraColor 尺寸、HDR 和 targetTexture 格式保持一致。

            descriptor.depthBufferBits = 0; // 后处理颜色 RT 不需要深度缓冲，深度仍由 CameraDepth 单独管理。

            return descriptor; // 返回后处理颜色 RT 描述，供分配 Pass 使用。
        }

        public static RenderTextureDescriptor CreateScreenSpaceReflectionColorDescriptor(Camera camera)
        {
            var descriptor = CreateCameraColorDescriptor(camera);
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateScreenSpaceReflectionDenoisedColorDescriptor(Camera camera)
        {
            return CreateScreenSpaceReflectionColorDescriptor(camera);
        }

        public static RenderTextureDescriptor CreateScreenSpaceReflectionTemporalColorDescriptor(Camera camera)
        {
            var descriptor = CreateScreenSpaceReflectionColorDescriptor(camera);
            descriptor.useMipMap = true;
            descriptor.autoGenerateMips = false;
            descriptor.mipCount = CalculateMipCount(descriptor.width, descriptor.height);
            return descriptor;
        }

        public static RenderTextureDescriptor CreateScreenSpaceAmbientOcclusionDescriptor(Camera camera)
        {
            var descriptor = CreateCameraColorDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.R8;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateScreenSpaceSubsurfaceColorDescriptor(Camera camera)
        {
            var descriptor = CreateCameraColorDescriptor(camera);
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateScreenSpaceSubsurfaceBaseColorDescriptor(Camera camera)
        {
            var descriptor = CreateScreenSpaceSubsurfaceColorDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.ARGB32;
            descriptor.sRGB = false;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateScreenSpaceSubsurfaceEmissionDescriptor(Camera camera)
        {
            var descriptor = CreateScreenSpaceSubsurfaceColorDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.DefaultHDR;
            descriptor.sRGB = false;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateScreenSpaceSubsurfaceSetupDescriptor(Camera camera)
        {
            var descriptor = CreateScreenSpaceSubsurfaceColorDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            descriptor.enableRandomWrite = true;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateScreenSpaceSubsurfaceProfileIDAndTypeDescriptor(Camera camera)
        {
            var descriptor = CreateScreenSpaceSubsurfaceColorDescriptor(camera);
            descriptor.colorFormat = SelectScreenSpaceSubsurfaceScalarFormat(true);
            descriptor.enableRandomWrite = true;
            descriptor.sRGB = false;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateScreenSpaceSubsurfaceMaskDescriptor(Camera camera)
        {
            var descriptor = CreateScreenSpaceSubsurfaceColorDescriptor(camera);
            descriptor.colorFormat = SelectScreenSpaceSubsurfaceScalarFormat(false);
            descriptor.depthBufferBits = 0;
            descriptor.enableRandomWrite = false;
            descriptor.sRGB = false;
            return descriptor;
        }

        private static RenderTextureFormat SelectScreenSpaceSubsurfaceScalarFormat(bool randomWrite)
        {
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) &&
                (!randomWrite || SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.R8)))
            {
                return RenderTextureFormat.R8;
            }

            return RenderTextureFormat.RFloat;
        }

        public static RenderTextureDescriptor CreateScreenSpaceSubsurfaceTileDescriptor(Camera camera)
        {
            var descriptor = CreateScreenSpaceSubsurfaceColorDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.RFloat;
            descriptor.width = Mathf.Max(1, Mathf.CeilToInt(descriptor.width / 8f));
            descriptor.height = Mathf.Max(1, Mathf.CeilToInt(descriptor.height / 8f));
            descriptor.depthBufferBits = 0;
            descriptor.enableRandomWrite = true;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateScreenSpaceSubsurfaceCombineDescriptor(Camera camera)
        {
            var descriptor = CreateScreenSpaceSubsurfaceColorDescriptor(camera);
            descriptor.enableRandomWrite = true;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateScreenSpaceSubsurfaceVelocityDescriptor(Camera camera)
        {
            var descriptor = CreateScreenSpaceSubsurfaceColorDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateGBuffer0Descriptor(Camera camera) // 定义创建 Deferred GBuffer0 RT 描述的函数。
        {
            return CreateGBufferDescriptor(camera, RenderTextureFormat.ARGB32); // GBuffer0 第一版保存 baseColor.rgb 和 occlusion.a，普通 8 位通道足够起步。
        }

        public static RenderTextureDescriptor CreateGBuffer1Descriptor(Camera camera) // 定义创建 Deferred GBuffer1 RT 描述的函数。
        {
            return CreateGBufferDescriptor(camera, RenderTextureFormat.ARGBHalf); // GBuffer1 保存 oct normal.rg、metallic.b、smoothness.a；直接高光对法线量化很敏感，所以用 16 位通道避免格子状高光。
        }

        public static RenderTextureDescriptor CreateGBuffer2Descriptor(Camera camera) // 定义创建 Deferred GBuffer2 RT 描述的函数。
        {
            return CreateGBufferDescriptor(camera, RenderTextureFormat.DefaultHDR); // GBuffer2 第一版保存 emission.rgb 和 reflectance.a，使用 HDR 避免自发光过早被截断。
        }

        public static RenderTextureDescriptor CreateGBuffer3Descriptor(Camera camera)
        {
            return CreateGBufferDescriptor(camera, RenderTextureFormat.ARGBHalf);
        }

        public static RenderTextureDescriptor CreateGBuffer4Descriptor(Camera camera)
        {
            return CreateGBufferDescriptor(camera, RenderTextureFormat.ARGBHalf);
        }

        private static RenderTextureDescriptor CreateGBufferDescriptor( // 定义创建 GBuffer RT 描述的共用函数，保证五张 GBuffer 尺寸和采样设置一致。
            Camera camera, // 接收当前相机，用来匹配渲染尺寸和 targetTexture 尺寸。
            RenderTextureFormat format) // 接收当前 GBuffer 需要使用的颜色格式。
        {
            var descriptor = CreateCameraColorDescriptor(camera); // 先复用 CameraColor 的尺寸、targetTexture 尺寸和基础采样设置。

            descriptor.colorFormat = format; // 覆盖颜色格式，因为 GBuffer 布局由 Deferred 自己决定，不跟随相机 HDR 开关。

            descriptor.depthBufferBits = 0; // GBuffer 颜色目标不持有深度缓冲，Deferred 会复用独立 CameraDepth。

            descriptor.msaaSamples = 1; // Deferred 第一版不支持 MSAA GBuffer，先固定为 1 避免 MRT 采样数不一致。

            descriptor.useMipMap = false; // GBuffer 不需要 mipmap，关闭后减少显存和生成成本。

            descriptor.autoGenerateMips = false; // GBuffer 不自动生成 mipmap，避免 Unity 做无意义的后处理。

            return descriptor; // 返回创建好的 GBuffer RT 描述，供后续 Allocate GBuffer Pass 使用。
        }

        public static RenderTextureDescriptor CreateCameraDepthDescriptor(Camera camera) // 定义创建相机深度 RT 描述的函数。
        {
            var width = 1; // 定义默认宽度为 1，避免相机尺寸异常时创建 0 宽 RT。

            var height = 1; // 定义默认高度为 1，避免相机尺寸异常时创建 0 高 RT。

            if (camera != null) // 如果当前 request 有有效相机，就优先从相机读取目标尺寸。
            {
                width = Mathf.Max(1, camera.pixelWidth); // 使用相机像素宽度，并强制最小为 1。

                height = Mathf.Max(1, camera.pixelHeight); // 使用相机像素高度，并强制最小为 1。

                if (camera.targetTexture != null) // 如果相机输出到 RenderTexture，就以 targetTexture 的尺寸为准。
                {
                    width = Mathf.Max(1, camera.targetTexture.width); // 使用 targetTexture 宽度，并强制最小为 1。

                    height = Mathf.Max(1, camera.targetTexture.height); // 使用 targetTexture 高度，并强制最小为 1。
                }
            }

            var descriptor = new RenderTextureDescriptor(width, height, GraphicsFormat.None, SelectCameraDepthStencilFormat()); // 显式创建 depth/stencil RT，避免 RenderTextureFormat.Depth 被平台回落成无 stencil 的 DepthAuto。

            descriptor.msaaSamples = 1; // 当前阶段先关闭 MSAA，避免深度 RT 和相机颜色目标采样数不匹配。

            descriptor.useMipMap = false; // 深度缓冲不需要 mipmap，关闭后可以减少无意义资源开销。

            descriptor.autoGenerateMips = false; // 深度缓冲不生成 mipmap，避免 Unity 做额外工作。

            return descriptor; // 返回创建好的深度 RT 描述，供分配 Pass 使用。
        }

        private static GraphicsFormat SelectCameraDepthStencilFormat()
        {
            // Prefer D24S8 because it is broadly supported and still provides enough precision for the current deferred path.
            if (SystemInfo.IsFormatSupported(GraphicsFormat.D24_UNorm_S8_UInt, FormatUsage.Render))
            {
                return GraphicsFormat.D24_UNorm_S8_UInt;
            }

            // Fallback for platforms that expose only 32-bit float depth with stencil.
            if (SystemInfo.IsFormatSupported(GraphicsFormat.D32_SFloat_S8_UInt, FormatUsage.Render))
            {
                return GraphicsFormat.D32_SFloat_S8_UInt;
            }

            return GraphicsFormat.D24_UNorm_S8_UInt;
        }

        public static RenderTextureDescriptor CreateHiZDepthDescriptor(Camera camera)
        {
            var cameraColorDescriptor = CreateCameraColorDescriptor(camera);
            var descriptor = new RenderTextureDescriptor(
                cameraColorDescriptor.width,
                cameraColorDescriptor.height,
                RenderTextureFormat.RFloat,
                0);

            descriptor.msaaSamples = 1;
            descriptor.useMipMap = true;
            descriptor.autoGenerateMips = false;
            descriptor.mipCount = CalculateMipCount(descriptor.width, descriptor.height);
            return descriptor;
        }

        public static int CalculateMipCount(int width, int height)
        {
            var maxDimension = Mathf.Max(1, Mathf.Max(width, height));
            return Mathf.FloorToInt(Mathf.Log(maxDimension, 2f)) + 1;
        }

        public static void SetCameraTargetViewport(CommandBuffer cmd, Camera camera)
        {
            if (cmd == null)
            {
                return;
            }

            var descriptor = CreateCameraColorDescriptor(camera);
            SetViewport(cmd, descriptor.width, descriptor.height);
        }

        public static void SetViewport(CommandBuffer cmd, int width, int height)
        {
            if (cmd == null)
            {
                return;
            }

            cmd.SetViewport(new Rect(0f, 0f, Mathf.Max(1, width), Mathf.Max(1, height)));
        }

        public static RenderTextureDescriptor CreateMainLightShadowMapDescriptor(BurtShadowData shadowData) // 定义创建主光阴影图 RT 描述的函数。
        {
            var resolution = BurtShadowData.DefaultMainLightShadowResolution; // 使用阴影数据类提供的默认分辨率，避免这里继续写死 1024。

            if (shadowData != null) // 如果当前 request 提供了阴影数据，就优先使用灯光解析出来的分辨率。
            {
                resolution = BurtShadowUtility.ResolveMainLightShadowAtlasResolution(shadowData); // 读取主光阴影分辨率，并强制最小为 1。
            }

            var depthFormat = GraphicsFormatUtility.GetDepthStencilFormat(32, 0);
            var descriptor = new RenderTextureDescriptor(resolution, resolution, GraphicsFormat.None, depthFormat);
            descriptor.shadowSamplingMode = SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES2 ? ShadowSamplingMode.CompareDepths : ShadowSamplingMode.None; // 创建 Shadowmap 格式的深度纹理描述，供主光阴影 Pass 写入深度。

            descriptor.msaaSamples = 1; // 阴影图不使用 MSAA，保证后续深度采样和比较逻辑简单稳定。

            descriptor.useMipMap = false; // 阴影图当前阶段不生成 mipmap，避免多余显存和生成开销。

            descriptor.autoGenerateMips = false; // 关闭自动 mipmap 生成，防止 Unity 对深度纹理做无意义的额外处理。

            return descriptor; // 返回创建好的主光阴影图描述，供分配 Pass 使用。
        }

        public static RenderTextureDescriptor CreateAdditionalLightShadowAtlasDescriptor(BurtLightingData lightingData)
        {
            var tileResolution = lightingData != null && lightingData.AdditionalLightShadowTileResolution > 0
                ? lightingData.AdditionalLightShadowTileResolution
                : BurtLightingData.DefaultAdditionalLightShadowTileResolution;
            var atlasTileCountX = lightingData != null && lightingData.AdditionalLightShadowAtlasTileCountX > 0
                ? lightingData.AdditionalLightShadowAtlasTileCountX
                : 5;
            var atlasResolution = lightingData != null && lightingData.AdditionalLightShadowAtlasResolution > 0
                ? lightingData.AdditionalLightShadowAtlasResolution
                : tileResolution * atlasTileCountX;
            atlasResolution = Mathf.Max(1, atlasResolution);

            var depthFormat = GraphicsFormatUtility.GetDepthStencilFormat(32, 0);
            var descriptor = new RenderTextureDescriptor(atlasResolution, atlasResolution, GraphicsFormat.None, depthFormat);
            descriptor.shadowSamplingMode = SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES2 ? ShadowSamplingMode.CompareDepths : ShadowSamplingMode.None;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }
    }
}
