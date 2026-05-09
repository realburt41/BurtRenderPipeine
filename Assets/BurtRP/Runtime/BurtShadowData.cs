using UnityEngine; // Imports Unity types such as Light, LightShadows, Mathf, and VisibleLight helpers.
using UnityEngine.Rendering; // Imports VisibleLight so shadow data can be extracted from Unity culling results.

namespace Burt.RenderPipeline // Keeps shadow data in the same BurtRP runtime namespace as lighting and render requests.
{
    public sealed class BurtShadowData // Stores shadow-related data collected for one BurtRenderRequest.
    {
        private const int DefaultShadowResolution = 1024; // Defines the fallback shadow-map resolution for the current tutorial stage.

        public bool HasMainLightShadow { get; private set; } // Tells future shadow passes whether the selected main light should cast shadows.

        public int MainLightIndex { get; private set; } // Stores the visible-light index that owns the main-light shadow data.

        public LightShadows MainLightShadowType { get; private set; } // Stores whether the main light uses hard shadows, soft shadows, or no shadows.

        public float MainLightShadowStrength { get; private set; } // Stores the main light shadow strength in the 0 to 1 range.

        public float MainLightShadowNearPlane { get; private set; } // Stores the near plane used when rendering the main light shadow map.

        public int MainLightShadowResolution { get; private set; } // Stores the shadow-map resolution requested by the selected main light.

        private BurtShadowData() // Hides direct construction so all instances go through initialized factory functions.
        {
        } // The constructor is intentionally empty because ResetToDefaults fills every property.

        public static BurtShadowData None() // Creates a valid shadow data object that represents no active main-light shadow.
        {
            var data = new BurtShadowData(); // Allocates a new shadow data object for the caller.

            data.ResetToDefaults(); // Initializes all fields to safe no-shadow values.

            return data; // Returns the initialized no-shadow data object.
        }

        public static BurtShadowData CreateForMainLight( // Creates shadow data for the selected main Directional Light.
            VisibleLight visibleLight, // Receives the visible-light entry selected as BurtRP's main light.
            int mainLightIndex) // Receives the index of that visible light inside CullingResults.visibleLights.
        {
            var data = None(); // Starts from safe no-shadow defaults so every early return remains valid.

            data.MainLightIndex = mainLightIndex; // Stores the selected main-light index even if that light does not cast shadows.

            var light = visibleLight.light; // Reads the Unity Light component behind this visible-light entry.

            if (light == null) // Checks whether Unity provided a managed Light component reference.
            {
                return data; // Returns no-shadow data because shadow settings live on the Light component.
            }

            data.MainLightShadowType = light.shadows; // Copies the Unity light shadow mode into BurtRP shadow data.

            data.MainLightShadowStrength = Mathf.Clamp01(light.shadowStrength); // Copies and clamps the Unity shadow strength into a safe 0 to 1 range.

            data.MainLightShadowNearPlane = Mathf.Max(0.001f, light.shadowNearPlane); // Copies the Unity shadow near plane while avoiding invalid zero or negative values.

            data.MainLightShadowResolution = ResolveShadowResolution(light); // Resolves the light's requested shadow-map resolution into an integer size.

            data.HasMainLightShadow = data.MainLightShadowType != LightShadows.None && data.MainLightShadowStrength > 0f; // Enables shadows only when the light mode and strength both allow them.

            return data; // Returns the completed main-light shadow data.
        }

        private void ResetToDefaults() // Initializes this object to a safe no-shadow state.
        {
            HasMainLightShadow = false; // Marks that no main-light shadow should be rendered.

            MainLightIndex = -1; // Uses -1 to mean no visible-light entry owns the shadow data.

            MainLightShadowType = LightShadows.None; // Uses Unity's no-shadow enum value as the default shadow mode.

            MainLightShadowStrength = 0f; // Uses zero strength so shaders and future passes know shadows are disabled.

            MainLightShadowNearPlane = 0.1f; // Uses a small positive near plane as a safe default for future shadow cameras.

            MainLightShadowResolution = DefaultShadowResolution; // Uses the tutorial default resolution until a real light overrides it.
        }

        private static int ResolveShadowResolution(Light light) // Converts Unity light shadow settings into an integer shadow-map resolution.
        {
            if (light == null) // Checks whether a valid Light component was provided.
            {
                return DefaultShadowResolution; // Returns the fallback resolution because no light-specific settings exist.
            }

            if (light.shadowCustomResolution > 0) // Checks whether the Light component requests an explicit custom shadow resolution.
            {
                return Mathf.Max(1, light.shadowCustomResolution); // Returns the custom resolution while guarding against invalid values.
            }

            var shadowResolutionLevel = (int)light.shadowResolution; // Reads Unity's shadow resolution enum as an integer to avoid depending on enum names.

            switch (shadowResolutionLevel) // Maps Unity's common quality levels to concrete tutorial-stage texture sizes.
            {
                case 0: // Treats the lowest explicit Unity enum value as a low-resolution shadow map.
                    return 512; // Returns a small shadow resolution for low quality.

                case 1: // Treats the next Unity enum value as a medium-resolution shadow map.
                    return 1024; // Returns the tutorial default resolution for medium quality.

                case 2: // Treats the next Unity enum value as a high-resolution shadow map.
                    return 2048; // Returns a larger shadow resolution for high quality.

                case 3: // Treats the next Unity enum value as a very-high-resolution shadow map.
                    return 4096; // Returns the largest tutorial-stage shadow resolution.

                default: // Handles FromQualitySettings or any unknown enum value.
                    return DefaultShadowResolution; // Returns the fallback resolution until BurtRP owns a real shadow settings asset.
            }
        }
    }

    internal static class BurtShadowUtility // 定义阴影渲染辅助工具，集中判断当前 request 是否真的需要主光阴影。
    {
        public static BurtShadowData ResolveMainLightShadowData(BurtRenderRequest request) // 定义从 request 中安全读取主光阴影数据的函数。
        {
            if (request == null) // 如果 request 为空，说明当前没有可分析的渲染任务。
            {
                return null; // 返回空值，让调用方用统一方式跳过阴影流程。
            }

            var lightingData = request.LightingData; // 从 request 中读取预先收集好的灯光数据。

            if (lightingData == null) // 如果灯光数据为空，说明 request 创建阶段没有成功生成灯光信息。
            {
                return null; // 返回空值，避免后续访问 ShadowData 时出现空引用。
            }

            return lightingData.ShadowData; // 返回灯光数据里保存的主光阴影数据，可能是无阴影的安全对象。
        }

        public static bool ShouldUseMainLightShadow(BurtRenderRequest request) // 定义判断当前 request 是否应该启用主光阴影流程的函数。
        {
            var shadowData = ResolveMainLightShadowData(request); // 先用安全读取函数拿到阴影数据。

            if (shadowData == null) // 如果阴影数据不存在，说明当前 request 不具备渲染阴影的条件。
            {
                return false; // 返回 false，让调用方跳过阴影资源和阴影 Pass。
            }

            return shadowData.HasMainLightShadow; // 只有主光存在、开启阴影并且强度大于 0 时才返回 true。
        }
    }
}
