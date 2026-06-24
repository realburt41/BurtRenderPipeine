using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public enum BurtMultipassShaderPass
    {
        Forward = 0,
        ForwardOnly = 1,
        DepthOnly = 2,
        ShadowCaster = 3,
        GBuffer = 4,
        FurBlurProperty = 5,
        FurBlurVelocity = 6,
    }

    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public sealed class BurtMultipassRenderer : MonoBehaviour
    {
        public const int MaxMultipassLayerCount = 32;
        private const int DefaultMultipassLayerCount = 16;

        private const int MaxShaderPassTypeCount = 7;
        private static readonly List<BurtMultipassRenderer> s_Renderers = new List<BurtMultipassRenderer>();
        private static readonly Matrix4x4[] s_InstanceMatrices = new Matrix4x4[MaxMultipassLayerCount];
        private static MaterialPropertyBlock s_DrawPropertyBlock;
        private static readonly int FurScaleId = Shader.PropertyToID("_FurScale");
        private static readonly int FurMaxCountId = Shader.PropertyToID("_FurMaxCount");

#if UNITY_EDITOR
        internal static bool s_ForceGlobal32Layer;
        internal static int s_ForceGlobalLayerCount = MaxMultipassLayerCount;
#endif

        private static readonly string[][] ShaderPassNames =
        {
            new[]
            {
                "Burt Multipass Fur Forward",
                "Burt Lit Forward",
                "Burt Hair Forward",
                "Burt Subsurface Forward",
                "Burt Unlit Forward",
                "BurtForward"
            },
            new[]
            {
                "Burt Unlit Forward Only",
                "BurtForwardOnly"
            },
            new[]
            {
                "Burt Multipass Fur Depth Only",
                "Burt Lit Depth Only",
                "Burt Hair Depth Only",
                "Burt Depth Only",
                "BurtDepthOnly"
            },
            new[]
            {
                "Burt Multipass Fur Shadow Caster",
                "Burt Lit Shadow Caster",
                "Burt Hair Shadow Caster",
                "Burt Unlit Shadow Caster",
                "ShadowCaster"
            },
            new[]
            {
                "Burt Multipass Fur GBuffer",
                "Burt Lit GBuffer",
                "Burt Hair GBuffer",
                "BurtGBuffer"
            },
            new[]
            {
                "Burt Multipass Fur Blur Property"
            },
            new[]
            {
                "Burt Multipass Fur Blur Velocity"
            }
        };

        [Tooltip("Number of multipass shell layers to draw.")]
        [Range(1, MaxMultipassLayerCount)]
        public int m_LayerCount = DefaultMultipassLayerCount;

        [Tooltip("Use a separate shell layer count for each renderer material slot.")]
        public bool m_SupportDifferentPassCount;

        [Tooltip("Per-material shell layer counts. Used only when Support Different Pass Count is enabled.")]
        [SerializeField]
        public int[] m_LayerCountList = Array.Empty<int>();

        [Tooltip("Optional material overrides for each renderer material slot.")]
        [SerializeField]
        public Material[] m_OverrideMaterials = Array.Empty<Material>();

        [Tooltip("Distance fade curve. The evaluated value is multiplied by the original layer count.")]
        public AnimationCurve m_DistanceFadeCurve = AnimationCurve.Linear(0.0f, 1.0f, 100.0f, 1.0f);

        [Tooltip("Rendering layer mask used by Burt multipass draws.")]
        public int m_RenderingLayerMask = 1;

        [NonSerialized]
        public Renderer m_Renderer;

        [NonSerialized]
        public bool m_Force32Layer;

        [NonSerialized]
        public int m_ForceLayerCount = MaxMultipassLayerCount;

        private List<int>[][] supportPasses = Array.Empty<List<int>[]>();
        private int cachedMaterialHash;
        private bool cacheDirty = true;
        private bool registered;
        private Matrix4x4 previousObjectToWorld = Matrix4x4.identity;
        private Matrix4x4 capturedObjectToWorld = Matrix4x4.identity;
        private bool hasPreviousObjectToWorld;
        private bool hasCapturedObjectToWorld;
        private int previousObjectToWorldFrame = -1;
        private int capturedObjectToWorldFrame = -1;

#if UNITY_EDITOR
        private int editorLod;
#endif

        public bool IsVisible => m_Renderer != null && m_Renderer.isVisible;

        internal static IReadOnlyList<BurtMultipassRenderer> RegisteredRenderers => s_Renderers;

        public int LayerCount => Mathf.Clamp(m_LayerCount, 1, MaxMultipassLayerCount);

        public static void SetForceGlobal32Layer(bool enable, int layerCount = MaxMultipassLayerCount)
        {
#if UNITY_EDITOR
            s_ForceGlobal32Layer = enable;
            s_ForceGlobalLayerCount = Mathf.Clamp(layerCount, 0, MaxMultipassLayerCount);
#endif
        }

        public List<int> GetSupportPass(int submeshIndex, BurtMultipassShaderPass pass)
        {
            EnsureCache();
            var passIndex = (int)pass;
            if (submeshIndex < 0 || submeshIndex >= supportPasses.Length || passIndex < 0 || passIndex >= MaxShaderPassTypeCount)
            {
                return null;
            }

            return supportPasses[submeshIndex][passIndex];
        }

        public int GetPassCount(int submeshIndex)
        {
            if (m_SupportDifferentPassCount && m_LayerCountList != null && submeshIndex >= 0 && submeshIndex < m_LayerCountList.Length)
            {
                return Mathf.Clamp(m_LayerCountList[submeshIndex], 1, MaxMultipassLayerCount);
            }

            return LayerCount;
        }

        public AnimationCurve GetDistanceFadeCurve(int submeshIndex = 0)
        {
            return m_DistanceFadeCurve;
        }

        public Material[] GetOverrideMaterials()
        {
            return m_OverrideMaterials;
        }

        public void SetOverrideMaterial(int submeshIndex, Material material)
        {
            EnsureRenderer();
            SyncSubmeshConfiguration();
            if (m_OverrideMaterials == null || submeshIndex < 0 || submeshIndex >= m_OverrideMaterials.Length)
            {
                Debug.LogError($"SetOverrideMaterial exceed on {name}.", this);
                return;
            }

            if (m_OverrideMaterials[submeshIndex] == material)
            {
                return;
            }

            m_OverrideMaterials[submeshIndex] = material;
            cacheDirty = true;
            OnMaterialChanged();
        }

        public void SetForce32Layer(bool enable, int layerCount = MaxMultipassLayerCount)
        {
            m_Force32Layer = enable;
            m_ForceLayerCount = Mathf.Clamp(layerCount, 0, MaxMultipassLayerCount);
        }

        public Transform GetRootTransform()
        {
            var skinnedRenderer = m_Renderer as SkinnedMeshRenderer;
            if (skinnedRenderer != null)
            {
                return skinnedRenderer.rootBone != null ? skinnedRenderer.rootBone : skinnedRenderer.transform;
            }

            return transform;
        }

        public void OnMeshChanged()
        {
            EnsureRenderer();
            SyncSubmeshConfiguration();
            cacheDirty = true;
            InitializeMaterialCache();
        }

        public void OnMaterialChanged()
        {
            EnsureRenderer();
            if (m_Renderer == null)
            {
                return;
            }

            var hash = CalculateMaterialHash();
            if (!cacheDirty && hash == cachedMaterialHash)
            {
                return;
            }

            cachedMaterialHash = hash;
            cacheDirty = false;
            InitializeMaterialCache();
        }

#if UNITY_EDITOR
        public void SetLod(int lod)
        {
            editorLod = lod;
        }

        public int GetLod()
        {
            return editorLod;
        }
#endif

        private void Awake()
        {
            EnsureRenderer();
        }

        private void OnEnable()
        {
            EnsureRenderer();
            Register(this);
            OnMaterialChanged();
        }

        private void OnDisable()
        {
            Unregister(this);
        }

        private void OnValidate()
        {
            EnsureRenderer();
            m_LayerCount = Mathf.Clamp(m_LayerCount, 1, MaxMultipassLayerCount);
            m_ForceLayerCount = Mathf.Clamp(m_ForceLayerCount, 0, MaxMultipassLayerCount);
            SyncSubmeshConfiguration();
            cacheDirty = true;
        }

        private void Update()
        {
            OnMaterialChanged();
        }

        private void EnsureRenderer()
        {
            if (m_Renderer == null)
            {
                m_Renderer = GetComponent<Renderer>();
            }
        }

        private int GetMaterialCount()
        {
            var materials = m_Renderer != null ? m_Renderer.sharedMaterials : null;
            return materials != null ? materials.Length : 0;
        }

        private void SyncSubmeshConfiguration()
        {
            var materialCount = GetMaterialCount();
            if (m_LayerCountList == null)
            {
                m_LayerCountList = Array.Empty<int>();
            }

            if (m_OverrideMaterials == null)
            {
                m_OverrideMaterials = Array.Empty<Material>();
            }

            if (m_LayerCountList.Length != materialCount)
            {
                var oldLength = m_LayerCountList.Length;
                Array.Resize(ref m_LayerCountList, materialCount);
                for (var i = oldLength; i < m_LayerCountList.Length; i++)
                {
                    m_LayerCountList[i] = LayerCount;
                }
            }

            for (var i = 0; i < m_LayerCountList.Length; i++)
            {
                m_LayerCountList[i] = Mathf.Clamp(m_LayerCountList[i], 1, MaxMultipassLayerCount);
            }

            if (m_OverrideMaterials.Length != materialCount)
            {
                Array.Resize(ref m_OverrideMaterials, materialCount);
            }

            if (m_DistanceFadeCurve == null)
            {
                m_DistanceFadeCurve = AnimationCurve.Linear(0.0f, 1.0f, 100.0f, 1.0f);
            }
        }

        private void InitializeMaterialCache()
        {
            var materials = m_Renderer != null ? m_Renderer.sharedMaterials : null;
            var materialCount = materials != null ? materials.Length : 0;
            SyncSubmeshConfiguration();
            Array.Resize(ref supportPasses, materialCount);
            for (var submeshIndex = 0; submeshIndex < materialCount; submeshIndex++)
            {
                if (supportPasses[submeshIndex] == null || supportPasses[submeshIndex].Length != MaxShaderPassTypeCount)
                {
                    supportPasses[submeshIndex] = new List<int>[MaxShaderPassTypeCount];
                }

                for (var passIndex = 0; passIndex < MaxShaderPassTypeCount; passIndex++)
                {
                    if (supportPasses[submeshIndex][passIndex] == null)
                    {
                        supportPasses[submeshIndex][passIndex] = new List<int>();
                    }
                    else
                    {
                        supportPasses[submeshIndex][passIndex].Clear();
                    }
                }

                var material = ResolveMaterial(materials, submeshIndex);
                if (material == null)
                {
                    continue;
                }

                for (var passIndex = 0; passIndex < MaxShaderPassTypeCount; passIndex++)
                {
                    AddMaterialPasses(material, ShaderPassNames[passIndex], supportPasses[submeshIndex][passIndex]);
                }
            }
        }

        private Material ResolveMaterial(Material[] materials, int submeshIndex)
        {
            if (m_OverrideMaterials != null &&
                submeshIndex >= 0 &&
                submeshIndex < m_OverrideMaterials.Length &&
                m_OverrideMaterials[submeshIndex] != null)
            {
                return m_OverrideMaterials[submeshIndex];
            }

            return materials != null && submeshIndex >= 0 && submeshIndex < materials.Length ? materials[submeshIndex] : null;
        }

        private static void AddMaterialPasses(Material material, string[] names, List<int> target)
        {
            if (material == null || names == null || target == null)
            {
                return;
            }

            for (var i = 0; i < names.Length; i++)
            {
                var passId = material.FindPass(names[i]);
                if (passId >= 0 && !target.Contains(passId))
                {
                    target.Add(passId);
                }
            }
        }

        private int CalculateMaterialHash()
        {
            var hash = 17;
            var materials = m_Renderer != null ? m_Renderer.sharedMaterials : null;
            if (materials != null)
            {
                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    hash = hash * 31 + (material != null ? material.GetInstanceID() : 0);
                }
            }

            if (m_OverrideMaterials != null)
            {
                for (var i = 0; i < m_OverrideMaterials.Length; i++)
                {
                    var material = m_OverrideMaterials[i];
                    hash = hash * 31 + (material != null ? material.GetInstanceID() : 0);
                }
            }

            return hash;
        }

        private void EnsureCache()
        {
            if (cacheDirty)
            {
                OnMaterialChanged();
            }
        }

        private static void Register(BurtMultipassRenderer renderer)
        {
            if (renderer == null || renderer.registered)
            {
                return;
            }

            renderer.registered = true;
            s_Renderers.Add(renderer);
        }

        private static void Unregister(BurtMultipassRenderer renderer)
        {
            if (renderer == null || !renderer.registered)
            {
                return;
            }

            renderer.registered = false;
            s_Renderers.Remove(renderer);
        }

        internal static void DrawAll(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            BurtMultipassShaderPass pass,
            RenderQueueRange range,
            int renderingLayerMask = ~0)
        {
            if (cmd == null || context == null || context.Request == null || context.Request.Camera == null)
            {
                return;
            }

            var camera = context.Request.Camera;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            for (var rendererIndex = 0; rendererIndex < s_Renderers.Count; rendererIndex++)
            {
                var multipassRenderer = s_Renderers[rendererIndex];
                if (!IsRendererDrawable(multipassRenderer, camera, planes, renderingLayerMask, pass))
                {
                    continue;
                }

                multipassRenderer.Draw(cmd, camera, pass, range);
            }
        }

        internal static void DrawOne(
            CommandBuffer cmd,
            BurtMultipassRenderer multipassRenderer,
            Camera camera,
            BurtMultipassShaderPass pass,
            RenderQueueRange range,
            int renderingLayerMask = ~0,
            bool skipCameraFrustumTest = false)
        {
            if (cmd == null || !IsRendererDrawable(multipassRenderer, camera, null, renderingLayerMask, pass, skipCameraFrustumTest))
            {
                return;
            }

            multipassRenderer.Draw(cmd, camera, pass, range);
        }

        private static bool IsRendererDrawable(
            BurtMultipassRenderer multipassRenderer,
            Camera camera,
            Plane[] planes,
            int renderingLayerMask,
            BurtMultipassShaderPass pass,
            bool skipCameraFrustumTest = false)
        {
            if (multipassRenderer == null || !multipassRenderer.isActiveAndEnabled)
            {
                return false;
            }

            var renderer = multipassRenderer.m_Renderer;
            if (renderer == null || !renderer.enabled || renderer.gameObject == null || !renderer.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (camera != null && (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
            {
                return false;
            }

            if ((multipassRenderer.m_RenderingLayerMask & renderingLayerMask) == 0)
            {
                return false;
            }

            if (pass == BurtMultipassShaderPass.ShadowCaster && renderer.shadowCastingMode == ShadowCastingMode.Off)
            {
                return false;
            }

            return skipCameraFrustumTest || planes == null || planes.Length == 0 || GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
        }

        private void Draw(CommandBuffer cmd, Camera camera, BurtMultipassShaderPass pass, RenderQueueRange range)
        {
            EnsureCache();
            var renderer = m_Renderer;
            var materials = renderer != null ? renderer.sharedMaterials : null;
            if (renderer == null || materials == null || materials.Length == 0)
            {
                return;
            }

            var mesh = ResolveMesh(renderer);
            if (mesh == null)
            {
                return;
            }

            var rootTransform = GetRootTransform();
            var instanceMatrix = ResolveRendererMatrix(renderer, rootTransform);
            var previousMatrix = ResolvePreviousObjectToWorld(instanceMatrix);
            FillInstanceMatrices(instanceMatrix, MaxMultipassLayerCount);

            var submeshCount = Mathf.Min(materials.Length, mesh.subMeshCount);
            for (var submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
            {
                var material = ResolveMaterial(materials, submeshIndex);
                if (!CanDrawMaterial(material, range))
                {
                    continue;
                }

                EnsureMaterialInstancing(material);

                var passList = GetSupportPass(submeshIndex, pass);
                if (passList == null || passList.Count == 0)
                {
                    continue;
                }

                var layerCount = ResolveLayerCount(submeshIndex, camera, rootTransform);
                if (layerCount <= 0)
                {
                    continue;
                }

                var propertyBlock = GetDrawPropertyBlock();
                propertyBlock.Clear();
                renderer.GetPropertyBlock(propertyBlock, submeshIndex);
                propertyBlock.SetVector(FurScaleId, rootTransform.lossyScale);
                propertyBlock.SetInteger(FurMaxCountId, layerCount);
                propertyBlock.SetMatrix(BurtFurBlurPassUtility.PreviousObjectToWorldId, previousMatrix);
                for (var passIndex = 0; passIndex < passList.Count; passIndex++)
                {
                    cmd.DrawMeshInstanced(
                        mesh,
                        submeshIndex,
                        material,
                        passList[passIndex],
                        s_InstanceMatrices,
                        layerCount,
                        propertyBlock);
                }
            }

            if (pass == BurtMultipassShaderPass.FurBlurVelocity)
            {
                CaptureCurrentObjectToWorld(instanceMatrix);
            }
        }

        private Matrix4x4 ResolvePreviousObjectToWorld(Matrix4x4 currentMatrix)
        {
            PromoteCapturedObjectToWorldIfNeeded(Time.frameCount);
            var frameDelta = Time.frameCount - previousObjectToWorldFrame;
            return hasPreviousObjectToWorld && frameDelta >= 0 && frameDelta <= 1 ? previousObjectToWorld : currentMatrix;
        }

        private void CaptureCurrentObjectToWorld(Matrix4x4 currentMatrix)
        {
            var frame = Time.frameCount;
            PromoteCapturedObjectToWorldIfNeeded(frame);
            capturedObjectToWorld = currentMatrix;
            hasCapturedObjectToWorld = true;
            capturedObjectToWorldFrame = frame;
        }

        private void PromoteCapturedObjectToWorldIfNeeded(int frame)
        {
            if (!hasCapturedObjectToWorld || capturedObjectToWorldFrame >= frame)
            {
                return;
            }

            previousObjectToWorld = capturedObjectToWorld;
            hasPreviousObjectToWorld = true;
            previousObjectToWorldFrame = capturedObjectToWorldFrame;
            hasCapturedObjectToWorld = false;
            capturedObjectToWorldFrame = -1;
        }

        private int ResolveLayerCount(int submeshIndex, Camera camera, Transform rootTransform)
        {
            var originLayerCount = GetPassCount(submeshIndex);
            var layerCount = originLayerCount;
            var distanceFadeCurve = GetDistanceFadeCurve(submeshIndex);
            if (distanceFadeCurve != null && distanceFadeCurve.length >= 2 && camera != null && rootTransform != null)
            {
                var distance = Vector3.Distance(camera.transform.position, rootTransform.position);
                if (distance >= distanceFadeCurve[0].time)
                {
                    layerCount = Mathf.RoundToInt(Mathf.Max(0.0f, distanceFadeCurve.Evaluate(distance)) * originLayerCount);
                }
            }

            layerCount = Mathf.Clamp(layerCount, 0, MaxMultipassLayerCount);
            if (IsForceLayerCountEnabled(out var forceLayerCount))
            {
                layerCount = Mathf.Clamp(forceLayerCount, 0, MaxMultipassLayerCount);
            }

            return layerCount;
        }

        private bool IsForceLayerCountEnabled(out int forceLayerCount)
        {
#if UNITY_EDITOR
            if (s_ForceGlobal32Layer)
            {
                forceLayerCount = s_ForceGlobalLayerCount;
                return true;
            }
#endif

            forceLayerCount = m_ForceLayerCount;
            return m_Force32Layer;
        }

        private static MaterialPropertyBlock GetDrawPropertyBlock()
        {
            if (s_DrawPropertyBlock == null)
            {
                s_DrawPropertyBlock = new MaterialPropertyBlock();
            }

            return s_DrawPropertyBlock;
        }

        private static Mesh ResolveMesh(Renderer renderer)
        {
            var skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
            if (skinnedMeshRenderer != null)
            {
                return skinnedMeshRenderer.sharedMesh;
            }

            var meshFilter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            return meshFilter != null ? meshFilter.sharedMesh : null;
        }

        private static void EnsureMaterialInstancing(Material material)
        {
            if (material != null && !material.enableInstancing)
            {
                material.enableInstancing = true;
            }
        }

        private static Matrix4x4 ResolveRendererMatrix(Renderer renderer, Transform rootTransform)
        {
            var meshRenderer = renderer as MeshRenderer;
            if (meshRenderer != null && renderer.transform != null)
            {
                return renderer.transform.localToWorldMatrix;
            }

            return rootTransform != null ? rootTransform.localToWorldMatrix : Matrix4x4.identity;
        }

        private static void FillInstanceMatrices(Matrix4x4 matrix, int layerCount)
        {
            for (var i = 0; i < layerCount; i++)
            {
                s_InstanceMatrices[i] = matrix;
            }
        }

        private static bool CanDrawMaterial(Material material, RenderQueueRange range)
        {
            if (material == null)
            {
                return false;
            }

            var renderQueue = material.renderQueue;
            if (renderQueue < 0)
            {
                renderQueue = RenderQueueFromTag(material);
            }

            return renderQueue >= range.lowerBound && renderQueue <= range.upperBound;
        }

        private static int RenderQueueFromTag(Material material)
        {
            var queue = material.GetTag("Queue", true, "Geometry");
            if (queue.StartsWith("Transparent", StringComparison.OrdinalIgnoreCase))
            {
                return (int)RenderQueue.Transparent;
            }

            if (queue.StartsWith("AlphaTest", StringComparison.OrdinalIgnoreCase))
            {
                return (int)RenderQueue.AlphaTest;
            }

            if (queue.StartsWith("Overlay", StringComparison.OrdinalIgnoreCase))
            {
                return (int)RenderQueue.Overlay;
            }

            return (int)RenderQueue.Geometry;
        }
    }
}
