// 引入泛型集合命名空间，用来保存当前帧内的相机栈和栈内 request。
using System.Collections.Generic;
// 引入 Unity 渲染命名空间，用来记录 RenderTargetIdentifier 和默认 CameraTarget。
using UnityEngine.Rendering;
// 定义 BurtRP 运行时命名空间，让 Frame 层可以直接访问 BurtRenderRequest 和相机枚举。
namespace Burt.RenderPipeline
{
    // 表示一个连续的逻辑相机栈；当前只做分组诊断，不改变真正的 request 执行顺序。
    internal sealed class BurtCameraStackGroup
    {
        // 保存属于这个逻辑相机栈的 request 引用；列表顺序必须等于当前帧排序后的执行顺序。
        private readonly List<BurtRenderRequest> requests = new List<BurtRenderRequest>();
        // 暴露只读 request 列表，让调试和未来栈级渲染可以查看组内内容，但不能直接替换列表。
        public IReadOnlyList<BurtRenderRequest> Requests => requests;
        // 记录这个栈所在的排序层；第一版用它保证分组不会跨越当前实际执行层级。
        public int SortLayer { get; private set; }
        // 记录这个栈的逻辑编号；Base、Overlay、UI 只有 StackId 相同时才允许进入同一组。
        public int StackId { get; private set; }
        // 记录创建这个栈时第一个 request 的角色，方便调试判断这个组是 Base 栈还是孤立 Overlay/UI。
        public BurtCameraRole RootRole { get; private set; }
        // 记录这个栈的首个输出目标；后续做栈级 RT 复用时会用它判断是否能共享颜色和深度。
        public RenderTargetIdentifier TargetIdentifier { get; private set; }
        // 记录这个组是否来自 SceneView 或 Preview；编辑器相机先强制单独成组，避免和 Game 相机误合并。
        public bool IsEditorCameraGroup { get; private set; }
        // 返回当前组内 request 数量，调试输出和未来栈级 pass 插入都会用到。
        public int RequestCount => requests.Count;
        // 返回这个组是否已经有 Base 相机，后续共享 RT 需要有明确的起点。
        public bool HasBaseCamera => BaseCameraCount > 0;
        // 返回这个组是否有多个 Base 相机，这种情况暂时不适合启用 Stack 级 RT 共享。
        public bool HasMultipleBaseCameras => BaseCameraCount > 1;
        // 返回这个组是否适合使用 Stack 级颜色和深度 RT；当前只作计划诊断。
        public bool ShouldUseSharedRenderTargets => !IsEditorCameraGroup && BaseCameraCount == 1;
        // 返回这个组未来是否应该只做一次 FinalBlit，和共享 RT 条件保持一致。
        public bool ShouldUseSingleFinalBlit => ShouldUseSharedRenderTargets;
        // 根据当前组内角色和目标返回一个简短的 RT 计划名称，便于 Console 里快速阅读。
        public string RenderTargetPlanName
        {
            get
            {
                // 编辑器辅助相机先保持独立 RT 计划，避免和 Game 相机栈误合并。
                if (IsEditorCameraGroup)
                {
                    // 返回编辑器相机的独立计划名称。
                    return "EditorCameraRT";
                }
                // 没有 Base 的栈不能决定共享 RT 的初始内容，先标记为需要配置检查。
                if (!HasBaseCamera)
                {
                    // 返回缺少 Base 的计划名称。
                    return "MissingBase";
                }
                // 多个 Base 会让颜色和深度起点产生歧义，先标记为非法栈。
                if (HasMultipleBaseCameras)
                {
                    // 返回重复 Base 的计划名称。
                    return "MultipleBase";
                }
                // 有 Overlay 或 UI 时代表这个栈未来需要共享一套 StackColor/StackDepth。
                if (HasOverlayOrUICamera)
                {
                    // 返回多相机栈的共享 RT 计划名称。
                    return "SharedStackRT";
                }
                // 只有单个 Base 时仍然可以用 Stack 级 RT 生命周期，方便后处理插入。
                return "SingleBaseStackRT";
            }
        }
        // 返回这个栈是否包含 Overlay 或 UI 相机，用来判断是否真的需要栈内叠加。
        public bool HasOverlayOrUICamera
        {
            get
            {
                // 遍历当前组的 request，找到 Overlay 或 UI 就立即返回。
                for (var index = 0; index < requests.Count; index++)
                {
                    // 取出当前 request，后续需要做空值保护。
                    var request = requests[index];
                    // 空 request 不能提供角色信息，直接检查下一个。
                    if (request == null)
                    {
                        // 跳过空 request。
                        continue;
                    }
                    // Overlay 和 UI 都表示这个栈需要在 Base 结果上继续叠加。
                    if (request.CameraRole == BurtCameraRole.Overlay || request.CameraRole == BurtCameraRole.UI)
                    {
                        // 找到叠加相机，直接返回 true。
                        return true;
                    }
                }
                // 遍历完都没有 Overlay/UI，说明这是单 Base 栈或编辑器相机组。
                return false;
            }
        }
        // 统计组内 Base 相机数量，用来发现一个 StackId 下错误配置了多个 Base 的情况。
        public int BaseCameraCount
        {
            get
            {
                // 准备计数器，从 0 开始统计组内 Base 角色数量。
                var count = 0;
                // 遍历当前组内所有 request，因为 request 数量通常很少，线性扫描足够清晰。
                for (var index = 0; index < requests.Count; index++)
                {
                    // 读取当前 request，后续要做空值保护。
                    var request = requests[index];
                    // 如果 request 有效并且角色是 Base，就把计数加一。
                    if (request != null && request.CameraRole == BurtCameraRole.Base)
                    {
                        // 记录一个 Base 相机。
                        count++;
                    }
                }
                // 返回最终统计结果。
                return count;
            }
        }
        // 根据第一个 request 初始化这个栈组；调用方会先 Reset 再 AddRequest。
        public void ResetFromFirstRequest(BurtRenderRequest request)
        {
            // 清空上一次使用时留下的 request，保证复用对象不会串帧。
            requests.Clear();
            // 如果第一个 request 为空，就写入安全默认值，避免调试代码访问未初始化字段。
            if (request == null)
            {
                // 默认排序层使用 0，表示没有来自真实 request 的排序信息。
                SortLayer = 0;
                // 默认逻辑栈使用 0，和没有 BurtCameraData 的相机保持一致。
                StackId = 0;
                // 默认根角色使用 Base，作为最保守的相机栈语义。
                RootRole = BurtCameraRole.Base;
                // 默认目标使用 CameraTarget，避免 RenderTargetIdentifier 保持未说明状态。
                TargetIdentifier = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
                // 空 request 不属于编辑器相机组。
                IsEditorCameraGroup = false;
                // 空 request 初始化完成，直接结束函数。
                return;
            }
            // 缓存第一个 request 的排序层，让同组判断和调试输出都使用同一份快照。
            SortLayer = request.SortLayer;
            // 缓存第一个 request 的逻辑栈编号，让后续 Overlay/UI 只能合入同一个 StackId。
            StackId = request.StackId;
            // 缓存第一个 request 的角色，帮助判断这个组是否以 Base 开始。
            RootRole = request.CameraRole;
            // 缓存第一个 request 的输出目标，为未来栈级颜色/深度资源共享做准备。
            TargetIdentifier = request.TargetIdentifier;
            // 编辑器相机先单独成组，避免 SceneView、Preview 和 Game 相机共用 stackId=0 时被误合并。
            IsEditorCameraGroup = IsEditorOnlyRole(request.CameraRole);
        }
        // 判断一个 request 是否能追加到当前栈组；这个判断只影响诊断分组，不影响真实渲染顺序。
        public bool CanAppend(BurtRenderRequest request)
        {
            // 空 request 不能追加，因为后续无法读取它的角色、StackId 和目标。
            if (request == null)
            {
                // 返回 false，让调用方为异常数据开启新组或跳过。
                return false;
            }
            // 无效 request 不进入 Frame 分组；真实渲染循环也会跳过它。
            if (!request.IsValid)
            {
                // 返回 false，保持 Frame 诊断只描述有效渲染任务。
                return false;
            }
            // 如果当前组还没有 request，任何有效 request 都可以作为第一个成员。
            if (requests.Count == 0)
            {
                // 返回 true，调用方会先 ResetFromFirstRequest 再 AddRequest。
                return true;
            }
            // 已经是编辑器相机组时不再追加，因为 SceneView/Preview 暂时不参与 Game 相机栈合成。
            if (IsEditorCameraGroup)
            {
                // 返回 false，确保编辑器相机一机一组。
                return false;
            }
            // 如果新 request 是编辑器相机，也让它单独开组，避免和普通相机栈混在一起。
            if (IsEditorOnlyRole(request.CameraRole))
            {
                // 返回 false，保护 Game/Scene/Preview 三类相机的边界。
                return false;
            }
            // SortLayer 不同表示当前实际执行层级已经变化，不允许跨层合并成同一个诊断栈。
            if (request.SortLayer != SortLayer)
            {
                // 返回 false，让新的排序层从新栈组开始。
                return false;
            }
            // StackId 不同表示逻辑相机栈不同，不允许互相合并。
            if (request.StackId != StackId)
            {
                // 返回 false，保持 BurtCameraData.StackId 的语义清晰。
                return false;
            }
            // 输出目标不同表示相机不应该共享同一套颜色/深度资源，因此先拆成不同诊断栈。
            if (!request.TargetIdentifier.Equals(TargetIdentifier))
            {
                // 返回 false，避免 targetTexture 相机和主 BackBuffer 相机被误合并。
                return false;
            }
            // 一个新的 Base 相机通常代表另一个相机栈起点，即使 StackId 配错相同也不强行合并。
            if (request.CameraRole == BurtCameraRole.Base)
            {
                // 返回 false，让调试日志暴露“同层同栈多个 Base”的配置问题。
                return false;
            }
            // 通过所有边界检查后，说明这个 request 可以作为 Overlay/UI 追加到当前逻辑栈。
            return true;
        }
        // 把一个有效 request 加入当前栈组；调用前应先用 CanAppend 或 ResetFromFirstRequest 保证语义正确。
        public void AddRequest(BurtRenderRequest request)
        {
            // 如果传入空 request，就直接忽略，避免诊断层影响真实渲染稳定性。
            if (request == null)
            {
                // 结束函数，不把空引用写入列表。
                return;
            }
            // 把 request 按当前排序顺序追加到组内列表。
            requests.Add(request);
        }
        // 根据当前栈的 RT 计划和指定 request 的栈内位置，生成真正执行时要使用的 RenderTarget 生命周期选项。
        public BurtRequestRenderOptions CreateRenderOptions(int requestIndex)
        {
            // 读取当前栈内 request 数量，执行路径和调试路径都使用同一份数量快照。
            var requestCount = requests.Count;
            // 如果这个栈暂时不允许共享 RT，就让每个 request 自己申请、输出和释放，保证编辑器相机和非法栈安全。
            if (!ShouldUseSharedRenderTargets)
            {
                // 创建独立 request 选项，同时保留栈内索引、StackId 和 RTPlan，方便日志定位。
                return BurtRequestRenderOptions.CreateIsolatedRequest(requestIndex, requestCount, StackId, RenderTargetPlanName);
            }
            // 对满足条件的 Game 相机栈启用共享 RT：第一个 request 申请，最后一个 request FinalBlit 和释放。
            return BurtRequestRenderOptions.CreateSharedStackRequest(requestIndex, requestCount, StackId, RenderTargetPlanName);
        }
        // 清理这个栈组，方便 BurtRenderFrame 在下一帧复用同一个对象。
        public void Clear()
        {
            // 清空组内 request 引用，避免下一帧调试看到上一帧残留数据。
            requests.Clear();
            // 重置排序层到默认值，表示当前对象暂时没有绑定真实栈。
            SortLayer = 0;
            // 重置逻辑栈编号到默认值。
            StackId = 0;
            // 重置根角色到 Base，作为安全默认状态。
            RootRole = BurtCameraRole.Base;
            // 重置输出目标到 CameraTarget，避免保留上一帧的 RenderTexture 引用。
            TargetIdentifier = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
            // 重置编辑器相机标记。
            IsEditorCameraGroup = false;
        }
        // 判断角色是否属于编辑器或引擎辅助相机；这些相机暂时不参与 Game 相机栈合并。
        private static bool IsEditorOnlyRole(BurtCameraRole role)
        {
            // SceneView、Preview 和 Reflection 都应当独立分组，避免和 Game 相机共享 RT 生命周期。
            return role == BurtCameraRole.SceneView || role == BurtCameraRole.Preview || role == BurtCameraRole.Reflection;
        }
    }
    // 表示一帧内所有有效 BurtRenderRequest 的分组快照；当前只为后续栈级渲染和调试服务。
    internal sealed class BurtRenderFrame
    {
        // 保存当前帧按执行顺序形成的相机栈组。
        private readonly List<BurtCameraStackGroup> stackGroups = new List<BurtCameraStackGroup>();
        // 保存可复用的栈组对象，减少每帧打开 Frame Debug 时的临时分配。
        private readonly Stack<BurtCameraStackGroup> groupPool = new Stack<BurtCameraStackGroup>();
        // 暴露只读栈组列表，外部可以查看当前帧结构，但不能替换内部列表。
        public IReadOnlyList<BurtCameraStackGroup> StackGroups => stackGroups;
        // 记录输入列表里的 request 总数，包含无效或空 request，方便排查创建阶段问题。
        public int SourceRequestCount { get; private set; }
        // 记录真正进入 Frame 分组的有效 request 数量，应该和后续真实执行的有效 request 数量一致。
        public int ValidRequestCount { get; private set; }
        // 用已经排序好的 request 列表构建当前帧分组；这个函数不排序也不改变任何 request。
        public void BuildFromSortedRequests(IList<BurtRenderRequest> sortedRequests)
        {
            // 先清理上一帧的分组，保证当前 Frame 快照只描述本帧 request。
            Clear();
            // 记录输入列表数量；空列表引用按 0 处理，避免后续空引用异常。
            SourceRequestCount = sortedRequests != null ? sortedRequests.Count : 0;
            // 如果调用方传入空列表，就保持空 Frame 并结束。
            if (sortedRequests == null)
            {
                // 结束函数，因为没有 request 可以分组。
                return;
            }
            // 遍历已经排好序的 request；遍历顺序就是当前管线真实渲染顺序。
            for (var index = 0; index < sortedRequests.Count; index++)
            {
                // 读取当前位置的 request，后面统一做空值和有效性检查。
                var request = sortedRequests[index];
                // 空 request 不进入 Frame 分组，真实渲染循环也会跳过它。
                if (request == null)
                {
                    // 继续检查下一个 request。
                    continue;
                }
                // 无效 request 不进入 Frame 分组，避免调试误以为它会被渲染。
                if (!request.IsValid)
                {
                    // 继续检查下一个 request。
                    continue;
                }
                // 记录一个有效 request，方便和日志中的组内数量做总数对齐。
                ValidRequestCount++;
                // 取出当前最后一个栈组；如果没有任何组，就让 currentGroup 保持 null。
                var currentGroup = stackGroups.Count > 0 ? stackGroups[stackGroups.Count - 1] : null;
                // 如果没有栈组，或者当前 request 不能合入最后一个栈组，就新开一个栈组。
                if (currentGroup == null || !currentGroup.CanAppend(request))
                {
                    // 从对象池取一个栈组对象，避免每帧反复 new 小对象。
                    currentGroup = RentGroup();
                    // 用当前 request 初始化新栈组的排序层、StackId、角色和输出目标。
                    currentGroup.ResetFromFirstRequest(request);
                    // 把新栈组追加到当前帧栈列表；列表顺序就是未来栈级执行顺序。
                    stackGroups.Add(currentGroup);
                }
                // 把当前 request 追加进选定的栈组。
                currentGroup.AddRequest(request);
            }
        }
        // 清理当前帧分组，并把栈组对象放回池中供下一帧复用。
        public void Clear()
        {
            // 遍历当前帧所有栈组，逐个归还对象池。
            for (var index = 0; index < stackGroups.Count; index++)
            {
                // 释放当前栈组；ReleaseGroup 内部会清空它的状态。
                ReleaseGroup(stackGroups[index]);
            }
            // 清空当前帧栈列表，保证下一次 Build 从空列表开始。
            stackGroups.Clear();
            // 重置输入 request 统计。
            SourceRequestCount = 0;
            // 重置有效 request 统计。
            ValidRequestCount = 0;
        }
        // 从池里获取一个栈组对象；池为空时才创建新对象。
        private BurtCameraStackGroup RentGroup()
        {
            // 如果池里有可复用对象，就优先复用。
            if (groupPool.Count > 0)
            {
                // 弹出一个已经清理过的栈组对象。
                return groupPool.Pop();
            }
            // 池为空时创建新栈组对象。
            return new BurtCameraStackGroup();
        }
        // 把栈组对象清理后放回池中。
        private void ReleaseGroup(BurtCameraStackGroup group)
        {
            // 空组对象不需要释放。
            if (group == null)
            {
                // 直接返回，避免空引用异常。
                return;
            }
            // 清空组内状态和 request 引用，避免对象池保留上一帧内容。
            group.Clear();
            // 把对象放回池里，下一次 Build 可以复用。
            groupPool.Push(group);
        }
    }
}
