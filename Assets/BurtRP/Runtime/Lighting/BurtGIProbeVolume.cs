using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;

namespace Burt.RenderPipeline
{
    public enum BurtGIProbeTimeSlice
    {
        Morning = 0,
        Day = 1,
        Sunset = 2,
        Night = 3
    }

    public enum BurtGIProbeTimeSliceAll
    {
        MidNight = 0,
        LateNight = 1,
        SunRiseBefore0 = 2,
        SunRiseBefore1 = 3,
        SunRise = 4,
        SunRiseAfter0 = 5,
        SunRiseAfter1 = 6,
        Morning = 7,
        Midday = 8,
        Afternoon = 9,
        SunSetBefore0 = 10,
        SunSetBefore1 = 11,
        SunSet = 12,
        SunSetAfter0 = 13,
        SunSetAfter1 = 14,
        EarlyNight = 15,
        MAX = EarlyNight + 1
    }

    public enum BurtGIProbeVolumeMode
    {
        Scene,
        Local
    }

    public static class BurtGIProbeTimeSliceUtility
    {
        public static string ToXRenderName(BurtGIProbeTimeSlice slice)
        {
            return slice == BurtGIProbeTimeSlice.Sunset ? "SunSet" : slice.ToString();
        }

        public static string ToXRenderName(BurtGIProbeTimeSliceAll slice)
        {
            return slice.ToString();
        }

        public static BurtGIProbeTimeSlice ToCoreSlice(BurtGIProbeTimeSliceAll slice)
        {
            if (BurtGIProbeTimeSliceClockTable.TryGetClock(slice, out var clock))
            {
                return BurtGIProbeTimeSliceClockTable.GetSliceForHour(clock.TimeInHours);
            }

            return BurtGIProbeTimeSlice.Day;
        }

        public static BurtGIProbeTimeSlice NormalizeLegacyValue(BurtGIProbeTimeSlice slice)
        {
            if (Enum.IsDefined(typeof(BurtGIProbeTimeSlice), slice))
            {
                return slice;
            }

            return TryParseXRenderValue((int)slice, out var parsedSlice)
                ? parsedSlice
                : BurtGIProbeTimeSlice.Day;
        }

        public static bool TryParseXRenderValue(int value, out BurtGIProbeTimeSlice slice)
        {
            if (Enum.IsDefined(typeof(BurtGIProbeTimeSliceAll), value) &&
                value != (int)BurtGIProbeTimeSliceAll.MAX)
            {
                slice = ToCoreSlice((BurtGIProbeTimeSliceAll)value);
                return true;
            }

            slice = BurtGIProbeTimeSlice.Day;
            return false;
        }

        public static bool TryParseXRenderName(string value, out BurtGIProbeTimeSlice slice)
        {
            if (string.IsNullOrEmpty(value))
            {
                slice = BurtGIProbeTimeSlice.Day;
                return false;
            }

            if (string.Equals(value, "SunSet", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Sunset", StringComparison.OrdinalIgnoreCase))
            {
                slice = BurtGIProbeTimeSlice.Sunset;
                return true;
            }

            return Enum.TryParse(value, true, out slice) && Enum.IsDefined(typeof(BurtGIProbeTimeSlice), slice);
        }

        public static bool TryParseXRenderName(string value, out BurtGIProbeTimeSliceAll slice)
        {
            if (string.IsNullOrEmpty(value))
            {
                slice = BurtGIProbeTimeSliceAll.Afternoon;
                return false;
            }

            return Enum.TryParse(value, true, out slice) && Enum.IsDefined(typeof(BurtGIProbeTimeSliceAll), slice) &&
                slice != BurtGIProbeTimeSliceAll.MAX;
        }
    }

    internal readonly struct BurtGIProbeTimeSliceClock
    {
        internal readonly int Hour;
        internal readonly int Minute;

        internal BurtGIProbeTimeSliceClock(int hour, int minute)
        {
            Hour = hour;
            Minute = minute;
        }

        internal float TimeInHours => Hour + Minute / 60f;
    }

    internal static class BurtGIProbeTimeSliceClockTable
    {
        private static readonly BurtGIProbeTimeSliceClock[] TimeSliceClocks =
        {
            new BurtGIProbeTimeSliceClock(7, 30),
            new BurtGIProbeTimeSliceClock(15, 50),
            new BurtGIProbeTimeSliceClock(17, 30),
            new BurtGIProbeTimeSliceClock(21, 0)
        };

        private static readonly BurtGIProbeTimeSliceClock[] AllTimeSliceClocks =
        {
            new BurtGIProbeTimeSliceClock(0, 0),
            new BurtGIProbeTimeSliceClock(3, 0),
            new BurtGIProbeTimeSliceClock(5, 40),
            new BurtGIProbeTimeSliceClock(5, 55),
            new BurtGIProbeTimeSliceClock(6, 0),
            new BurtGIProbeTimeSliceClock(6, 5),
            new BurtGIProbeTimeSliceClock(6, 20),
            new BurtGIProbeTimeSliceClock(7, 30),
            new BurtGIProbeTimeSliceClock(12, 0),
            new BurtGIProbeTimeSliceClock(15, 50),
            new BurtGIProbeTimeSliceClock(17, 0),
            new BurtGIProbeTimeSliceClock(17, 15),
            new BurtGIProbeTimeSliceClock(17, 30),
            new BurtGIProbeTimeSliceClock(18, 5),
            new BurtGIProbeTimeSliceClock(18, 20),
            new BurtGIProbeTimeSliceClock(21, 0)
        };

        private static readonly BurtGIProbeTimeSlice[] OrderedSlices =
        {
            BurtGIProbeTimeSlice.Morning,
            BurtGIProbeTimeSlice.Day,
            BurtGIProbeTimeSlice.Sunset,
            BurtGIProbeTimeSlice.Night
        };

        internal static bool TryGetClock(BurtGIProbeTimeSlice timeSlice, out BurtGIProbeTimeSliceClock clock)
        {
            var index = (int)timeSlice;
            if ((uint)index < (uint)TimeSliceClocks.Length)
            {
                clock = TimeSliceClocks[index];
                return true;
            }

            clock = default;
            return false;
        }

        internal static bool TryGetClock(BurtGIProbeTimeSliceAll timeSlice, out BurtGIProbeTimeSliceClock clock)
        {
            var index = (int)timeSlice;
            if ((uint)index < (uint)AllTimeSliceClocks.Length)
            {
                clock = AllTimeSliceClocks[index];
                return true;
            }

            clock = default;
            return false;
        }

        internal static BurtGIProbeTimeSlice GetSliceForHour(float hour)
        {
            if (float.IsNaN(hour) || float.IsInfinity(hour))
            {
                return BurtGIProbeTimeSlice.Day;
            }

            hour %= 24f;
            if (hour < 0f)
            {
                hour += 24f;
            }

            for (var i = OrderedSlices.Length - 1; i >= 0; --i)
            {
                var current = OrderedSlices[i];
                var next = OrderedSlices[(i + 1) % OrderedSlices.Length];
                TryGetClock(current, out var currentClock);
                TryGetClock(next, out var nextClock);
                var boundary = ComputeWrappedMidpoint(currentClock.TimeInHours, nextClock.TimeInHours);
                if (IsHourInRange(hour, currentClock.TimeInHours, boundary))
                {
                    return current;
                }
            }

            return BurtGIProbeTimeSlice.Day;
        }

        private static bool IsHourInRange(float hour, float beginHour, float endHour)
        {
            if (beginHour <= endHour)
            {
                return hour >= beginHour && hour < endHour;
            }

            return hour >= beginHour || hour < endHour;
        }

        private static float ComputeWrappedMidpoint(float beginHour, float endHour)
        {
            var delta = endHour - beginHour;
            if (delta < 0f)
            {
                delta += 24f;
            }

            var midpoint = beginHour + delta * 0.5f;
            return midpoint >= 24f ? midpoint - 24f : midpoint;
        }
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/BurtRP/XGI Probe Volume")]
    [MovedFrom(true, "UnityEngine.Rendering", "FunPlus.WorldX.XRender.Runtime", "XGIProbeVolume")]
    public sealed class BurtGIProbeVolume : MonoBehaviour
    {
        [Tooltip("Diffuse irradiance in world-space volume coordinates. Supports Texture3D assets and Tex3D RenderTextures.")]
        public Texture irradiance;

        [Tooltip("World-space half extent represented by the irradiance texture.")]
        [Min(0.01f)] public float extent = 12f;

        [Tooltip("XRender-compatible placement mode. Scene is reserved for renderer-driven volume fitting; Local uses the explicit size.")]
        [HideInInspector]
        public BurtGIProbeVolumeMode mode = BurtGIProbeVolumeMode.Local;

        [Tooltip("XRender-style local probe-volume size. Leave components at zero to use the legacy cube extent.")]
        public Vector3 size;

        [Header("XGI Placement")]
        [Tooltip("Override the XGI brick subdivision range inside this probe volume, matching XRender XGIProbeVolume.")]
        public bool overridesSubdivLevels;

        [Range(0, 7)] public int lowestSubdivLevelOverride;
        [Range(0, 7)] public int highestSubdivLevelOverride = 7;

        [Tooltip("Keep empty-space placement inside this volume for XGI baking compatibility.")]
        public bool fillEmptySpaces;

        internal void GetSubdivisionOverride(int maxSubdivisionLevel, out int minLevel, out int maxLevel)
        {
            if (overridesSubdivLevels)
            {
                maxLevel = Mathf.Min(highestSubdivLevelOverride, maxSubdivisionLevel);
                minLevel = Mathf.Min(lowestSubdivLevelOverride, maxLevel);
                return;
            }

            maxLevel = maxSubdivisionLevel;
            minLevel = 0;
        }

        public Matrix4x4 GetVolume()
        {
            return Matrix4x4.TRS(transform.position, transform.rotation, ResolveLocalSize());
        }

        [Tooltip("Fade width, as a fraction of the volume half extent.")]
        [Range(0.001f, 1f)] public float edgeFade = 0.1f;

        [Tooltip("Diffuse irradiance multiplier.")]
        [Min(0f)] public float intensity = 1f;

        [Tooltip("Higher-priority volumes win before distance is considered.")]
        public int priority;

        [Header("XGI Time Slice")]
        [Tooltip("Restrict this volume to one XGI time slice. Use one volume per baked slice; the active slice is selected through BurtGIProbeVolume.SetActiveTimeSlice.")]
        public bool useTimeSlice;

        public BurtGIProbeTimeSlice timeSlice = BurtGIProbeTimeSlice.Day;

        [Header("XGI Virtual Probe Data")]
        [Tooltip("Use XRender-compatible virtual probe addressing instead of the direct irradiance texture.")]
        public bool useVirtualProbeData;

        [Tooltip("Little-endian uint page table. Each virtual chunk occupies 243 entries.")]
        public TextAsset virtualPageTable;

        [Tooltip("Little-endian uint3 indirection records, stored as 12 bytes per entry.")]
        public TextAsset virtualIndirection;

        [Tooltip("Physical-pool SH texture: RGB L0, alpha L1R.x.")]
        public Texture virtualL0L1Rx;

        [Tooltip("Physical-pool SH texture: RGB L1G.xyz, alpha L1R.y.")]
        public Texture virtualL1GL1Ry;

        [Tooltip("Physical-pool SH texture: RGB L1B.xyz, alpha L1R.z.")]
        public Texture virtualL1BL1Rz;

        [Tooltip("Optional physical-pool L2 SH textures, matching XRender's L2_0 through L2_3 layout.")]
        public Texture virtualL20;
        public Texture virtualL21;
        public Texture virtualL22;
        public Texture virtualL23;

        [Tooltip("Optional physical-pool R8 validity texture matching XRender's baked validity channel.")]
        public Texture virtualValidity;

        [Tooltip("Optional physical-pool sky visibility texture: L0/L1 coefficients in XRender layout.")]
        public Texture virtualSkyVisibilityL0L1;

        [Tooltip("Optional physical-pool R8 texture whose values index the 255 XGI precomputed sky shading directions. Value 255 keeps the surface normal.")]
        public Texture virtualSkyShadingDirectionIndices;

        [Tooltip("Optional little-endian float3 array with the 255 XGI precomputed sky shading directions. When omitted, BRP regenerates XRender's default direction table.")]
        public TextAsset virtualSkyShadingDirections;

        [Tooltip("Enable virtual probe shading, matching XRender's XGIProbe enableShading runtime flag.")]
        public bool virtualEnableShading = true;

        [Tooltip("Runtime SH bands allowed for virtual probe sampling, matching XRender's systemParameters.shBands clamp.")]
        public BurtXGIProbeSHBands virtualSHBands = BurtXGIProbeSHBands.SphericalHarmonicsL2;

        [Tooltip("Sky visibility tint and intensity, matching the baked XGI probe settings.")]
        public Color virtualSkyVisibilityTint = Color.white;
        [Min(0f)] public float virtualSkyVisibilityIntensity = 1f;
        [Tooltip("Runtime sky visibility offset from the XGI baking config. Kept with the volume so the apply path preserves XRender's runtime data contract.")]
        public float virtualSkyVisibilityOffset;

        [Tooltip("Overall virtual probe lighting intensity, matching XRender's XGIProbe lightIntensity.")]
        [Min(0f)] public float virtualLightIntensity = 1.5f;

        [Tooltip("Tint and intensity applied to virtual probe SH lighting, matching XRender's main-light SH tint.")]
        public Color virtualMainLightSHTint = Color.white;
        [Min(0f)] public float virtualMainLightSHIntensity = 1f;

        [Tooltip("Main-light intensity baked into this XGI time slice. Runtime main-light SH is scaled by current / baked intensity like XRender.")]
        [Min(0.0001f)] public float virtualTimeSliceMainLightIntensity = 1f;

        [Tooltip("Extra main-light SH occlusion factor matching XRender EnvVolume main-light occlusion.")]
        [Min(0f)] public float virtualMainLightOcclusion = 1f;

        [Tooltip("Apply BRP pre-exposure to virtual probe main-light SH tint, matching XRender's preExposure scale.")]
        public bool virtualMainLightSHUsesPreExposure = true;

        [Tooltip("Physical pool dimensions in texels.")]
        public Vector3Int virtualPhysicalPoolDimensions = new Vector3Int(4, 4, 4);

        [Tooltip("Dimensions of the indirection entry grid.")]
        public Vector3Int virtualIndirectionDimensions = Vector3Int.one;

        [Tooltip("Inclusive bounds of the loaded entry region in world entry coordinates.")]
        public Vector3Int virtualMinLoadedEntry;
        public Vector3Int virtualMaxLoadedEntry;

        [Tooltip("World entry coordinate represented by indirection buffer element zero.")]
        public Vector3Int virtualMinEntryIndex;

        [Tooltip("World size of one indirection entry and the smallest brick size.")]
        [Min(0.0001f)] public float virtualIndirectionEntrySize = 1f;
        [Min(0.0001f)] public float virtualMinBrickSize = 1f;

        [Tooltip("World-space offset, normal bias, and view bias matching the baked XGI data.")]
        public Vector3 virtualPositionOffset;
        public float virtualNormalBias;
        public float virtualViewBias;

        private static readonly List<BurtGIProbeVolume> ActiveVolumes = new List<BurtGIProbeVolume>();
        private static BurtGIProbeTimeSlice activeTimeSlice = BurtGIProbeTimeSlice.Day;

        private GraphicsBuffer pageTableBuffer;
        private GraphicsBuffer indirectionBuffer;
        private GraphicsBuffer skyShadingDirectionBuffer;
        private GraphicsBuffer runtimePageTableBuffer;
        private GraphicsBuffer runtimeIndirectionBuffer;
        private bool ownsRuntimeVirtualBuffers;
        private TextAsset pageTableSource;
        private TextAsset indirectionSource;
        private TextAsset skyShadingDirectionSource;

        public bool IsReady => isActiveAndEnabled && extent > 0.01f && intensity > 0f &&
            (IsVirtualReady || IsDirectIrradianceReady);

        public bool IsActiveForCurrentTimeSlice => !useTimeSlice || timeSlice == activeTimeSlice;

        public static BurtGIProbeTimeSlice ActiveTimeSlice => activeTimeSlice;

        public static void SetActiveTimeSlice(BurtGIProbeTimeSlice slice)
        {
            if (activeTimeSlice == slice)
            {
                return;
            }

            activeTimeSlice = slice;
            BurtGIVirtualProbeCellStreamer.InvalidateActiveTimeSliceStreaming();
        }

        public static void SetActiveTimeSlice(BurtGIProbeTimeSliceAll slice)
        {
            SetActiveTimeSlice(BurtGIProbeTimeSliceUtility.ToCoreSlice(slice));
        }

        public static void SetActiveTimeSlice(int xrenderTimeSliceValue)
        {
            if (BurtGIProbeTimeSliceUtility.TryParseXRenderValue(xrenderTimeSliceValue, out var slice))
            {
                SetActiveTimeSlice(slice);
            }
        }

        public static void SetActiveTimeSliceForHour(float hour)
        {
            SetActiveTimeSlice(ResolveTimeSliceForHour(hour));
        }

        public static BurtGIProbeTimeSlice ResolveTimeSliceForHour(float hour)
        {
            return BurtGIProbeTimeSliceClockTable.GetSliceForHour(hour);
        }

        public static bool TryGetTimeSliceClock(BurtGIProbeTimeSlice slice, out int hour, out int minute)
        {
            if (BurtGIProbeTimeSliceClockTable.TryGetClock(slice, out var clock))
            {
                hour = clock.Hour;
                minute = clock.Minute;
                return true;
            }

            hour = 0;
            minute = 0;
            return false;
        }

        public static bool TryGetTimeSliceClock(BurtGIProbeTimeSliceAll slice, out int hour, out int minute)
        {
            if (BurtGIProbeTimeSliceClockTable.TryGetClock(slice, out var clock))
            {
                hour = clock.Hour;
                minute = clock.Minute;
                return true;
            }

            hour = 0;
            minute = 0;
            return false;
        }

        public bool IsDirectIrradianceReady => irradiance != null && irradiance.dimension == TextureDimension.Tex3D;

        public bool IsVirtualReady => useVirtualProbeData &&
            (HasRuntimeVirtualBuffers || (virtualPageTable != null && virtualIndirection != null)) &&
            IsTexture3D(virtualL0L1Rx) &&
            virtualPhysicalPoolDimensions.x > 0 && virtualPhysicalPoolDimensions.y > 0 && virtualPhysicalPoolDimensions.z > 0 &&
            virtualIndirectionDimensions.x > 0 && virtualIndirectionDimensions.y > 0 && virtualIndirectionDimensions.z > 0 &&
            virtualIndirectionEntrySize > 0.0001f && virtualMinBrickSize > 0.0001f;

        public bool HasLoadedVirtualEntries => virtualMaxLoadedEntry.x >= virtualMinLoadedEntry.x &&
            virtualMaxLoadedEntry.y >= virtualMinLoadedEntry.y &&
            virtualMaxLoadedEntry.z >= virtualMinLoadedEntry.z;

        public bool HasVirtualL1 => virtualSHBands.HasL1() &&
            IsTexture3D(virtualL1GL1Ry) && IsTexture3D(virtualL1BL1Rz);

        public bool HasVirtualL2 => HasVirtualL1 && virtualSHBands.HasL2() &&
            IsTexture3D(virtualL20) && IsTexture3D(virtualL21) && IsTexture3D(virtualL22) && IsTexture3D(virtualL23);

        public bool HasVirtualValidity => IsTexture3D(virtualValidity);

        public bool HasVirtualSkyVisibility => IsTexture3D(virtualSkyVisibilityL0L1) && virtualSkyVisibilityIntensity > 0f;

        public bool HasVirtualSkyShadingDirection => HasVirtualSkyVisibility &&
            IsTexture3D(virtualSkyShadingDirectionIndices) &&
            skyShadingDirectionBuffer != null && skyShadingDirectionBuffer.IsValid();

        public string VirtualSkyShadingDirectionStatus
        {
            get
            {
                if (!IsTexture3D(virtualSkyShadingDirectionIndices))
                {
                    return "Disabled";
                }

                var sourceBytes = virtualSkyShadingDirections != null ? virtualSkyShadingDirections.bytes.Length : 0;
                if (skyShadingDirectionBuffer != null && skyShadingDirectionBuffer.IsValid())
                {
                    var source = virtualSkyShadingDirections != null ? "AssetBytes=" + sourceBytes : "DefaultXGI";
                    return "Ready(" + source + ",Count=" + skyShadingDirectionBuffer.count + ")";
                }

                return virtualSkyShadingDirections != null
                    ? "MissingBuffer(AssetBytes=" + sourceBytes + ",Expected>=3060,MultipleOf12)"
                    : "MissingBuffer(DefaultXGI)";
            }
        }

        internal GraphicsBuffer PageTableBuffer => HasRuntimeVirtualBuffers ? runtimePageTableBuffer : pageTableBuffer;
        internal GraphicsBuffer IndirectionBuffer => HasRuntimeVirtualBuffers ? runtimeIndirectionBuffer : indirectionBuffer;

        public bool HasRuntimeVirtualBuffers => runtimePageTableBuffer != null && runtimePageTableBuffer.IsValid() &&
            runtimeIndirectionBuffer != null && runtimeIndirectionBuffer.IsValid();

        internal Vector4 CenterExtent
        {
            get
            {
                var center = transform.position;
                return new Vector4(center.x, center.y, center.z, extent);
            }
        }

        internal Vector3 LocalHalfExtents
        {
            get
            {
                return ResolveLocalSize() * 0.5f;
            }
        }

        private Vector3 ResolveLocalSize()
        {
            if (size.x > 0.0001f && size.y > 0.0001f && size.z > 0.0001f)
            {
                return size;
            }

            var diameter = Mathf.Max(extent * 2f, 0.0001f);
            return new Vector3(diameter, diameter, diameter);
        }

        internal Matrix4x4 DirectWorldToLocalMatrix
        {
            get
            {
                var localToWorld = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                return localToWorld.inverse;
            }
        }

        private void OnEnable()
        {
            if (!ActiveVolumes.Contains(this))
            {
                ActiveVolumes.Add(this);
            }
        }

        private void Reset()
        {
            size = new Vector3(10f, 10f, 10f);
            extent = 5f;
        }

        private void OnDisable()
        {
            ActiveVolumes.Remove(this);
            ReleaseVirtualBuffers();
        }

        private void OnDestroy()
        {
            ReleaseVirtualBuffers();
        }

        internal static bool TryGetBestForCamera(Camera camera, out BurtGIProbeVolume volume)
        {
            volume = null;
            if (camera == null)
            {
                return false;
            }

            var cameraPosition = camera.transform.position;
            var bestPriority = int.MinValue;
            var bestDistanceSq = float.PositiveInfinity;
            for (var index = ActiveVolumes.Count - 1; index >= 0; --index)
            {
                var candidate = ActiveVolumes[index];
                if (candidate == null)
                {
                    ActiveVolumes.RemoveAt(index);
                    continue;
                }

                if (!candidate.IsReady || !candidate.IsActiveForCurrentTimeSlice || !Contains(candidate, cameraPosition))
                {
                    continue;
                }

                var distanceSq = (candidate.transform.position - cameraPosition).sqrMagnitude;
                if (candidate.priority < bestPriority || (candidate.priority == bestPriority && distanceSq >= bestDistanceSq))
                {
                    continue;
                }

                volume = candidate;
                bestPriority = candidate.priority;
                bestDistanceSq = distanceSq;
            }

            return volume != null;
        }

        private static bool Contains(BurtGIProbeVolume volume, Vector3 position)
        {
            var localPosition = volume.DirectWorldToLocalMatrix.MultiplyPoint3x4(position);
            var halfExtents = volume.LocalHalfExtents;
            return Mathf.Abs(localPosition.x) <= halfExtents.x &&
                Mathf.Abs(localPosition.y) <= halfExtents.y &&
                Mathf.Abs(localPosition.z) <= halfExtents.z;
        }

        internal bool TryEnsureVirtualBuffers()
        {
            if (!IsVirtualReady)
            {
                return false;
            }

            if (HasRuntimeVirtualBuffers)
            {
                return TryEnsureVirtualSkyShadingDirectionBuffer();
            }

            var pageTableBytes = virtualPageTable.bytes;
            var indirectionBytes = virtualIndirection.bytes;
            if (pageTableBytes.Length < sizeof(uint) || pageTableBytes.Length % sizeof(uint) != 0 ||
                indirectionBytes.Length < sizeof(uint) * 3 || indirectionBytes.Length % (sizeof(uint) * 3) != 0)
            {
                return false;
            }

            if (pageTableBuffer != null && pageTableBuffer.IsValid() &&
                indirectionBuffer != null && indirectionBuffer.IsValid() &&
                pageTableSource == virtualPageTable && indirectionSource == virtualIndirection)
            {
                return TryEnsureVirtualSkyShadingDirectionBuffer();
            }

            ReleaseSerializedVirtualBuffers();
            var pageTableData = new uint[pageTableBytes.Length / sizeof(uint)];
            Buffer.BlockCopy(pageTableBytes, 0, pageTableData, 0, pageTableBytes.Length);

            var indirectionWords = new uint[indirectionBytes.Length / sizeof(uint)];
            Buffer.BlockCopy(indirectionBytes, 0, indirectionWords, 0, indirectionBytes.Length);
            var indirectionData = new Vector3Int[indirectionWords.Length / 3];
            var requiredPageTableEntryCount = 0L;
            for (var index = 0; index < indirectionData.Length; ++index)
            {
                var wordOffset = index * 3;
                var metadataX = indirectionWords[wordOffset];
                indirectionData[index] = new Vector3Int(
                    unchecked((int)metadataX),
                    unchecked((int)indirectionWords[wordOffset + 1]),
                    unchecked((int)indirectionWords[wordOffset + 2]));

                if (metadataX != uint.MaxValue)
                {
                    requiredPageTableEntryCount = Math.Max(requiredPageTableEntryCount, ((long)(metadataX & 0x1fffffffu) + 1L) * 243L);
                }
            }

            var requiredIndirectionEntryCount = (long)virtualIndirectionDimensions.x * virtualIndirectionDimensions.y * virtualIndirectionDimensions.z;
            if (indirectionData.Length < requiredIndirectionEntryCount || pageTableData.Length < requiredPageTableEntryCount)
            {
                return false;
            }

            pageTableBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pageTableData.Length, sizeof(uint));
            pageTableBuffer.name = name + " BurtGI Virtual Page Table";
            pageTableBuffer.SetData(pageTableData);

            indirectionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indirectionData.Length, sizeof(uint) * 3);
            indirectionBuffer.name = name + " BurtGI Virtual Indirection";
            indirectionBuffer.SetData(indirectionData);
            pageTableSource = virtualPageTable;
            indirectionSource = virtualIndirection;
            return TryEnsureVirtualSkyShadingDirectionBuffer();
        }

        public void SetVirtualProbeRuntimeBuffers(GraphicsBuffer pageTable, GraphicsBuffer indirection)
        {
            if (!AreValidVirtualRuntimeBuffers(pageTable, indirection))
            {
                ClearVirtualProbeRuntimeBuffers();
                return;
            }

            ReleaseSerializedVirtualBuffers();
            ReleaseOwnedRuntimeVirtualBuffers();
            runtimePageTableBuffer = pageTable;
            runtimeIndirectionBuffer = indirection;
        }

        public bool TryAllocateVirtualProbeRuntimeBuffers(int pageTableEntryCount, int indirectionEntryCount)
        {
            if (pageTableEntryCount <= 0 || indirectionEntryCount <= 0)
            {
                return false;
            }

            ReleaseSerializedVirtualBuffers();
            ReleaseOwnedRuntimeVirtualBuffers();
            runtimePageTableBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pageTableEntryCount, sizeof(uint));
            runtimeIndirectionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indirectionEntryCount, sizeof(uint) * 3);
            runtimePageTableBuffer.name = name + " BurtGI Runtime Page Table";
            runtimeIndirectionBuffer.name = name + " BurtGI Runtime Indirection";
            ownsRuntimeVirtualBuffers = true;
            return true;
        }

        public bool TryUpdateVirtualPageTable(uint[] entries, int sourceIndex, int destinationIndex, int count)
        {
            var buffer = PageTableBuffer;
            if (!TryValidateBufferUpdate(buffer, sizeof(uint), entries, sourceIndex, destinationIndex, count))
            {
                return false;
            }

            buffer.SetData(entries, sourceIndex, destinationIndex, count);
            return true;
        }

        public bool TryUpdateVirtualIndirection(Vector3Int[] entries, int sourceIndex, int destinationIndex, int count)
        {
            var buffer = IndirectionBuffer;
            if (!TryValidateBufferUpdate(buffer, sizeof(uint) * 3, entries, sourceIndex, destinationIndex, count))
            {
                return false;
            }

            buffer.SetData(entries, sourceIndex, destinationIndex, count);
            return true;
        }

        public int VirtualPageTableEntryCount => PageTableBuffer != null && PageTableBuffer.IsValid() ? PageTableBuffer.count : 0;

        public int VirtualIndirectionEntryCount => IndirectionBuffer != null && IndirectionBuffer.IsValid() ? IndirectionBuffer.count : 0;

        public void ClearVirtualProbeRuntimeBuffers()
        {
            ReleaseOwnedRuntimeVirtualBuffers();
            runtimePageTableBuffer = null;
            runtimeIndirectionBuffer = null;
        }

        private void ReleaseVirtualBuffers()
        {
            ClearVirtualProbeRuntimeBuffers();
            ReleaseSerializedVirtualBuffers();
        }

        private void ReleaseSerializedVirtualBuffers()
        {
            pageTableBuffer?.Release();
            indirectionBuffer?.Release();
            skyShadingDirectionBuffer?.Release();
            pageTableBuffer = null;
            indirectionBuffer = null;
            skyShadingDirectionBuffer = null;
            pageTableSource = null;
            indirectionSource = null;
            skyShadingDirectionSource = null;
        }

        private void ReleaseOwnedRuntimeVirtualBuffers()
        {
            if (!ownsRuntimeVirtualBuffers)
            {
                return;
            }

            runtimePageTableBuffer?.Release();
            runtimeIndirectionBuffer?.Release();
            ownsRuntimeVirtualBuffers = false;
        }

        private bool TryEnsureVirtualSkyShadingDirectionBuffer()
        {
            if (!IsTexture3D(virtualSkyShadingDirectionIndices))
            {
                ReleaseVirtualSkyShadingDirectionBuffer();
                return true;
            }

            const int directionStride = sizeof(float) * 3;
            const int requiredDirectionCount = 255;

            if (skyShadingDirectionBuffer != null && skyShadingDirectionBuffer.IsValid() &&
                skyShadingDirectionSource == virtualSkyShadingDirections)
            {
                return true;
            }

            ReleaseVirtualSkyShadingDirectionBuffer();
            Vector3[] directions;
            if (virtualSkyShadingDirections == null)
            {
                directions = CreateDefaultXGISkyShadingDirections(requiredDirectionCount);
            }
            else
            {
                var directionBytes = virtualSkyShadingDirections.bytes;
                if (directionBytes.Length < directionStride * requiredDirectionCount || directionBytes.Length % directionStride != 0)
                {
                    return true;
                }

                var directionValues = new float[directionBytes.Length / sizeof(float)];
                Buffer.BlockCopy(directionBytes, 0, directionValues, 0, directionBytes.Length);
                directions = new Vector3[directionValues.Length / 3];
                for (var index = 0; index < directions.Length; ++index)
                {
                    var valueOffset = index * 3;
                    directions[index] = new Vector3(
                        directionValues[valueOffset],
                        directionValues[valueOffset + 1],
                        directionValues[valueOffset + 2]);
                }
            }

            skyShadingDirectionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, directions.Length, directionStride);
            skyShadingDirectionBuffer.name = name + " BurtGI Virtual Sky Shading Directions";
            skyShadingDirectionBuffer.SetData(directions);
            skyShadingDirectionSource = virtualSkyShadingDirections;
            return true;
        }

        private static Vector3[] CreateDefaultXGISkyShadingDirections(int directionCount)
        {
            var directions = new Vector3[directionCount];
            var sqrtDirectionCount = Mathf.Sqrt(directionCount);
            var phi = 0f;
            for (var index = 0; index < directionCount; ++index)
            {
                var h = -1f + 2f * index / (directionCount - 1f);
                var theta = Mathf.Acos(h);
                if (index == 0 || index == directionCount - 1)
                {
                    phi = 0f;
                }
                else
                {
                    phi += 3.6f / sqrtDirectionCount / Mathf.Sqrt(1f - h * h);
                }

                directions[index] = new Vector3(
                    Mathf.Sin(theta) * Mathf.Cos(phi),
                    Mathf.Sin(theta) * Mathf.Sin(phi),
                    Mathf.Cos(theta));
            }

            return directions;
        }

        private void ReleaseVirtualSkyShadingDirectionBuffer()
        {
            skyShadingDirectionBuffer?.Release();
            skyShadingDirectionBuffer = null;
            skyShadingDirectionSource = null;
        }

        internal GraphicsBuffer SkyShadingDirectionBuffer => skyShadingDirectionBuffer;

        private static bool IsTexture3D(Texture texture)
        {
            return texture != null && texture.dimension == TextureDimension.Tex3D;
        }

        private static bool AreValidVirtualRuntimeBuffers(GraphicsBuffer pageTable, GraphicsBuffer indirection)
        {
            return pageTable != null && pageTable.IsValid() && pageTable.stride == sizeof(uint) &&
                indirection != null && indirection.IsValid() && indirection.stride == sizeof(uint) * 3;
        }

        private static bool TryValidateBufferUpdate<T>(GraphicsBuffer buffer, int requiredStride, T[] source, int sourceIndex, int destinationIndex, int count) where T : struct
        {
            return buffer != null && buffer.IsValid() && buffer.stride == requiredStride &&
                source != null && sourceIndex >= 0 && destinationIndex >= 0 && count >= 0 &&
                sourceIndex <= source.Length - count && destinationIndex <= buffer.count - count;
        }
    }

    public struct BurtGIProbeVolumeBounds : IEquatable<BurtGIProbeVolumeBounds>
    {
        public Vector3 corner;
        public Vector3 x;
        public Vector3 y;
        public Vector3 z;
        public Bounds bounds;
        public bool overridesSubdivLevels;
        public int lowestSubdivLevelOverride;
        public int highestSubdivLevelOverride;
        public bool fillEmptySpaces;

        public BurtGIProbeVolumeBounds(Matrix4x4 trs, int maxSubdivisionLevel = 0, int minSubdivisionLevel = 0)
        {
            x = trs.GetColumn(0);
            y = trs.GetColumn(1);
            z = trs.GetColumn(2);
            corner = (Vector3)trs.GetColumn(3) - x * 0.5f - y * 0.5f - z * 0.5f;
            bounds = default;
            overridesSubdivLevels = maxSubdivisionLevel > 0 || minSubdivisionLevel > 0;
            lowestSubdivLevelOverride = minSubdivisionLevel;
            highestSubdivLevelOverride = maxSubdivisionLevel;
            fillEmptySpaces = false;
            bounds = CalculateAABB();
        }

        public BurtGIProbeVolumeBounds(Bounds value)
        {
            var size = value.size;
            corner = value.center - size * 0.5f;
            x = new Vector3(size.x, 0f, 0f);
            y = new Vector3(0f, size.y, 0f);
            z = new Vector3(0f, 0f, size.z);
            bounds = value;
            overridesSubdivLevels = false;
            lowestSubdivLevelOverride = 0;
            highestSubdivLevelOverride = 0;
            fillEmptySpaces = false;
        }

        public void GetSubdivisionOverride(int maxSubdivisionLevel, out int minLevel, out int maxLevel)
        {
            if (overridesSubdivLevels)
            {
                maxLevel = Mathf.Min(highestSubdivLevelOverride, maxSubdivisionLevel);
                minLevel = Mathf.Min(lowestSubdivLevelOverride, maxLevel);
                return;
            }

            maxLevel = maxSubdivisionLevel;
            minLevel = 0;
        }

        public Bounds CalculateAABB()
        {
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (var ix = 0; ix < 2; ix++)
            {
                for (var iy = 0; iy < 2; iy++)
                {
                    for (var iz = 0; iz < 2; iz++)
                    {
                        var point = corner + x * ix + y * iy + z * iz;
                        min = Vector3.Min(min, point);
                        max = Vector3.Max(max, point);
                    }
                }
            }

            return new Bounds((min + max) * 0.5f, max - min);
        }

        public readonly void CalculateCenterAndSize(out Vector3 center, out Vector3 size)
        {
            size = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            center = corner + x * 0.5f + y * 0.5f + z * 0.5f;
        }

        public void Transform(Matrix4x4 trs)
        {
            corner = trs.MultiplyPoint(corner);
            x = trs.MultiplyVector(x);
            y = trs.MultiplyVector(y);
            z = trs.MultiplyVector(z);
            bounds = CalculateAABB();
        }

        public bool Equals(BurtGIProbeVolumeBounds other)
        {
            return corner == other.corner &&
                x == other.x &&
                y == other.y &&
                z == other.z &&
                bounds == other.bounds &&
                overridesSubdivLevels == other.overridesSubdivLevels &&
                lowestSubdivLevelOverride == other.lowestSubdivLevelOverride &&
                highestSubdivLevelOverride == other.highestSubdivLevelOverride &&
                fillEmptySpaces == other.fillEmptySpaces;
        }
    }

    public static class BurtGIProbeVolumePositioning
    {
        private static readonly Vector3[] Axes = new Vector3[6];
        private static readonly Vector3[] AABBCorners = new Vector3[8];

        public static bool TryGetVolumeBounds(BurtGIProbeVolume volume, out BurtGIProbeVolumeBounds obb)
        {
            obb = default;
            if (volume == null)
            {
                return false;
            }

            var matrix = volume.GetVolume();
            var x = (Vector3)matrix.GetColumn(0);
            var y = (Vector3)matrix.GetColumn(1);
            var z = (Vector3)matrix.GetColumn(2);
            if (x.sqrMagnitude <= 0.00000025f || y.sqrMagnitude <= 0.00000025f || z.sqrMagnitude <= 0.00000025f)
            {
                return false;
            }

            obb = new BurtGIProbeVolumeBounds
            {
                corner = (Vector3)matrix.GetColumn(3) - x * 0.5f - y * 0.5f - z * 0.5f,
                x = x,
                y = y,
                z = z,
                overridesSubdivLevels = volume.overridesSubdivLevels,
                lowestSubdivLevelOverride = volume.lowestSubdivLevelOverride,
                highestSubdivLevelOverride = volume.highestSubdivLevelOverride,
                fillEmptySpaces = volume.fillEmptySpaces
            };
            obb.bounds = obb.CalculateAABB();
            return true;
        }

        public static bool OBBIntersect(in BurtGIProbeVolumeBounds a, in BurtGIProbeVolumeBounds b)
        {
            a.CalculateCenterAndSize(out var aCenter, out var aSize);
            b.CalculateCenterAndSize(out var bCenter, out var bSize);

            var aRadius = aSize.sqrMagnitude * 0.5f;
            var bRadius = bSize.sqrMagnitude * 0.5f;
            if (Vector3.SqrMagnitude(aCenter - bCenter) > aRadius + bRadius)
            {
                return false;
            }

            Axes[0] = a.x.normalized;
            Axes[1] = a.y.normalized;
            Axes[2] = a.z.normalized;
            Axes[3] = b.x.normalized;
            Axes[4] = b.y.normalized;
            Axes[5] = b.z.normalized;

            for (var i = 0; i < 6; i++)
            {
                var aProjection = ProjectOBB(in a, Axes[i]);
                var bProjection = ProjectOBB(in b, Axes[i]);
                if (aProjection.y < bProjection.x || bProjection.y < aProjection.x)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool OBBContains(in BurtGIProbeVolumeBounds obb, Vector3 point)
        {
            var lenX2 = obb.x.sqrMagnitude;
            var lenY2 = obb.y.sqrMagnitude;
            var lenZ2 = obb.z.sqrMagnitude;
            point -= obb.corner;
            point = new Vector3(Vector3.Dot(point, obb.x), Vector3.Dot(point, obb.y), Vector3.Dot(point, obb.z));
            return 0f < point.x && point.x < lenX2 &&
                0f < point.y && point.y < lenY2 &&
                0f < point.z && point.z < lenZ2;
        }

        public static bool OBBAABBIntersect(in BurtGIProbeVolumeBounds obb, Bounds aabb, in Bounds obbAABB)
        {
            if (!obbAABB.Intersects(aabb))
            {
                return false;
            }

            var boundsMin = aabb.min;
            var boundsMax = aabb.max;
            AABBCorners[0] = new Vector3(boundsMin.x, boundsMin.y, boundsMin.z);
            AABBCorners[1] = new Vector3(boundsMax.x, boundsMin.y, boundsMin.z);
            AABBCorners[2] = new Vector3(boundsMax.x, boundsMax.y, boundsMin.z);
            AABBCorners[3] = new Vector3(boundsMin.x, boundsMax.y, boundsMin.z);
            AABBCorners[4] = new Vector3(boundsMin.x, boundsMin.y, boundsMax.z);
            AABBCorners[5] = new Vector3(boundsMax.x, boundsMin.y, boundsMax.z);
            AABBCorners[6] = new Vector3(boundsMax.x, boundsMax.y, boundsMax.z);
            AABBCorners[7] = new Vector3(boundsMin.x, boundsMax.y, boundsMax.z);

            Axes[0] = obb.x.normalized;
            Axes[1] = obb.y.normalized;
            Axes[2] = obb.z.normalized;
            for (var i = 0; i < 3; i++)
            {
                var obbProjection = ProjectOBB(in obb, Axes[i]);
                var aabbProjection = ProjectAABB(AABBCorners, Axes[i]);
                if (obbProjection.y < aabbProjection.x || aabbProjection.y < obbProjection.x)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector2 ProjectOBB(in BurtGIProbeVolumeBounds obb, Vector3 axis)
        {
            var min = Vector3.Dot(axis, obb.corner);
            var max = min;
            for (var ix = 0; ix < 2; ix++)
            {
                for (var iy = 0; iy < 2; iy++)
                {
                    for (var iz = 0; iz < 2; iz++)
                    {
                        var projection = Vector3.Dot(axis, obb.corner + obb.x * ix + obb.y * iy + obb.z * iz);
                        if (projection < min)
                        {
                            min = projection;
                        }
                        else if (projection > max)
                        {
                            max = projection;
                        }
                    }
                }
            }

            return new Vector2(min, max);
        }

        private static Vector2 ProjectAABB(Vector3[] corners, Vector3 axis)
        {
            var min = Vector3.Dot(axis, corners[0]);
            var max = min;
            for (var i = 1; i < 8; i++)
            {
                var projection = Vector3.Dot(axis, corners[i]);
                if (projection < min)
                {
                    min = projection;
                }
                else if (projection > max)
                {
                    max = projection;
                }
            }

            return new Vector2(min, max);
        }
    }

    internal struct BurtGIProbeRuntimeInfoMetrics
    {
        public float physicalUsagePercent;
        public float physicalUsed;
        public float physicalTotal;
        public float physicalEstimatedMemCost;
        public float virtualUsagePercent;
        public float virtualUsed;
        public float virtualTotal;
        public float virtualEstimatedMemCost;
        public float virtualFragmentationRate;
        public Vector4 indirectionEntriesSize;
        public float indirectionUsagePercent;
        public float indirectionUsed;
        public float indirectionTotal;
        public float indirectionEstimatedMemCost;
    }

    internal static class BurtGIProbeVolumeUtility
    {
        private static readonly int IrradianceTextureId = Shader.PropertyToID("_BurtGIProbeVolumeIrradianceTexture");
        private static readonly int CenterExtentId = Shader.PropertyToID("_BurtGIProbeVolumeCenterExtent");
        private static readonly int ParamsId = Shader.PropertyToID("_BurtGIProbeVolumeParams");
        private static readonly int DirectWorldToLocalId = Shader.PropertyToID("_BurtGIProbeVolumeDirectWorldToLocal");
        private static readonly int DirectHalfExtentId = Shader.PropertyToID("_BurtGIProbeVolumeDirectHalfExtent");
        private static readonly int VirtualPageTableId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualPageTable");
        private static readonly int VirtualIndirectionId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualIndirection");
        private static readonly int VirtualL0L1RxId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL0L1Rx");
        private static readonly int VirtualL1GL1RyId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL1GL1Ry");
        private static readonly int VirtualL1BL1RzId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL1BL1Rz");
        private static readonly int VirtualL20Id = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL20");
        private static readonly int VirtualL21Id = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL21");
        private static readonly int VirtualL22Id = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL22");
        private static readonly int VirtualL23Id = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL23");
        private static readonly int VirtualValidityId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualValidity");
        private static readonly int VirtualSkyVisibilityL0L1Id = Shader.PropertyToID("_BurtGIProbeVolumeVirtualSkyVisibilityL0L1");
        private static readonly int VirtualSkyShadingDirectionIndicesId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualSkyShadingDirectionIndices");
        private static readonly int VirtualSkyShadingDirectionsId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualSkyShadingDirections");
        private static readonly int VirtualPosOffsetMinBrickSizeId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualPosOffsetMinBrickSize");
        private static readonly int VirtualIndirectionDimensionsId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualIndirectionDimensions");
        private static readonly int VirtualMinLoadedEntryId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualMinLoadedEntry");
        private static readonly int VirtualMaxLoadedEntryId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualMaxLoadedEntry");
        private static readonly int VirtualMinEntryIndexEntrySizeId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualMinEntryIndexEntrySize");
        private static readonly int VirtualPhysicalPoolDimensionsId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualPhysicalPoolDimensions");
        private static readonly int VirtualPhysicalPoolDimensionsRcpId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualPhysicalPoolDimensionsRcp");
        private static readonly int VirtualBiasL2Id = Shader.PropertyToID("_BurtGIProbeVolumeVirtualBiasL2");
        private static readonly int VirtualSkyVisibilityParamsId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualSkyVisibilityParams");
        private static readonly int VirtualSkyVisibilityOffsetId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualSkyVisibilityOffset");
        private static readonly int VirtualMainLightSHParamsId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualMainLightSHParams");
        private static readonly int VirtualSkyShadingDirectionEnabledId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualSkyShadingDirectionEnabled");
        private static readonly int VirtualBufferCountsId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualBufferCounts");

        public static bool TryResolveRuntimeInfoMetrics(Camera camera, out BurtGIProbeRuntimeInfoMetrics metrics)
        {
            metrics = default;
            if (!BurtGIProbeVolume.TryGetBestForCamera(camera, out var volume) || !volume.IsVirtualReady)
            {
                return false;
            }

            var expectedIndirectionEntries = Mathf.Max(1, volume.virtualIndirectionDimensions.x) *
                Mathf.Max(1, volume.virtualIndirectionDimensions.y) *
                Mathf.Max(1, volume.virtualIndirectionDimensions.z);
            var loadedEntryCount = ResolveLoadedVirtualEntryCount(volume);
            var pageTableEntries = Mathf.Max(0, volume.VirtualPageTableEntryCount);
            var indirectionEntries = Mathf.Max(0, volume.VirtualIndirectionEntryCount);
            var physicalCapacity = 0;
            var physicalUsed = 0;
            if (BurtGIVirtualProbeCellStreamer.TryGetForProbeVolume(volume, out var streamer))
            {
                physicalCapacity = streamer.physicalPool != null ? streamer.physicalPool.ChunkCapacity : 0;
                physicalUsed = streamer.OccupiedRuntimeChunkCount;
            }

            metrics.physicalUsed = physicalUsed;
            metrics.physicalTotal = physicalCapacity;
            metrics.physicalUsagePercent = ResolvePercent(physicalUsed, physicalCapacity);
            metrics.physicalEstimatedMemCost = BytesToMB(EstimateVirtualPhysicalPoolBytes(volume));
            metrics.virtualUsed = loadedEntryCount;
            metrics.virtualTotal = expectedIndirectionEntries;
            metrics.virtualUsagePercent = ResolvePercent(loadedEntryCount, expectedIndirectionEntries);
            metrics.virtualEstimatedMemCost = BytesToMB((long)pageTableEntries * sizeof(uint) + (long)indirectionEntries * sizeof(uint) * 3L);
            metrics.virtualFragmentationRate = loadedEntryCount > 0 ? Mathf.Clamp01(1f - (loadedEntryCount / (float)Mathf.Max(1, expectedIndirectionEntries))) * 100f : 0f;
            metrics.indirectionEntriesSize = new Vector4(
                volume.virtualIndirectionDimensions.x,
                volume.virtualIndirectionDimensions.y,
                volume.virtualIndirectionDimensions.z,
                0f);
            metrics.indirectionUsed = loadedEntryCount;
            metrics.indirectionTotal = expectedIndirectionEntries;
            metrics.indirectionUsagePercent = ResolvePercent(loadedEntryCount, expectedIndirectionEntries);
            metrics.indirectionEstimatedMemCost = BytesToMB((long)indirectionEntries * sizeof(uint) * 3L);
            return true;
        }

        public static string GetDebugStatus(Camera camera)
        {
            if (!BurtGIProbeVolume.TryGetBestForCamera(camera, out var volume))
            {
                return "Inactive(TimeSlice=" + BurtGIProbeVolume.ActiveTimeSlice +
                    ",TimeDriver=" + BurtGIProbeTimeOfDayController.GetDebugStatus() +
                    ",StreamingSource=" + BurtGIProbeStreamingPivot.GetDebugStatus(camera) +
                    ",SceneBinding=" + BurtGIProbeSceneBinding.GetDebugStatus(camera) + ")";
            }

            var builder = new StringBuilder(256);
            builder.Append(volume.IsVirtualReady ? "XGIVirtual" : "Direct");
            builder.Append("(Name=").Append(volume.name);
            builder.Append(",Ready=").Append(volume.IsReady);
            builder.Append(",Intensity=").Append(volume.intensity.ToString("0.###"));
            if (!volume.IsVirtualReady)
            {
                builder.Append(",Texture=").Append(volume.irradiance != null ? volume.irradiance.name : "<none>");
                builder.Append(')');
                return builder.ToString();
            }

            var hasRuntimeBuffers = volume.HasRuntimeVirtualBuffers;
            var hasSerializedAssets = volume.virtualPageTable != null && volume.virtualIndirection != null;
            var pageTableEntries = volume.VirtualPageTableEntryCount;
            var indirectionEntries = volume.VirtualIndirectionEntryCount;
            var expectedIndirectionEntries = Mathf.Max(1, volume.virtualIndirectionDimensions.x) *
                Mathf.Max(1, volume.virtualIndirectionDimensions.y) *
                Mathf.Max(1, volume.virtualIndirectionDimensions.z);
            var loadedEntryCount = ResolveLoadedVirtualEntryCount(volume);
            var physicalCapacity = 0;
            var physicalUsed = 0;
            var physicalSHChunks = 0;
            var physicalSharedChunks = 0;
            var streamingStatus = "Disabled";
            var activeBakedAssetName = "<none>";
            var loadedCells = 0;
            var configuredCells = 0;
            var resolvedSlices = 0;
            var lastStreamingStatus = "None";
            if (BurtGIVirtualProbeCellStreamer.TryGetForProbeVolume(volume, out var streamer))
            {
                streamingStatus = streamer.IsInitialized ? "Active" : "Configured";
                physicalCapacity = streamer.physicalPool != null ? streamer.physicalPool.ChunkCapacity : 0;
                physicalUsed = streamer.OccupiedRuntimeChunkCount;
                physicalSHChunks = streamer.LoadedPhysicalChunkCount;
                physicalSharedChunks = streamer.LoadedSharedChunkCount;
                activeBakedAssetName = streamer.ActiveBakedDataAsset != null ? streamer.ActiveBakedDataAsset.name : "<none>";
                loadedCells = streamer.LoadedCellCount;
                configuredCells = streamer.ConfiguredCellCount;
                resolvedSlices = streamer.ResolvedSliceCount;
                lastStreamingStatus = streamer.LastStreamingStatus;
            }

            builder.Append(",TimeSlice=").Append(BurtGIProbeVolume.ActiveTimeSlice);
            builder.Append(",TimeDriver=").Append(BurtGIProbeTimeOfDayController.GetDebugStatus());
            builder.Append(",StreamingSource=").Append(BurtGIProbeStreamingPivot.GetDebugStatus(camera));
            builder.Append(",SceneBinding=").Append(BurtGIProbeSceneBinding.GetDebugStatus(camera));
            builder.Append(",Buffers=").Append(hasRuntimeBuffers ? "Runtime" : (hasSerializedAssets ? "Serialized" : "Missing"));
            builder.Append(",PageTable=").Append(pageTableEntries);
            builder.Append(",Indirection=").Append(indirectionEntries).Append('/').Append(expectedIndirectionEntries);
            builder.Append(",IndirectionUsage=").Append(FormatPercent(loadedEntryCount, expectedIndirectionEntries));
            builder.Append(",Cells=").Append(loadedCells).Append('/').Append(configuredCells);
            builder.Append(",BakedAsset=").Append(activeBakedAssetName);
            builder.Append(",Physical=").Append(physicalUsed).Append('/').Append(physicalCapacity);
            builder.Append(",PhysicalSH=").Append(physicalSHChunks);
            builder.Append(",PhysicalShared=").Append(physicalSharedChunks);
            builder.Append(",PhysicalUsage=").Append(FormatPercent(physicalUsed, physicalCapacity));
            builder.Append(",PhysicalMemMB=").Append(FormatMB(EstimateVirtualPhysicalPoolBytes(volume)));
            builder.Append(",VirtualMemMB=").Append(FormatMB((long)pageTableEntries * sizeof(uint) + (long)indirectionEntries * sizeof(uint) * 3L));
            builder.Append(",Streaming=").Append(streamingStatus);
            builder.Append(",StreamingLast=").Append(lastStreamingStatus);
            builder.Append(",ResolvedSlices=").Append(resolvedSlices);
            builder.Append(",EnableShading=").Append(volume.virtualEnableShading);
            builder.Append(",ApplyEnabled=").Append(volume.virtualEnableShading && volume.HasLoadedVirtualEntries);
            builder.Append(",SHBands=").Append(volume.virtualSHBands);
            builder.Append(",L1=").Append(volume.HasVirtualL1);
            builder.Append(",L2=").Append(volume.HasVirtualL2);
            builder.Append(",Validity=").Append(volume.HasVirtualValidity);
            builder.Append(",SkyVisibility=").Append(volume.HasVirtualSkyVisibility);
            builder.Append(",SkyDirection=").Append(volume.HasVirtualSkyShadingDirection);
            builder.Append(",SkyDirectionStatus=").Append(volume.VirtualSkyShadingDirectionStatus);
            builder.Append(')');
            return builder.ToString();
        }

        public static void Upload(CommandBuffer cmd, Camera camera)
        {
            Upload(cmd, null, null, camera);
        }

        public static void Upload(CommandBuffer cmd, BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            Upload(cmd, request, asset, request != null ? request.Camera : null);
        }

        internal static bool UploadForDebug(CommandBuffer cmd, BurtGIProbeVolume volume, BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (cmd == null || volume == null)
            {
                return false;
            }

            cmd.SetGlobalVector(CenterExtentId, volume.CenterExtent);
            if (volume.TryEnsureVirtualBuffers())
            {
                UploadVirtualProbeData(cmd, volume, request, asset);
                return true;
            }

            if (volume.IsDirectIrradianceReady)
            {
                UploadDirectProbeData(cmd, volume);
                return true;
            }

            return false;
        }

        private static void Upload(CommandBuffer cmd, BurtRenderRequest request, BurtRenderPipelineAsset asset, Camera camera)
        {
            if (cmd == null)
            {
                return;
            }

            if (BurtGIProbeVolume.TryGetBestForCamera(camera, out var volume))
            {
                cmd.SetGlobalVector(CenterExtentId, volume.CenterExtent);
                if (volume.TryEnsureVirtualBuffers())
                {
                    UploadVirtualProbeData(cmd, volume, request, asset);
                    return;
                }

                if (volume.IsDirectIrradianceReady)
                {
                    UploadDirectProbeData(cmd, volume);
                    return;
                }
            }

            cmd.SetGlobalTexture(IrradianceTextureId, BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalVector(CenterExtentId, Vector4.zero);
            cmd.SetGlobalMatrix(DirectWorldToLocalId, Matrix4x4.identity);
            cmd.SetGlobalVector(DirectHalfExtentId, Vector4.zero);
            cmd.SetGlobalVector(ParamsId, Vector4.zero);
        }

        private static void UploadDirectProbeData(CommandBuffer cmd, BurtGIProbeVolume volume)
        {
            cmd.SetGlobalTexture(IrradianceTextureId, volume.irradiance);
            cmd.SetGlobalMatrix(DirectWorldToLocalId, volume.DirectWorldToLocalMatrix);
            var halfExtents = volume.LocalHalfExtents;
            cmd.SetGlobalVector(DirectHalfExtentId, new Vector4(halfExtents.x, halfExtents.y, halfExtents.z, 0f));
            cmd.SetGlobalVector(ParamsId, new Vector4(1f, volume.intensity, 1f / Mathf.Max(volume.edgeFade, 0.001f), 1f));
        }

        private static void UploadVirtualProbeData(CommandBuffer cmd, BurtGIProbeVolume volume, BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            var pool = volume.virtualPhysicalPoolDimensions;
            var reciprocalPool = new Vector4(1f / pool.x, 1f / pool.y, 1f / pool.z, 1f / (pool.x * pool.y));
            cmd.SetGlobalTexture(IrradianceTextureId, BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalBuffer(VirtualPageTableId, volume.PageTableBuffer);
            cmd.SetGlobalBuffer(VirtualIndirectionId, volume.IndirectionBuffer);
            cmd.SetGlobalTexture(VirtualL0L1RxId, volume.virtualL0L1Rx);
            cmd.SetGlobalTexture(VirtualL1GL1RyId, volume.HasVirtualL1 ? volume.virtualL1GL1Ry : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalTexture(VirtualL1BL1RzId, volume.HasVirtualL1 ? volume.virtualL1BL1Rz : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalTexture(VirtualL20Id, volume.HasVirtualL2 ? volume.virtualL20 : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalTexture(VirtualL21Id, volume.HasVirtualL2 ? volume.virtualL21 : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalTexture(VirtualL22Id, volume.HasVirtualL2 ? volume.virtualL22 : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalTexture(VirtualL23Id, volume.HasVirtualL2 ? volume.virtualL23 : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalTexture(VirtualValidityId, volume.HasVirtualValidity ? volume.virtualValidity : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalTexture(VirtualSkyVisibilityL0L1Id, volume.HasVirtualSkyVisibility ? volume.virtualSkyVisibilityL0L1 : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalTexture(VirtualSkyShadingDirectionIndicesId, volume.HasVirtualSkyShadingDirection ? volume.virtualSkyShadingDirectionIndices : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            if (volume.HasVirtualSkyShadingDirection)
            {
                cmd.SetGlobalBuffer(VirtualSkyShadingDirectionsId, volume.SkyShadingDirectionBuffer);
            }
            cmd.SetGlobalVector(VirtualPosOffsetMinBrickSizeId, new Vector4(volume.virtualPositionOffset.x, volume.virtualPositionOffset.y, volume.virtualPositionOffset.z, volume.virtualMinBrickSize));
            cmd.SetGlobalVector(VirtualIndirectionDimensionsId, new Vector4(volume.virtualIndirectionDimensions.x, volume.virtualIndirectionDimensions.y, volume.virtualIndirectionDimensions.z, 0f));
            cmd.SetGlobalVector(VirtualMinLoadedEntryId, new Vector4(volume.virtualMinLoadedEntry.x, volume.virtualMinLoadedEntry.y, volume.virtualMinLoadedEntry.z, 0f));
            cmd.SetGlobalVector(VirtualMaxLoadedEntryId, new Vector4(volume.virtualMaxLoadedEntry.x, volume.virtualMaxLoadedEntry.y, volume.virtualMaxLoadedEntry.z, 0f));
            cmd.SetGlobalVector(VirtualMinEntryIndexEntrySizeId, new Vector4(volume.virtualMinEntryIndex.x, volume.virtualMinEntryIndex.y, volume.virtualMinEntryIndex.z, volume.virtualIndirectionEntrySize));
            cmd.SetGlobalVector(VirtualPhysicalPoolDimensionsId, new Vector4(pool.x, pool.y, pool.z, 0f));
            cmd.SetGlobalVector(VirtualPhysicalPoolDimensionsRcpId, reciprocalPool);
            cmd.SetGlobalVector(VirtualBiasL2Id, new Vector4(volume.virtualNormalBias, volume.virtualViewBias, volume.HasVirtualL2 ? 1f : 0f, volume.HasVirtualSkyVisibility ? 1f : 0f));
            cmd.SetGlobalVector(VirtualSkyVisibilityParamsId, new Vector4(volume.virtualSkyVisibilityTint.r, volume.virtualSkyVisibilityTint.g, volume.virtualSkyVisibilityTint.b, volume.virtualSkyVisibilityIntensity));
            cmd.SetGlobalFloat(VirtualSkyVisibilityOffsetId, volume.virtualSkyVisibilityOffset);
            var mainLightSHIntensity = Mathf.Max(0f, volume.virtualMainLightSHIntensity);
            var mainLightSHScale = mainLightSHIntensity * ResolveMainLightIntensityScale(volume, request, asset);
            cmd.SetGlobalVector(VirtualMainLightSHParamsId, new Vector4(
                Mathf.Max(0f, volume.virtualMainLightSHTint.r * mainLightSHScale),
                Mathf.Max(0f, volume.virtualMainLightSHTint.g * mainLightSHScale),
                Mathf.Max(0f, volume.virtualMainLightSHTint.b * mainLightSHScale),
                mainLightSHIntensity));
            cmd.SetGlobalFloat(VirtualSkyShadingDirectionEnabledId, volume.HasVirtualSkyShadingDirection ? 1f : 0f);
            cmd.SetGlobalVector(VirtualBufferCountsId, new Vector4(volume.VirtualIndirectionEntryCount, volume.VirtualPageTableEntryCount, volume.HasVirtualValidity ? 1f : 0f, volume.HasVirtualL1 ? 1f : 0f));
            var shadingEnabled = volume.virtualEnableShading && volume.HasLoadedVirtualEntries ? 1f : 0f;
            cmd.SetGlobalVector(ParamsId, new Vector4(shadingEnabled, volume.intensity * Mathf.Max(0f, volume.virtualLightIntensity), 1f / Mathf.Max(volume.edgeFade, 0.001f), 2f));
        }

        private static float ResolveMainLightIntensityScale(BurtGIProbeVolume volume, BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            var bakedMainLightIntensity = Mathf.Max(volume != null ? volume.virtualTimeSliceMainLightIntensity : 1f, 0.0001f);
            var runtimeMainLightIntensity = ResolveRuntimeMainLightIntensity(request);
            var mainLightScale = runtimeMainLightIntensity / bakedMainLightIntensity;
            var occlusion = Mathf.Max(0f, volume != null ? volume.virtualMainLightOcclusion : 1f);
            var preExposure = volume != null && volume.virtualMainLightSHUsesPreExposure
                ? PreExposureUtility.ResolveForFrame(request, asset).PreExposure
                : 1f;
            return Mathf.Max(0f, mainLightScale * occlusion * preExposure);
        }

        private static float ResolveRuntimeMainLightIntensity(BurtRenderRequest request)
        {
            var lightingData = request != null ? request.LightingData : null;
            if (lightingData == null || !lightingData.HasMainLight)
            {
                return 1f;
            }

            var color = lightingData.MainLightColor;
            return Mathf.Max(0f, color.r, color.g, color.b);
        }

        private static int ResolveLoadedVirtualEntryCount(BurtGIProbeVolume volume)
        {
            var min = volume.virtualMinLoadedEntry;
            var max = volume.virtualMaxLoadedEntry;
            if (max.x < min.x || max.y < min.y || max.z < min.z)
            {
                return 0;
            }

            return Mathf.Max(0, max.x - min.x + 1) *
                Mathf.Max(0, max.y - min.y + 1) *
                Mathf.Max(0, max.z - min.z + 1);
        }

        private static long EstimateVirtualPhysicalPoolBytes(BurtGIProbeVolume volume)
        {
            var dimensions = volume.virtualPhysicalPoolDimensions;
            var texelCount = (long)Mathf.Max(0, dimensions.x) * Mathf.Max(0, dimensions.y) * Mathf.Max(0, dimensions.z);
            var bytesPerTexel = 8L;
            if (volume.HasVirtualL1)
            {
                bytesPerTexel += 8L;
            }

            if (volume.HasVirtualL2)
            {
                bytesPerTexel += 16L;
            }

            if (volume.HasVirtualValidity)
            {
                bytesPerTexel += 1L;
            }

            if (volume.HasVirtualSkyVisibility)
            {
                bytesPerTexel += 8L;
            }

            if (volume.HasVirtualSkyShadingDirection)
            {
                bytesPerTexel += 1L;
            }

            return texelCount * bytesPerTexel;
        }

        private static float ResolvePercent(int value, int total)
        {
            return total > 0 ? value * 100f / total : 0f;
        }

        private static float BytesToMB(long bytes)
        {
            return bytes / (1024f * 1024f);
        }

        private static string FormatPercent(int value, int total)
        {
            if (total <= 0)
            {
                return "0%";
            }

            return (value * 100f / total).ToString("0.##") + "%";
        }

        private static string FormatMB(long bytes)
        {
            return (bytes / (1024f * 1024f)).ToString("0.###");
        }
    }
}
