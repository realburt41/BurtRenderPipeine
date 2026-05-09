using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Color、Vector3、LightType 和 RenderSettings。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 CullingResults 和 VisibleLight。

namespace Burt.RenderPipeline // 定义 BurtRP 运行时命名空间，让灯光数据和 request、pass 保持在同一模块里。
{
    public sealed class BurtLightingData // 保存一个 BurtRenderRequest 收集到的灯光信息。
    {
        private static readonly Vector3 DefaultMainLightDirection = new Vector3(0.3f, 0.8f, 0.4f).normalized; // 定义没有可见方向光时使用的稳定兜底方向。

        public bool HasMainLight { get; private set; } // 标记当前 request 是否找到了真实可见的方向光。

        public int MainLightIndex { get; private set; } // 保存主光在 CullingResults.visibleLights 里的索引。

        public int VisibleLightCount { get; private set; } // 保存当前相机剔除结果里可见灯光的数量，主要用于调试和后续多光源扩展。

        public Vector3 MainLightDirection { get; private set; } // 保存从着色点指向主光的世界空间方向。

        public Color MainLightColor { get; private set; } // 保存 Unity 计算过强度后的主光最终颜色。

        public Color AmbientLightColor { get; private set; } // 保存从 Unity Lighting 设置读取到的原始环境光颜色，Simple Lit 路径会直接使用它。

        public BurtShadowData ShadowData { get; private set; } // 保存主光对应的阴影数据，后续 shadow pass 会读取它。

        private BurtLightingData() // 隐藏构造函数，强制调用方通过 Create 或 Default 获得已初始化的数据。
        {
        } // 构造函数不直接写初始化逻辑，避免和 ResetToDefaults、ResolveMainLight 的规则重复。

        public static BurtLightingData Default() // 创建一个即使没有剔除结果也可用的默认灯光数据。
        {
            var data = new BurtLightingData(); // 创建灯光数据对象。

            data.ResetToDefaults(0); // 按 0 个可见灯光初始化兜底主光、环境光和阴影数据。

            return data; // 返回初始化完成的默认灯光数据。
        }

        public static BurtLightingData Create(CullingResults cullingResults) // 从 Unity 当前相机的剔除结果里构建灯光数据。
        {
            var visibleLights = cullingResults.visibleLights; // 读取 Unity 给当前相机筛出的可见灯光列表。

            var data = new BurtLightingData(); // 创建本次 request 专用的灯光数据对象。

            data.ResetToDefaults(visibleLights.Length); // 先写入安全默认值，后面找到真实主光时再覆盖。

            data.ResolveMainLight(visibleLights); // 遍历可见灯光，选择第一盏方向光作为 BurtRP 当前主光。

            return data; // 返回已经准备好上传给 BurtSetupLightingPass 的灯光数据。
        }

        private void ResetToDefaults(int visibleLightCount) // 把对象重置到安全的默认光照状态。
        {
            HasMainLight = false; // 先标记为没有找到真实主光。

            MainLightIndex = -1; // 使用 -1 表示当前主光不对应 visibleLights 中的真实索引。

            VisibleLightCount = visibleLightCount; // 保存可见光数量，方便调试输出和后续多光源逻辑使用。

            MainLightDirection = DefaultMainLightDirection; // 使用兜底方向，避免无主光时 Lit 材质完全失去形体光照。

            MainLightColor = Color.white; // 使用白色兜底主光，避免没有灯光时材质直接变黑。

            AmbientLightColor = RenderSettings.ambientLight; // 读取 Unity Lighting 面板里的环境光颜色，后续 SetupLightingPass 会原样上传给 Simple Lit 路径。

            ShadowData = BurtShadowData.None(); // 初始化为无阴影状态，找到主光后再生成真正的阴影数据。
        }

        private void ResolveMainLight(Unity.Collections.NativeArray<VisibleLight> visibleLights) // 查找第一盏可见方向光，并把它保存为 BurtRP 主光。
        {
            for (var lightIndex = 0; lightIndex < visibleLights.Length; lightIndex++) // 遍历当前相机能看到的所有灯光。
            {
                var visibleLight = visibleLights[lightIndex]; // 从 NativeArray 中取出当前灯光数据。

                if (visibleLight.lightType != LightType.Directional) // 当前阶段只支持方向光作为主光。
                {
                    continue; // 非方向光先跳过，后续多光源阶段再接入。
                }

                var forwardColumn = visibleLight.localToWorldMatrix.GetColumn(2); // 读取灯光变换矩阵里的 forward 轴。

                var directionTowardLight = new Vector3(-forwardColumn.x, -forwardColumn.y, -forwardColumn.z); // Unity 方向光 forward 指向光照射方向，这里取反得到从表面指向光源的方向。

                if (directionTowardLight.sqrMagnitude <= 0.0001f) // 防御异常矩阵导致的零长度方向。
                {
                    continue; // 当前灯光方向无效，继续查找下一盏灯。
                }

                HasMainLight = true; // 标记已经找到真实可见主光。

                MainLightIndex = lightIndex; // 记录主光在 visibleLights 里的索引，阴影系统会使用这个索引。

                MainLightDirection = directionTowardLight.normalized; // 保存归一化后的世界空间主光方向。

                MainLightColor = visibleLight.finalColor; // 保存 Unity 已经乘过 light color 和 intensity 的最终颜色。

                ShadowData = BurtShadowData.CreateForMainLight(visibleLight, lightIndex); // 根据当前主光和索引创建主光阴影数据。

                return; // 当前规则只取第一盏方向光，所以找到后直接结束。
            }
        }
    }
}
