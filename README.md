# BurtRenderPipeine

BurtRenderPipeine 是一个面向实时渲染质量的 Unity 自定义渲染管线。

## 已做内容

- 搭建了 SRP 渲染入口、相机 request、相机栈和最终输出流程。
- 实现了 Forward / Deferred 双路径，包括不透明、透明、天空盒、Depth Prepass、Gizmos、GBuffer、Deferred Lighting、Hair Lighting 和 ForwardOnly 兜底。
- 实现了 RenderGraph 风格的 Pass 编排、资源注册、生命周期追踪、资源校验和调试 dump。
- 接入了 PBR Lit、Clear Coat、Subsurface、Hair、Unlit 材质，以及 GBuffer 编码、解码和材质调试。
- Clear Coat 和 Subsurface 已拆成独立 shader；Clear Coat 支持 mask / roughness，Subsurface 已有第一版 strength 近似。
- 接入了 SkyLight、IBL、Atmosphere Scattering、主光阴影和 additional point light shadow atlas。
- Atmosphere 已支持 Sky + Aerial Perspective、sun source、自定义太阳方向、horizon / ground、曝光兼容、Aerial tint / near fade / max opacity 和 Rayleigh / Mie / transmittance / aerial debug。
- additional point shadow 已修正 face / split culling sphere 稳定性问题，并增加 stable key、slice、rect、matrix hash、bias 等诊断信息。
- 接入了 additional light buffer、additional light shading 和 tile light debug。
- 接入了 HiZ Depth、SSAO 和 SSR。
- SSAO 已有质量预设、空间降噪、RHalf 时域历史、边缘感知时域累积、历史深度校验和 Surface Stability 诊断视图。
- SSR 已有质量预设、全/半分辨率、时域历史和 HiZ 实验诊断。
- 接入了后处理链路，包括 TAA、Bloom、Tonemapping、Color Adjustments、物理曝光和自动曝光。
- Bloom 已有质量档、mip/stage 诊断、threshold / soft knee、scatter、tint、alpha 和 debug view。
- TAA 已有 Halton jitter、Motion Vector、颜色/深度/置信度历史、抗闪烁、响应式拒绝，以及覆盖率/深度/运动边缘的历史压制。
- 自动曝光已支持 per-camera EV 状态、log-luminance reduce、轻量 histogram、AsyncGPUReadback timeout 恢复和 RenderGraph debug 字段。
- 接入了 Shading Debug Overlay，并拆分为 Burt Material Debug、Burt Lighting Debug、Burt Post Process Debug；debug 按钮支持再次点击回到正常画面。

## 当前管线情况

- 核心渲染：Forward / Deferred、RenderGraph 资源生命周期和 SceneView / GameView 输出链路已经成形。
- 材质：Default Lit、Hair、Clear Coat、Subsurface、Unlit 已接入；Clear Coat / Subsurface 仍是第一版质量，需要继续做 profile、thickness、transmission 等扩展。
- 光照：SkyLight、IBL、主光阴影、additional point light shadow 和 tiled additional light 已接入；point shadow 稳定性已修正，仍需更多动态场景回归。
- 后处理：TAA、Bloom、SSAO、SSR、物理曝光、自动曝光、Tonemapping 和 Color Adjustments 已接入，当前重点是默认效果标定和稳定性回归。
- 调试：RenderGraph dump、Shading Debug Overlay、GBuffer / Shadow / Atmosphere / SSAO / SSR / TAA / Bloom / Auto Exposure debug view 已覆盖主要排查入口。

## 子 agent 跟进情况

- SSAO：已合入 RHalf 时域历史、temporal final copy 和 Surface Stability debug view。
- 材质：Clear Coat / Subsurface GUI 与独立 shader 已推进，Clear Coat 参数区调整到 Normal 之后。
- 大气：Atmosphere + Aerial Perspective 已接入，并给出 Global Volume 推荐参数。
- 阴影：主光和 additional light shadow caster 为空时会跳过 DrawShadows，避免空 caster warning 和旧阴影干扰。
- TAA / Bloom / SSR / 自动曝光：当前仓库已有多项 debug、质量和诊断改动；仍需在 Unity 场景中做最终画面回归。
- 仍有两个子 agent 本轮没有返回最终状态，文档只记录当前代码中可见且已合入的结果。

## GBuffer 通道规划

当前 Deferred 路径使用四张 GBuffer，并用 GBuffer pass 写入的 Stencil 低 2 bit 标记 shading model：`0 = Default Lit`、`1 = Hair`、`2 = Clear Coat`、`3 = Subsurface`，材质 pass 使用 `ReadMask/WriteMask = 3`。Hair lighting pass 已使用 Stencil Ref 1 做 pass 级过滤；Default Lit / Clear Coat / Subsurface 当前仍共用 Lit lighting pass，再通过 GBuffer 解码后的 model id 进入对应 shading 分支。位数口径按当前 RT 申请方式记录：`GBuffer0 = ARGB32`，每通道 8-bit UNorm；`GBuffer1 = ARGBHalf`，每通道 16-bit float；`GBuffer2 = DefaultHDR`，实际位宽由 Unity / 平台 HDR 格式决定；`GBuffer3 = ARGBHalf`，当前用于 Clear Coat 独立法线，其他模型先写默认/占位值。表格按 Stencil model tag + GBuffer payload 规划计位；当前 HLSL 里 `GBuffer1.b` 仍临时镜像 `shadingModelID`，用于现有 decode / debug / shader 内过滤过渡，后续完全切到 Stencil 分 pass 后可以释放这部分冗余。

### Default Lit (0) / `BurtRP/Lit`

<table>
<thead>
<tr>
<th>GBuffer 通道</th>
<th>R</th>
<th>G</th>
<th>B</th>
<th>A</th>
<th>备注</th>
</tr>
</thead>
<tbody>
<tr>
<td>Stencil<br/>Depth/Stencil</td>
<td colspan="4">shading model = 0<br/>2 bit，Ref 0，ReadMask/WriteMask 3</td>
<td>GBuffer pass 写入的 model tag；后续可用于 Lit pass 级过滤</td>
</tr>
<tr>
<td>GBuffer0<br/>ARGB32</td>
<td colspan="3">baseColor<br/>24 bit, 8x3</td>
<td>occlusion<br/>8 bit</td>
<td>基础颜色 + AO</td>
</tr>
<tr>
<td>GBuffer1<br/>ARGBHalf</td>
<td colspan="2">normalWS octa<br/>32 bit, 16f x2</td>
<td>metallic<br/>16f material scalar</td>
<td>smoothness<br/>16f</td>
<td>Default Lit 的 material channel 为 metallic</td>
</tr>
<tr>
<td>GBuffer2<br/>DefaultHDR</td>
<td colspan="3">emission<br/>DefaultHDR RGB</td>
<td>reflectance<br/>DefaultHDR A</td>
<td>实际位宽由 Unity / 平台 HDR 格式决定</td>
</tr>
<tr>
<td>GBuffer3<br/>ARGBHalf</td>
<td colspan="2">reserved normal mirror<br/>32 bit, 16f x2</td>
<td>reserved<br/>16f</td>
<td>reserved<br/>16f</td>
<td>Default Lit 当前不消费；写入默认 normal，避免未绑定采样</td>
</tr>
</tbody>
</table>

### Hair (1) / `BurtRP/Hair`

<table>
<thead>
<tr>
<th>GBuffer 通道</th>
<th>R</th>
<th>G</th>
<th>B</th>
<th>A</th>
<th>备注</th>
</tr>
</thead>
<tbody>
<tr>
<td>Stencil<br/>Depth/Stencil</td>
<td colspan="4">shading model = 1<br/>2 bit，Ref 1，ReadMask/WriteMask 3</td>
<td>Deferred Hair lighting pass 使用 Stencil Ref 1 过滤</td>
</tr>
<tr>
<td>GBuffer0<br/>ARGB32</td>
<td colspan="3">baseColor<br/>24 bit, 8x3</td>
<td>occlusion<br/>8 bit</td>
<td>基础颜色 + AO</td>
</tr>
<tr>
<td>GBuffer1<br/>ARGBHalf</td>
<td colspan="2">strandDirectionWS octa<br/>32 bit, 16f x2</td>
<td>hairScatter + longitudinalShiftScale<br/>5 bit + 4 bit material payload</td>
<td>smoothness<br/>16f</td>
<td>metallic 不占 GBuffer；Deferred 按非金属处理</td>
</tr>
<tr>
<td>GBuffer2<br/>DefaultHDR</td>
<td colspan="3">emission<br/>DefaultHDR RGB</td>
<td>reflectance<br/>DefaultHDR A</td>
<td>reflectance 写入前已包含 _HairSpecularScale</td>
</tr>
<tr>
<td>GBuffer3<br/>ARGBHalf</td>
<td colspan="2">reserved strand/normal mirror<br/>32 bit, 16f x2</td>
<td>reserved<br/>16f</td>
<td>reserved<br/>16f</td>
<td>Hair 当前不消费；保留给未来 deep opacity / second direction / coverage 等扩展</td>
</tr>
</tbody>
</table>

### Clear Coat (2) / `BurtRP/Clear Coat`

<table>
<thead>
<tr>
<th>GBuffer 通道</th>
<th>R</th>
<th>G</th>
<th>B</th>
<th>A</th>
<th>备注</th>
</tr>
</thead>
<tbody>
<tr>
<td>Stencil<br/>Depth/Stencil</td>
<td colspan="4">shading model = 2<br/>2 bit，Ref 2，ReadMask/WriteMask 3</td>
<td>GBuffer pass 写入的 model tag；当前 lighting 仍走 Lit pass 内分支</td>
</tr>
<tr>
<td>GBuffer0<br/>ARGB32</td>
<td colspan="3">baseColor<br/>24 bit, 8x3</td>
<td>occlusion<br/>8 bit</td>
<td>基础颜色 + AO</td>
</tr>
<tr>
<td>GBuffer1<br/>ARGBHalf</td>
<td colspan="2">base normalWS octa<br/>32 bit, 16f x2</td>
<td>metallic + clearCoatMask + clearCoatRoughness<br/>3 bit + 3 bit + 3 bit material payload</td>
<td>smoothness<br/>16f</td>
<td>base layer normal 保留给底层 diffuse/specular；material 参数当前各 8 档量化</td>
</tr>
<tr>
<td>GBuffer2<br/>DefaultHDR</td>
<td colspan="3">emission<br/>DefaultHDR RGB</td>
<td>reflectance<br/>DefaultHDR A</td>
<td>实际位宽由 Unity / 平台 HDR 格式决定</td>
</tr>
<tr>
<td>GBuffer3<br/>ARGBHalf</td>
<td colspan="2">clearCoatNormalWS octa<br/>32 bit, 16f x2</td>
<td>reserved<br/>16f</td>
<td>reserved<br/>16f</td>
<td>Clear Coat 顶层 direct specular 和 IBL reflection 使用该法线；对照 XRender：GBufferOut0 存 Geometry.NormalWS 供顶层 coat 高光，Coat.ButtomNormal 另存给底层。Burt 当前把顶层 _ClearCoatNormalMap 存到专用 GBuffer3，GBuffer1.rg 保留底层/base normal</td>
</tr>
</tbody>
</table>

### Subsurface (3) / `BurtRP/Subsurface`

<table>
<thead>
<tr>
<th>GBuffer 通道</th>
<th>R</th>
<th>G</th>
<th>B</th>
<th>A</th>
<th>备注</th>
</tr>
</thead>
<tbody>
<tr>
<td>Stencil<br/>Depth/Stencil</td>
<td colspan="4">shading model = 3<br/>2 bit，Ref 3，ReadMask/WriteMask 3</td>
<td>GBuffer pass 写入的 model tag；当前 lighting 仍走 Lit pass 内分支</td>
</tr>
<tr>
<td>GBuffer0<br/>ARGB32</td>
<td colspan="3">baseColor<br/>24 bit, 8x3</td>
<td>occlusion<br/>8 bit</td>
<td>基础颜色 + AO</td>
</tr>
<tr>
<td>GBuffer1<br/>ARGBHalf</td>
<td colspan="2">normalWS octa<br/>32 bit, 16f x2</td>
<td>subsurfaceStrength<br/>16f material scalar</td>
<td>smoothness<br/>16f</td>
<td>metallic 不占 GBuffer；Deferred 按非金属处理</td>
</tr>
<tr>
<td>GBuffer2<br/>DefaultHDR</td>
<td colspan="3">emission<br/>DefaultHDR RGB</td>
<td>reflectance<br/>DefaultHDR A</td>
<td>thickness / profile / diffusion 需要额外通道、profile 索引或后处理资源</td>
</tr>
<tr>
<td>GBuffer3<br/>ARGBHalf</td>
<td colspan="2">reserved normal mirror<br/>32 bit, 16f x2</td>
<td>reserved<br/>16f</td>
<td>reserved<br/>16f</td>
<td>Subsurface 当前不消费；后续可评估 thickness / bent normal / profile index 是否需要占用</td>
</tr>
</tbody>
</table>

新增 shading model 必须先明确 Stencil Ref、`GBuffer1.rg` 的方向语义和 `GBuffer1.b` 的 `materialChannel` 打包规则；只有模型确实需要稳定的多参数存储时，再扩展 GBuffer RT 或拆出专用 buffer。

## 后续 TODO

- 自动曝光：已加入亮度热力图、测光权重和直方图范围调试，并限制直方图 readback 采样规模；后续继续验证真实场景稳定性，再决定是否需要 compute histogram。
- 在 Unity 场景中继续验证 additional point shadow 多光源稳定性，重点看 point light face 边界、slot / atlas slice 是否还跳变，以及 slice culling sphere 是否不再出现无效半径。
- 继续用固定动态场景标定 TAA 的拖影、闪烁和锐度平衡。
- 标定 Bloom、SSR、SSAO、PBR、SkyLight、IBL、物理曝光、自动曝光和 Tonemapping 的默认效果。
- 补充固定测试场景，用于回归检查灯光、阴影、反射、大气、材质和后处理效果。
- 材质模型：Subsurface 后续继续做 thickness / profile / diffusion；Transmission、Fuzz / Fabric / Foliage、Eye、Fur 待接入。
- 环境效果：Atmosphere 后续可继续做 LUT、IBL 烘焙联动、透明物体策略和更完整 aerial perspective；Fog / Volumetric / Weather 待接入。
- 反射与 GI：Probe 系统、Reflection Probe 选择/混合、Lumen 类动态 GI。
- 场景：Terrain / Vegetation / Ocean。
- 其他：Decal、Refraction。
