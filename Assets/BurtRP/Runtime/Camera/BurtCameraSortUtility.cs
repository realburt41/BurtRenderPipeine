// 引入 UnityEngine 命名空间，用来访问 Camera、Mathf 等 Unity 类型和数学工具。
using UnityEngine;

// 定义 Burt 自己的渲染管线命名空间，让排序工具和 request、pipeline 使用同一套类型。
namespace Burt.RenderPipeline
{
    // 集中维护相机 request 的排序层计算和比较规则，避免排序逻辑散落在 BurtRenderRequest 与 BurtRenderPipeline 中。
    internal static class BurtCameraSortUtility
    {
        // 根据当前相机状态计算排序层；这个函数会在每帧创建 BurtRenderRequest 时调用，所以会读取最新的 RenderOrder 或 Camera.depth。
        public static int ResolveSortLayer(Camera camera, BurtCameraData cameraData)
        {
            // 如果相机挂了 BurtCameraData，就以 BurtRP 自己的 RenderOrder 作为排序权威来源。
            if (cameraData != null)
            {
                // 直接返回当前帧读到的 RenderOrder，避免依赖 Camera.depth 同步时机导致排序滞后。
                return cameraData.RenderOrder;
            }

            // 如果相机本身为空，说明调用方传入了异常数据，返回 0 作为安全兜底，后续有效性检查会跳过无效 request。
            if (camera == null)
            {
                // 返回默认排序层，避免空相机触发 NullReferenceException。
                return 0;
            }

            // 没有 BurtCameraData 时才回退到 Unity 原生 Camera.depth，并保持旧实现的 RoundToInt 行为以尽量不改变排序结果。
            return Mathf.RoundToInt(camera.depth);
        }

        // 比较两个 BurtRenderRequest 的执行顺序；List.Sort 会调用这个函数决定 request 从前到后的排列。
        public static int CompareRequests(BurtRenderRequest left, BurtRenderRequest right)
        {
            // 如果两个引用完全相同，包含同时为 null 的情况，就认为它们排序相等。
            if (ReferenceEquals(left, right))
            {
                // 返回 0 表示不需要交换顺序。
                return 0;
            }

            // 如果左侧 request 为空，就把它排到有效 request 后面，避免空对象先被执行。
            if (left == null)
            {
                // 返回 1 表示 left 应该位于 right 后面。
                return 1;
            }

            // 如果右侧 request 为空，就把右侧排到后面，让左侧有效 request 更早执行。
            if (right == null)
            {
                // 返回 -1 表示 left 应该位于 right 前面。
                return -1;
            }

            // 首先比较每帧创建 request 时缓存下来的 SortLayer，数值越小越早渲染，保持旧的主要排序行为。
            var sortCompare = left.SortLayer.CompareTo(right.SortLayer);

            // 如果 SortLayer 不相等，就直接使用这个比较结果，不再看其他字段。
            if (sortCompare != 0)
            {
                // 返回 SortLayer 的比较结果，保持“RenderOrder/Camera.depth 小的先渲染”的现有行为。
                return sortCompare;
            }

            // 同一 SortLayer 内先按逻辑栈编号排序，让同一个 stackId 的 Base/Overlay/UI 更容易聚在一起。
            var stackCompare = left.StackId.CompareTo(right.StackId);
            if (stackCompare != 0)
            {
                // 返回逻辑栈编号比较结果；第一版只排序，不做跨栈合成。
                return stackCompare;
            }

            // 同一层同一栈内再按角色排序：Base -> Overlay -> UI -> SceneView -> Preview -> Reflection。
            var roleCompare = GetRoleSortOrder(left.CameraRole).CompareTo(GetRoleSortOrder(right.CameraRole));
            if (roleCompare != 0)
            {
                // 返回角色比较结果，让叠加相机默认排在基础相机之后。
                return roleCompare;
            }

            // SortLayer、StackId 和角色都相同时，用左侧相机的 InstanceID 作为稳定的次级排序键；没有相机时使用 0 兜底。
            var leftId = left.Camera != null ? left.Camera.GetInstanceID() : 0;

            // SortLayer 相同时，用右侧相机的 InstanceID 作为稳定的次级排序键；没有相机时使用 0 兜底。
            var rightId = right.Camera != null ? right.Camera.GetInstanceID() : 0;

            // 返回相机 InstanceID 的比较结果，让同层 request 的顺序在同一帧内保持确定。
            return leftId.CompareTo(rightId);
        }

        // 把相机角色转换为同层排序权重；数值越小越早执行。
        private static int GetRoleSortOrder(BurtCameraRole role)
        {
            // 第一版只定义基础顺序，不在这里做任何合成或清屏策略。
            switch (role)
            {
                case BurtCameraRole.Base:
                    return 0;
                case BurtCameraRole.Overlay:
                    return 1;
                case BurtCameraRole.UI:
                    return 2;
                case BurtCameraRole.SceneView:
                    return 3;
                case BurtCameraRole.Preview:
                    return 4;
                case BurtCameraRole.Reflection:
                    return 5;
                default:
                    return 100;
            }
        }
    }
}
