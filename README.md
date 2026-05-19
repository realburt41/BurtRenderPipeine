
# BurtRenderPipeine

BurtRenderPipeine 是一个面向实时画面质量的 Unity 自定义 SRP。当前实现以相机 request 和 RenderGraph 为主干，Forward 负责稳定兼容路径，Deferred 负责五张 GBuffer、不透明材质分支、屏幕空间效果和后处理链路。

## 当前管线概览

- 渲染入口：`BurtRenderPipeline` 为每个 Unity Camera 创建 `BurtRenderRequest`，排序后构建 `BurtRenderFrame` / 相机栈，再把 request 交给对应的图组装器执行。
- 路径选择：普通 SceneView / GameView 可走 Forward 或 Deferred；Preview 和 Reflection request 当前固定走 Forward，避免预览和反射捕获被 Deferred 中间资源影响。
- 相机栈：Base / Overlay request 由 `BurtRequestRenderOptions` 决定 CameraColor / CameraDepth 的申请、共享、FinalBlit 和释放时机。
- RenderGraph：负责 pass 编排、资源注册、读写声明、生命周期追踪、校验、风险诊断和 debug dump。当前 pass culling 仍以诊断为主，实际 pass 默认执行。
- Forward 路径：覆盖不透明、透明、天空盒、Depth Prepass、主光阴影、后处理、Gizmos、Unsupported Shader debug 和最终输出。
- Deferred 路径：覆盖 Camera RT、五张 GBuffer、HiZ、SSAO、Deferred Lighting、Clear Coat、Hair、Subsurface、ForwardOnly 不透明兜底、Fog、Atmosphere、SSR、透明、后处理和最终输出。

## Deferred Pass 顺序

当前 Deferred request 的主要执行链如下：

1. 申请 CameraColor / CameraDepth。
2. 申请 GBuffer0-4、HiZDepth、灯光 buffer、阴影图、SSAO/SSR/5S 所需临时资源。
3. 上传 LightingGlobals / ShadowGlobals，构建 tiled / clustered additional light list。
4. 绘制主光阴影和 additional light shadow atlas。
5. 设置并清理 Camera RT，执行 Depth Prepass。
6. 绑定并清理 GBuffer MRT，绘制 `BurtGBuffer` 不透明材质。
7. 执行 SSAO trace / blur / temporal。
8. 清理 Deferred lighting target，并按材质 id 分别执行 Default Lit、Hair、Clear Coat、Subsurface lighting。
9. 绘制 `BurtForwardOnly` 不透明兜底。
10. 执行 screen-space 5S：copy source、水平 blur、垂直 blur、写回 CameraColor。
11. 构建 HiZ Depth。
12. 执行 Fog、Skybox、Atmosphere、Aerial Perspective。
13. 执行 SSR trace / denoise / temporal / composite。
14. 绘制透明物体和 unsupported shader debug。
15. 执行后处理、FinalBlit，并按资源生命周期反向释放临时资源。

## 资源状态

- 相机资源：`CameraColor`、`CameraDepth`、`FinalCameraTarget`。
- GBuffer：`GBuffer0`、`GBuffer1`、`GBuffer2`、`GBuffer3`、`GBuffer4`。
- 光照资源：`LightingGlobals`、`ShadowGlobals`、`MainLightShadowMap`、additional light buffer、tile light count/list/offset buffer、cluster light count/list/offset buffer。
- 屏幕空间资源：`HiZDepth`、`ScreenSpaceAmbientOcclusionRaw`、`ScreenSpaceAmbientOcclusion`、`ScreenSpaceSubsurfaceSource`、`ScreenSpaceSubsurfaceTemp`、`ScreenSpaceSubsurfaceBlur`、`ScreenSpaceReflectionColor`、`ScreenSpaceReflectionDenoisedColor`、`ScreenSpaceReflectionTemporalColor`。
- 后处理资源：`PostProcessColor` 作为后处理链路中间 RT，最终结果回写 `CameraColor` 后再 blit 到外部目标。

## 材质与 GBuffer

Deferred 使用 Stencil 低 2 bit 标记材质分支：`0 = Default Lit`、`1 = Hair`、`2 = Clear Coat`、`3 = Subsurface`。Deferred lighting pass 使用相同 Ref 过滤像素，shader 内也保留 GBuffer 解码 id 作为兜底判断。

### Default Lit (0) / `BurtRP/Lit`

Stencil：Depth/Stencil 的低 2 bit 写入 Model ID 0，Deferred Lit lighting pass 使用 Ref 0 过滤；不占用 GBuffer RGBA 通道。

<table>
<thead>
<tr>
<th align="center">通道</th>
<th align="center">R</th>
<th align="center">G</th>
<th align="center">B</th>
<th align="center">A</th>
<th align="center">备注</th>
</tr>
</thead>
<tbody>
<tr>
<td align="center">GBuffer0 / ARGB32</td>
<td colspan="3" align="center">BaseColor.rgb / 24 bit</td>
<td align="center">Occlusion / 8 bit</td>
<td align="center">基础色和环境遮蔽</td>
</tr>
<tr>
<td align="center">GBuffer1 / ARGBHalf</td>
<td colspan="2" align="center">NormalWS Octa / 32 bit</td>
<td align="center">Packed Model + Metallic / 16f</td>
<td align="center">Smoothness / 16f</td>
<td align="center"><code>GBuffer1.b</code> 位于 Default Lit bucket 时表示 Metallic</td>
</tr>
<tr>
<td align="center">GBuffer2 / DefaultHDR</td>
<td colspan="3" align="center">Emission.rgb / DefaultHDR</td>
<td align="center">Reflectance / DefaultHDR</td>
<td align="center">HDR 格式由 Unity / 平台决定</td>
</tr>
<tr>
<td align="center">GBuffer3 / ARGBHalf</td>
<td colspan="2" align="center">Reserved Normal Mirror / 32 bit</td>
<td colspan="2" align="center">Reserved / 32 bit</td>
<td align="center">写入安全默认值，避免未绑定采样</td>
</tr>
<tr>
<td align="center">GBuffer4 / ARGBHalf</td>
<td colspan="2" align="center">TangentWS Octa / 32 bit</td>
<td align="center">Signed Anisotropy, Encoded 0..1 / 16f</td>
<td align="center">Reserved / 16f</td>
<td align="center">直接光使用 anisotropic GGX，anisotropy 为 0 时退化为普通 GGX</td>
</tr>
</tbody>
</table>

### Hair (1) / `BurtRP/Hair`

Stencil：Depth/Stencil 的低 2 bit 写入 Model ID 1，Deferred Hair lighting pass 使用 Ref 1 过滤；不占用 GBuffer RGBA 通道。

<table>
<thead>
<tr>
<th align="center">通道</th>
<th align="center">R</th>
<th align="center">G</th>
<th align="center">B</th>
<th align="center">A</th>
<th align="center">备注</th>
</tr>
</thead>
<tbody>
<tr>
<td align="center">GBuffer0 / ARGB32</td>
<td colspan="3" align="center">BaseColor.rgb / 24 bit</td>
<td align="center">Occlusion / 8 bit</td>
<td align="center">基础色和环境遮蔽</td>
</tr>
<tr>
<td align="center">GBuffer1 / ARGBHalf</td>
<td colspan="2" align="center">StrandDirectionWS Octa / 32 bit</td>
<td align="center">Packed Model + Hair Payload / 16f</td>
<td align="center">Smoothness / 16f</td>
<td align="center">Hair Payload 打包 Scatter 和 Longitudinal Shift Scale</td>
</tr>
<tr>
<td align="center">GBuffer2 / DefaultHDR</td>
<td colspan="3" align="center">Emission.rgb / DefaultHDR</td>
<td align="center">Reflectance / DefaultHDR</td>
<td align="center">Reflectance 包含 Hair Specular Scale</td>
</tr>
<tr>
<td align="center">GBuffer3 / ARGBHalf</td>
<td colspan="4" align="center">Reserved / 64 bit</td>
<td align="center">预留给后续覆盖率、第二方向或深度不透明度</td>
</tr>
<tr>
<td align="center">GBuffer4 / ARGBHalf</td>
<td colspan="2" align="center">Fallback Tangent Octa / 32 bit</td>
<td colspan="2" align="center">Reserved / 32 bit</td>
<td align="center">Hair 不使用 Lit anisotropy 标量，方向性由 strand/lobe 表达</td>
</tr>
</tbody>
</table>

### Clear Coat (2) / `BurtRP/Clear Coat`

Stencil：Depth/Stencil 的低 2 bit 写入 Model ID 2，Deferred Clear Coat lighting pass 使用 Ref 2 过滤；不占用 GBuffer RGBA 通道。

<table>
<thead>
<tr>
<th align="center">通道</th>
<th align="center">R</th>
<th align="center">G</th>
<th align="center">B</th>
<th align="center">A</th>
<th align="center">备注</th>
</tr>
</thead>
<tbody>
<tr>
<td align="center">GBuffer0 / ARGB32</td>
<td colspan="3" align="center">BaseColor.rgb / 24 bit</td>
<td align="center">Occlusion / 8 bit</td>
<td align="center">基础色和环境遮蔽</td>
</tr>
<tr>
<td align="center">GBuffer1 / ARGBHalf</td>
<td colspan="2" align="center">Base NormalWS Octa / 32 bit</td>
<td align="center">Packed Model + Metallic / 16f</td>
<td align="center">Smoothness / 16f</td>
<td align="center">底层材质保留 Default Lit 语义</td>
</tr>
<tr>
<td align="center">GBuffer2 / DefaultHDR</td>
<td colspan="3" align="center">Emission.rgb / DefaultHDR</td>
<td align="center">Reflectance / DefaultHDR</td>
<td align="center">HDR 格式由 Unity / 平台决定</td>
</tr>
<tr>
<td align="center">GBuffer3 / ARGBHalf</td>
<td colspan="2" align="center">ClearCoatNormalWS Octa / 32 bit</td>
<td align="center">ClearCoatMask / 16f</td>
<td align="center">ClearCoatRoughness / 16f</td>
<td align="center">顶层 coat 的直接高光、IBL、SSR 读取该法线和粗糙度</td>
</tr>
<tr>
<td align="center">GBuffer4 / ARGBHalf</td>
<td colspan="2" align="center">Base TangentWS Octa / 32 bit</td>
<td align="center">Base Signed Anisotropy, Encoded 0..1 / 16f</td>
<td align="center">Reserved / 16f</td>
<td align="center">顶层 coat 保持 isotropic，底层继续使用 tangent / anisotropy</td>
</tr>
</tbody>
</table>

### Subsurface (3) / `BurtRP/Subsurface`

Stencil：Depth/Stencil 的低 2 bit 写入 Model ID 3，Deferred Subsurface lighting pass 使用 Ref 3 过滤；不占用 GBuffer RGBA 通道。

<table>
<thead>
<tr>
<th align="center">通道</th>
<th align="center">R</th>
<th align="center">G</th>
<th align="center">B</th>
<th align="center">A</th>
<th align="center">备注</th>
</tr>
</thead>
<tbody>
<tr>
<td align="center">GBuffer0 / ARGB32</td>
<td colspan="3" align="center">BaseColor.rgb / 24 bit</td>
<td align="center">Occlusion / 8 bit</td>
<td align="center">基础色和环境遮蔽</td>
</tr>
<tr>
<td align="center">GBuffer1 / ARGBHalf</td>
<td colspan="2" align="center">NormalWS Octa / 32 bit</td>
<td align="center">Packed Model + SubsurfaceStrength / 16f</td>
<td align="center">Smoothness / 16f</td>
<td align="center">Subsurface 按非金属处理，Material Scalar 表示 Strength</td>
</tr>
<tr>
<td align="center">GBuffer2 / DefaultHDR</td>
<td colspan="3" align="center">Emission.rgb / DefaultHDR</td>
<td align="center">Reflectance / DefaultHDR</td>
<td align="center">Profile Index 不占用 GBuffer2</td>
</tr>
<tr>
<td align="center">GBuffer3 / ARGBHalf</td>
<td colspan="3" align="center">SubsurfaceTint.rgb / 48 bit</td>
<td align="center">Packed Power + Ambient / 16f</td>
<td align="center">直射 LUT、transmission 和 5S blur 共享这些控制量</td>
</tr>
<tr>
<td align="center">GBuffer4 / ARGBHalf</td>
<td colspan="2" align="center">TangentWS Octa / 32 bit</td>
<td align="center">SubsurfaceDistortion / 16f</td>
<td align="center">Packed Thickness + ProfileIndex / 16f</td>
<td align="center">Profile Index 映射到管线资产的 0-7 Profile Palette</td>
</tr>
</tbody>
</table>

当前材质分支：

- Default Lit：基于 BaseColor、Metallic、Smoothness、Reflectance、AO、Tangent 和 Anisotropy 计算直接光、IBL 与 SSR。
- Hair：`GBuffer1.rg` 存 Strand Direction，Material Scalar 打包 Scatter 和 Longitudinal Shift Scale，Deferred Hair lighting 单独处理，按非金属材质计算。
- Clear Coat：底层保留 Base Normal / Metallic / Smoothness / Tangent / Anisotropy，顶层 Coat 使用独立 Normal / Mask / Roughness；直接高光、IBL、SSR 和 Debug 反射粗糙度读取顶层数据。
- Subsurface：材质引用 `BurtSubsurfaceProfile`，管线资产维护最多 8 个 Profile；`GBuffer4.a` 打包 Thickness 和 Profile Index，直射光按 Profile 采样 Burley LUT atlas，并使用 Profile 内双 GGX 高光参数；屏幕空间 5S 只扩散 Diffuse Lighting，保留 Specular 和 Emission。
- Unlit 与 ForwardOnly：不进入 Deferred lighting 主分支，按 Forward pass 或 Deferred 后的兜底 pass 绘制。

## 5S 皮肤

- Profile 文件：`BurtSubsurfaceProfile` 是独立 ScriptableObject，包含 surface albedo、mean free path、tint、边界串色、双 GGX 高光参数和 5S blur 参数。
- Profile Palette：`BurtRenderPipelineAsset` 提供默认 profile 和附加 profile 列表，运行时解析为 0-7 槽位；材质通过 profile index 选择槽位。
- LUT：`BurtSubsurfaceLutUtility` 按 profile palette 生成预积分 LUT atlas，并在管线每帧绑定全局纹理。
- 直射光：Subsurface lighting 根据 profile index 读取 LUT 和双 GGX 高光参数，结合 thickness、distortion、power、ambient、tint 计算 wrapped diffuse、transmission 和 specular。
- 屏幕空间：Deferred Subsurface lighting 可输出 diffuse luminance 信息，后续 5S pass 使用 Subsurface stencil、depth、normal、GBuffer 和 profile 参数做 separable blur，再写回 CameraColor。
- Inspector：Profile editor 已提供 LUT 和 RGB diffusion curve 可视化，便于单独调 profile 文件。

## 光照

- 主光：支持主方向光、阴影图、阴影矩阵上传和 receiver 全局参数。
- Additional Light：收集可见 additional lights，上传 structured buffer；Deferred 支持 tile / cluster 两套索引 buffer，shader 端按运行时可用状态启用。
- Additional Shadow：支持 additional light shadow atlas 的申请、绘制、receiver 绑定和诊断信息。
- SkyLight / IBL：支持指定 cubemap、diffuse 近似、specular mip 采样、强度、tint、上下半球策略和反射粗糙度选择。
- Clear Coat 与 SSR：反射 normal 和 roughness 会按材质分支读取，Clear Coat 使用顶层 coat 数据。

## 环境效果

- Atmosphere：包含天空绘制和 Aerial Perspective，参数来自 Volume component，支持主光作为太阳方向。
- Fog：屏幕空间高度雾，读取 CameraColor / CameraDepth 后写回 CameraColor，可与 Aerial Perspective 配合。
- Volumetric Fog：屏幕空间 raymarch，当前作为独立 pass 插入天空/aerial 后、透明前。

## 屏幕空间效果

- HiZ Depth：按需申请并构建 mip，用于 SSR HiZ trace 和 HiZ debug。
- SSAO：支持 SSAO / GTAO / HBAO 算法、质量预设、半分辨率 trace、空间 blur、temporal accumulation、history/depth history 和多种 debug view。
- SSR：支持质量预设、半分辨率、max steps、max distance、thickness、roughness fade、temporal accumulation、denoise、HiZ experimental trace 和 HiZ diagnostics。
- Screen Space Subsurface：只在 Deferred 且开启 5S 时运行，Preview / Reflection request 会跳过。

## 后处理

- TAA：支持 camera/object motion vector、history、depth history、confidence history、anti-flicker history、jitter、clamp、feedback、motion/depth rejection 和 debug view。
- Bloom：支持 threshold、soft knee、firefly clamp、多 mip、高斯核缓存、stage tint、debug source 和 alpha 输出开关。
- Tonemapping / Color：支持 ACES/Film 曲线、post exposure、color adjustments。
- Exposure：支持物理曝光、自动曝光、log luminance reduce、轻量 histogram、AsyncGPUReadback 状态和 debug view。

## 调试与诊断

- Shading Debug Overlay 拆分材质、光照、后处理调试入口。
- RenderGraph dump 会输出 request、相机、RT plan、pipeline state、lighting state、post process state、deferred state、pass 列表、资源生命周期、资源风险和校验结果。
- GBuffer debug 支持 raw RT、base color、normal、roughness、metallic/material scalar、occlusion、emission、reflectance、材质 id、Hair、Clear Coat、Subsurface、tangent、anisotropy 等视图。
- Lighting debug 覆盖主光阴影、tile light、cluster light、additional light buffer 和 shadow atlas。
- 屏幕空间 debug 覆盖 Depth、HiZ、SSAO、SSR、TAA、Bloom、Auto Exposure、Atmosphere、Fog 和 Volumetric Fog。

## TODO

- SSDO：规划屏幕空间方向性遮蔽，复用 depth / normal / GBuffer，并与现有 SSAO、间接漫反射和 debug view 对齐。

## 当前边界

- Deferred 只对 SceneView / GameView 等普通 request 启用；Preview 和 Reflection 使用 Forward。
- RenderGraph 已有资源声明、生命周期和 culling readiness 诊断，但 pass culling 仍未作为默认执行策略启用。
- GBuffer 材质分支当前固定使用 Stencil 低 2 bit，新增不透明材质分支需要重新规划 id 和存储位。
- Subsurface profile palette 当前最多 8 个槽位，材质侧 profile index 会映射到该范围内。
- 部分调试信息依赖当前帧扫描可见 renderer 或 shader availability，属于诊断辅助，不参与最终画面判定。
