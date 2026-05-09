using UnityEngine; // Imports Unity types such as Color, Vector3, LightType, and RenderSettings.
using UnityEngine.Rendering; // Imports SRP types such as CullingResults and VisibleLight.

namespace Burt.RenderPipeline // Keeps lighting data inside the same BurtRP runtime namespace as requests and passes.
{
    public sealed class BurtLightingData // Stores all lighting information collected for one BurtRenderRequest.
    {
        private static readonly Vector3 DefaultMainLightDirection = new Vector3(0.3f, 0.8f, 0.4f).normalized; // Defines a stable fallback direction when no Directional Light is visible.

        public bool HasMainLight { get; private set; } // Tells later passes whether a real visible Directional Light was found.

        public int MainLightIndex { get; private set; } // Stores the index of the selected main light inside CullingResults.visibleLights.

        public int VisibleLightCount { get; private set; } // Stores how many visible lights Unity reported for the current camera.

        public Vector3 MainLightDirection { get; private set; } // Stores the world-space direction from the shaded point toward the selected main light.

        public Color MainLightColor { get; private set; } // Stores the selected main light color after Unity applies light intensity.

        public Color AmbientLightColor { get; private set; } // Stores the ambient color that BurtRP/Lit adds as baseline lighting.


        public BurtShadowData ShadowData { get; private set; } // Stores shadow data derived from the selected main light for future shadow passes.
        private BurtLightingData() // Hides direct construction so callers use Create or Default and always get initialized data.
        {
        } // The constructor body is empty because initialization is centralized in ResetToDefaults and ResolveMainLight.

        public static BurtLightingData Default() // Creates lighting data that is valid even when no culling results are available.
        {
            var data = new BurtLightingData(); // Allocates a new lighting data object for the caller.

            data.ResetToDefaults(0); // Initializes fallback light, ambient light, and visible-light count.

            return data; // Returns initialized fallback lighting data.
        }

        public static BurtLightingData Create(CullingResults cullingResults) // Builds lighting data from Unity's culling results for one render request.
        {
            var visibleLights = cullingResults.visibleLights; // Reads Unity's visible-light list for the current camera.

            var data = new BurtLightingData(); // Allocates a new lighting data object for this request.

            data.ResetToDefaults(visibleLights.Length); // Initializes fallback lighting before trying to find a real main light.

            data.ResolveMainLight(visibleLights); // Searches visible lights and stores the first usable Directional Light.

            return data; // Returns lighting data ready for BurtSetupLightingPass to upload.
        }

        private void ResetToDefaults(int visibleLightCount) // Initializes this object to a safe fallback lighting state.
        {
            HasMainLight = false; // Marks that no real main light has been selected yet.

            MainLightIndex = -1; // Uses -1 to mean the main light does not map to a visible-light list entry.

            VisibleLightCount = visibleLightCount; // Stores the visible-light count for debug and future light-list logic.

            MainLightDirection = DefaultMainLightDirection; // Uses the fallback direction so Lit materials still show shape without scene lights.

            MainLightColor = Color.white; // Uses white fallback light so Lit materials are not black by default.

            AmbientLightColor = RenderSettings.ambientLight; // Reads Unity's ambient color as BurtRP's first simple ambient-light source.

            ShadowData = BurtShadowData.None(); // Initializes shadow data to a valid no-shadow state before main-light selection.
        }

        private void ResolveMainLight(Unity.Collections.NativeArray<VisibleLight> visibleLights) // Finds the first visible Directional Light and stores it as BurtRP's main light.
        {
            for (var lightIndex = 0; lightIndex < visibleLights.Length; lightIndex++) // Iterates all lights visible to the current camera.
            {
                var visibleLight = visibleLights[lightIndex]; // Copies the current visible light data out of Unity's native array.

                if (visibleLight.lightType != LightType.Directional) // Checks whether this light is a Directional Light.
                {
                    continue; // Skips non-directional lights because the current Lit shader supports only one main Directional Light.
                }

                var forwardColumn = visibleLight.localToWorldMatrix.GetColumn(2); // Reads the light transform forward axis from Unity's visible-light matrix.

                var directionTowardLight = new Vector3(-forwardColumn.x, -forwardColumn.y, -forwardColumn.z); // Converts light forward into a direction from the shaded point toward the light.

                if (directionTowardLight.sqrMagnitude <= 0.0001f) // Guards against an invalid zero-length light direction.
                {
                    continue; // Skips this light and continues searching for a usable Directional Light.
                }

                HasMainLight = true; // Marks that a real scene Directional Light was selected.

                MainLightIndex = lightIndex; // Stores which visible-light entry became the main light.

                MainLightDirection = directionTowardLight.normalized; // Stores the normalized world-space direction used by Lambert lighting.

                MainLightColor = visibleLight.finalColor; // Stores Unity's final visible color, including light color and intensity.

                ShadowData = BurtShadowData.CreateForMainLight(visibleLight, lightIndex); // Captures shadow settings from the selected main light for later shadow-map work.
                return; // Stops after the first Directional Light, which is BurtRP's current main-light selection rule.
            }
        }
    }
}
