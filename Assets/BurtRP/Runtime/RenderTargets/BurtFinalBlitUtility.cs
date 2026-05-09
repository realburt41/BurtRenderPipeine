using UnityEngine; // 引入 UnityEngine 命名空间，用来读取 SystemInfo 和 CameraType 等 Unity 运行时状态。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让 FinalBlit 工具可以直接访问 BurtRenderRequest。
{
    internal static class BurtFinalBlitUtility // 定义 FinalBlit 专用工具类，把最终拷贝的方向判断从 Pass 执行代码中拆出来。
    {
        public static float ResolveFinalBlitYFlip(BurtRenderRequest request) // 根据当前相机输出目标和图形 API 决定 FinalBlit 是否要翻转采样 UV 的 Y 轴。
        {
            if (!SystemInfo.graphicsUVStartsAtTop) // 如果当前平台纹理坐标不是从上往下开始，就不需要处理 D3D 类平台的上下方向差异。
            {
                return 0f; // 返回 0 表示 shader 保持原始 UV，不做 Y 翻转。
            }

            var camera = request != null ? request.Camera : null; // 从 request 中安全取得相机，request 为空时使用 null 进入兜底逻辑。

            if (camera == null) // 如果没有相机，说明无法判断最终目标类型。
            {
                return 0f; // 默认保持原始方向，避免未知相机被额外翻转后出现倒置。
            }

            if (camera.cameraType == CameraType.SceneView) // SceneView 是编辑器专用目标，当前 BurtRP 的中间 RT 拷贝到 Scene 窗口时需要修正 Y 方向。
            {
                return 1f; // 返回 1 表示 shader 用 1 - uv.y 修正 Scene 窗口上下颠倒。
            }

            if (camera.cameraType == CameraType.Preview) // Preview 相机用于 Inspector/Camera Preview 等编辑器预览窗口，方向表现和 SceneView 一样需要修正。
            {
                return 1f; // 返回 1 表示 shader 翻转 Preview 输出，修正相机预览窗口上下颠倒。
            }

            if (camera.cameraType == CameraType.Game) // GameView 在当前输出链路里不需要额外翻转，否则会把已经正确的画面再次翻转。
            {
                return 0f; // 返回 0 表示 GameView 保持原始 UV，修正“Scene 正了但 Game 反了”的情况。
            }

            return 0f; // 其他相机类型和输出到 RenderTexture 的离屏相机先保持原始方向，避免 RT 到 RT 拷贝被额外翻转。
        }
    }
}
