using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Camera、Mathf、RenderTextureDescriptor 和 RenderTextureFormat。

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

            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.Depth, 32); // 创建深度专用 RT 描述，32 位深度让深度测试更稳定。

            descriptor.msaaSamples = 1; // 当前阶段先关闭 MSAA，避免深度 RT 和相机颜色目标采样数不匹配。

            descriptor.useMipMap = false; // 深度缓冲不需要 mipmap，关闭后可以减少无意义资源开销。

            descriptor.autoGenerateMips = false; // 深度缓冲不生成 mipmap，避免 Unity 做额外工作。

            return descriptor; // 返回创建好的深度 RT 描述，供分配 Pass 使用。
        }

        public static RenderTextureDescriptor CreateMainLightShadowMapDescriptor(BurtShadowData shadowData) // 定义创建主光阴影图 RT 描述的函数。
        {
            var resolution = BurtShadowData.DefaultMainLightShadowResolution; // 使用阴影数据类提供的默认分辨率，避免这里继续写死 1024。

            if (shadowData != null) // 如果当前 request 提供了阴影数据，就优先使用灯光解析出来的分辨率。
            {
                resolution = Mathf.Max(1, shadowData.MainLightShadowResolution); // 读取主光阴影分辨率，并强制最小为 1。
            }

            var descriptor = new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.Shadowmap, 32); // 创建 Shadowmap 格式的深度纹理描述，供主光阴影 Pass 写入深度。

            descriptor.msaaSamples = 1; // 阴影图不使用 MSAA，保证后续深度采样和比较逻辑简单稳定。

            descriptor.useMipMap = false; // 阴影图当前阶段不生成 mipmap，避免多余显存和生成开销。

            descriptor.autoGenerateMips = false; // 关闭自动 mipmap 生成，防止 Unity 对深度纹理做无意义的额外处理。

            return descriptor; // 返回创建好的主光阴影图描述，供分配 Pass 使用。
        }
    }
}
