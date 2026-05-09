// 引入 UnityEngine 命名空间，用来访问 Camera、CameraType 等 Unity 相机类型。
using UnityEngine;

// 定义 Burt 自己的渲染管线命名空间，保证工具类和 request、pipeline 位于同一个模块。
namespace Burt.RenderPipeline
{
    // 集中处理“Unity 相机应该被 BurtRP 归类成哪种渲染请求”的规则，避免 BurtRenderRequest 继续承担分类细节。
    internal static class BurtCameraUtility
    {
        // 根据 Unity Camera 和可选的 BurtCameraData 推导 BurtRP 的 request 类型。
        public static BurtRenderRequestType ResolveRequestType(Camera camera, BurtCameraData cameraData)
        {
            // 如果传入的相机为空，说明调用方没有可分类的对象，直接返回 Unknown 作为安全兜底。
            if (camera == null)
            {
                // 返回未知类型，让上层可以把它当成异常或无效请求处理。
                return BurtRenderRequestType.Unknown;
            }

            // 当前版本暂时没有从 BurtCameraData 上读取 UI/Overlay 类型字段，但保留参数是为了以后扩展分类规则时不用改调用点。
            _ = cameraData;

            // Unity 的 Preview 相机通常来自材质球预览、Inspector 预览或编辑器内部预览，需要单独标记为 Preview request。
            if (camera.cameraType == CameraType.Preview)
            {
                // 返回 Preview 类型，保持现有预览相机路径不变。
                return BurtRenderRequestType.Preview;
            }

            // 当前 BurtCameraData 还没有定义 UI 相机标记，所以除 Preview 以外的相机继续保持 MainCamera 行为。
            return BurtRenderRequestType.MainCamera;
        }
    }
}
