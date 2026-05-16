# BurtRenderPipeine

BurtRenderPipeine 是一个面向实时渲染质量的 Unity 自定义渲染管线。

## 已做内容

- 搭建了 SRP 渲染入口、相机 request、相机栈和最终输出流程。
- 实现了 Forward 渲染路径，包括不透明、透明、天空盒、Depth Prepass、Gizmos 和不支持 Shader 显示。
- 实现了 Deferred 渲染路径，包括 GBuffer、Deferred Lighting、Hair Lighting、ForwardOnly 兜底和透明前向绘制。
- 实现了 RenderGraph 风格的 Pass 编排、资源注册、生命周期追踪、资源校验和调试 dump。
- 接入了 PBR Lit、Hair、Unlit 材质，以及 GBuffer 编码、解码和材质调试。
- 接入了 SkyLight、IBL、主光阴影和 additional point light shadow atlas。
- 接入了 additional light buffer、additional light shading 和 tile light debug。
- 接入了 HiZ Depth、SSAO 和 SSR。
- 接入了后处理链路，包括 TAA、Bloom、Tonemapping、Color Adjustments 和物理曝光。
- 接入了 Shading Debug Overlay，以及 Depth、GBuffer、HiZ、Tile Light、Shadow、SSAO、SSR 等调试视图。

## 后续 TODO

- 修正 point shadow 相关 quad 显示异常。
- 继续完善 TAA、Bloom、SSR 和 SSAO 的画面质量与稳定性。
- 标定 PBR、SkyLight、IBL、物理曝光和 Tonemapping 的默认效果。
- 补充固定测试场景，用于回归检查灯光、阴影、反射和后处理效果。
- 材质模型：Clear Coat、Subsurface、Transmission、Fuzz / Fabric / Foliage、Eye、Fur。
- 环境效果：Atmosphere / Fog / Volumetric / Weather。
- 反射与 GI：Probe 系统、Reflection Probe 选择/混合、Lumen 类动态 GI。
- 场景：Terrain / Vegetation / Ocean。
- 其他：Decal、Refraction。
