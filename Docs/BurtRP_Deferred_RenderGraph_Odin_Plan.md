# BurtRP Deferred / RenderGraph / Odin 实施计划

更新时间：2026-05-10

维护规则：后续主 Agent 对 Deferred、RenderGraph、Odin、阴影、后处理、PBR 和调试体系的阶段性判断，都实时补充到这份文档，避免计划散落在聊天上下文里。

## 0. 当前状态快照

截至当前评估，BurtRP 的主线状态是：Forward PBR 垂直切片已经可运行，后处理 No-op 框架已经通过基础画面验证，Tonemapping 第一版已经接入，Deferred 仍处在设计和 shader 侧 GBuffer 草案阶段。

当前优先级：

1. 先验证 Tonemapping 第一版在 SceneView / GameView / Preview 下不引入 YFlip、变色或丢画面。
2. Tonemapping 稳定后再开始 Deferred GBuffer 运行时资源和 Debug View。
3. 在 Deferred 前补齐 RenderGraph 的虚拟资源语义，例如 `LightingGlobals` 和 `ShadowGlobals`。
4. 阴影短期不重写，先整理 Shadow Settings 和 Debug Mode，再进入 CSM。
5. RenderGraph 暂时保持顺序执行，先补资源语义，不急着做自动调度。

当前不建议立刻做：

- 不建议直接上完整 Deferred。
- 不建议直接做 PCSS 或多光源阴影。
- 不建议在 PostProcess No-op 未验证前继续叠加 Bloom / Color Grading。
- 不建议一次性迁移所有 Inspector 到 Odin，优先新配置和独立 settings。


## 1. 当前阶段判断

BurtRP 目前处在“Forward PBR 垂直切片验证阶段”。它已经不是空 SRP 骨架，但还不是完整生产级渲染管线。

已经完成或基本可用的部分：

- `BurtRenderPipeline` 已经接入 Unity SRP 入口，可以从 Unity 相机列表生成 `BurtRenderRequest`。
- `BurtRenderRequest` 已经承担“相机驱动”到“任务驱动”的中间层，包含相机、剔除结果、相机角色、排序层、目标 RT、灯光数据等上下文。
- `BurtRenderFrame` / `BurtCameraStackGroup` 已经能把 request 组织成 Frame / Stack 结构。
- Camera Stack 的共享 `CameraColor` / `CameraDepth` 生命周期已经开始落地。
- Forward Graph 已经有完整的顺序 Pass 链路：Allocate、Setup Lighting、Shadow、Set RT、Clear、Depth Prepass、Opaque、Skybox、Transparent、Debug、FinalBlit、Release。
- 主光阴影已经有 shadow map 申请、caster 绘制、采样、debug view 和基础诊断。
- PBR 已经有基础材质输入、MaskMap、法线、自发光、Reflectance、直接高光、间接光、Shading Debug 的核心雏形。
- SceneView / Camera Preview 已经能在编辑器下跟随选中 Camera 的 `BurtCameraData` 清屏设置。
- `BurtGBuffer.hlsl` 已经有 GBuffer 编解码草案，但还没有运行时 Deferred 管线。

尚未完成或仍是临时方案的部分：

- RenderGraph 仍是顺序 Pass 列表，不是自动调度系统。
- RenderTarget 资源模型还不支持正式 MRT / Ping-Pong / transient aliasing。
- 后处理 No-op 框架第一阶段已经由 PostProcess Agent Russell 接入，当前需要先验证画面一致性和 RenderGraph Debug 输出。
- Deferred 只有 shader 侧 GBuffer 草案，没有 C# 资源、Pass 和 Renderer Mode。
- 多光源、级联阴影、点光/聚光阴影、后处理栈、材质 Inspector、正式 Odin 面板还未系统化。

## 2. 当前并行边界

PostProcess Agent Russell 已完成第一版后处理框架。主 Agent 后续可以 review 它的接入点，但在验证前仍应谨慎修改以下高冲突文件：

- `Assets/BurtRP/Runtime/BurtRenderPipelineAsset.cs`
- `Assets/BurtRP/Runtime/BurtForwardGraphAssembler.cs`
- `Assets/BurtRP/Runtime/RenderTargets/BurtRenderTargetPasses.cs`
- `Assets/BurtRP/Runtime/PostProcessing/**`

主 Agent 可以安全推进的方向：

- Deferred 架构设计。
- RenderGraph 升级设计。
- Odin 面板迁移设计。
- 验证清单和代码债清单。
- 不直接修改后处理接入点的文档和方案。

## 3. 后处理与 Deferred 的关系

后处理应该成为 Forward 和 Deferred 的共同尾部链路。

推荐最终结构：

```text
Forward Opaque / Deferred Lighting
    ↓
Skybox
    ↓
Forward Transparent
    ↓
PostProcess Stack
    ↓
FinalBlit
```

Deferred 不应该绕过后处理；Deferred Lighting 的输出仍然写入 `CameraColor`，后处理继续读取 `CameraColor` 并输出回 `CameraColor` 或 `PostProcessColor`，最后由 `FinalBlit` 输出到最终目标。

因此，PostProcess Agent 已完成的 No-op 框架对 Deferred 是有价值的：它会提前验证全屏 Pass、YFlip、临时 RT 和 FinalBlit 前插入点。

## 4. Deferred 第一版目标

Deferred 第一版不要追求完整 URP/HDRP 级功能，而是先打通最小闭环。

第一版目标：

- 支持 Asset 上选择 Renderer Mode：`Forward` / `Deferred`。
- Opaque 物体写入 GBuffer。
- Deferred Lighting 全屏 Pass 从 GBuffer + Depth 重建数据并写入 `CameraColor`。
- Skybox 仍在 Deferred Lighting 后绘制。
- Transparent 仍走 Forward，绘制到 `CameraColor`。
- PostProcess 继续走统一尾部链路。
- Debug View 能显示 GBuffer0 / GBuffer1 / GBuffer2 / GBufferNormal / GBufferMaterial。

第一版不做：

- Stencil light volume。
- Tile/Cluster 光照。
- 多光源 Deferred。
- SSAO / SSR / Contact Shadow。
- Decal / DBUFFER。
- MSAA Deferred。
- 正式光照体积优化。

## 5. Deferred 推荐 Pass 顺序

Forward 当前顺序大致是：

```text
Allocate CameraColor
Allocate CameraDepth
Setup Lighting
MainLight Shadow
Set Render Target
Clear
Depth Prepass
Draw Opaque
Draw Skybox
Draw Transparent
Debug
PostProcess
FinalBlit
Release
```

Deferred 第一版建议顺序：

```text
Allocate CameraColor
Allocate CameraDepth
Allocate GBuffer0
Allocate GBuffer1
Allocate GBuffer2
Setup Lighting
Allocate MainLight Shadow Map
Draw MainLight Shadow Caster
Set GBuffer Render Targets
Clear GBuffer And Depth
Depth Prepass 或 GBuffer Pass 写深度
Draw GBuffer Opaque
Deferred Lighting To CameraColor
Draw Skybox
Draw Transparent Forward
Unsupported Shader Debug
Shading / GBuffer Debug
PostProcess
FinalBlit
Release MainLight Shadow Map
Release GBuffer0
Release GBuffer1
Release GBuffer2
Release CameraColor
Release CameraDepth
```

关于 Depth Prepass：

- 第一版可以保留 Depth Prepass，降低变量数量，保证 CameraDepth 在 GBuffer 前已经建立。
- 后续可以优化成 GBuffer Pass 同时写 Depth，减少一次 Opaque 绘制。
- 如果保留 Depth Prepass，GBuffer Opaque 使用 `ZTest LEqual`，可以和 Forward 当前行为接近。

## 6. GBuffer 资源规划

当前已有 `BurtGBuffer.hlsl` 草案，建议先沿用三张 GBuffer。

推荐布局：

```text
GBuffer0: RGBA8 或 ARGB32
  rgb = baseColor
  a   = occlusion

GBuffer1: ARGB32
  rg = oct normalWS
  b  = metallic
  a  = smoothness

GBuffer2: HDR 格式优先
  rgb = emission
  a   = reflectance
```

后续可升级：

- 如果 emission 不需要 HDR，GBuffer2 可先用普通颜色格式降低复杂度。
- 如果要加 material flags，可考虑 GBuffer3 或复用 GBuffer2.a，但不要过早塞太多语义。
- Deferred 不应存 F0，继续用 `baseColor + metallic + reflectance` 重建，保持 XRender reflectance 方向。

C# 侧需要新增资源名：

```text
GBuffer0
GBuffer1
GBuffer2
```

对应 Shader 全局名：

```text
_BurtGBuffer0
_BurtGBuffer1
_BurtGBuffer2
```

## 7. RenderGraph 升级路线

现在的 `BurtRenderGraph` 是顺序列表，短期不要一次性改成复杂系统。建议分三阶段升级。

### 阶段 A：资源语义补齐

目标：让现有顺序图可以描述 Deferred 和 PostProcess。

需要做：

- `BurtRenderGraphResourceRegistry` 支持注册 GBuffer。
- `BurtRenderPassBuilder` 支持 `ReadGBuffer0/1/2`、`WriteGBuffer0/1/2`。
- Debug dump 可以打印 GBuffer 读写。
- Validation 可以发现 GBuffer read-before-write。
- 支持一个 Pass 写多个颜色目标的声明语义。

不做：

- 自动重排 Pass。
- 自动剔除 Pass。
- 自动计算资源生命周期。

### 阶段 B：RenderTarget 生命周期抽象

目标：让 CameraColor、CameraDepth、GBuffer、PostProcessColor 都走统一创建和释放接口。

需要做：

- 资源描述从 Pass 内部散落的 descriptor 创建，逐步迁移到统一 Descriptor Utility。
- 每种资源有明确的格式、尺寸、MSAA、depthBits、filterMode。
- Graph 或 Frame 能知道哪些资源是 transient，哪些是 external。
- Release Pass 不再完全手写成固定资源，逐步转成资源生命周期表驱动。

### 阶段 C：真正 RenderGraph 能力

目标：开始具备真实 RenderGraph 的基础能力。

需要做：

- Pass culling：没有被最终输出依赖的 Pass 可跳过。
- Resource aliasing：生命周期不重叠的 RT 可复用。
- 自动插入 Release 或统一释放。
- 资源状态和 debug marker 更清晰。

这个阶段不应在 Deferred 第一版前做，否则会拖慢主线。

## 8. Deferred 代码落点建议

建议新增文件：

```text
Assets/BurtRP/Runtime/Deferred/BurtDeferredGraphAssembler.cs
Assets/BurtRP/Runtime/Deferred/BurtDeferredPasses.cs
Assets/BurtRP/Runtime/Deferred/BurtGBufferRenderTargetPasses.cs
Assets/BurtRP/Runtime/Deferred/BurtGBufferDescriptorUtility.cs
Assets/BurtRP/Runtime/Shaders/BurtDeferredLighting.shader
Assets/BurtRP/Runtime/Shaders/BurtGBufferDebug.shader
```

建议修改文件：

```text
Assets/BurtRP/Runtime/BurtRenderPipelineAsset.cs
Assets/BurtRP/Runtime/BurtRenderPipeline.cs
Assets/BurtRP/Runtime/BurtRenderRequest.cs
Assets/BurtRP/Runtime/BurtRenderGraphResourceRegistry.cs
Assets/BurtRP/Runtime/BurtRenderPassBuilder.cs
Assets/BurtRP/Runtime/Debugging/BurtShadingDebugSettings.cs
```

PostProcess Agent 已经修改过 `BurtForwardGraphAssembler` 和 `BurtRenderPipelineAsset`；后续改 Deferred 前应先 review 当前后处理接入点，避免覆盖 No-op 框架。

## 9. Renderer Mode 设计

建议新增枚举：

```csharp
public enum BurtRendererMode
{
    Forward = 0,
    Deferred = 1
}
```

资产上增加字段：

```text
Renderer Mode
Enable GBuffer Debug
```

运行时选择逻辑：

```text
BurtRenderPipeline
    ↓
Create request
    ↓
根据 asset.RendererMode 或 camera override 选择 GraphAssembler
    ↓
ForwardGraphAssembler / DeferredGraphAssembler
```

第一版先只做全局 Asset 模式，不做 per-camera override。等 Camera Stack 更稳定后再考虑每个 Camera 单独选择渲染路径。

## 10. Odin 面板迁移计划

工程已有 Odin，后续新面板优先使用 Odin。

推荐策略：

- 新增配置类直接用 Odin。
- 老的 `BurtRenderPipelineAssetEditor` 暂时保留，避免一次性大迁移。
- 等 PostProcess / Deferred 设置稳定后，再统一移除或弱化手写 Editor。

优先 Odin 化的对象：

```text
BurtRenderPipelineAsset
BurtCameraData
BurtPostProcessSettings
BurtDeferredSettings
BurtShadowSettings
BurtDebugSettings
```

推荐分组：

```text
Pipeline
  Renderer Mode
  Clear
  Render Targets

Shading
  PBR
  PreIntegrated FG LUT

Lighting
  Main Light
  Shadows

Post Processing
  Enable
  Tonemapping
  Color Adjustment
  Bloom

Debug
  Camera Sort
  RenderFrame
  RenderGraph
  Shading Debug
  Depth Debug
  Shadow Debug
```

推荐 Odin 属性：

```text
[TitleGroup]
[FoldoutGroup]
[TabGroup]
[ShowIf]
[HideIf]
[InlineProperty]
[MinValue]
[MaxValue]
[InfoBox]
[Button]
```

注意：如果项目没有 asmdef，直接在 Runtime 里 `using Sirenix.OdinInspector` 通常可以工作；如果后续加 asmdef，需要显式引用 Odin 程序集。

## 11. 阴影计划

阴影当前处在“主光单阴影图可用，但还不是生产级阴影系统”的阶段。现有能力已经能验证主光 shadow map 是否生成、ShadowCaster 是否绘制、Lit shader 是否采样、Debug View 是否显示，但还缺少级联、过滤质量、跨渲染路径统一和稳定的编辑器面板。

### 11.1 当前阴影状态

已经具备：

- `BurtShadowData` 保存主光阴影相关数据和默认参数。
- `BurtForwardGraphAssembler` 会在需要阴影时插入主光阴影图申请、ShadowCaster 绘制和释放 Pass。
- `BurtRenderRequest.CreateCameraRequest` 会在 Cull 前写入 `cullingParameters.shadowDistance`，保证 Unity 能收集 shadow caster。
- `BurtLit.shader` 有 `ShadowCaster` Pass，并支持 alpha clip 轮廓一致性。
- `BurtShadows.hlsl` 已经有主光 shadow map 采样入口。
- `BurtDebugMainLightShadowMap.shader` 和 `BurtDebugMainLightShadowMapPass` 能直接查看主光阴影图。
- 已经修过 depth debug / shadow debug / final blit 的 YFlip 方向问题。
- 当前常见黑色环带问题已通过 bias / sample bias 调整缓解。

仍然欠缺：

- 只有单张主光 shadow map，没有 CSM 级联。
- 没有 PCF / PCSS / VSM / EVSM 等可配置过滤质量体系。
- Shadow settings 还散在 `BurtRenderPipelineAsset` 上，后续应该拆成 Odin 分组或独立 settings。
- 没有 per-light shadow override 面板。
- 没有稳定的 shadow atlas 规划，后续多光源阴影无法直接扩展。
- Deferred 第一版还没有明确如何复用 Forward 的阴影采样。
- Debug View 只覆盖主光 shadow map，没有 cascade split、shadow coord、shadow attenuation 等细分 debug。

### 11.2 阴影短期目标

短期不要马上做复杂软阴影，先把“主光阴影系统”做稳定。

推荐短期任务：

1. 把主光阴影配置从 `BurtRenderPipelineAsset` 的散字段整理成 `BurtShadowSettings`。
2. 使用 Odin 给 Shadow Settings 做清晰面板：Enable、Resolution、Distance、Depth Bias、Normal Bias、Sample Bias、Debug。
3. 给 Shadow Debug 增加更多模式：ShadowMap、ShadowCoord、ShadowAttenuation、ReceiverDepth。
4. 明确 ShadowCaster Pass 的 LightMode 和 alpha clip 规则，保证 Lit / Transparent / Cutout 轮廓一致。
5. 把 shadow bias 的单位和含义写清楚：常量 bias、normal bias、sample bias 分别作用在哪个阶段。
6. 建立一组标准验证场景：平面 + Cube、斜坡、薄片、远距离物体、Alpha Clip 叶片。

短期验收标准：

- 主光阴影在 GameView、SceneView 下方向一致。
- 常用 Bias 参数下没有明显 acne、peter-panning 和黑色环带。
- ShadowDistance 改动会立即影响阴影视距。
- Alpha Clip 物体的可见轮廓和投影轮廓一致。
- Debug View 能快速定位问题来自 shadow map、shadow coord 还是 receiver comparison。

### 11.3 CSM 级联计划

CSM 是主光阴影进入生产可用的关键节点，但建议放在 Deferred 第一版资源模型明确之后再正式做。

原因：

- CSM 会增加 shadow map atlas 或 texture array 的资源管理复杂度。
- CSM 需要每个 cascade 独立 view-projection、split sphere、bias 和裁剪范围。
- Deferred 和 Forward 都需要读取同一套 cascade 参数，所以应先稳定全局 shader 常量布局。

推荐 CSM 第一版：

```text
Cascade Count: 1 / 2 / 4
Split Mode: Manual Ratios
Shadow Storage: Atlas 2D
Filtering: 先沿用当前采样或 2x2 PCF
Debug: Cascade Index / Cascade Border / Atlas View
```

CSM Pass 顺序建议：

```text
Allocate MainLightShadowAtlas
For each cascade:
    Setup Cascade ViewProjection
    Draw ShadowCaster into atlas viewport
Upload Cascade Matrices / SplitSpheres / AtlasScaleOffset
Forward 或 Deferred Lighting 采样 cascade shadow
Release MainLightShadowAtlas
```

CSM 数据建议：

```text
_BurtMainLightShadowMatrices[4]
_BurtMainLightShadowSplitSpheres[4]
_BurtMainLightShadowAtlasScaleOffsets[4]
_BurtMainLightShadowCascadeCount
_BurtMainLightShadowDistance
```

CSM Debug 建议：

- `MainLightShadowMap`：显示 atlas。
- `CascadeIndex`：按颜色显示当前像素落在哪个 cascade。
- `CascadeBlend`：后续做 cascade fade 时显示混合区域。
- `ShadowAttenuation`：显示最终阴影衰减。

### 11.4 软阴影和过滤计划

软阴影不要早于 CSM 第一版。先让 hard shadow / small PCF 稳定，再扩展质量档位。

推荐过滤等级：

```text
None: 单点比较，用于调试。
PCF 2x2: 最低成本软化。
PCF 3x3: 默认质量档。
PCF 5x5: 高质量但成本较高。
```

后续可研究：

- 接触硬化 PCSS。
- EVSM / MSM 这类可过滤阴影图。
- 屏幕空间 contact shadow。

第一阶段不建议做 PCSS，原因是它会引入 blocker search、半径估计和性能调参，容易在基础阴影还不稳定时制造更多变量。

### 11.5 多光源阴影计划

多光源阴影应该晚于多光源光照和主光 CSM。

推荐前置条件：

- Forward 支持多附加光。
- Deferred 第一版能跑主光。
- 有统一 light list 或最小 visible light buffer。
- RenderGraph 能管理 shadow atlas 生命周期。

多光源阴影第一版建议只支持：

- 少量 punctual light shadow。
- 单 atlas。
- 固定最大 shadowed light 数量。
- Debug 输出 shadow atlas 和每盏灯的 atlas rect。

暂时不做：

- Cube map point light shadow。
- Per-object shadow mask。
- Mixed Lighting / Baked ShadowMask。

### 11.6 Forward / Deferred 阴影统一

阴影采样函数必须放在 shader library 中，Forward 和 Deferred 共用。

推荐原则：

- Shadow map 生成只做一次，不因 Forward / Deferred 分叉复制逻辑。
- `BurtShadows.hlsl` 提供统一的世界坐标到 shadow attenuation 函数。
- Forward Lit 直接调用该函数。
- Deferred Lighting 从 depth 重建 world position 后调用同一个函数。
- Debug View 读取同一套 shadow 参数，避免 Forward 和 Deferred debug 结果不一致。

Deferred Lighting 阴影输入：

```text
Depth -> reconstruct positionWS
Normal -> GBuffer normalWS
positionWS -> BurtMainLightRealtimeShadow(positionWS)
```

### 11.7 阴影与 RenderGraph 的关系

短期可以继续保持手写 Shadow Pass 顺序，但资源名要提前为 CSM / atlas 留空间。

当前资源：

```text
MainLightShadowMap
```

后续建议演进为：

```text
MainLightShadowAtlas
AdditionalLightShadowAtlas
```

RenderGraph 需要支持：

- Shadow atlas 是 CameraColor 之前的图内资源。
- Shadow pass 写 ShadowAtlas。
- Opaque / Deferred Lighting 读 ShadowAtlas。
- Debug pass 读 ShadowAtlas 并写 CameraColor。
- Release pass 在所有读者之后执行。

### 11.8 阴影与 Odin 面板

Shadow Settings 适合用 Odin 独立分组，不建议继续把所有字段平铺在 Asset 上。

推荐结构：

```text
BurtShadowSettings
  Enable Main Light Shadows
  Main Light Resolution
  Main Light Distance
  Bias
    Depth Bias
    Normal Bias
    Sample Bias
  Cascade
    Enable Cascades
    Cascade Count
    Split Ratios
  Filtering
    Filter Mode
    Filter Radius
  Debug
    Enable Debug View
    Debug Mode
    Debug Exposure
    Debug YFlip Mode
    Enable Debug Log
```

Odin 建议：

```text
[FoldoutGroup("Shadows/Main Light")]
[FoldoutGroup("Shadows/Bias")]
[FoldoutGroup("Shadows/Cascade")]
[ShowIf]
[MinValue]
[MaxValue]
[InfoBox]
```

### 11.9 阴影推荐执行顺序

1. 保持当前主光阴影实现稳定，不立即重写。
2. 整理 `BurtShadowSettings`，用 Odin 做面板。
3. 增加 Shadow Debug Mode：ShadowMap / ShadowAttenuation / CascadeIndex 预留。
4. 把主光阴影资源名从概念上升级为 atlas，但第一版仍可只有一块区域。
5. 实现 CSM 2/4 cascades。
6. 加 PCF 2x2 / 3x3 质量档位。
7. Deferred Lighting 接入同一套主光阴影采样。
8. 多光源光照稳定后，再做 additional light shadow atlas。

### 11.10 阴影风险

- Bias 参数容易相互抵消或叠加过度，必须明确每个 bias 的作用阶段。
- CSM 会放大 YFlip、深度范围和矩阵约定问题，做之前需要先锁定 shadow debug 方向。
- Alpha Clip 如果 Forward / DepthOnly / ShadowCaster 使用不同采样路径，会出现可见物体和阴影轮廓不一致。
- Deferred 重建 positionWS 如果不准，会导致阴影边缘抖动或整体偏移。
- 多光源阴影会快速增加 RT 和 draw call，需要先有明确最大数量和 atlas 策略。

## 12. 后处理框架第一阶段状态与主线决策

PostProcess Agent Russell 已完成第一阶段 No-op 后处理框架，当前状态：

- 已新增 `Runtime/PostProcessing` 模块。
- 已新增 `BurtPostProcessSettings`，并用 Odin 属性组织配置。
- 已在 `BurtRenderPipelineAsset` 上挂接 `postProcessSettings`。
- 已新增 `PostProcessColor` 资源、Builder 读写接口和 Graph Context 访问入口。
- 已新增 `Burt Allocate Post Process Color`、`Burt No-op Post Process`、`Burt Release Post Process Color` 三个 Pass。
- 已新增 `Hidden/BurtRP/PostProcessCopy`，只做原样拷贝，不做 YFlip。
- 当前插入点是 `Draw Transparent / Unsupported Shader Debug` 之后、Depth/Shadow Debug 之前、`FinalBlit` 之前。
- 后处理只在 `ShouldFinalBlit` 的 request 上执行，避免 Camera Stack 共享 RT 时重复处理。

第一阶段完成后，建议按这个顺序决策：

1. 确认 No-op PostProcess 不改变 SceneView / GameView / Preview 输出。
2. 确认 RenderGraph Debug 能看到 PostProcess Pass。
3. 如果稳定，先做 Tonemapping。
4. Tonemapping 稳定后，再开始 Deferred GBuffer 资源接入。
5. Deferred 第一版完成后，再把 PostProcess 放到 Forward 和 Deferred 共同尾部路径。

如果 PostProcess 框架引入 RT 或 YFlip 问题，应先修后处理框架，不要同时开 Deferred。
### 12.1 后处理 No-op 验证记录

2026-05-10 SceneView RenderGraph Debug 验证结果：

- 打开 `Enable Post Processing` 和 `Enable No-op Copy` 后，RenderGraph Pass Count 从原有链路增加到 19。
- 后处理 Pass 正确插在普通场景绘制之后、FinalBlit 之前：
  - `Burt Allocate Post Process Color`
  - `Burt No-op Post Process`
  - `Burt Release Post Process Color`
- `Burt No-op Post Process` 的资源声明符合第一阶段预期：读取 `CameraColor, PostProcessColor`，写入 `PostProcessColor, CameraColor`。
- `PostProcessColor` 已经进入资源表，并且生产者/消费者关系可在 RenderGraph Debug 中看到。
- `FinalBlit` 仍然只读取 `CameraColor` 并写入 `FinalCameraTarget`，说明后处理框架没有绕过最终输出路径。
- 当前剩余校验提示是 `Burt Setup Lighting` 未声明资源读写；这不是后处理问题，而是全局 shader 状态 Pass 尚未被 RenderGraph 资源模型表达。

后续处理建议：

- 如果 SceneView / GameView / Preview 画面和关闭 No-op 时一致，则后处理框架第一阶段可以视为通过。
- `Burt Setup Lighting` 后续应被标记为 Global State Side Effect Pass，或引入 `LightingGlobals` 这类虚拟资源，避免 RenderGraph Debug 持续输出误导性 warning。
- 主光阴影采样后续也应该在 Opaque / Deferred Lighting 的资源声明里显式 `Read MainLightShadowMap`，否则资源 debug 只能看到 shadow map 被释放，看不到被 shading 消费。

### 12.2 Tonemapping 第一版状态

2026-05-10 Russell 已完成 Tonemapping 第一版，主线审查结论如下：

- 新增 `BurtTonemappingMode`，当前包含 `None`、`Neutral`、`ACES` 三种模式。
- `BurtPostProcessSettings` 只保留后处理框架级开关、No-op Copy 验证和日志开关。
- 新增 `BurtTonemappingVolumeComponent`，Tonemapping 模式和曝光补偿改为从 Global Volume 读取。
- `BurtRenderPipelineAsset` 新增 `Post Process Volume Layer Mask`，用来决定哪些 Volume 会影响 BurtRP 后处理。
- 原 `Burt No-op Post Process` 已升级为 `Burt Post Process`，同一个 Pass 支持 No-op Copy 和 Tonemapping。
- 后处理链路仍保持 `CameraColor -> PostProcessColor -> CameraColor -> FinalBlit`，Tonemapping 只发生在第一段拷贝中。
- 第二段 `PostProcessColor -> CameraColor` 会强制关闭 Tonemapping，只做纯拷贝，避免同一帧重复套两次曲线。
- 后处理 shader 不做 YFlip，最终方向仍由 `Burt Final Blit` 统一处理，这是保持 SceneView / GameView / Camera Preview 方向稳定的关键约束。

当前验证结论：

- 用户已确认 No-op 阶段：SceneView 画面和关闭后处理时一致。
- 用户已确认 No-op 阶段：GameView 画面和关闭后处理时一致。
- 用户已确认 No-op 阶段：Camera Preview 不反、不变色、不丢画面。
- 用户已确认 Global Volume Tonemapping 第一版测试通过。
- 这意味着 `Burt Post Process`、`PostProcessColor`、VolumeStack 参数读取和 FinalBlit YFlip 当前可以作为稳定尾部链路继续使用。

Tonemapping 第一版建议测试顺序：

1. `BurtRenderPipelineAsset` 上保持 `Enable Post Processing = On`，并先保持 `Enable No-op Copy = On`。
2. 新建或选中一个 `Global Volume`，在 Volume Profile 里添加 `BurtRP/Post Processing/Tonemapping`。
3. `mode = None` 时确认画面继续与 No-op 验证一致。
4. 切到 `Neutral`，`postExposure = 0`，确认只出现亮度压缩，不出现 YFlip 或 Preview 丢画面。
5. 切到 `ACES`，`postExposure = 0`，确认高光被压缩、画面对比变化符合预期。
6. 分别测试 `postExposure = 1` 和 `postExposure = -1`，确认亮度变化方向正确。
7. 关闭 `Enable No-op Copy`，保持 Volume Tonemapping 为 `ACES`，确认后处理 Pass 仍会因为有效 Volume 效果被插入。
8. 打开 RenderGraph Debug，确认 Pass 名称是 `Burt Post Process`，资源关系仍是读取 `CameraColor, PostProcessColor`，写入 `PostProcessColor, CameraColor`。

Tonemapping 后续不急着扩展 Color Grading。下一步更重要的是让后处理框架成为 Forward / Deferred 共用尾部链路，然后再逐步加入 Bloom、Color Adjustment、LUT、FXAA/TAA 等正式后处理功能。

### 12.3 RenderGraph 资源语义补齐记录

2026-05-10 Tonemapping 验证通过后，主线开始补 RenderGraph 的非 RenderTarget 资源语义。

已经新增的逻辑全局资源：

```text
LightingGlobals
ShadowGlobals
```

为什么要加：

- `Burt Setup Lighting` 实际会写入主光方向、主光颜色、环境光、阴影默认强度等 shader 全局状态。
- 这些状态不是 RT，但它们确实是后续 Opaque / Transparent / Deferred Lighting 的输入。
- 如果 RenderGraph 只理解 CameraColor / CameraDepth 这类 RT，`Burt Setup Lighting` 就会一直显示“Pass 未声明任何资源读写”，这会干扰真正问题的判断。
- Deferred 第一版会更依赖全局光照和阴影状态，所以必须先把这类“逻辑资源”纳入 Debug / Validation。

当前语义：

```text
Burt Setup Lighting
  Write Global: LightingGlobals, ShadowGlobals

Burt Draw Main Light Shadow Caster
  Write: MainLightShadowMap
  Write Global: ShadowGlobals

Burt Draw Opaque
  Read Global: LightingGlobals, ShadowGlobals
  Read: MainLightShadowMap（仅当当前 request 需要主光阴影）

Burt Draw Transparent
  Read Global: LightingGlobals, ShadowGlobals
  Read: MainLightShadowMap（仅当当前 request 需要主光阴影）
```

这一步仍不代表 BurtRP 已经是真正 RenderGraph；它只是让当前顺序图能更准确表达“谁写了全局状态、谁读取了全局状态”。真正的 Pass Culling、自动调度和资源别名仍然放在 RenderGraph 阶段 C。

用户随后打开 SceneView RenderGraph Debug 验证：

- `Validation: OK`。
- `Burt Setup Lighting` 不再出现“Pass 未声明任何资源读写”。
- `LightingGlobals` 的生产者是 `Burt Setup Lighting`，消费者是 `Burt Draw Opaque` 和 `Burt Draw Transparent`。
- `ShadowGlobals` 的生产者是 `Burt Setup Lighting` 和 `Burt Draw Main Light Shadow Caster`，消费者是 `Burt Draw Opaque` 和 `Burt Draw Transparent`。
- `MainLightShadowMap` 已能显示真实 shading 消费者：`Burt Draw Opaque`、`Burt Draw Transparent` 和释放 Pass。

这个结果说明当前 Forward 图的资源声明已经足够进入 Deferred GBuffer 资源准备阶段。

### 12.4 Deferred GBuffer 资源准备记录

2026-05-10 在不切换渲染路径、不改变 Forward 输出的前提下，开始补 Deferred 第一版需要的 GBuffer 资源接口。

新增 RenderGraph 资源：

```text
GBuffer0
GBuffer1
GBuffer2
```

对应 shader 全局纹理名：

```text
_BurtGBuffer0
_BurtGBuffer1
_BurtGBuffer2
```

第一版资源语义：

```text
GBuffer0: baseColor.rgb + occlusion.a
GBuffer1: oct normal.rg + metallic.b + smoothness.a
GBuffer2: emission.rgb + reflectance.a
```

当前只完成资源层准备：

- `BurtRenderGraphResourceRegistry` 支持注册和读取 `GBuffer0/1/2`。
- `BurtRenderPassBuilder` 支持 `ReadGBuffer0/1/2` 和 `WriteGBuffer0/1/2`。
- `BurtRenderGraphContext` 支持通过 `GBuffer0Target/GBuffer1Target/GBuffer2Target` 读取句柄。
- `BurtRenderTargetDescriptorUtility` 已提供 `CreateGBuffer0Descriptor`、`CreateGBuffer1Descriptor`、`CreateGBuffer2Descriptor`。

这一步不会让当前 RenderGraph Debug 出现 GBuffer Pass，因为还没有创建 Deferred Assembler、Allocate GBuffer Pass 或 Draw GBuffer Pass。这样做的目的是先铺资源接口，保证下一步新增 Pass 时不会同时修改太多模块。

### 12.5 Deferred 组装器骨架和 GBuffer 生命周期记录

2026-05-10 已新增 Deferred 第二阶段骨架，但默认渲染路径仍然是 Forward。

新增管线配置：

```text
BurtRendererMode
  Forward
  Deferred
```

当前默认值：

```text
RendererMode = Forward
```

为什么默认仍然是 Forward：

- Forward PBR、阴影、后处理和 Camera Stack 已经进入可验证状态。
- Deferred 目前还没有 Draw GBuffer、Deferred Lighting 和 GBuffer Debug。
- 如果默认切到 Deferred，会让当前稳定画面承担不必要风险。

新增运行时代码：

```text
Runtime/Deferred/BurtDeferredGraphAssembler.cs
Runtime/Deferred/BurtGBufferRenderTargetPasses.cs
```

当前 Deferred 组装器行为：

```text
Allocate GBuffer0
Allocate GBuffer1
Allocate GBuffer2
Forward Fallback Graph
Release GBuffer2
Release GBuffer1
Release GBuffer0
```

为什么临时复用 Forward：

- 这样可以先验证 Renderer Mode 切换、GBuffer 资源注册、GBuffer 临时 RT 申请和释放。
- 这样不会在还没有 Deferred Lighting 时让画面变黑。
- 后续只需要逐步把 `Forward Fallback Graph` 替换为正式 Deferred Pass 链。

预期验证方式：

1. 默认 `Renderer Mode = Forward` 时，RenderGraph Debug 不应出现 GBuffer Pass，画面不变。
2. 手动切到 `Renderer Mode = Deferred` 时，RenderGraph Debug 应出现：
   - `Burt Allocate GBuffer0`
   - `Burt Allocate GBuffer1`
   - `Burt Allocate GBuffer2`
   - `Burt Release GBuffer2`
   - `Burt Release GBuffer1`
   - `Burt Release GBuffer0`
3. Deferred 实验模式当前画面仍应和 Forward 一致，因为真实绘制仍走 Forward fallback。
4. Deferred 实验模式下 `Validation` 应保持 `OK`，GBuffer 资源应该至少有 Allocate 生产者和 Release 消费者。

后续 Deferred 计划：

1. 验证 `Burt Draw GBuffer Opaque` 是否真的写入三张 GBuffer。
2. 验证 `Burt Deferred Lighting` 是否能用 `Hidden/BurtRP/DeferredLighting` 合成到 `CameraColor`。
3. Deferred Lighting 稳定后，逐步移除或开关化当前的 Forward Opaque fallback。
4. 保持 Skybox、Transparent、PostProcess、FinalBlit 继续复用当前尾部链路。
5. 添加 GBuffer Debug View，直接显示 GBuffer0/1/2 和解码后的 normal/material。

### 12.6 Deferred MRT 绑定和清理记录

2026-05-10 已把 Deferred 组装器从“整段 Forward fallback”推进到“可插入 GBuffer 阶段的独立顺序图”。

新增 Pass：

```text
Burt Set GBuffer Render Targets
Burt Clear GBuffer Render Targets
```

当前 Deferred 实验模式顺序变为：

```text
Allocate CameraColor
Allocate CameraDepth
Allocate GBuffer0
Allocate GBuffer1
Allocate GBuffer2
Setup Lighting
MainLight Shadow（如果启用）
Set Camera Render Target
Clear Camera Render Target
Depth Prepass
Set GBuffer Render Targets
Clear GBuffer Render Targets
Draw GBuffer Opaque
Set Camera Render Target
Forward Opaque fallback
Burt Deferred Lighting
Skybox
Transparent Forward
Unsupported Shader Debug
PostProcess（如果启用）
Debug View（如果启用）
FinalBlit
Release Shadow
Release GBuffer2
Release GBuffer1
Release GBuffer0
Release CameraColor
Release CameraDepth
```

为什么这样做：

- `Set/Clear GBuffer` 需要 `CameraDepth` 已经申请，所以不能继续放在完整 Forward fallback 前面。
- 组装器先申请 Camera RT，再申请 GBuffer，这样 MRT 绑定时四个目标都是真实临时 RT。
- GBuffer 清理和绘制后会立刻重新绑定 `CameraColor + CameraDepth`，避免后续全屏合成或 Forward fallback 继续写进 GBuffer。
- Forward Opaque fallback 暂时保留在 `Burt Deferred Lighting` 前面，shader 缺失时它提供稳定底图；shader 可用后 Deferred Lighting 会覆盖它。
- GBuffer 清理当前只清 GBuffer 颜色，不清 `CameraDepth`，避免结构验证阶段改变 Forward fallback 的深度行为。
- GBuffer 生命周期当前跟随本 request 的 CameraColor/CameraDepth 申请，不跨 request 保留；等正式 Draw GBuffer 接入后再评估 Camera Stack 下的共享策略。

当前验证重点：

1. 默认 `Renderer Mode = Forward` 时，RenderGraph Debug 不应出现 GBuffer Pass。
2. 切到 `Renderer Mode = Deferred` 时，RenderGraph Debug 应出现 GBuffer allocate、set、clear、draw、release。
3. Deferred 实验模式当前画面应仍然接近 Forward，因为真实场景绘制仍回到 `CameraColor`。
4. `Validation` 应保持 OK；如果出现 GBuffer read-before-write，优先检查组装器是否在没有申请 GBuffer 的 request 上插入了 release。

下一步 Deferred 计划更新：

1. 验证 Gibbs 提供的 `BurtGBuffer` Pass 是否真的输出三张 GBuffer。
2. 验证 Gibbs 提供的 `Hidden/BurtRP/DeferredLighting` shader 是否能被 C# Pass 找到。
3. 比较 Deferred Lighting 与 Forward Opaque 的亮度、法线、高光和阴影。
4. Deferred Lighting 出图后，逐步移除当前的 Forward Opaque fallback。
5. 等 Deferred Lighting 稳定后，再添加 GBuffer Debug View。

### 12.7 Draw GBuffer Opaque 管线侧接入记录

2026-05-10 主线按“shading 交给长期 PBR Agent，管线结构由主 Agent 负责”的分工，先把 C# 管线侧 `Draw GBuffer Opaque` 接入。

新增管线侧能力：

```text
BurtDrawingSettingsUtility.CreateGBufferDrawingSettings
Burt Draw GBuffer Opaque
```

约定 shader tag：

```text
LightMode = BurtGBuffer
```

为什么先做 C# 管线侧：

- PBR / shader 侧已经交给长期 PBR Agent Gibbs 继续推进，主 Agent 不再在同一步里改 BRDF 和 GBuffer shader 编码。
- C# 侧先固定 `ShaderTagId("BurtGBuffer")`，Gibbs 只需要在 `BurtLit.shader` 中提供同名 Pass。
- 在 shader pass 还不存在时，`Burt Draw GBuffer Opaque` 会自然不绘制任何 Renderer，不会破坏当前画面。
- 这让 RenderGraph Debug 可以提前看到 GBuffer 绘制节点，后续 shader 侧一接入就能直接验证 GBuffer 内容。

当前 Deferred 中间顺序：

```text
Set Camera Render Target
Clear Camera Render Target
Depth Prepass
Set GBuffer Render Targets
Clear GBuffer Render Targets
Draw GBuffer Opaque
Set Camera Render Target
Forward Opaque fallback
Burt Deferred Lighting
```

为什么这样排：

- `Clear Camera Render Target` 必须早于 GBuffer 阶段，否则它会清掉 `CameraDepth`，导致 GBuffer 前建立的深度失效。
- `Depth Prepass` 放在 GBuffer 前面，第一版可以让 GBuffer pass 先复用已有深度，降低变量数量。
- `Clear GBuffer Render Targets` 只清 GBuffer 颜色，不清 `CameraDepth`，避免破坏刚写好的深度。
- `Draw GBuffer Opaque` 之后立即 `Set Camera Render Target`，避免后续全屏合成或 Forward fallback 继续写进 GBuffer。
- `Forward Opaque fallback` 暂时保留在 `Burt Deferred Lighting` 前面，作用是当 Deferred Lighting shader 缺失时仍然有稳定画面；shader 可用后 Deferred Lighting 会覆盖这张临时底图。

下一步分工：

- Gibbs：实现或完善 `BurtLit.shader` 的 `LightMode = BurtGBuffer` Pass，输出 `SV_Target0/1/2`。
- 主 Agent：等 GBuffer shader pass 可用后，继续做 `Burt Deferred Lighting` 全屏 Pass 和 CameraColor 合成。
- 主 Agent：在 Deferred Lighting 出图前，Forward fallback 暂时保留，避免 Deferred 模式黑屏。

### 12.8 Deferred Lighting 管线侧接入记录

2026-05-10 在 Gibbs 完成 `LightMode = BurtGBuffer` shader pass 后，主线接入 C# 管线侧 Deferred Lighting 框架。

新增文件：

```text
Runtime/Deferred/BurtDeferredLightingPasses.cs
```

新增 Pass：

```text
Burt Deferred Lighting
```

当前 shader 约定：

```text
Shader.Find("Hidden/BurtRP/DeferredLighting")
```

Pass 资源声明：

```text
Read: GBuffer0, GBuffer1, GBuffer2, CameraDepth
Read Global: LightingGlobals, ShadowGlobals
Read: MainLightShadowMap（仅当前 request 生成主光阴影时）
Write: CameraColor
```

C# 侧会上传给 shader 的资源和参数：

```text
_BurtGBuffer0
_BurtGBuffer1
_BurtGBuffer2
_BurtCameraDepthTexture
_BurtMainLightShadowMap（存在时）
_BurtDeferredInverseViewProjectionMatrix
_BurtDeferredCameraWorldPosition
_BurtDeferredCameraClipPlanes
_BurtDeferredScreenSize
```

为什么先接 C# 框架：

- RenderGraph 需要先能表达 Deferred Lighting 对 GBuffer、Depth、LightingGlobals、ShadowGlobals 和 CameraColor 的依赖。
- shader 如果还没导入或名字不匹配，Pass 会安全跳过，并且当前 Deferred 路径仍有 Forward Opaque fallback。
- shader 可用后，`Burt Deferred Lighting` 会在 Skybox 和 Transparent 前把 GBuffer 光照结果写入 `CameraColor`。
- Forward Opaque fallback 目前仍保留在 Deferred Lighting 前，只作为临时安全底图；等 Deferred Lighting 稳定后会移除。

当前 Deferred 核心顺序：

```text
Depth Prepass
Set GBuffer Render Targets
Clear GBuffer Render Targets
Draw GBuffer Opaque
Set Camera Render Target
Forward Opaque fallback
Burt Deferred Lighting
Draw Skybox
Draw Transparent
PostProcess
FinalBlit
```

下一步：

1. Gibbs 完成或确认 `Hidden/BurtRP/DeferredLighting` shader。
2. 用户打开 `Renderer Mode = Deferred` 和 `RenderGraph Debug`，确认出现 `Burt Deferred Lighting`。
3. 若 shader 已可用，比较 Deferred Lighting 与 Forward Opaque 的亮度、法线、高光和阴影是否接近。
4. Deferred Lighting 稳定后，主线移除或开关化 `Forward Opaque fallback`。
5. 再添加 GBuffer Debug View，用于直接查看三张 GBuffer 和解码后的 normal/material。

### 12.9 Deferred Forward Fallback 开关与 GBuffer Debug 接入记录

2026-05-10 主 Agent 在不改动 PBR/Deferred Lighting 核心公式的前提下，继续推进 Deferred 管线侧调试能力。

本次新增资产配置：
```text
Deferred - 延迟渲染
  Enable Deferred Forward Opaque Fallback
  GBuffer Debug View Mode
```

新增模式枚举：
```text
BurtGBufferDebugViewMode
  Disabled
  GBuffer0
  GBuffer1
  GBuffer2
  BaseColor
  NormalWS
  Metallic
  Smoothness
  Occlusion
  Emission
  Reflectance
  RawDepth
```

新增文件：
```text
Runtime/Deferred/BurtGBufferDebugPasses.cs
Runtime/Shaders/BurtDebugGBuffer.shader
```

为什么先加 Forward Opaque Fallback 开关：
- Deferred Lighting 第一版刚接入时，Forward Opaque fallback 可以避免 shader 缺失或光照错误导致整屏黑掉。
- 当 `Hidden/BurtRP/DeferredLighting` 已经能稳定输出后，需要关闭 fallback 才能确认画面真的来自 Deferred Lighting，而不是被 Forward 底图掩盖。
- 这个开关默认保留兼容行为；测试纯 Deferred 时手动关闭即可。

为什么现在加 GBuffer Debug：
- Deferred 的第一类问题通常不是最终 BRDF，而是 GBuffer 有没有写对：baseColor、normal、metallic、smoothness、occlusion、emission、reflectance 是否落在预期通道。
- 如果没有 GBuffer Debug，Deferred Lighting 变暗、法线反、金属高光异常时只能从最终画面反推，定位效率很低。
- 这个 Pass 放在 PostProcess 之后、FinalBlit 之前，和现有 Depth/Shadow Debug 一样直接覆盖 CameraColor，便于观察原始缓存。
- GBuffer Debug 只在 `Renderer Mode = Deferred` 且当前 request 真正申请 GBuffer 时插入，避免 Forward 路径读取不存在的 RT。

当前 Deferred 调试顺序更新为：
```text
Depth Prepass
Set GBuffer Render Targets
Clear GBuffer Render Targets
Draw GBuffer Opaque
Set Camera Render Target
Forward Opaque Fallback（可关闭）
Burt Deferred Lighting
Draw Skybox
Draw Transparent
Unsupported Shader Debug
PostProcess
Depth / Shadow / GBuffer Debug
FinalBlit
```

短期测试建议：
1. 切到 `Renderer Mode = Deferred`。
2. 先保持 `Enable Deferred Forward Opaque Fallback = On`，确认画面不黑。
3. 打开 RenderGraph Debug，确认出现 `Burt Debug GBuffer` 只在 `GBuffer Debug View Mode != Disabled` 时插入。
4. 依次查看 `BaseColor`、`NormalWS`、`Metallic`、`Smoothness`、`Occlusion`、`Reflectance`。
5. 如果 Deferred Lighting 看起来正确，再关闭 `Enable Deferred Forward Opaque Fallback`，确认不透明物体仍然由 Deferred Lighting 输出。
6. 如果关闭 fallback 后物体消失，优先检查 `Burt Draw GBuffer Opaque` 是否真的绘制了 `LightMode = BurtGBuffer` 的材质。

与 Gibbs 的分工：
- 主 Agent 负责 C# 管线调度、RenderGraph 资源声明、Debug Pass 插入点和资产开关。
- Gibbs 继续负责 `Hidden/BurtRP/DeferredLighting`、`BurtDeferred.hlsl` 注释修复和 PBR/Deferred shader 侧一致性。
- 本次新增的 `Hidden/BurtRP/DebugGBuffer` 只是调试可视化 shader，不承担正式 lighting 公式。

### 12.10 Deferred 全屏采样 YFlip 修复记录

2026-05-10 用户切到 `Renderer Mode = Deferred` 后发现画面上下颠倒，主 Agent 对 Deferred 全屏采样路径做了一次对齐修复。

问题判断：
- Forward 路径本身方向正常，说明 `FinalBlit` 的 SceneView / GameView / Preview 方向规则没有整体失效。
- Deferred Lighting 是全屏三角形从 GBuffer 采样再写回 `CameraColor`，方向问题集中在 “GBuffer/Depth 作为源纹理被采样” 这一层。
- XRender 的 `GetFullScreenTriangleTexCoord` 在 `PLATFORM_UV_STARTS_AT_TOP` 平台会翻转采样 UV 的 y，而 BurtRP 之前的 `BurtGetFullScreenTriangleTexCoord` 没有做这一步。

本次修复：
- `BurtDeferred.hlsl` 的 `BurtGetFullScreenTriangleTexCoord` 改为对齐 XRender：`UNITY_UV_STARTS_AT_TOP` 时执行 `uv.y = 1.0f - uv.y`。
- `BurtBuildDeferredClipPosition` 保持原有 NDC.y 翻转逻辑，因为这一步对应 XRender 的 `TransformScreenUVToPositionNDC`。
- `BurtDebugGBufferPass` 不再叠加 `FinalBlit` 的预翻转，避免 GBuffer Debug 在新的平台采样 UV 修正后被二次翻转。

为什么不是改 FinalBlit：
- FinalBlit 已经被 Forward、Depth Debug、Shadow Debug 和 PostProcess 验证过，问题只在 Deferred 从 GBuffer 采样的全屏 Pass。
- 如果改 FinalBlit，会重新影响 Forward 和后处理；因此这次只修 Deferred 共享采样函数。

后续验证：
1. 切到 `Renderer Mode = Deferred`，关闭所有 Debug View，确认 SceneView / GameView 方向是否恢复。
2. 打开 `GBuffer Debug View Mode = BaseColor`，确认 GBuffer Debug 方向是否和正常画面一致。
3. 如果 GameView 仍然反，而 SceneView 已正常，再单独检查 `BurtFinalBlitUtility.ResolveFinalBlitYFlip` 是否需要 Deferred 专属分支。

## 13. 验证清单

### Camera / Clear

- GameView Main Camera：`ClearDataSource=BurtCameraData`。
- SceneView：选中 Burt Camera 时 `ClearDataSource=EditorSelectedCamera`。
- Camera Preview：选中 Burt Camera 时 `ClearDataSource=EditorSelectedCamera`。
- Skybox / SolidColor 切换不影响 FinalBlit 方向。

### Forward PBR

- BaseColor、MaskMap、NormalMap、Emission 正常。
- Smoothness 拉满时直接高光不消失。
- Reflectance 不暴露 F0，非金属默认接近 0.04。
- SH 间接漫反射有效。
- Reflection Probe 间接高光有效。
- Shading Debug 各模式能稳定切换。

### Shadow

- 主光 shadow map 有内容。
- Shadow Debug View 方向正确。
- 常见 bias 下没有明显黑色环带。
- ShadowDistance 改动影响可见阴影范围。

### PostProcess

- No-op 开关打开/关闭画面一致。
- SceneView / GameView / Preview 不翻转。
- Global Volume 里的 Tonemapping `None` 模式与 No-op 验证结果一致。
- Global Volume 里的 Tonemapping `Neutral` 能压缩 HDR 亮度，但不改变最终输出方向。
- Global Volume 里的 Tonemapping `ACES` 能压高光并保持 Camera Preview 不反、不变色、不丢画面。
- Global Volume 里的 `postExposure` 正值变亮、负值变暗，且关闭 Tonemapping 时不影响 No-op Copy。
- Frame Debug 能看到后处理 Pass 插入点。
- 后处理关闭时不申请额外 RT。

### Deferred 第一版

- GBuffer Debug 能显示 baseColor、normal、metallic、smoothness、emission。
- Deferred Lighting 与 Forward 在简单材质下大致一致。
- Skybox 在 Deferred Lighting 后正确显示。
- Transparent 仍可在 Deferred 后叠加。
- PostProcess 能同时处理 Forward 和 Deferred 输出。

## 14. 风险清单

- SceneView / Preview 是 Unity 内部相机，编辑器同步逻辑必须保持可关闭或至少可诊断。
- 当前 RenderGraph 不做自动生命周期管理，新增 GBuffer 和 PostProcess RT 时容易漏 Release。
- Deferred 会要求 MRT，现有 `SetRenderTarget` Pass 只处理一个 color + depth，需要新增专用 SetGBufferRenderTargets Pass。
- PBR Forward 如果尚未稳定，Deferred 对齐会困难；需要先锁定 BRDF 公式和 debug view。
- Odin 面板迁移如果和手写 Editor 混用，可能出现字段显示重复或开关不同步。
- 后处理和 Deferred 都会触碰 `CameraColor`，必须明确谁读、谁写、谁最终交给 `FinalBlit`。

## 15. 推荐近期执行顺序

1. 验证 PostProcess Agent 已完成的 No-op 后处理框架。
2. 主 Agent 检查后处理接入点和 RenderGraph Debug 输出。
3. 做 Tonemapping 第一版。
4. 新增 Deferred Settings / Renderer Mode 设计，但先不切默认路径。
5. 注册 GBuffer 资源和 Debug 声明。
6. 给 `BurtLit.shader` 增加 GBuffer Pass。
7. 新增 Deferred Lighting Pass。
8. 让 Deferred 第一版只服务不透明物体，透明仍走 Forward。
9. 对齐 Forward / Deferred 的 Shading Debug。
10. 等 Forward PBR 和后处理稳定后，整理 Shadow Settings 和 Shadow Debug Mode。
11. 再考虑 RenderGraph 阶段 B 升级。

### 12.11 Deferred 全屏三角形投影/UV 约定修正

2026-05-10 继续排查 Deferred 模式下“不是单纯上下反，而像投影矩阵不对”的问题。结论是：上一轮只把采样 UV 对齐了 XRender，但顶点位置也复用了同一个已翻转 UV，导致平台翻转被顶点位置抵消，`screenUV -> NDC -> world` 的链路和 XRender 不一致。

本次修正：
- `BurtDeferred.hlsl` 新增 `BurtGetFullScreenTriangleRawCoord`，只生成未翻转的全屏三角形坐标。
- `BurtGetFullScreenTriangleVertexPosition` 改为使用未翻转坐标，保证全屏三角形的 SV_POSITION 始终是标准 `(-1,-1) / (3,-1) / (-1,3)` 覆盖方式。
- `BurtGetFullScreenTriangleTexCoord` 继续只对采样 UV 做 `UNITY_UV_STARTS_AT_TOP` 翻转，专门服务于 GBuffer / CameraDepth 采样。
- `BurtBuildDeferredClipPosition` 保留 `UNITY_UV_STARTS_AT_TOP` 下的 NDC.y 翻转，因为这一步对应 XRender `TransformScreenUVToPositionNDC`，用于把 top-origin 的屏幕 UV 还原成标准 NDC。

为什么这么做：
- XRender 的 `GetFullScreenTriangleVertexPosition` 和 `GetFullScreenTriangleTexCoord` 是分开的：顶点位置不翻，采样 UV 按平台翻。
- BurtRP 之前把“采样 UV”直接拿去生成“顶点位置”，会让 D3D 类平台上 `screenUV` 实际变回 bottom-origin，后面的 NDC.y 翻转就会变成错误的重建输入。
- 这个错误不会像普通贴图那样只是上下颠倒，而是会影响 Deferred Lighting 用深度重建 `positionWS`、`viewDirectionWS` 和 `shadowCoord` 的结果，所以视觉上更像投影矩阵或世界位置重建不对。

后续验证：
1. `Renderer Mode = Deferred`，关闭所有 Debug View，确认 SceneView / GameView / Preview 画面方向和透视都正常。
2. 打开 `GBuffer Debug View Mode = BaseColor`，确认 RenderGraph 中出现 `Burt Debug GBuffer`，并且画面变成无光照基础色。
3. 如果 BaseColor 仍不生效，下一步优先检查 RenderGraph 是否插入 `Burt Debug GBuffer`，再检查 `Hidden/BurtRP/DebugGBuffer` 是否被 Unity 成功导入。
4. 如果 BaseColor 生效但 Deferred Lighting 仍异常，下一步再比较 `GL.GetGPUProjectionMatrix(camera.projectionMatrix, true)` 与实际 `SetupCameraProperties` 的矩阵约定。

### 12.12 GBuffer Debug Overlay 接入修正

2026-05-10 用户反馈 Deferred 画面效果已正确，但 `GBuffer Debug View Mode = BaseColor` 没有效果；RenderGraph Debug 日志里也没有 `Burt Debug GBuffer`。这说明问题不是 `Hidden/BurtRP/DebugGBuffer` 没画出来，而是 RenderGraph 组装阶段没有插入 GBuffer Debug Pass。

问题原因：
- SceneView Overlay 里的 `GBuffer Base Color` 属于 `BurtShadingDebugSettings.Mode`。
- Deferred RenderGraph 之前只检查 `BurtRenderPipelineAsset.GBufferDebugViewMode`。
- Overlay 切换时只同步了 `enableDepthDebugView` 和 `enableMainLightShadowDebugView`，没有同步 `gBufferDebugViewMode`。
- 因此 Overlay 看起来选中了 GBuffer Debug，但资产上的 GBuffer Debug 仍是 `Disabled`，所以 `Burt Debug GBuffer` 不会进入 RenderGraph。

本次修正：
- 新增运行时 `BurtGBufferDebugViewUtility`，统一把资产面板模式和 Shading Debug Overlay 模式合并成最终 GBuffer Debug 模式。
- `BurtDeferredGraphAssembler` 改为调用 `BurtGBufferDebugViewUtility.ShouldUseGBufferDebugView`，Overlay 选择 GBuffer 模式时也会插入 `Burt Debug GBuffer`。
- `BurtDebugGBufferPass` 的 Configure / Execute / shader mode 上传都改为读取统一解析结果，避免“图插入了但执行阶段又跳过”。
- `BurtShadingDebugOverlayUtility.SyncExistingDebugViews` 现在会同步 `gBufferDebugViewMode`，让资产 Inspector 和 RenderGraph 条件一致。
- `BurtGBufferDebugViewMode` 补充 `Roughness` 和 `DiffuseColor`，`BurtDebugGBuffer.shader` 增加对应模式显示，和 Overlay 的 GBuffer 分组对齐。

为什么这样做：
- GBuffer Debug 是真实 RT 可视化，不是 Forward shader 内部的 roundtrip debug，所以必须驱动 RenderGraph 插入全屏 Pass。
- Overlay 是当前推荐的调试入口；它的选择必须能转成管线资产或运行时图组装条件，否则用户看到的 UI 状态和实际渲染图会不一致。
- 资产面板仍保留直接调试入口；当资产上显式选择 GBuffer Debug 模式时，不依赖 Overlay 也能工作。

后续验证：
1. 在 SceneView Overlay 选择 `GBuffer Base Color`，RenderGraph Debug 应出现 `Burt Debug GBuffer`。
2. Pass 顺序应位于 PostProcess 之后、FinalBlit 之前，用于最终覆盖显示调试图。
3. 切回 `None` 后，`Burt Debug GBuffer` 应从 RenderGraph 消失。
4. 依次验证 `GBuffer Normal WS / Metallic / Smoothness / Occlusion / Reflectance / Roughness / Diffuse Color`。

### 12.13 GBuffer Debug 验证通过后的下一步

2026-05-10 用户确认 Deferred 画面效果已经正确，并且 GBuffer Debug 已经能看到效果。当前 Deferred 第一版进入“可观察闭环”阶段：GBuffer 写入、Deferred Lighting、PostProcess、FinalBlit 和真实 GBuffer Debug 都已经串起来。

当前状态判断：
- Forward 路径仍是稳定基线，继续作为视觉对照。
- Deferred 路径已经能走完整渲染链路，不再只是资源生命周期验证。
- GBuffer Debug 已经是从真实 GBuffer RT 采样，不是 Forward shader 内部 roundtrip。
- 下一阶段重点不应马上扩很多功能，而应该先做 Forward/Deferred 一致性校准。

推荐下一步优先级：
1. 做 Deferred / Forward 对齐诊断：固定同一场景、同一材质、同一主光，比较 BaseColor、Normal、Smoothness、Metallic、Occlusion、Direct/Indirect 结果。
2. 给 Deferred Lighting 增加分阶段 Debug View：显示 Deferred DirectDiffuse、DirectSpecular、IndirectDiffuse、IndirectSpecular、Emission 和 FinalLighting，便于定位和 Forward 差异。
3. 暂时保留透明物体走 Forward，先把不透明 PBR Deferred 闭环做准。
4. Deferred Forward Opaque Fallback 作为迁移保护保留，但要新增明确测试：开启 fallback 与关闭 fallback 时，支持 BurtGBuffer 的不透明物体应该仍能显示。
5. RenderGraph 下一步可补充资源生命周期诊断，例如标注 Debug Pass 是否由 Asset 或 Overlay 触发，避免未来再次出现 UI 状态和图组装状态不一致。

暂缓事项：
- 暂不优先做 Deferred 多光源，因为当前主光 + IBL 的 Forward/Deferred 还没有完成严格对齐。
- 暂不优先做复杂后处理链，因为后处理框架和 Tonemapping 已有第一版，当前瓶颈是 Deferred shading 可解释性。
- 暂不优先做真正 RenderGraph 自动调度，当前顺序图足够支持 Deferred 第一版验证。


### 12.14 Deferred Lighting Debug 接入

2026-05-10 在 GBuffer Debug 已验证通过后，主 Agent 开始做 Deferred / Forward 光照对齐诊断的第一步：让 Deferred Lighting Pass 也能响应 Shading Debug Overlay 里的材质、BRDF 和 Lighting 调试模式。

本次修改：
- `BurtDeferredLighting.shader` 引入 `BurtShadingDebug.hlsl`，Deferred Lighting 不再只输出最终 `lighting + emission`，而是可以在 shader 内部提前返回调试颜色。
- Deferred Lighting 新增 `shadingGBufferData` 副本，`DetailLighting` 模式下只把参与光照计算的 `baseColor` 改成 `0.18` 中灰，真实 `gbufferData` 仍保持原样。
- Deferred Lighting 从真实 GBuffer 解码数据构造 `BurtSurfaceData`，让 `Albedo / Smoothness / Metallic / Occlusion / Reflectance` 等材质调试项和 Forward 使用同一个公共函数。
- Deferred Lighting 把 `BurtPBRShadingComponents` 的 direct / indirect / BRDF / IBL 拆项全部写入 `BurtShadingDebugData`，让 `DirectDiffuse / DirectSpecular / IndirectDiffuse / IndirectSpecular / D / Visibility / Fresnel / DFG` 等模式能直接观察 Deferred 结果。
- Deferred Lighting 的 GBuffer 类调试字段直接来自真实 `gbufferData`，而不是 Forward 的内部 roundtrip，这样可以继续验证实际 MRT 写入和采样是否正确。

为什么这样做：
- GBuffer Debug 只能证明材质数据写进 RT 后能被采样出来，但不能证明 Deferred Lighting 的 BRDF、主光、阴影、SH 和 Reflection Probe 与 Forward 一致。
- Forward 已经有一套可用的 `BurtShadingDebugData`；Deferred 复用同一套数据结构，可以减少两套 Debug View 口径不一致的风险。
- `DetailLighting` 必须只改 shading 输入副本，不能改真实 GBuffer，否则 Albedo 和 GBuffer Debug 会被中灰覆盖，后续排查会混淆“材质数据错”与“光照数据错”。
- 这一步没有新增新的 Debug enum，先复用现有 Lighting / BRDF 模式，降低 Overlay、资产 Inspector 和 shader 常量同步的风险。

推荐验证：
1. `Renderer Mode = Forward`，选择 `DirectDiffuse / DirectSpecular / IndirectDiffuse / IndirectSpecular`，记录同一材质球的视觉结果。
2. 切到 `Renderer Mode = Deferred`，保持同一个 Debug Mode，比较物体区域是否和 Forward 基本一致。
3. `DetailLighting` 模式下检查材质颜色是否被统一成中灰光照，但 `GBuffer Base Color` 仍能显示原始材质颜色。
4. 如果 Deferred 的直接光一致、间接光不一致，优先让 Gibbs 继续检查 PBR/IBL；如果只有特定窗口异常，主 Agent 回到 Camera RT / FinalBlit 路径排查。

下一步计划：
- 主 Agent 继续做 Deferred / Forward 对齐诊断工具，优先补足 RenderGraph 日志里“当前 Renderer Mode、GBuffer Debug 来源、Shading Debug Mode”的可见性。
- Gibbs 继续推进 PBR shading，重点对齐 XRender 的 direct specular、IBL DFG、Reflection Probe 采样和 roughness/smoothness 语义。
- Russell 继续维护后处理链路，但在 Deferred 光照稳定前不扩大复杂效果数量。

### 12.15 RenderGraph Debug 增加管线状态可见性

2026-05-10 在 Deferred Lighting Debug 验证通过后，主 Agent 增加 RenderGraph Debug 的状态摘要，目标是让后续 Forward / Deferred 对齐时不用只靠 Pass 列表反推当前画面来源。

本次修改：
- `BurtCameraRenderer` 输出 RenderGraph Debug 时改为把 `BurtRenderPipelineAsset` 一起传入 dump。
- `BurtRenderGraph` 新增带资产参数的 `DumpDebugInfo` 入口，旧入口保留，避免影响已有调用。
- `BurtRenderGraphDebugUtility` 新增 `Pipeline State` 段落，显示 `RendererMode`、`DeferredForwardOpaqueFallback`、`DepthPrepass`、`ShadingDebugMode`、`GBufferDebugAssetMode`、`GBufferDebugResolvedMode`、`GBufferDebugSource`、Depth Debug、Shadow Debug 和 Unsupported Shader Debug 状态。
- `BurtGBufferDebugViewUtility` 新增 `ResolveGBufferDebugViewSource`，用来区分 GBuffer Debug 是资产面板触发，还是 Shading Debug Overlay 触发。

为什么这样做：
- 现在 Deferred 路径已经有 GBuffer Debug 和 Deferred Lighting Debug 两类调试入口，单看 Pass 列表不一定能立刻知道当前 Debug 来自资产面板还是 Overlay。
- `GBufferDebugResolvedMode` 可以直接告诉我们最终生效的 GBuffer 模式，避免再次出现 UI 已选中但 RenderGraph 没插入 Debug Pass 的问题。
- `RendererMode` 和 `DeferredForwardOpaqueFallback` 放在同一段，可以帮助判断当前 Deferred 画面是否仍混有 Forward Opaque 兜底底图。
- Shading Debug 是全局状态，不完全属于某个资产；在 RenderGraph dump 里显式输出可以降低 SceneView / GameView 切换时的误判成本。

推荐验证：
1. 打开 `Enable Render Graph Debug`，正常模式应出现 `Pipeline State:` 段落。
2. 切换 `Renderer Mode = Forward / Deferred`，日志里的 `RendererMode` 应同步变化。
3. 用 Overlay 选择 `GBuffer Base Color`，日志应显示 `GBufferDebugSource=ShadingDebugOverlay` 且 `GBufferDebugResolvedMode=BaseColor`。
4. 在资产面板直接选择 GBuffer Debug 模式，日志应显示 `GBufferDebugSource=Asset`，资产面板优先级高于 Overlay。

下一步计划：
- 继续做 Forward / Deferred 对齐辅助：优先给 Deferred 的 Forward Opaque Fallback 增加更明确的迁移/诊断开关说明，确认何时可以默认关闭。
- 在 Gibbs 继续 PBR 的同时，主 Agent 负责保证 RenderGraph 侧的资源生命周期、日志和 Pass 插入点可解释。

### 12.16 Deferred ForwardOnly 不透明兜底修正

2026-05-10 根据新版 RenderGraph Debug，当前 Deferred 日志显示 `DeferredForwardOpaqueFallback=True`，并且旧 Pass 顺序是 `Burt Draw Opaque` 在 `Burt Deferred Lighting` 之前。这个顺序有架构问题：Deferred Lighting 是全屏写回 `CameraColor`，所以它会覆盖前面 Forward Opaque 写入的颜色；旧 fallback 既不能真正保住不写 GBuffer 的物体，也会造成不必要的重复绘制。

本次修正：
- 新增 `LightMode=BurtForwardOnly` 约定，表示“这个 shader 不写 GBuffer，但允许在 Deferred Lighting 后作为前向不透明兜底绘制”。
- `BurtDrawingSettingsUtility` 新增 `CreateForwardOnlyDrawingSettings`，只匹配 `BurtForwardOnly`，不再匹配普通 `BurtForward`。
- 新增 `BurtDrawDeferredForwardOnlyOpaquePass`，Pass 名称为 `Burt Draw Deferred Forward Only Opaque`，位置在 `Burt Deferred Lighting` 之后、`Burt Draw Skybox` 之前。
- `BurtDeferredGraphAssembler` 不再在 Deferred Lighting 前插入普通 `Burt Draw Opaque`，而是按资产开关插入新的 ForwardOnly 兜底 Pass。
- 管线资产 Inspector 文案改为 `启用 ForwardOnly 不透明兜底`，RenderGraph Debug 字段改为 `DeferredForwardOnlyOpaqueFallback`，避免误解成“所有 Forward Opaque 都会兜底”。
- `BurtUnlitColor.shader` 新增 `Burt Unlit Forward Only` Pass，让当前不写 GBuffer 的 Unlit shader 在 Deferred 模式下有明确兜底路径。

为什么这样做：
- Deferred 模式下，支持 `BurtGBuffer` 的不透明材质应该只通过 GBuffer + Deferred Lighting 进入最终画面，不能再被普通 `BurtForward` 画一遍。
- 不支持 GBuffer、但属于 BurtRP 自己维护的特殊不透明材质，可以显式增加 `BurtForwardOnly` Pass，在 Deferred Lighting 后补画。
- 这个设计接近 URP Deferred 里的 ForwardOnly 思路：Deferred 负责常规不透明，ForwardOnly 负责无法延迟化的特殊材质。
- 旧的 `Burt Draw Opaque` 在 Deferred Lighting 前作为“底图”没有实际保护价值，因为后续全屏 Deferred Lighting 会覆盖它。

推荐验证：
1. 重新打开 RenderGraph Debug，Deferred 模式下 Pass 顺序应变成 `Burt Deferred Lighting` 之后接 `Burt Draw Deferred Forward Only Opaque`，不再出现 Deferred Lighting 前的普通 `Burt Draw Opaque`。
2. 使用普通 `BurtLit` 材质时，关闭/开启 `启用 ForwardOnly 不透明兜底` 不应该改变画面，因为 `BurtLit` 已经写 GBuffer。
3. 使用 `BurtUnlitColor` 不透明材质时，开启 `启用 ForwardOnly 不透明兜底` 应能在 Deferred 下看到颜色；关闭后它会暴露为不参与 GBuffer 的缺失情况。
4. 如果未来某个特殊不透明 shader 不能写 GBuffer，应给它添加 `LightMode=BurtForwardOnly`，而不是让 Deferred 重新绘制所有 `BurtForward`。

下一步计划：
- 主 Agent 继续检查 Deferred 中 ForwardOnly、Unsupported Shader、Transparent 三类后置绘制的顺序和资源声明。
- 如果 `BurtUnlitColor` 验证正常，下一步可以开始整理 Deferred 的“材质能力分类”：GBuffer、ForwardOnly、Transparent、Unsupported。

### 12.17 Deferred 移除普通 Forward Opaque 后间接光兜底

2026-05-10 用户反馈 ForwardOnly 兜底修正后，Deferred 间接光效果消失。原因判断：旧的 `Burt Draw Opaque` 虽然会被 `Burt Deferred Lighting` 覆盖颜色，但它仍会通过 `DrawRenderers` 让 Unity 绑定 `unity_SH*` 和 `unity_SpecCube0` 等 per-object 间接光变量；移除这个普通 Forward Opaque 后，Deferred Lighting 作为全屏 Pass 不再可靠拥有这些 Renderer 级数据，所以 `ShadeSH9` 和 Reflection Probe 采样可能变黑。

本次修正：
- `Burt Setup Lighting` 主动读取 `RenderSettings.ambientProbe`，并按 UnityCG `ShadeSH9` 的打包方式上传到 `unity_SHAr / unity_SHAg / unity_SHAb / unity_SHBr / unity_SHBg / unity_SHBb / unity_SHC`。
- `BurtDrawingSettingsUtility.CreateGBufferDrawingSettings` 临时请求和 Forward 一样的 SH / Reflection Probe per-object 数据，作为当前阶段的兼容桥接。
- `BurtLighting.hlsl` 新增 `BurtSelectIndirectFallbackIfBlack`：当 Unity 内置 SH 或 `unity_SpecCube0` 在 Deferred 全屏 Pass 中明显为黑时，回退使用 `_BurtAmbientLightColor`，避免间接漫反射和间接高光完全消失。

为什么这样做：
- 正确的 Deferred 间接光不能长期依赖“上一个 DrawRenderers 留下来的 per-object 全局状态”，那是顺序相关且不稳定的。
- 当前阶段还没有真正的 Deferred probe/light-probe 数据结构，所以先用全局 ambient probe + 环境色兜底，恢复基础间接光可见性。
- Reflection Probe 的完整 Deferred 方案后续应单独设计：要么先做全局 sky reflection，要么做 object/probe volume 索引写入 GBuffer 或额外 buffer；不能永久靠 fullscreen pass 读取 `unity_SpecCube0` 的偶然状态。

推荐验证：
1. 关闭所有 Debug View，在 Deferred 模式下确认阴影面和背光面不再全黑。
2. 打开 Shading Debug 的 `IndirectDiffuse`，确认 Deferred 下能看到非黑的间接漫反射。
3. 打开 Shading Debug 的 `IndirectSpecular`，确认高 smoothness 材质至少有环境色兜底的镜面响应。
4. 如果需要严格验证 Reflection Probe，还要后续新增真正的 Deferred Reflection Probe 数据源；当前修复只是恢复基础可见效果和诊断能力。

下一步计划：
- 主 Agent 继续把 Deferred 的间接光拆成两个阶段：当前全局 Ambient/IBL 兜底，以及后续真正的 probe 数据接入。
- Gibbs 继续负责 PBR/IBL 公式细节；主 Agent 负责保证 Deferred Pass 有稳定的数据来源，而不是依赖 Forward 副作用。

### 12.18 BurtRP 全局间接光数据入口

2026-05-10 在临时恢复间接光可见性后，主 Agent 继续把这部分整理成更明确的数据入口，目标是让 Deferred Lighting 不再依赖普通 Forward Opaque 或 Unity per-object 变量的偶然残留。

本次修改：
- 新增 `BurtIndirectLightingUtility`，作为 BurtRP 自己的全局间接光上传入口。
- `BurtIndirectLightingUtility.UploadGlobalIndirectLighting` 现在负责上传：
  - `RenderSettings.ambientProbe` 打包后的 `_BurtAmbientSHAr / _BurtAmbientSHAg / _BurtAmbientSHAb / _BurtAmbientSHBr / _BurtAmbientSHBg / _BurtAmbientSHBb / _BurtAmbientSHC`。
  - `_BurtAmbientSHEnabled`，让 shader 知道 BurtRP 自有 SH 是否有效。
  - `RenderSettings.customReflection` 或 `ReflectionProbe.defaultTexture` 到 `_BurtSkyReflectionTexture`。
  - `_BurtSkyReflectionHDR / _BurtSkyReflectionIntensity / _BurtSkyReflectionEnabled`。
- `Burt Setup Lighting` 改为调用 `BurtIndirectLightingUtility.UploadGlobalIndirectLighting`，不再把 SH 上传逻辑散落在 Pass 内部。
- `BurtLighting.hlsl` 新增 BurtRP 自有 ambient SH 和 sky reflection 变量。
- `BurtSampleIndirectDiffuseIrradiance` 优先使用 `_BurtAmbientSH*`，只有未启用时才回退 Unity 内置 `ShadeSH9`。
- `SampleIndirectSpecularRadiance` 优先采样 `_BurtSkyReflectionTexture`，没有天空反射时才回退旧的 `unity_SpecCube0` 或环境色兜底。
- RenderGraph Debug 的 `Pipeline State` 段落追加间接光状态行：`IndirectDiffuseSource`、`IndirectSpecularSource`、`ReflectionIntensity`。

为什么这样做：
- Deferred Lighting 是 fullscreen pass，不属于任何 Renderer，所以它不应该读取“某个 Renderer 上一次 DrawRenderers 绑定的 unity_SH / unity_SpecCube0”。
- 全局 ambient probe 和 sky reflection 是 Deferred 第一版可以稳定使用的数据源，虽然它还不是完整的 per-object probe 方案，但比依赖 Forward 副作用可靠得多。
- 后续如果要做真正 Reflection Probe / Probe Volume，只需要替换 `BurtIndirectLightingUtility` 和 shader 采样入口，不需要再改 Deferred Pass 顺序。

当前限制：
- 当前 `_BurtSkyReflectionHDR` 第一版使用默认直读 RGB 解码参数，复杂的 Unity RGBM probe decode 还没有完全接入。
- 当前是全局 sky reflection，不是 per-object reflection probe selection。
- 当前 GBuffer 仍没有保存 probe index、probe weight 或 probe volume 坐标。

推荐验证：
1. 打开 RenderGraph Debug，确认 `Pipeline State` 里出现 `IndirectDiffuseSource=RenderSettings.ambientProbe`。
2. 如果场景有天空盒反射，确认 `IndirectSpecularSource=ReflectionProbe.defaultTexture` 或 `RenderSettings.customReflection`。
3. 打开 `IndirectDiffuse` Debug，确认 Deferred 下背光面有来自 ambient probe 的非黑漫反射。
4. 打开 `IndirectSpecular` Debug，确认光滑材质至少能看到全局 sky reflection 或环境色兜底。

下一步计划：
- 主 Agent 继续整理 Deferred 材质能力分类：GBuffer、ForwardOnly、Transparent、Unsupported。
- Gibbs 继续检查 IBL 公式侧问题，尤其是 sky reflection HDR decode、roughness->mip 和 energy compensation。
- 后续正式做 Reflection Probe 时，再设计 per-object/probe-volume 数据，而不是回退到 Forward DrawRenderers 副作用。

### 12.19 Deferred 全局 IBL 数据源继续收口

2026-05-10 在恢复 Deferred 间接光后，继续把数据来源从“DrawRenderers 的 per-object 副作用”收口到 BurtRP 显式上传的全局 IBL。

本次修改：
- `BurtIndirectLightingUtility` 新增 `_BurtSkyReflectionMaxMip` 上传，用当前 sky/custom reflection cubemap 的 `mipmapCount - 1` 作为最大 LOD。
- `BurtLighting.hlsl` 的全局 sky reflection 采样不再写死 `maxMipLevel = 6`，而是读取 `_BurtSkyReflectionMaxMip`。
- `unity_SpecCube0` legacy 兜底路径暂时保留固定 6，因为这条路径只是兼容旧状态，后续会被正式 Reflection Probe 数据源替换。
- `BurtDrawingSettingsUtility.CreateGBufferDrawingSettings` 改为 `PerObjectData.None`，明确 GBuffer Pass 只写材质属性，不再通过 GBuffer DrawRenderers 刷新 SH / Reflection Probe 副作用。
- RenderGraph Debug 的间接光状态行追加 `SkyReflectionMaxMip`，方便判断 roughness 到 mip 的映射是否使用了当前 cubemap 的真实 mip 链。

为什么这样做：
- Deferred Lighting 是 fullscreen pass，它应该读取 BurtRP 明确上传的全局 IBL，而不是依赖某个 Renderer 在 GBuffer 或 Forward 阶段留下的 `unity_SH* / unity_SpecCube0`。
- sky reflection 的 mip 上限和 cubemap 尺寸有关，固定 6 只适合部分 Unity spec cube；全局 sky reflection 接入后需要让 C# 把真实 mip 上限告诉 shader。
- GBuffer 阶段清掉 `PerObjectData` 能更早暴露真正的数据缺口，避免“看起来有间接光，其实来自上一批 renderer 状态残留”。

当前限制：
- `_BurtSkyReflectionHDR` 仍然使用 `(1, 1, 0, 0)` 直读 RGB 参数；如果 Unity 生成的 default reflection 或 custom reflection 使用 RGBM/HDR encode，亮度可能仍需后续修正。
- 当前全局 sky reflection 不是 per-object reflection probe selection，多个 reflection probe 的空间混合还没有实现。
- 当前 GBuffer 没有 probe index、probe weight、lightmap、shadowmask 等 GI 相关字段；真正的 baked GI / probe volume 仍需单独设计。

推荐验证：
1. Deferred 模式下打开 RenderGraph Debug，确认 `IndirectDiffuseSource=RenderSettings.ambientProbe`、`IndirectSpecularSource=...` 和 `SkyReflectionMaxMip=...` 都出现。
2. 打开 `IndirectDiffuse` Debug，确认背光面有方向性环境漫反射，不是纯环境色平铺。
3. 打开 `IndirectSpecular` Debug，确认光滑材质可以看到 sky/custom reflection，并且 roughness 增大时反射逐渐变糊。
4. 关闭所有 Debug View，对比 Forward 和 Deferred 的同一材质；当前允许亮度略有差异，但不应该再出现 Deferred 间接光全黑。

下一步计划：
- 主 Agent 继续整理 Deferred 材质能力分类和诊断输出，保证 `BurtGBuffer / BurtForwardOnly / Transparent / Unsupported` 的职责清晰。
- Gibbs 继续复核 PBR/IBL 公式和 HDR decode 风险。
- Russell 继续关注后处理链路是否会因为恢复 HDR 间接光而暴露 RT 格式或 Tonemapping 问题。

### 12.20 PBR / PostProcess 子 Agent 复核后的补充

2026-05-10 Gibbs 和 Russell 完成只读复核，主 Agent 根据复核结论继续做了两个小收口。

Gibbs 复核结论：
- `BurtIndirectLightingUtility` 的 SH 打包方式和 Unity Core `SHCoefficients(SphericalHarmonicsL2)` 一致。
- `BurtEvaluateAmbientSH9` 的 L0/L1/L2 评估方式和 Unity Core `SampleSH9` 多项式一致。
- 当前最大风险是 sky reflection HDR decode：`_BurtSkyReflectionHDR=(1,1,0,0)` 只适合直读 RGB 的 cubemap，如果 Unity default reflection 或 custom reflection 是 RGBM/dLDR 编码，亮度可能偏暗或动态范围丢失。
- 黑色 fallback 不适合作为最终方案，因为用户可能故意使用黑色 custom reflection，或者某个 mip/方向本来就是黑。

本次补充修改：
- `BurtEvaluateAmbientSH9` 在 `UNITY_COLORSPACE_GAMMA` 下追加 Gamma 输出转换，用来更接近 Unity Core `SampleSH9` 在 Gamma 项目下的语义。
- 显式启用 `_BurtSkyReflectionTexture` 后，`SampleIndirectSpecularRadiance` 不再对全局 sky reflection 做黑色检测 fallback，而是尊重 cubemap 自身结果；只有 legacy `unity_SpecCube0` 路径仍保留环境色兜底。

Russell 复核结论：
- 后处理链路本身不应该改变方向或颜色，但它强依赖 `CameraColor` 是否是线性 HDR scene color。
- 如果相机 `allowHDR` 关闭，或者 `targetTexture` 是 LDR 格式，Deferred 恢复的 HDR 间接光可能在 Tonemapping 前就被截断。
- 后续 RenderGraph Debug 最好追加 `CameraColor / PostProcessColor / FinalCameraTarget` 的格式、sRGB、MSAA、targetTexture 和 Tonemapping 参数，方便排查 No-op 或 Tonemapping 变色问题。

下一步计划：
- 短期先验证 Deferred 间接光恢复：`IndirectDiffuse`、`IndirectSpecular`、关闭 Debug View 的最终画面。
- 如果 sky reflection 偏暗，下一步优先处理 HDR decode 参数，而不是改 BRDF。
- 如果 No-op 或 Tonemapping 后画面变色，下一步优先做 RenderGraph Debug 的 RT 格式输出。

### 12.21 修正 Unity 版本 API：默认反射纹理入口

2026-05-10 用户反馈 `RenderSettings.defaultReflection` 编译错误：当前 Unity 版本没有公开这个 API。

本次修正：
- `BurtIndirectLightingUtility` 不再访问 `RenderSettings.defaultReflection`。
- 默认天空反射改为使用 Unity 公开入口 `ReflectionProbe.defaultTexture`。
- 默认天空反射的 HDR 解码参数改为使用 `ReflectionProbe.defaultTextureHDRDecodeValues`。
- 自定义反射仍使用 `RenderSettings.customReflection`，并暂时用 `_BurtSkyReflectionHDR=(1,1,0,0)` 直读 RGB。
- RenderGraph Debug 的默认反射来源从 `RenderSettings.defaultReflection` 改为 `ReflectionProbe.defaultTexture`。

为什么这样做：
- URP 14 也是通过 `ReflectionProbe.defaultTexture` 和 `ReflectionProbe.defaultTextureHDRDecodeValues` 绑定全局 glossy environment。
- `RenderSettings.defaultReflection` 在这个工程当前 Unity 版本不是公开 C# API，继续使用会直接编译失败。
- 使用 Unity 公开的 ReflectionProbe 默认纹理入口，可以保持默认天空反射可用，并减少 HDR decode 偏暗风险。

推荐验证：
1. Unity Console 不应再出现 `RenderSettings.defaultReflection` 的 CS0117。
2. RenderGraph Debug 中默认反射来源应显示 `IndirectSpecularSource=ReflectionProbe.defaultTexture`。
3. 如果配置了 Lighting 面板的 Custom Reflection，则来源仍应显示 `RenderSettings.customReflection`。


### 12.22 修正 Custom Reflection 非 Cubemap 异常

2026-05-10 用户反馈 `RenderSettings.customReflection is currently not referencing a cubemap`，异常来自 `RenderSettings.customReflection` getter。

本次修正：
- `BurtIndirectLightingUtility` 新增 `TryResolveCustomReflection`，统一安全读取 Lighting 面板里的 Custom Reflection。
- 只有 `RenderSettings.defaultReflectionMode == DefaultReflectionMode.Custom` 时才读取 `RenderSettings.customReflection`。
- 如果 Unity 在 getter 中抛出 `ArgumentException`，说明当前 Custom Reflection 引用不是 Cubemap；BurtRP 会吞掉这个异常并回退到 `ReflectionProbe.defaultTexture` 或环境色兜底。
- RenderGraph Debug 如果检测到 Custom 模式但没有合法 Cubemap，会显示 `IndirectSpecularSource=InvalidCustomReflectionFallback`。

为什么这样做：
- Unity 的 `RenderSettings.customReflection` getter 在引用非 Cubemap 时会直接抛异常，而不是返回 null。
- Skybox 默认反射模式下不应该主动读取 Custom Reflection 字段，否则场景里残留的无效引用也会中断渲染。
- 渲染管线不应该因为 Lighting 面板一个无效引用整帧崩掉，正确做法是安全回退并在 Debug 状态里暴露配置问题。

推荐验证：
1. Unity Console 不应再出现 `RenderSettings.customReflection is currently not referencing a cubemap`。
2. 如果 Lighting 面板 Reflection Source 是 Skybox，Debug 应显示 `ReflectionProbe.defaultTexture` 或 `AmbientColorFallback`。
3. 如果 Reflection Source 是 Custom 但引用不是 Cubemap，Debug 应显示 `InvalidCustomReflectionFallback`。
4. 如果 Reflection Source 是 Custom 且引用合法 Cubemap，Debug 应显示 `RenderSettings.customReflection`。

### 12.23 修正 Burt Sky Reflection 采样器声明

2026-05-10 用户反馈 `BurtLit` 编译错误：`undeclared identifier 'sampler_BurtSkyReflectionTexture'`。

本次修正：
- `BurtLighting.hlsl` 中 `_BurtSkyReflectionTexture` 从手写 `samplerCUBE` 改成 `UNITY_DECLARE_TEXCUBE(_BurtSkyReflectionTexture)`。

为什么这样做：
- 当前 shader 用 `UNITY_SAMPLE_TEXCUBE_LOD(_BurtSkyReflectionTexture, ...)` 采样 cubemap。
- Unity 的 `UNITY_SAMPLE_TEXCUBE_LOD` 在 D3D/桌面平台会展开成读取 `sampler_BurtSkyReflectionTexture`。
- 手写 `samplerCUBE _BurtSkyReflectionTexture` 只声明了纹理，没有声明 Unity 宏期望的 sampler，所以编译器报 `sampler_BurtSkyReflectionTexture` 未声明。
- `UNITY_DECLARE_TEXCUBE` 会同时声明 cubemap 纹理和配套 sampler，和 Unity 内置 `unity_SpecCube0` 的声明方式一致。

推荐验证：
1. `Burt Lit Forward` 不应再报 `sampler_BurtSkyReflectionTexture` 未声明。
2. 如果还有 cubemap 相关 shader error，优先贴完整 shader error，因为下一步可能是某个平台宏或 include 顺序问题。

### 12.24 RenderGraph Debug 增加 RT / Camera / PostProcess / Deferred 状态

2026-05-10 按当前稳定化计划，主 Agent 给 RenderGraph Debug 补充了一组更面向排错的状态输出。

本次修改：
- `BurtRenderGraphDebugUtility` 在 `Pipeline State` 后追加 `Camera State`。
  - 输出相机名、`CameraType`、像素尺寸、`AllowHDR`、`TargetTexture` 和最终 `RenderTargetIdentifier`。
  - 如果相机绑定了 `targetTexture`，额外输出该 RenderTexture 的尺寸、格式、GraphicsFormat、深度位数、sRGB、MSAA、MipMap 和 Dimension。
- 追加 `Render Target State`。
  - 输出 `CameraColor`、`CameraDepth`、`PostProcessColor`、`GBuffer0/1/2` 的注册状态和 `RenderTextureDescriptor`。
  - 对当前路径不使用的资源输出 `<skipped>`，避免把“按条件没创建”和“资源丢失”混在一起。
  - 输出 `FinalCameraTarget` 是否注册以及最终目标 identifier。
- 追加 `PostProcess State`。
  - 输出后处理资产总开关、No-op Copy、当前 request 是否真正运行后处理链、Tonemapping 模式、PostExposure 线性倍率、Color Adjustments 和 Volume LayerMask。
- 追加 `Deferred State`。
  - 输出 Deferred 是否启用、ForwardOnly fallback、GBuffer0/1/2 是否注册、最终 GBuffer Debug 模式和来源。

为什么这样做：
- Deferred、IBL 和 Tonemapping 后续都会依赖 `CameraColor` 是否为 HDR、targetTexture 是否为 LDR、PostProcessColor 是否继承正确格式。
- SceneView、GameView、Preview 和自定义 targetTexture 的差异，很多时候不是 shader 公式问题，而是 RT 描述或最终目标不同。
- 把这些信息直接打进 RenderGraph Debug 后，后续看到画面偏暗、发灰、反向、丢 HDR 或后处理变色时，可以先从日志判断数据路径，而不是靠猜。

推荐验证：
1. 打开 RenderGraph Debug，确认日志中出现 `Camera State`、`Render Target State`、`PostProcess State`、`Deferred State` 四段。
2. GameView 普通相机应能看到 `AllowHDR` 和 `TargetTexture=<none>` 或具体 RenderTexture 信息。
3. 如果启用后处理，应能看到 `PostProcessColor` 不是 `<skipped>`，并且格式和 `CameraColor` 对齐。
4. Deferred 模式下应能看到 `GBuffer0/1/2 Registered=True` 和各自格式；Forward 模式下应显示 `<skipped>`。
5. 如果后续 Tonemapping 异常，优先检查 `CameraColor` / `PostProcessColor` 是否为 HDR，以及 `targetTexture` 是否把格式限制成 LDR。


### 12.25 Deferred 材质路径分类诊断

2026-05-10 根据 SceneView / GameView 的 RenderGraph Debug 日志确认：
- `CameraColor` 和 `PostProcessColor` 都是 `ARGBHalf / R16G16B16A16_SFloat`，HDR 中间链路正常。
- SceneView 的 `targetTexture` 也是 `ARGBHalf`，GameView 没有自定义 `targetTexture`，因此当前不是 LDR RT 截断问题。
- Deferred 下 `GBuffer0/1/2 Registered=True`，PostProcess 也正确插入，资源链路已经稳定。
- `IndirectSpecularSource=ReflectionProbe.defaultTexture`，说明全局天空反射来源已经能被 Deferred Lighting 看到。

本次新增：
- `BurtDeferredMaterialDebugUtility`，只在 RenderGraph Debug 输出时做近似场景扫描。
- RenderGraph Debug 新增 `Deferred Material State` 段。
- 统计当前相机层级和视锥内的活动 Renderer / 材质槽，并按 Deferred 策略分类：
  - `Opaque GBuffer`：shader 提供 `LightMode=BurtGBuffer`，会写入 GBuffer 并进入 Deferred Lighting。
  - `Opaque ForwardOnly`：shader 提供 `LightMode=BurtForwardOnly`，会在 Deferred Lighting 之后前向兜底绘制。
  - `Opaque MissingDeferredPath`：不透明材质既没有 `BurtGBuffer` 也没有 `BurtForwardOnly`，在 Deferred 下可能不可见或只能被错误材质覆盖。
  - `Transparent Forward`：透明材质仍使用 `LightMode=BurtForward` 走透明 Forward Pass。
  - `Transparent MissingForward`：透明材质没有 `BurtForward`，不会被当前透明 Pass 绘制。

为什么这样做：
- 现在 RT、GBuffer、后处理、IBL 全局来源都已经能从日志确认，下一类高频问题会变成“某个材质到底被哪条 Deferred 路径画出来”。
- Deferred 第一版不再绘制普通不透明 `BurtForward`，这是为了避免已经进 GBuffer 的材质被重复画；因此没有 `BurtGBuffer` 的不透明 shader 必须显式走 `BurtForwardOnly`。
- 这个诊断不是 Unity 内部 `CullingResults` 的精确列表，而是基于活动 Renderer、相机 LayerMask 和视锥 AABB 的近似扫描；它用于快速发现缺 pass 的材质，不作为最终渲染统计。

推荐验证：
1. 打开 RenderGraph Debug 后确认日志出现 `Deferred Material State`。
2. 如果 `MissingDeferredPath=0`，说明当前可见不透明材质基本都有 Deferred 路径。
3. 如果 `MissingDeferredPath>0`，先看 `FirstMissingDeferredPath` 给出的材质和 shader，决定给它补 `BurtGBuffer` 还是 `BurtForwardOnly`。
4. 如果透明物体不显示，看 `TransparentSlots` 和 `MissingForward`，透明 shader 目前仍需要 `BurtForward`。


### 12.26 RenderGraph Debug 改成剪切板按钮工作流

2026-05-10 根据使用反馈，把每帧长 Console 日志改成更适合复制的 Inspector 按钮工作流。

本次新增：
- `BurtRenderGraphDebugClipboardUtility`。
  - 缓存最近一次 RenderGraph Debug 完整 dump。
  - 记录对应的 `Frame`、`Request`、`Camera` 和文本长度。
  - 支持一次性“下一帧复制到剪切板”。
- `BurtCameraRenderer` 的 RenderGraph Debug 行为调整：
  - `Enable RenderGraph Debug` 现在优先表示“捕获并缓存最近一次 dump”。
  - 默认不再把完整 dump 每帧打印到 Console。
  - 只有额外打开 `RenderGraph Console Log` 时，才继续输出完整长日志。
  - 如果用户点击“下一帧复制到剪切板”，即使常驻捕获没开，也会临时生成一帧 dump 并复制。
- `BurtRenderPipelineAssetEditor` 的 Debug 面板增加按钮：
  - `复制最近一次到剪切板`：直接复制当前缓存的 dump。
  - `下一帧复制到剪切板`：请求下一次渲染图生成后自动复制。
  - `清空缓存的 RenderGraph Debug`：清掉缓存，避免误复制旧帧。

为什么这样做：
- RenderGraph Debug 已经很长，继续每帧刷 Console 会影响阅读，也容易复制不完整。
- 剪切板按钮让你可以直接把完整 dump 发给主 Agent，不需要在 Console 里手动框选。
- 保留 `RenderGraph Console Log` 是为了必要时还能恢复旧的 Console 观察方式。

推荐使用方式：
1. 选中 `BurtRenderPipelineAsset`。
2. 如果只是想抓一帧，直接点 `下一帧复制到剪切板`。
3. 如果想持续观察最新状态，打开 `RenderGraph Debug Capture`，等画面刷新后点 `复制最近一次到剪切板`。
4. 平时不要开 `RenderGraph Console Log`，除非确实需要 Console 里持续看到完整 dump。


### 12.27 Deferred Lighting 分量 Debug

2026-05-10 按当前 Deferred 稳定化节奏，给 Shading Debug Overlay 补齐 Deferred Lighting 的分量观察入口，让 Forward 和 Deferred 都能用同一套按钮拆开看最终光照。

本次新增：
- `BurtShadingDebugMode` 增加 `ShadowAttenuation`、`AmbientOcclusion`、`Emission`、`FinalLighting` 四个 Lighting 类模式。
- `BurtShadingDebug.hlsl` 的 `BurtShadingDebugData` 增加阴影衰减、AO、自发光和最终材质光照字段，并在公共 `BurtTryEvaluateMaterialShadingDebug` 中统一输出。
- `BurtLit.shader` 的 Forward pass 现在会在 Shading Debug 数据里写入：
  - 主光 `shadowAttenuation`。
  - `surfaceData.occlusion` 作为参与 lighting 的 AO。
  - `BurtEvaluateEmission(...)` 得到的自发光。
  - `lightingColor + emissionColor` 作为后处理前的最终材质输出。
- `BurtDeferredLighting.shader` 的 Deferred Lighting pass 现在会在 Shading Debug 数据里写入：
  - 从重建世界坐标采样得到的主光阴影衰减。
  - `gbufferData.occlusion` 作为参与 lighting 的 AO。
  - `gbufferData.emission` 作为 GBuffer2.rgb 保存的自发光。
  - `pbrComponents.lighting + gbufferData.emission` 作为 Deferred 写回 `CameraColor` 前的最终材质输出。
- `BurtShadingDebugOverlay` 的 Lighting Dropdown 增加这些模式，方便在 SceneView Overlay 中直接切换。

为什么这样做：
- 当前 RenderGraph / GBuffer / IBL / PostProcess 链路已经能从日志确认，后续最容易出问题的是“亮度差异到底来自直接光、间接光、阴影、AO、自发光还是后处理”。
- `FinalLighting` 显示的是材质写入 `CameraColor` 前的结果，适合和关闭后处理、打开 Tonemapping 或观察 Scene/Game 视图差异时对比。
- `Emission` 在 Forward 来自贴图实时采样，在 Deferred 来自 GBuffer2.rgb，可直接验证自发光是否正确进入 GBuffer 并在 Deferred Lighting 中叠加。
- `AmbientOcclusion` 和已有的 `GBufferOcclusion` 不同：前者强调参与 lighting 的 AO 输入，后者强调 GBuffer 解码正确性；两者可以一起排查“编码对但 lighting 没用上”的问题。

推荐验证：
1. 打开 `Burt Shading Debug` Overlay，在 `Lighting` 下逐个切换 `Direct Diffuse`、`Direct Specular`、`Indirect Diffuse`、`Indirect Specular`、`Shadow Attenuation`、`Ambient Occlusion (Lighting)`、`Emission`、`Final Lighting`。
2. Forward 和 Deferred 模式下，`Final Lighting` 应该接近关闭后处理时的不透明材质结果；如果开启 Tonemapping，最终屏幕可以更暗或更压缩，但 Debug 值应仍是后处理前 HDR。
3. `Shadow Attenuation` 应该白色为受光、黑色为阴影；如果阴影方向或投影异常，先对比这个模式和 Main Light Shadow 全屏 Debug。
4. `Emission` 在没有自发光材质时应接近黑色；打开自发光后，Forward 与 Deferred 应该都能看到相同位置的贡献。
5. `Ambient Occlusion (Lighting)` 应该和材质 Mask Map G / Occlusion Strength 的混合结果一致；若 GBuffer 模式正常但这里异常，优先查 Deferred Lighting 解码后的 AO 传递。


### 12.28 Deferred 直接高光法线精度

2026-05-10 观察到 Deferred 下 `Direct Specular` Debug 有格子状断层，判断优先来源是 GBuffer1 的法线量化精度。

本次调整：
- `BurtRenderTargetDescriptorUtility.CreateGBuffer1Descriptor` 从 `RenderTextureFormat.ARGB32` 改成 `RenderTextureFormat.ARGBHalf`。
- GBuffer1 仍保持原布局：`rg = oct normalWS`、`b = metallic`、`a = smoothness`。
- 不改 shader 编解码路径，只提升 RT 存储精度，降低 Deferred Lighting 中 normalWS 解码后的阶梯误差。

为什么这样做：
- 当前 GBuffer1 用两个 8-bit 通道保存 octahedron normal，普通漫反射不太明显，但 GGX 直接高光在高 smoothness 时会把微小法线量化放大成块状或带状高光。
- `Direct Specular` Debug 刚好只显示高光分量，所以比正常画面更容易暴露这个问题。
- 先改为 `ARGBHalf` 是最稳的验证方案；如果后续要优化带宽，可以再做一个 GBuffer 精度选项，或把法线拆到更合适的 packed/16-bit 格式。

推荐验证：
1. 重新让 Unity 编译 shader / C# 后打开 Deferred。
2. 在 `Burt Shading Debug > Lighting` 里切到 `Direct Specular`，观察格子状高光是否明显减轻或消失。
3. 打开 RenderGraph Debug，确认 `GBuffer1` 从 `ARGB32 / R8G8B8A8_UNorm` 变成 `ARGBHalf / R16G16B16A16_SFloat`。
4. 如果仍有格子，下一步再查 Normal Map 压缩、Specular AA 的屏幕空间导数，以及深度重建 viewDirection 是否引入离散误差。

### 12.29 Deferred Reflection Probe 兜底与 Bloom 启动条件

2026-05-10 根据 Deferred 下 Reflection Probe 看起来失效的反馈，调整 BurtRP 的全局 IBL 上传路径。

本次调整：
- `BurtSetupLightingPass` 上传间接光时传入当前 `BurtRenderRequest.Camera`，让 IBL 解析逻辑知道当前 request 属于 SceneView、GameView 还是 Preview。
- `BurtIndirectLightingUtility` 不再只依赖 `RenderSettings.customReflection` / `ReflectionProbe.defaultTexture`。
- Deferred 没有 Forward DrawRenderers 的 per-object `unity_SpecCube0` 绑定副作用，所以第一版先从当前场景中选择一盏有效 `ReflectionProbe`，作为 Deferred fullscreen lighting 的全局近似反射源。
- 场景 Probe 的选择优先级高于 Lighting 面板的 Custom Reflection，因为 Unity 的物体反射语义里 ReflectionProbe 是覆盖源，Custom/Skybox 更像没有 Probe 时的 fallback。
- 选择到场景 Probe 时会使用 `probe.textureHDRDecodeValues` 和 `probe.intensity`，避免 HDR reflection cubemap 偏暗或强度不跟随 Probe 设置。
- RenderGraph Debug 的 `Pipeline State` 会输出 `IndirectSpecularSource`、`ReflectionIntensity`、`SourceIntensity`、`SkyReflectionMaxMip`，用于判断实际采样的是场景 Probe、Custom Reflection、默认天空反射还是环境色兜底。

当前限制：
- 这还是全局近似，不是 per-object probe blending，也没有 box projection。
- 多个 ReflectionProbe 同屏时，Deferred 第一版只会选择一盏作为全屏 IBL 源；后续要做 probe volume / clustered probe assignment 才能接近 Unity Forward 的 per-object 效果。
- 如果 `IndirectSpecularSource` 仍然显示 `ReflectionProbe.defaultTexture`，通常说明场景 Probe 没有启用、没有烘焙/刷新出纹理，或当前 loaded scene 中没有可用 Probe。

Bloom 启动判断：
- 技术上现在可以开始做 Bloom：HDR `CameraColor` / `PostProcessColor` 已经是 `ARGBHalf`，后处理框架和 Global Volume Tonemapping 已经闭环。
- 建议先验证 Reflection Probe / Indirect Specular 恢复，再开始 Bloom；原因是 Bloom 依赖 HDR 高亮，IBL 失效会让 Bloom 阈值、强度和视觉判断被误导。
- Bloom 第一版计划放在 Tonemapping 之前：Prefilter HDR 亮部 -> Downsample 金字塔 -> Upsample 合成 -> 写回 CameraColor -> Tonemapping。
- Bloom Debug 需要补充 mip 数、每级 RT 格式、threshold/knee/intensity/scatter 和最终是否执行，避免后续再回到 Console 长日志排查。

推荐验证：
1. Unity 重新编译后，Deferred 模式下打开 `Burt Shading Debug > Lighting > Indirect Specular`。
2. 点击 RenderGraph Debug 的复制按钮，确认 `IndirectSpecularSource=SceneReflectionProbe(<name>)` 或至少不再错误停留在不可用源。
3. 调整 ReflectionProbe 的 `Intensity`，观察 `SourceIntensity` 和画面间接高光是否同步变化。
4. 如果仍无效果，先确认 Probe 已 Bake 或 Realtime 已刷新出 cubemap，再把复制出来的 RenderGraph Debug 发回来。


### 12.30 Reflection Probe 镜面端 Mip 过糊修正

2026-05-10 根据测试反馈：Deferred 下 `Indirect Specular` 已经能选到 `SceneReflectionProbe(Reflection Probe)`，但金属度 1、光滑度 1 时镜面反射仍然偏糊。

原因判断：
- RenderGraph Debug 显示 `SkyReflectionMaxMip=9`，说明当前 ReflectionProbe cubemap 真实 mip 链较长。
- 之前把真实 mip 上限直接传给 XRender 风格 `ComputeReflectionCaptureMipFromRoughness`，导致满光滑材质在最小 roughness 保护下仍被推到约 mip 1.6。
- XRender / Unity reflection capture 的 roughness->mip 曲线使用的是反射预过滤有效 LOD 范围，通常按 0..6 设计，不应该直接等同于 512/1024 cubemap 的完整 mip 链长度。

本次调整：
- `BurtLighting.hlsl` 增加 `BURT_REFLECTION_CAPTURE_SPECULAR_MIP_MAX = 6.0`。
- `ComputeReflectionCaptureMipFromRoughness` 内部把传入的真实 mip 上限限制到有效高光预过滤范围，再计算 LOD。
- `BurtIndirectLightingUtility` 的 RenderGraph Debug 增加 `SpecularMipMax`，同时保留 `SkyReflectionMaxMip`：
  - `SkyReflectionMaxMip` 表示纹理真实 mip 链上限。
  - `SpecularMipMax` 表示 shader roughness->mip 实际使用的有效上限。

推荐验证：
1. 金属度 1、光滑度 1 的材质在 `Indirect Specular` Debug 下应明显变锐。
2. RenderGraph Debug 应显示类似 `SkyReflectionMaxMip=9 SpecularMipMax=6`。
3. 光滑度降低时仍应逐渐变糊，说明 roughness->mip 曲线没有被完全绕过。


### 12.31 Editor Preview 与场景 ReflectionProbe 隔离

2026-05-10 在 ReflectionProbe 接回 Deferred 后，Cubemap / ReflectionProbe Inspector Preview 出现异常。原因是 Preview Camera 也会走 BurtRP request，而全局 IBL 解析会把当前场景的 ReflectionProbe 绑定到 `_BurtSkyReflectionTexture`，这会污染资产预览窗口。

本次调整：
- `BurtIndirectLightingUtility.ResolveSkyReflection` 增加 Preview Camera 分支。
- 当 `camera.cameraType == CameraType.Preview` 时，不再选择场景 ReflectionProbe，也不读取 Lighting 面板的 Custom Reflection。
- Preview 只使用 `ReflectionProbe.defaultTexture` 作为低风险兜底；没有默认纹理时退回环境色兜底。
- RenderGraph Debug 中 Preview request 的 `IndirectSpecularSource` 会显示为：
  - `PreviewFallback->ReflectionProbe.defaultTexture`
  - 或 `PreviewFallback->AmbientColorFallback`

这样做的目的：
- SceneView / GameView 仍然可以使用 `SceneReflectionProbe(<name>)` 恢复 Deferred IBL。
- Cubemap / ReflectionProbe 的 Inspector Preview 不再被场景反射探针覆盖，避免预览内容变成当前场景 IBL。
- 后续如果要做更完整的编辑器预览支持，应单独接 Unity PreviewScene 的光照/反射数据，而不是偷用主场景 Probe。

推荐验证：
1. 选中 Cubemap 资源，确认 Inspector Preview 恢复正常。
2. 选中 ReflectionProbe 组件，确认 Preview 恢复正常。
3. 打开 RenderGraph Debug 复制 Preview request，确认 `IndirectSpecularSource` 是 `PreviewFallback->...`。
4. 回到 SceneView / GameView，确认 Deferred 普通画面仍显示 `SceneReflectionProbe(<name>)`。


### 12.32 Preview Request 强制走 Forward 稳定路径

2026-05-10 上一版只隔离了 Preview 的 IBL 数据源，但 Cubemap / ReflectionProbe Preview 仍然异常，说明问题不只来自场景 ReflectionProbe 全局纹理，还来自 Preview request 继续进入 Deferred GBuffer / 后处理路径。

本次调整：
- `BurtRenderPipeline.ResolveGraphAssembler(request)` 改为 request-aware。
- 当 `request.Type == BurtRenderRequestType.Preview` 时，强制使用 `BurtForwardGraphAssembler`，即使资产当前 `RendererMode=Deferred`。
- `BurtRenderGraph.ShouldRegisterGBufferTargets` 对 Preview 返回 false，保证资源表不再注册 GBuffer。
- `BurtPostProcessUtility` 对 Preview 返回 false：
  - 不注册 `PostProcessColor`。
  - 不执行 No-op Copy / Tonemapping / Color Adjustments。
  - 不刷新场景 VolumeStack。
- RenderGraph Debug 增加 `EffectiveRendererMode`，Preview 在 Deferred 资产下应显示：
  - `RendererMode=Deferred EffectiveRendererMode=Forward`
- Preview 的 `Deferred Material State` 明确输出 `<skipped: preview request uses Forward path>`。

这样做的目的：
- SceneView / GameView 继续验证 Deferred。
- Inspector Preview、Cubemap Preview、ReflectionProbe Preview 先保持编辑器稳定，不跟随 Deferred 实验路径。
- 后续如果要支持 Deferred Preview，应单独做 PreviewScene 兼容，而不是复用主场景的 Deferred / Volume / IBL 状态。

推荐验证：
1. 选中 Cubemap 资源，确认 Preview 不再黑屏、反色、过曝或被场景反射覆盖。
2. 选中 ReflectionProbe 组件，确认 Preview 恢复。
3. 复制 Preview request 的 RenderGraph Debug，确认：
   - `EffectiveRendererMode=Forward`
   - `PostProcess State ... ShouldRunFramework=False`
   - `GBuffer0/1/2 Registered=False <skipped>`
4. 再复制 SceneView / GameView，确认它们仍是 `EffectiveRendererMode=Deferred`。



### 12.33 Preview 专用绘制 Pass

2026-05-10 用户反馈在强制 Preview 走 Forward 后，Cubemap / ReflectionProbe Inspector Preview 仍未恢复。进一步判断：普通 Forward 路径只匹配 `LightMode=BurtForward`，而 Unity 资产预览常使用 `SRPDefaultUnlit`、`ForwardBase`、`Always` 等内部/兼容 shader pass；关闭 Unsupported Shader Debug 后，这些预览物体不会再被错误材质覆盖，但也不会被普通 `BurtDrawOpaquePass` 绘制，所以仍可能黑屏或缺失。

本次调整：
- 新增 `BurtDrawEditorPreviewPass`，只服务 `request.Type == Preview`。
- `BurtForwardGraphAssembler` 在 Preview 下跳过普通 DepthPrepass / DrawOpaque / Skybox / DrawTransparent / Unsupported Shader Debug，改为插入 `Burt Draw Editor Preview`。
- Preview 专用绘制使用更宽松的 LightMode 列表：
  - `BurtForward`
  - `BurtForwardOnly`
  - `SRPDefaultUnlit`
  - `ForwardBase`
  - `Always`
  - `UniversalForward`
  - `UniversalForwardOnly`
  - `LightweightForward`
  - `Vertex`
  - `VertexLMRGBM`
  - `VertexLM`
  - `PrepassBase`
- Preview 仍保留 Setup Lighting、可选主光阴影、FinalBlit 和 RT 生命周期；但不会叠加场景 Depth Debug / MainLightShadow Debug，避免 Inspector 预览被调试视图覆盖。

这样做的目的：
- 普通场景继续保持严格的 BurtRP pass 约束，不让 Built-in/URP shader 静默进入主渲染路径。
- Editor Preview 使用独立宽松路径，优先保证 Cubemap / ReflectionProbe / 材质预览可用。
- 如果后续还出现 Preview 异常，RenderGraph Debug 应先确认 Preview pass 是否出现 `Burt Draw Editor Preview`，再判断是否需要处理 FinalBlit YFlip 或 Unity 内部预览相机 targetTexture 的特殊方向。

推荐验证：
1. 选中 Cubemap 资源，确认 Inspector Preview 有内容且不是场景 ReflectionProbe。
2. 选中 ReflectionProbe 组件，确认 Preview 恢复。
3. 复制 Preview request 的 RenderGraph Debug，确认 Pass 列表包含 `Burt Draw Editor Preview`，且不包含 `Burt Draw Opaque` / `Burt Draw Unsupported Shaders` / GBuffer / Deferred Lighting。
4. 回归 SceneView / GameView，确认 Deferred 画面和 ReflectionProbe IBL 仍正常。


### 12.34 ReflectionProbe Preview rollback

2026-05-10 rollback note: removed the failed ReflectionProbe Preview-specific fixes to keep the codebase clean.

Cleaned up:
- Removed the Custom-mode preview shader/pass attempt.
- Removed the Preview Skybox fallback for selected ReflectionProbe.
- Removed Preview-time Selection-based ReflectionProbe lookup; Preview now uses the normal preview fallback path.
- Removed extra Unity `unity_SpecCube0` global writes that were only added for the failed Preview attempt.
- Narrowed the generic Editor Preview LightMode fallback list back to the Cubemap/material preview-safe set.

Kept intentionally:
- Generic `Burt Draw Editor Preview`, because it also supports Cubemap/material preview.
- RenderGraph targeted clipboard workflow, because it is useful debug infrastructure.
- Scene/Game `SceneReflectionProbe(...)` IBL approximation, because it affects real rendering rather than the Inspector Preview hack.

Validation:
1. There should be no `Hidden/BurtRP/ReflectionProbePreview` shader and no `Burt Draw Reflection Probe Preview` pass.
2. If we also want to remove real Scene/Game ReflectionProbe IBL later, do that as a separate rollback.


