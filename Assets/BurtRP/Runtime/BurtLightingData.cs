using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Color、Vector3、LightType 和 RenderSettings。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 CullingResults 和 VisibleLight。

namespace Burt.RenderPipeline // 定义 BurtRP 运行时命名空间，让灯光数据和 request、pass 保持在同一模块里。
{
    public enum BurtAdditionalLightShadowStatus
    {
        None,
        UnsupportedPointLight,
        UnsupportedLightType,
        ShadowTypeNone,
        ShadowStrengthZero,
        SlotLimitExceeded,
        Marked,
        AtlasInvalid,
        PrepareInvalidVisibleLightIndex,
        PrepareSpotMatrixFailed,
        PreparePointMatrixFailed,
        Prepared,
        Active
    }

    public sealed class BurtLightingData // 保存一个 BurtRenderRequest 收集到的灯光信息。
    {
        public const int MaxAdditionalLights = 32; // 第一版追加光源走全局数组，先限制数量避免 shader 常量数组过大。
        public const int MaxShadowedAdditionalLights = 4; // ?????????????????????????? atlas ???
        public const int PointLightShadowFaceCount = 6;
        public const int MaxAdditionalLightShadowSlices = MaxShadowedAdditionalLights * PointLightShadowFaceCount;
        public const int DefaultAdditionalLightShadowTileResolution = 512; // V1 ???????? tile ?????????????

        public const int AdditionalLightBufferVectorCount = MaxAdditionalLights * 4; // One additional light uses four float4 rows in the GPU buffer.

        public const int AdditionalLightBufferStride = 16; // StructuredBuffer<float4> stride in bytes.

        public const string AdditionalLightShadingPathLabel = "ArrayFallback"; // Deferred/Forward shaders currently read global arrays; buffer is diagnostic/future tiled data.

        private const float AdditionalLightTypeDirectional = 0f;
        private const float AdditionalLightTypePoint = 1f;
        private const float AdditionalLightTypeSpot = 2f;
        private static readonly Vector3 DefaultMainLightDirection = new Vector3(0.3f, 0.8f, 0.4f).normalized; // 定义没有可见方向光时使用的稳定兜底方向。

        public bool HasMainLight { get; private set; } // 标记当前 request 是否找到了真实可见的方向光。

        public int MainLightIndex { get; private set; } // 保存主光在 CullingResults.visibleLights 里的索引。

        public int VisibleLightCount { get; private set; } // 保存当前相机剔除结果里可见灯光的数量，主要用于调试和后续多光源扩展。

        public Vector3 MainLightDirection { get; private set; } // 保存从着色点指向主光的世界空间方向。

        public Color MainLightColor { get; private set; } // 保存 Unity 计算过强度后的主光最终颜色。

        public Color AmbientLightColor { get; private set; } // 保存从 Unity Lighting 设置读取到的原始环境光颜色，Simple Lit 路径会直接使用它。

        public BurtShadowData ShadowData { get; private set; } // 保存主光对应的阴影数据，后续 shadow pass 会读取它。

        public int AdditionalLightCount { get; private set; } // 保存已打包进 shader 全局数组的追加光源数量，不包含主方向光。

        public Vector4[] AdditionalLightPositionAndRange { get; } = new Vector4[MaxAdditionalLights]; // xyz=位置，w=range；方向光保留 0。

        public Vector4[] AdditionalLightColorAndType { get; } = new Vector4[MaxAdditionalLights]; // rgb=finalColor，w=0 directional / 1 point / 2 spot。

        public Vector4[] AdditionalLightDirectionAndSpot { get; } = new Vector4[MaxAdditionalLights]; // xyz=方向光方向或聚光灯 forward，w=备用。

        public Vector4[] AdditionalLightSpotParams { get; } = new Vector4[MaxAdditionalLights]; // x=innerCos，y=outerCos，z=1/(inner-outer)，w=备用。
        public int ShadowedAdditionalLightCount { get; private set; } // ??? request ??????? atlas ?????
        public bool HasShadowedAdditionalLights => ShadowedAdditionalLightCount > 0; // ? RenderGraph ? shadow pass ???????????? shadow atlas?
        public bool AdditionalLightShadowCacheValid { get; private set; } // ?? spot shadow ??? atlas rect ??????? request ?????
        public int AdditionalLightShadowTileResolution { get; private set; } // ?????? atlas ?? tile ???????? texel size ???
        public int AdditionalLightShadowAtlasResolution { get; private set; } // ?????? atlas ??????? RT ??? shader ?????
        public int AdditionalLightShadowAtlasTileCountX { get; private set; }
        public int AdditionalLightShadowAtlasTileCountY { get; private set; }
        public int AdditionalLightShadowActiveSliceCount { get; private set; }
        public int[] AdditionalLightShadowVisibleLightIndices { get; } = new int[MaxAdditionalLights]; // ? additional light slot ?? visible light ???shadow pass ????? Unity ? DrawShadows ???
        public Vector4[] AdditionalLightShadowData { get; } = new Vector4[MaxAdditionalLights]; // x=enabled, y=strength, z=atlas tile, w=soft shadow.
        public Vector4[] AdditionalLightShadowLightParams { get; } = new Vector4[MaxAdditionalLights]; // x=first slice, y=slice count, z=light type, w=receiver normal bias in world units.
        public Vector4[] AdditionalLightShadowAtlasRects { get; } = new Vector4[MaxAdditionalLights]; // xy=min uv, zw=max uv?? additional light slot ???shader ???? lightIndex ???
        public Matrix4x4[] AdditionalLightShadowViewMatrices { get; } = new Matrix4x4[MaxAdditionalLights]; // ?? spot shadow view matrix??? setup/draw/deferred ???????
        public Matrix4x4[] AdditionalLightShadowProjectionMatrices { get; } = new Matrix4x4[MaxAdditionalLights]; // ?? spot shadow projection matrix?? shadow draw pass ?????
        public ShadowSplitData[] AdditionalLightShadowSplitDatas { get; } = new ShadowSplitData[MaxAdditionalLights]; // ?? spot shadow split data?DrawShadows ??????
        public LightShadows[] AdditionalLightShadowSourceModes { get; } = new LightShadows[MaxAdditionalLights];
        public float[] AdditionalLightShadowSourceStrengths { get; } = new float[MaxAdditionalLights];
        public int[] AdditionalLightShadowSliceLightIndices { get; } = new int[MaxAdditionalLightShadowSlices];
        public int[] AdditionalLightShadowSliceFaceIndices { get; } = new int[MaxAdditionalLightShadowSlices];
        public Vector4[] AdditionalLightShadowSliceAtlasRects { get; } = new Vector4[MaxAdditionalLightShadowSlices];
        public Matrix4x4[] AdditionalLightShadowSliceViewMatrices { get; } = new Matrix4x4[MaxAdditionalLightShadowSlices];
        public Matrix4x4[] AdditionalLightShadowSliceProjectionMatrices { get; } = new Matrix4x4[MaxAdditionalLightShadowSlices];
        public Matrix4x4[] AdditionalLightShadowSliceWorldToShadowMatrices { get; } = new Matrix4x4[MaxAdditionalLightShadowSlices];
        public ShadowSplitData[] AdditionalLightShadowSliceSplitDatas { get; } = new ShadowSplitData[MaxAdditionalLightShadowSlices];
        public BurtAdditionalLightShadowStatus[] AdditionalLightShadowStatuses { get; } = new BurtAdditionalLightShadowStatus[MaxAdditionalLights];
        public int AdditionalLightShadowCandidateCount { get; private set; }
        public int AdditionalLightShadowMarkedCount { get; private set; }
        public int AdditionalLightShadowSlotLimitExceededCount { get; private set; }
        public int AdditionalLightShadowPointUnsupportedCount { get; private set; }
        public int AdditionalLightShadowUnsupportedTypeCount { get; private set; }
        public int AdditionalLightShadowNoneCount { get; private set; }
        public int AdditionalLightShadowStrengthZeroCount { get; private set; }
        public bool AdditionalLightShadowPrepareAttempted { get; private set; }
        public bool AdditionalLightShadowPrepareSucceeded { get; private set; }
        public int AdditionalLightShadowPrepareFailedCount { get; private set; }
        public bool AdditionalLightShadowAtlasRegistered { get; private set; }
        public bool AdditionalLightShadowAtlasValid { get; private set; }

        public Vector4[] AdditionalLightBufferData { get; } = new Vector4[AdditionalLightBufferVectorCount]; // Packed rows consumed by the RenderGraph additional-light buffer path.

        public bool AdditionalLightBufferUploadAttempted { get; private set; } // True once SetupLighting tried to bind the graph-owned additional-light buffer.

        public bool AdditionalLightBufferUploaded { get; private set; } // True when packed additional-light rows were uploaded to a live GPU buffer.

        public bool TileLightDebugBuildAttempted { get; private set; } // True once the tiled debug skeleton tried to build per-tile light data.

        public bool TileLightDebugUploaded { get; private set; } // True when the tiled debug buffers were uploaded to live GPU buffers.

        public string TileLightDebugBuildMode { get; private set; } // Label describing how the debug tile data was generated.

        public int TileLightTileSize { get; private set; } // Pixel size of one debug tile.

        public int TileLightGridX { get; private set; } // Tile grid width in tiles.

        public int TileLightGridY { get; private set; } // Tile grid height in tiles.

        public int TileLightTileCount { get; private set; } // Total number of tiles generated for the current camera.

        public int TileLightMaxLightsPerTile { get; private set; } // Per-tile list capacity used by the debug skeleton.

        public int TileLightListCapacity { get; private set; } // Total number of uint light-index slots allocated for the list buffer.

        public int TileLightMinCount { get; private set; } // Minimum per-tile light count observed by the debug builder.

        public int TileLightMaxCount { get; private set; } // Maximum per-tile light count observed by the debug builder.

        public float TileLightAverageCount { get; private set; } // Average unclamped light count per tile.

        public int TileLightOverflowTileCount { get; private set; } // Tiles whose raw light count exceeds the per-tile list capacity.

        public int TileLightMaxOverflowExtraCount { get; private set; } // Largest raw-count excess above the per-tile list capacity.

        public string AdditionalLightShadingPath => AdditionalLightShadingPathLabel; // Reports the shader path currently used by lighting.

        public uint[] TileLightDebugCountSnapshot { get; private set; } // CPU copy used by the debug view texture fallback.

        public int TileLightDebugCountSnapshotLength { get; private set; } // Valid tile count in TileLightDebugCountSnapshot.

        private BurtLightingData() // 隐藏构造函数，强制调用方通过 Create 或 Default 获得已初始化的数据。
        {
        } // 构造函数不直接写初始化逻辑，避免和 ResetToDefaults、ResolveMainLight 的规则重复。

        public static BurtRenderBufferDescriptor CreateAdditionalLightBufferDescriptor()
        {
            return new BurtRenderBufferDescriptor(
                AdditionalLightBufferVectorCount,
                AdditionalLightBufferStride,
                GraphicsBuffer.Target.Structured,
                "_BurtAdditionalLightBuffer");
        }

        public void SetAdditionalLightBufferUploadState(bool attempted, bool uploaded)
        {
            AdditionalLightBufferUploadAttempted = attempted;
            AdditionalLightBufferUploaded = uploaded;
        }

        public void SetAdditionalLightShadowCacheState(bool isValid, int tileResolution, int atlasResolution)
        {
            SetAdditionalLightShadowCacheState(isValid, tileResolution, atlasResolution, 0, 0, AdditionalLightShadowActiveSliceCount);
        }

        public void SetAdditionalLightShadowCacheState(
            bool isValid,
            int tileResolution,
            int atlasResolution,
            int atlasTileCountX,
            int atlasTileCountY,
            int activeSliceCount)
        {
            AdditionalLightShadowCacheValid = isValid;
            AdditionalLightShadowTileResolution = tileResolution;
            AdditionalLightShadowAtlasResolution = atlasResolution;
            AdditionalLightShadowAtlasTileCountX = Mathf.Max(0, atlasTileCountX);
            AdditionalLightShadowAtlasTileCountY = Mathf.Max(0, atlasTileCountY);
            AdditionalLightShadowActiveSliceCount = Mathf.Clamp(activeSliceCount, 0, MaxAdditionalLightShadowSlices);

            if (isValid)
            {
                PromotePreparedAdditionalLightShadowSlots();
            }
        }

        public void SetAdditionalLightShadowPrepareState(bool attempted, bool succeeded, int failedCount)
        {
            AdditionalLightShadowPrepareAttempted = attempted;
            AdditionalLightShadowPrepareSucceeded = succeeded;
            AdditionalLightShadowPrepareFailedCount = Mathf.Max(0, failedCount);
        }

        public void SetAdditionalLightShadowAtlasState(bool registered, bool valid)
        {
            AdditionalLightShadowAtlasRegistered = registered;
            AdditionalLightShadowAtlasValid = valid;
        }

        public void SetAdditionalLightShadowSlot(
            int lightIndex,
            int atlasTileIndex,
            Vector4 atlasRect,
            Matrix4x4 viewMatrix,
            Matrix4x4 projectionMatrix,
            Matrix4x4 worldToShadowMatrix,
            ShadowSplitData splitData)
        {
            if (lightIndex < 0 || lightIndex >= MaxAdditionalLights)
            {
                return;
            }

            var shadowData = AdditionalLightShadowData[lightIndex];
            AdditionalLightShadowData[lightIndex] = new Vector4(1f, Mathf.Clamp01(shadowData.y), atlasTileIndex, shadowData.w);
            AdditionalLightShadowAtlasRects[lightIndex] = atlasRect;
            AdditionalLightShadowViewMatrices[lightIndex] = viewMatrix;
            AdditionalLightShadowProjectionMatrices[lightIndex] = projectionMatrix;
            AdditionalLightShadowSplitDatas[lightIndex] = splitData;
            AdditionalLightShadowStatuses[lightIndex] = BurtAdditionalLightShadowStatus.Prepared;
        }

        public void SetAdditionalLightShadowSlice(
            int sliceIndex,
            int lightIndex,
            int faceIndex,
            Vector4 atlasRect,
            Matrix4x4 viewMatrix,
            Matrix4x4 projectionMatrix,
            Matrix4x4 worldToShadowMatrix,
            ShadowSplitData splitData)
        {
            if (sliceIndex < 0 || sliceIndex >= MaxAdditionalLightShadowSlices)
            {
                return;
            }

            AdditionalLightShadowSliceLightIndices[sliceIndex] = lightIndex;
            AdditionalLightShadowSliceFaceIndices[sliceIndex] = faceIndex;
            AdditionalLightShadowSliceAtlasRects[sliceIndex] = atlasRect;
            AdditionalLightShadowSliceViewMatrices[sliceIndex] = viewMatrix;
            AdditionalLightShadowSliceProjectionMatrices[sliceIndex] = projectionMatrix;
            AdditionalLightShadowSliceWorldToShadowMatrices[sliceIndex] = worldToShadowMatrix;
            AdditionalLightShadowSliceSplitDatas[sliceIndex] = splitData;
        }

        public void SetAdditionalLightShadowLightParams(int lightIndex, int firstSliceIndex, int sliceCount, float lightType, float receiverNormalBias = 0f)
        {
            if (lightIndex < 0 || lightIndex >= MaxAdditionalLights)
            {
                return;
            }

            var shadowData = AdditionalLightShadowData[lightIndex];
            AdditionalLightShadowData[lightIndex] = new Vector4(shadowData.x, shadowData.y, Mathf.Max(0, firstSliceIndex), shadowData.w);
            AdditionalLightShadowLightParams[lightIndex] = new Vector4(
                Mathf.Max(0, firstSliceIndex),
                Mathf.Clamp(sliceCount, 0, PointLightShadowFaceCount),
                lightType,
                Mathf.Max(0f, receiverNormalBias));
            if (firstSliceIndex >= 0 && firstSliceIndex < MaxAdditionalLightShadowSlices)
            {
                AdditionalLightShadowAtlasRects[lightIndex] = AdditionalLightShadowSliceAtlasRects[firstSliceIndex];
            }
        }

        public void DisableAdditionalLightShadowSlot(int lightIndex)
        {
            DisableAdditionalLightShadowSlot(lightIndex, BurtAdditionalLightShadowStatus.None);
        }

        public void DisableAdditionalLightShadowSlot(int lightIndex, BurtAdditionalLightShadowStatus status)
        {
            if (lightIndex < 0 || lightIndex >= MaxAdditionalLights)
            {
                return;
            }

            AdditionalLightShadowVisibleLightIndices[lightIndex] = -1;
            AdditionalLightShadowData[lightIndex] = Vector4.zero;
            AdditionalLightShadowLightParams[lightIndex] = Vector4.zero;
            AdditionalLightShadowAtlasRects[lightIndex] = new Vector4(0f, 0f, 1f, 1f);
            AdditionalLightShadowViewMatrices[lightIndex] = Matrix4x4.identity;
            AdditionalLightShadowProjectionMatrices[lightIndex] = Matrix4x4.identity;
            AdditionalLightShadowSplitDatas[lightIndex] = default;
            AdditionalLightShadowStatuses[lightIndex] = status;
        }

        public void SetTileLightDebugState(
            bool attempted,
            bool uploaded,
            string buildMode,
            int tileSize,
            int tileGridX,
            int tileGridY,
            int tileCount,
            int maxLightsPerTile,
            int listCapacity,
            int minCount,
            int maxCount,
            float averageCount,
            int overflowTileCount = 0,
            int maxOverflowExtraCount = 0)
        {
            TileLightDebugBuildAttempted = attempted;
            TileLightDebugUploaded = uploaded;
            TileLightDebugBuildMode = string.IsNullOrEmpty(buildMode) ? "Disabled" : buildMode;
            TileLightTileSize = tileSize;
            TileLightGridX = tileGridX;
            TileLightGridY = tileGridY;
            TileLightTileCount = tileCount;
            TileLightMaxLightsPerTile = maxLightsPerTile;
            TileLightListCapacity = listCapacity;
            TileLightMinCount = minCount;
            TileLightMaxCount = maxCount;
            TileLightAverageCount = averageCount;
            TileLightOverflowTileCount = overflowTileCount;
            TileLightMaxOverflowExtraCount = maxOverflowExtraCount;
        }

        public void SetTileLightDebugCountSnapshot(uint[] sourceCounts, int count)
        {
            var safeCount = sourceCounts != null ? Mathf.Min(Mathf.Max(0, count), sourceCounts.Length) : 0;
            if (safeCount <= 0)
            {
                TileLightDebugCountSnapshotLength = 0;
                return;
            }

            if (TileLightDebugCountSnapshot == null || TileLightDebugCountSnapshot.Length < safeCount)
            {
                TileLightDebugCountSnapshot = new uint[safeCount];
            }

            System.Array.Copy(sourceCounts, TileLightDebugCountSnapshot, safeCount);
            TileLightDebugCountSnapshotLength = safeCount;
        }

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

            data.CollectAdditionalLights(visibleLights); // 把非主光方向光、点光和聚光灯打包成 shader 可读取的追加光源数组。

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

            AdditionalLightCount = 0; // 默认没有追加光源，SetupLightingPass 仍会上传清零数组来避免上一相机残留。
            ShadowedAdditionalLightCount = 0;

            SetAdditionalLightBufferUploadState(false, false);
            SetAdditionalLightShadowCacheState(false, 0, 0);
            ResetAdditionalLightShadowDiagnostics();
            SetTileLightDebugState(false, false, "Disabled", 0, 0, 0, 0, 0, 0, 0, 0, 0f);
            TileLightDebugCountSnapshotLength = 0;

            ClearAdditionalLightArrays(); // 清空数组全部槽位，保证数量变少时 shader 不会读到上一帧数据。
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

        private void CollectAdditionalLights(Unity.Collections.NativeArray<VisibleLight> visibleLights)
        {
            for (var lightIndex = 0; lightIndex < visibleLights.Length && AdditionalLightCount < MaxAdditionalLights; lightIndex++)
            {
                if (lightIndex == MainLightIndex)
                {
                    continue;
                }

                var visibleLight = visibleLights[lightIndex];

                switch (visibleLight.lightType)
                {
                    case LightType.Directional:
                        TryAddDirectionalAdditionalLight(visibleLight, lightIndex);
                        break;
                    case LightType.Point:
                        TryAddPointAdditionalLight(visibleLight, lightIndex);
                        break;
                    case LightType.Spot:
                        TryAddSpotAdditionalLight(visibleLight, lightIndex);
                        break;
                }
            }
        }

        private void TryAddDirectionalAdditionalLight(VisibleLight visibleLight, int visibleLightIndex)
        {
            var forwardColumn = visibleLight.localToWorldMatrix.GetColumn(2);
            var directionTowardLight = new Vector3(-forwardColumn.x, -forwardColumn.y, -forwardColumn.z);

            if (directionTowardLight.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var slot = AdditionalLightCount++;
            var color = visibleLight.finalColor;
            var direction = directionTowardLight.normalized;
            AdditionalLightColorAndType[slot] = new Vector4(color.r, color.g, color.b, AdditionalLightTypeDirectional);
            AdditionalLightDirectionAndSpot[slot] = new Vector4(direction.x, direction.y, direction.z, 0f);
            PackAdditionalLightBufferSlot(slot);
            TryMarkUnsupportedAdditionalLightShadow(slot, visibleLight, visibleLightIndex, BurtAdditionalLightShadowStatus.UnsupportedLightType);
        }

        private void TryAddPointAdditionalLight(VisibleLight visibleLight, int visibleLightIndex)
        {
            AddLocalAdditionalLight(visibleLight, AdditionalLightTypePoint, Vector3.forward, 1f, -1f, 0f, true, visibleLightIndex);
        }

        private void TryAddSpotAdditionalLight(VisibleLight visibleLight, int visibleLightIndex)
        {
            var forwardColumn = visibleLight.localToWorldMatrix.GetColumn(2);
            var spotDirection = new Vector3(forwardColumn.x, forwardColumn.y, forwardColumn.z);

            if (spotDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var outerAngle = Mathf.Clamp(visibleLight.spotAngle, 0.01f, 179f);
            var innerAngle = ResolveInnerSpotAngle(visibleLight, outerAngle);
            var outerCos = Mathf.Cos(0.5f * outerAngle * Mathf.Deg2Rad);
            var innerCos = Mathf.Cos(0.5f * innerAngle * Mathf.Deg2Rad);
            var invAngleRange = 1f / Mathf.Max(innerCos - outerCos, 0.001f);

            AddLocalAdditionalLight(visibleLight, AdditionalLightTypeSpot, spotDirection.normalized, innerCos, outerCos, invAngleRange, true, visibleLightIndex);
        }

        private void AddLocalAdditionalLight(
            VisibleLight visibleLight,
            float lightType,
            Vector3 direction,
            float innerCos,
            float outerCos,
            float invAngleRange,
            bool canCastShadow = false,
            int visibleLightIndex = -1)
        {
            var range = Mathf.Max(visibleLight.range, 0.0001f);
            var positionColumn = visibleLight.localToWorldMatrix.GetColumn(3);
            var color = visibleLight.finalColor;
            var slot = AdditionalLightCount++;

            AdditionalLightPositionAndRange[slot] = new Vector4(positionColumn.x, positionColumn.y, positionColumn.z, range);
            AdditionalLightColorAndType[slot] = new Vector4(color.r, color.g, color.b, lightType);
            AdditionalLightDirectionAndSpot[slot] = new Vector4(direction.x, direction.y, direction.z, 0f);
            AdditionalLightSpotParams[slot] = new Vector4(innerCos, outerCos, invAngleRange, 0f);
            PackAdditionalLightBufferSlot(slot);

            if (canCastShadow)
            {
                TryMarkAdditionalLightShadow(slot, visibleLight, visibleLightIndex);
            }
        }

        private static float ResolveInnerSpotAngle(VisibleLight visibleLight, float outerAngle)
        {
            if (visibleLight.light != null)
            {
                var innerAngle = visibleLight.light.innerSpotAngle;

                if (innerAngle > 0.01f && innerAngle < outerAngle)
                {
                    return innerAngle;
                }
            }

            return Mathf.Max(0.01f, outerAngle * 0.8f);
        }

        private void ClearAdditionalLightArrays()
        {
            for (var lightIndex = 0; lightIndex < MaxAdditionalLights; lightIndex++)
            {
                AdditionalLightPositionAndRange[lightIndex] = Vector4.zero;
                AdditionalLightColorAndType[lightIndex] = Vector4.zero;
                AdditionalLightDirectionAndSpot[lightIndex] = Vector4.zero;
                AdditionalLightSpotParams[lightIndex] = Vector4.zero;
                AdditionalLightShadowVisibleLightIndices[lightIndex] = -1;
                AdditionalLightShadowData[lightIndex] = Vector4.zero;
                AdditionalLightShadowLightParams[lightIndex] = Vector4.zero;
                AdditionalLightShadowAtlasRects[lightIndex] = new Vector4(0f, 0f, 1f, 1f);
                AdditionalLightShadowViewMatrices[lightIndex] = Matrix4x4.identity;
                AdditionalLightShadowProjectionMatrices[lightIndex] = Matrix4x4.identity;
                AdditionalLightShadowSplitDatas[lightIndex] = default;
                AdditionalLightShadowSourceModes[lightIndex] = LightShadows.None;
                AdditionalLightShadowSourceStrengths[lightIndex] = 0f;
                AdditionalLightShadowStatuses[lightIndex] = BurtAdditionalLightShadowStatus.None;
                PackAdditionalLightBufferSlot(lightIndex);
            }

            ClearAdditionalLightShadowSliceArrays();
        }

        private void TryMarkAdditionalLightShadow(int slot, VisibleLight visibleLight, int visibleLightIndex)
        {
            if (slot < 0 || slot >= MaxAdditionalLights)
            {
                return;
            }

            var light = visibleLight.light;
            AdditionalLightShadowVisibleLightIndices[slot] = visibleLightIndex;
            AdditionalLightShadowSourceModes[slot] = light != null ? light.shadows : LightShadows.None;
            AdditionalLightShadowSourceStrengths[slot] = light != null ? light.shadowStrength : 0f;
            if (light == null || light.shadows == LightShadows.None)
            {
                SetAdditionalLightShadowStatus(slot, BurtAdditionalLightShadowStatus.ShadowTypeNone);
                AdditionalLightShadowNoneCount++;
                return;
            }

            if (light.shadowStrength <= 0.0001f)
            {
                SetAdditionalLightShadowStatus(slot, BurtAdditionalLightShadowStatus.ShadowStrengthZero);
                AdditionalLightShadowStrengthZeroCount++;
                return;
            }

            AdditionalLightShadowCandidateCount++;
            if (visibleLight.lightType == LightType.Point)
            {
                if (ShadowedAdditionalLightCount >= MaxShadowedAdditionalLights)
                {
                    SetAdditionalLightShadowStatus(slot, BurtAdditionalLightShadowStatus.SlotLimitExceeded);
                    AdditionalLightShadowSlotLimitExceededCount++;
                    return;
                }

                var pointSoftShadow = light.shadows == LightShadows.Soft ? 1f : 0f;
                AdditionalLightShadowData[slot] = new Vector4(1f, Mathf.Clamp01(light.shadowStrength), 0f, pointSoftShadow);
                AdditionalLightShadowLightParams[slot] = new Vector4(0f, PointLightShadowFaceCount, AdditionalLightTypePoint, 0f);
                SetAdditionalLightShadowStatus(slot, BurtAdditionalLightShadowStatus.Marked);
                ShadowedAdditionalLightCount++;
                AdditionalLightShadowMarkedCount++;
                SetAdditionalLightShadowCacheState(false, 0, 0);
                return;
            }

            if (visibleLight.lightType != LightType.Spot)
            {
                SetAdditionalLightShadowStatus(slot, BurtAdditionalLightShadowStatus.UnsupportedLightType);
                AdditionalLightShadowUnsupportedTypeCount++;
                return;
            }

            if (ShadowedAdditionalLightCount >= MaxShadowedAdditionalLights)
            {
                SetAdditionalLightShadowStatus(slot, BurtAdditionalLightShadowStatus.SlotLimitExceeded);
                AdditionalLightShadowSlotLimitExceededCount++;
                return;
            }

            var softShadow = light.shadows == LightShadows.Soft ? 1f : 0f;
            AdditionalLightShadowData[slot] = new Vector4(1f, Mathf.Clamp01(light.shadowStrength), 0f, softShadow);
            AdditionalLightShadowLightParams[slot] = new Vector4(0f, 1f, AdditionalLightTypeSpot, 0f);
            SetAdditionalLightShadowStatus(slot, BurtAdditionalLightShadowStatus.Marked);
            ShadowedAdditionalLightCount++;
            AdditionalLightShadowMarkedCount++;
            SetAdditionalLightShadowCacheState(false, 0, 0);
        }

        private void TryMarkUnsupportedAdditionalLightShadow(
            int slot,
            VisibleLight visibleLight,
            int visibleLightIndex,
            BurtAdditionalLightShadowStatus unsupportedStatus)
        {
            if (slot < 0 || slot >= MaxAdditionalLights)
            {
                return;
            }

            var light = visibleLight.light;
            AdditionalLightShadowVisibleLightIndices[slot] = visibleLightIndex;
            AdditionalLightShadowSourceModes[slot] = light != null ? light.shadows : LightShadows.None;
            AdditionalLightShadowSourceStrengths[slot] = light != null ? light.shadowStrength : 0f;
            if (light == null || light.shadows == LightShadows.None)
            {
                return;
            }

            if (light.shadowStrength <= 0.0001f)
            {
                SetAdditionalLightShadowStatus(slot, BurtAdditionalLightShadowStatus.ShadowStrengthZero);
                AdditionalLightShadowStrengthZeroCount++;
                return;
            }

            AdditionalLightShadowCandidateCount++;
            SetAdditionalLightShadowStatus(slot, unsupportedStatus);
            AdditionalLightShadowUnsupportedTypeCount++;
        }

        public void SetAdditionalLightShadowStatus(int lightIndex, BurtAdditionalLightShadowStatus status)
        {
            if (lightIndex < 0 || lightIndex >= MaxAdditionalLights)
            {
                return;
            }

            AdditionalLightShadowStatuses[lightIndex] = status;
        }

        private void ResetAdditionalLightShadowDiagnostics()
        {
            AdditionalLightShadowCandidateCount = 0;
            AdditionalLightShadowMarkedCount = 0;
            AdditionalLightShadowSlotLimitExceededCount = 0;
            AdditionalLightShadowPointUnsupportedCount = 0;
            AdditionalLightShadowUnsupportedTypeCount = 0;
            AdditionalLightShadowNoneCount = 0;
            AdditionalLightShadowStrengthZeroCount = 0;
            AdditionalLightShadowPrepareAttempted = false;
            AdditionalLightShadowPrepareSucceeded = false;
            AdditionalLightShadowPrepareFailedCount = 0;
            AdditionalLightShadowAtlasRegistered = false;
            AdditionalLightShadowAtlasValid = false;
            AdditionalLightShadowActiveSliceCount = 0;
        }

        private void PromotePreparedAdditionalLightShadowSlots()
        {
            var additionalLightCount = Mathf.Min(AdditionalLightCount, MaxAdditionalLights);
            for (var lightIndex = 0; lightIndex < additionalLightCount; lightIndex++)
            {
                if (AdditionalLightShadowData[lightIndex].x > 0.5f && AdditionalLightShadowVisibleLightIndices[lightIndex] >= 0)
                {
                    AdditionalLightShadowStatuses[lightIndex] = BurtAdditionalLightShadowStatus.Active;
                }
            }
        }

        private void ClearAdditionalLightShadowSliceArrays()
        {
            for (var sliceIndex = 0; sliceIndex < MaxAdditionalLightShadowSlices; sliceIndex++)
            {
                AdditionalLightShadowSliceLightIndices[sliceIndex] = -1;
                AdditionalLightShadowSliceFaceIndices[sliceIndex] = -1;
                AdditionalLightShadowSliceAtlasRects[sliceIndex] = new Vector4(0f, 0f, 1f, 1f);
                AdditionalLightShadowSliceViewMatrices[sliceIndex] = Matrix4x4.identity;
                AdditionalLightShadowSliceProjectionMatrices[sliceIndex] = Matrix4x4.identity;
                AdditionalLightShadowSliceWorldToShadowMatrices[sliceIndex] = Matrix4x4.identity;
                AdditionalLightShadowSliceSplitDatas[sliceIndex] = default;
            }
        }

        private void PackAdditionalLightBufferSlot(int lightIndex)
        {
            var baseIndex = lightIndex * 4;
            AdditionalLightBufferData[baseIndex] = AdditionalLightPositionAndRange[lightIndex];
            AdditionalLightBufferData[baseIndex + 1] = AdditionalLightColorAndType[lightIndex];
            AdditionalLightBufferData[baseIndex + 2] = AdditionalLightDirectionAndSpot[lightIndex];
            AdditionalLightBufferData[baseIndex + 3] = AdditionalLightSpotParams[lightIndex];
        }
    }
}
