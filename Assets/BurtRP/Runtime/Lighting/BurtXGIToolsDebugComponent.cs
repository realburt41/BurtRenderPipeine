using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Burt.RenderPipeline
{
    public enum BurtXGIToolsVoxelSize
    {
        _4 = 4,
        _8 = 8,
        _16 = 16,
        _32 = 32,
        _64 = 64,
        _128 = 128,
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        Max = _1024
    }

    public enum BurtXGIToolsVoxelDebugLayer
    {
        Visibility,
        ClipmapIndex,
        Profile,
        Material_Albedo,
        Material_Normal,
        Material_Emissive,
        Lighting,
        Lighting_Direct,
        Lighting_Indirect,
        Lighting_Detail,
        Lighting_HitSide
    }

    public enum BurtXGIToolsSdfDebugLayer
    {
        Visibility,
        ClipmapIndex,
        TraceProfile,
        Material_Albedo,
        Material_Normal,
        Material_Emissive,
        Lighting,
        Lighting_Direct,
        Lighting_Indirect,
        Lighting_Detail
    }

    public enum BurtXGIToolsProbeDebugLayer
    {
        Visibility,
        BrickSize,
        Validity,
        SH_Sky_Visibility,
        SH,
        SHL0,
        SHL0L1
    }

    [ExecuteAlways]
    [AddComponentMenu("")]
    [MovedFrom(true, "UnityEngine.Rendering", "FunPlus.WorldX.XRender.Runtime", "XGIToolsDebugComponent")]
    public sealed class BurtXGIToolsDebugComponent : MonoBehaviour, ISerializationCallbackReceiver
    {
        public const int MaxProbeSubdivisionLevel = 7;

        [HideInInspector] public bool drawCells;
        [HideInInspector] public bool drawBricks;
        [HideInInspector] public bool realtimeSubdivision;
        [HideInInspector] public int subdivisionCellUpdatePerFrame = 4;
        [HideInInspector] public float subdivisionDelayInSeconds = 1f;
        [HideInInspector] public float subdivisionViewCullingDistance = 500f;

        [HideInInspector] public bool drawProbes;
        [HideInInspector] public bool drawProbesDepthTest = true;
        [HideInInspector] public float drawProbeSize = 0.3f;
        [HideInInspector] public float drawProbeCullingDistance = 50f;
        [HideInInspector] public int minSubdivToVisualize;
        [HideInInspector] public int maxSubdivToVisualize = MaxProbeSubdivisionLevel;
        [HideInInspector] public BurtXGIToolsProbeDebugLayer drawProbesDebugLayer = BurtXGIToolsProbeDebugLayer.Visibility;

        [HideInInspector] public bool drawVirtualOffset;
        [HideInInspector] public float drawVirtualOffsetSize = 0.2f;

        [HideInInspector] public bool drawVoxel;
        [HideInInspector] public bool drawVoxelByTrace;
        [HideInInspector] public bool drawVoxelDebugIndirectProbe;
        [HideInInspector] public float drawVoxelDebugCameraOffset;
        [HideInInspector] public float drawVoxelDebugCullingDistance = 800f;
        [HideInInspector] public BurtXGIToolsVoxelDebugLayer drawVoxelsDebugLayer = BurtXGIToolsVoxelDebugLayer.Visibility;

        [HideInInspector] public bool drawSdf;
        [HideInInspector] public BurtXGIToolsSdfDebugLayer drawSdfDebugLayer = BurtXGIToolsSdfDebugLayer.Visibility;
        [HideInInspector] public bool drawSdfDebugUseOccupy = true;

        [HideInInspector] public bool drawRuntimeInfo;

        [HideInInspector] public bool drawRTX;

        [Header("Base")]
        public bool followCamera;
        [Range(0f, 50f)] public float followCameraOffset;

        [Header("Voxel")]
        [Range(1, 8)] public int clipmapCount = 6;
        public BurtXGIToolsVoxelSize voxelSize = BurtXGIToolsVoxelSize._256;
        [Range(0.01f, 4f)] public float voxelSizeWS = 0.2f;
        public SceneVoxelMaterialMemoryBudget materialBudget = SceneVoxelMaterialMemoryBudget.Medium;
        public SceneVoxelMaterialGenerateMethod materialGenMethod = SceneVoxelMaterialGenerateMethod.Atomic;
        public SceneVoxelLightingType lightingType = SceneVoxelLightingType.Direct;
        [Range(0, 8)] public int voxelDebugMipLevel;
        [Range(0.01f, 2f)] public float drawVoxelProbeSizeWS = 0.5f;
        public bool voxelAlwaysUpdate;
        public bool voxelDrawVegetation = true;
        public bool voxelDrawGrass;
        public bool voxelLightingDirectionalShadow = true;
        [FormerlySerializedAs("voxelLightingPunctualLight")]
        public bool voxelLightingPunctualLightShadow = true;
        public bool voxelLightingSkyLight = true;

        public float[] clipmapOffset = new float[8];
        public float[] clipmapUpdateDistance = new float[8];

        [FormerlySerializedAs("rayTracingRange")]
        [Header("Ray Tracing")]
        [Range(0.1f, 2000f)] public float rtxRange = 50f;
        public bool rtxEnableLODCulling = true;
        [Range(0f, 1000f)] public float rtxUpdateDistance = 10f;
        public Mesh rtxMesh;
        public Material rtxMaterial;

        [HideInInspector] public bool needRtxAddInstance;
        [HideInInspector] public bool needRtxRemoveInstance;
        [HideInInspector] public bool needRtxAddAABB;
        [HideInInspector] public bool needRtxRemoveAABB;

        private static GameObject parent;
        private static BurtXGIToolsDebugComponent current;

        public static BurtXGIToolsDebugComponent Instance
        {
            get
            {
                if (parent == null || current == null)
                {
                    if (parent == null)
                    {
                        parent = new GameObject
                        {
                            hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy,
                            name = "BurtXGITools.Debug.Component"
                        };
                    }

                    if (!parent.TryGetComponent(out current))
                    {
                        current = parent.AddComponent<BurtXGIToolsDebugComponent>();
                        current.enabled = false;
                    }
                }

                return current;
            }
        }

        public static BurtXGIToolsDebugComponent instance => Instance;
        public static BurtXGIToolsDebugComponent Current => current;
        public static bool IsSdfDebugRequested => current != null && current.drawSdf;

        public Vector3 Position
        {
            get
            {
#if UNITY_EDITOR
                if (followCamera && !Application.isPlaying &&
                    SceneView.lastActiveSceneView != null &&
                    SceneView.lastActiveSceneView.camera != null)
                {
                    var cameraForward = SceneView.lastActiveSceneView.rotation * Vector3.forward;
                    return SceneView.lastActiveSceneView.camera.transform.position + followCameraOffset * cameraForward;
                }
#endif
                return transform.position;
            }
        }

        public Vector3 Forward
        {
            get
            {
#if UNITY_EDITOR
                if (followCamera && !Application.isPlaying &&
                    SceneView.lastActiveSceneView != null &&
                    SceneView.lastActiveSceneView.camera != null)
                {
                    return SceneView.lastActiveSceneView.rotation * Vector3.forward;
                }
#endif
                return transform.forward;
            }
        }

        public Vector3 GetClipmapOffsetVec(int clipmapIndex)
        {
            NormalizeArrays();
            return Forward * clipmapOffset[Mathf.Clamp(clipmapIndex, 0, clipmapOffset.Length - 1)];
        }

        public void RtxAddInstance()
        {
            needRtxAddInstance = true;
        }

        public void RtxRemoveInstance()
        {
            needRtxRemoveInstance = true;
        }

        public void RtxAddAABB()
        {
            needRtxAddAABB = true;
        }

        public void RtxRemoveAABB()
        {
            needRtxRemoveAABB = true;
        }

        public static void Cleanup()
        {
            if (parent != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(parent);
                }
                else
                {
                    DestroyImmediate(parent);
                }
            }

            parent = null;
            current = null;
        }

        public void OnBeforeSerialize()
        {
            NormalizeArrays();
        }

        public void OnAfterDeserialize()
        {
            NormalizeArrays();
        }

        private void Awake()
        {
            NormalizeArrays();
        }

        private void NormalizeArrays()
        {
            subdivisionCellUpdatePerFrame = Mathf.Clamp(subdivisionCellUpdatePerFrame, 1, 1024);
            subdivisionDelayInSeconds = Mathf.Max(0f, subdivisionDelayInSeconds);
            subdivisionViewCullingDistance = Mathf.Max(0.01f, subdivisionViewCullingDistance);
            drawProbeSize = Mathf.Max(0.001f, drawProbeSize);
            drawProbeCullingDistance = Mathf.Max(0.01f, drawProbeCullingDistance);
            minSubdivToVisualize = Mathf.Clamp(minSubdivToVisualize, 0, MaxProbeSubdivisionLevel);
            maxSubdivToVisualize = Mathf.Clamp(maxSubdivToVisualize, minSubdivToVisualize, MaxProbeSubdivisionLevel);
            drawVirtualOffsetSize = Mathf.Max(0.001f, drawVirtualOffsetSize);

            if (clipmapOffset == null || clipmapOffset.Length != 8)
            {
                var oldValues = clipmapOffset;
                clipmapOffset = new float[8];
                CopyArray(oldValues, clipmapOffset);
            }

            if (clipmapUpdateDistance == null || clipmapUpdateDistance.Length != 8)
            {
                var oldValues = clipmapUpdateDistance;
                clipmapUpdateDistance = new float[8];
                CopyArray(oldValues, clipmapUpdateDistance);
                for (var i = 0; i < clipmapUpdateDistance.Length; ++i)
                {
                    if (clipmapUpdateDistance[i] <= 0f)
                    {
                        clipmapUpdateDistance[i] = 10f;
                    }
                }
            }
        }

        private static void CopyArray(float[] source, float[] destination)
        {
            if (source == null)
            {
                return;
            }

            var count = Mathf.Min(source.Length, destination.Length);
            for (var i = 0; i < count; ++i)
            {
                destination[i] = source[i];
            }
        }
    }
}
